# Hello World Example

A simple getting-started example demonstrating Claude Agent SDK basics with file path restriction hooks.

## What This Example Demonstrates

- **Basic one-shot query** using `QueryAsync`
- **PreToolUse hooks** to intercept and validate tool calls before execution
- **File path restrictions** - blocking script files from being written outside a designated directory
- **Streaming responses** from Claude

## Features

This example enforces a security policy: `.js` and `.ts` files can only be written to the `custom_scripts` directory. Any attempt to write script files elsewhere is blocked with a helpful error message.

## Running the Example

```bash
cd examples/Claude.AgentSdk.HelloWorld
dotnet run
```

Or with a custom prompt:

```bash
dotnet run -- "Write a TypeScript function that calculates fibonacci numbers"
```

## Key Code Patterns

### Setting Up Hooks

```csharp
var options = new ClaudeAgentOptions
{
    WorkingDirectory = workDir,
    Model = "opus",
    MaxTurns = 100,
    PermissionMode = PermissionMode.AcceptEdits,
    AllowedTools = ["Bash", "Read", "Write", "Edit", "MultiEdit", "Glob", "Grep"],
    Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
    {
        [HookEvent.PreToolUse] = new List<HookMatcher>
        {
            new()
            {
                Matcher = "Write|Edit|MultiEdit",  // Regex to match tool names
                Hooks =
                [
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is not PreToolUseHookInput preInput)
                            return new SyncHookOutput { Continue = true };

                        // Safely extract file path using helper method
                        var filePath = ExtractFilePath(preInput.ToolInput);
                        if (filePath is null)
                            return new SyncHookOutput { Continue = true };

                        // Block if writing script outside allowed directory
                        // Use path normalization to prevent path traversal attacks
                        var normalizedPath = Path.GetFullPath(filePath);
                        if (IsScriptFile(normalizedPath) && !IsInAllowedDirectory(normalizedPath))
                        {
                            Console.WriteLine($"BLOCKED: {Path.GetFileName(filePath)}");
                            Console.WriteLine($"  Reason: Script files must be in custom_scripts/");
                            return new SyncHookOutput
                            {
                                Continue = false,
                                Decision = "block",
                                StopReason = "Script files must be in custom_scripts/"
                            };
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                ]
            }
        }
    }
};

// Helper method for safe JSON property extraction
private static string? ExtractFilePath(JsonElement? input)
{
    try
    {
        if (input?.TryGetProperty("file_path", out var pathElement) == true)
            return pathElement.GetString();
    }
    catch { }
    return null;
}
```

### Processing Streamed Messages

```csharp
await foreach (var message in client.QueryAsync(prompt))
{
    switch (message)
    {
        case AssistantMessage assistant:
            foreach (var block in assistant.MessageContent.Content)
            {
                if (block is TextBlock text)
                    Console.WriteLine(text.Text);
                else if (block is ToolUseBlock toolUse)
                    Console.WriteLine($"[Using tool: {toolUse.Name}]");
            }
            break;

        case ResultMessage result:
            Console.WriteLine($"Cost: ${result.TotalCostUsd:F4}");
            break;
    }
}
```

## Project Structure

```
Claude.AgentSdk.HelloWorld/
├── Program.cs              # Main entry point with hook configuration
├── Claude.AgentSdk.HelloWorld.csproj
└── agent/                  # Working directory (created at runtime)
    └── custom_scripts/     # Allowed directory for script files
```

## Ported From

This example is ported from the official TypeScript demo:
`claude-agent-sdk-demos/hello-world/hello-world.ts`
