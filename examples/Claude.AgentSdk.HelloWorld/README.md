# Hello World Example

A simple getting-started example demonstrating Claude Agent SDK basics with file path restriction hooks and strongly-typed enum accessors.

## What This Example Demonstrates

- **Basic one-shot query** using `QueryAsync`
- **PreToolUse hooks** to intercept and validate tool calls before execution
- **File path restrictions** - blocking script files from being written outside a designated directory
- **Strongly-typed enum accessors** for `SystemMessage` and `ResultMessage`
- **Streaming responses** from Claude

## C#-Centric Features Used

### Fluent Options Builder

```csharp
using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptionsBuilder()
    .WithModel(ModelIdentifier.Opus)
    .WithFallbackModel(ModelIdentifier.Sonnet)
    .WithMaxTurns(100)
    .WithPermissionMode(PermissionMode.AcceptEdits)
    .WithWorkingDirectory(workDir)
    .AllowTools(ToolName.Bash, ToolName.Read, ToolName.Write, ToolName.Edit)
    .WithHooks(new HookConfigurationBuilder()
        .OnPreToolUse(ValidateFilePaths, matcher: "Write|Edit|MultiEdit")
        .Build())
    .Build();
```

### ModelIdentifier for Type-Safe Model Selection

```csharp
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptions
{
    // Strongly-typed model selection with IntelliSense
    ModelId = ModelIdentifier.Opus,
    FallbackModelId = ModelIdentifier.Sonnet,

    // Or use specific versions
    // ModelId = ModelIdentifier.ClaudeOpus45,

    MaxTurns = 100,
    PermissionMode = PermissionMode.AcceptEdits
};
```

### Strongly-Typed Tool Names

```csharp
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptions
{
    // Type-safe tool configuration
    AllowedTools = [ToolName.Bash, ToolName.Read, ToolName.Write, ToolName.Edit,
                    ToolName.MultiEdit, ToolName.Glob, ToolName.Grep]
};
```

### Fluent Hook Configuration

```csharp
using Claude.AgentSdk.Builders;

var hooks = new HookConfigurationBuilder()
    .OnPreToolUse(ValidateFilePaths, matcher: "Write|Edit|MultiEdit")
    .OnPostToolUse(LogFileChanges)
    .Build();
```

### Functional Match Patterns

```csharp
using Claude.AgentSdk.Messages;

await foreach (var message in client.QueryAsync(prompt))
{
    // Exhaustive pattern matching with type inference
    var output = message.Match(
        userMessage: u => $"User: {u.MessageContent.Content}",
        assistantMessage: a => ProcessAssistant(a),
        systemMessage: s => $"System: {s.Subtype}",
        resultMessage: r => {
            var ctx = r.Usage is not null ? $"{r.Usage.TotalContextTokens / 1000.0:F0}k" : "?";
            return $"[{r.DurationMs/1000.0:F1}s | ${r.TotalCostUsd:F4} | {ctx}]";
        },
        streamEvent: _ => null
    );

    if (output != null) Console.WriteLine(output);
}

// Or with default for partial matching
var isComplete = message.Match(
    resultMessage: r => r.SubtypeEnum == ResultMessageSubtype.Success,
    defaultCase: () => false
);
```

### Strongly-Typed Enum Accessors

```csharp
using Claude.AgentSdk.Types;

await foreach (var message in client.QueryAsync(prompt))
{
    switch (message)
    {
        // Handle system messages with SubtypeEnum accessor
        case SystemMessage system:
            if (system.SubtypeEnum == SystemMessageSubtype.Init)
            {
                Console.WriteLine($"[Session: {system.SessionId}, Model: {system.Model}]");
            }
            break;

        // Handle results with SubtypeEnum accessor
        case ResultMessage result:
            var resultType = result.SubtypeEnum switch
            {
                ResultMessageSubtype.Success => "Completed",
                ResultMessageSubtype.Error => "Error",
                ResultMessageSubtype.Partial => "Partial",
                _ => "Unknown"
            };
            var ctx = result.Usage is not null ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k" : "?";
            Console.WriteLine($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {ctx}]");
            break;
    }
}
```

### Content Block Pattern Matching

```csharp
case AssistantMessage assistant:
    foreach (var block in assistant.MessageContent.Content)
    {
        switch (block)
        {
            case TextBlock text:
                Console.WriteLine($"Claude says: {text.Text}");
                break;

            case ToolUseBlock toolUse:
                Console.WriteLine($"[Using tool: {toolUse.Name}]");
                break;

            case ThinkingBlock thinking:
                Console.WriteLine("[thinking...]");
                break;
        }
    }
    break;
```

### Message Extensions for Simplified Processing

```csharp
using Claude.AgentSdk.Extensions;

case AssistantMessage assistant:
    // Get all text at once
    Console.WriteLine(assistant.GetText());

    // Check for specific tool usage
    if (assistant.HasToolUse(ToolName.Write))
        Console.WriteLine("File written!");

    // Get all tool uses
    foreach (var tool in assistant.GetToolUses())
        Console.WriteLine($"Used: {tool.Name}");
    break;
```

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

### Processing Streamed Messages with Enum Accessors

```csharp
await foreach (var message in client.QueryAsync(prompt))
{
    switch (message)
    {
        // Use SubtypeEnum for type-safe system message handling
        case SystemMessage system:
            if (system.SubtypeEnum == SystemMessageSubtype.Init)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Session: {system.SessionId}, Model: {system.Model}]");
                Console.ResetColor();
            }
            break;

        case AssistantMessage assistant:
            foreach (var block in assistant.MessageContent.Content)
            {
                switch (block)
                {
                    case TextBlock text:
                        Console.WriteLine($"Claude says: {text.Text}");
                        break;

                    case ToolUseBlock toolUse:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[Using tool: {toolUse.Name}]");
                        Console.ResetColor();
                        break;

                    case ThinkingBlock thinking:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("[thinking...]");
                        Console.ResetColor();
                        break;
                }
            }
            break;

        // Use SubtypeEnum for type-safe result checking
        case ResultMessage result:
            var resultType = result.SubtypeEnum switch
            {
                ResultMessageSubtype.Success => "Completed",
                ResultMessageSubtype.Error => "Error",
                ResultMessageSubtype.Partial => "Partial",
                _ => "Unknown"
            };
            var ctx = result.Usage is not null ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k" : "?";
            Console.WriteLine($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {ctx}]");
            break;
    }
}
```

## Project Structure

```
Claude.AgentSdk.HelloWorld/
├── Program.cs              # Main entry point with hook configuration and enum usage
├── Claude.AgentSdk.HelloWorld.csproj
└── agent/                  # Working directory (created at runtime)
    └── custom_scripts/     # Allowed directory for script files
```

## Source Generator Reference

To use compile-time features, the project references the source generator:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Claude.AgentSdk\Claude.AgentSdk.csproj" />
  <ProjectReference Include="..\..\src\Claude.AgentSdk.Generators\Claude.AgentSdk.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Available Enum Types

| Type                   | Values                                            |
| ---------------------- | ------------------------------------------------- |
| `SystemMessageSubtype` | `Init`, `CompactBoundary`                         |
| `ResultMessageSubtype` | `Success`, `Error`, `Partial`                     |
| `ContentBlockType`     | `Text`, `Thinking`, `ToolUse`, `ToolResult`       |

## Generated Enum Mappings

All enums support compile-time generated string conversion:

```csharp
// Convert enum to JSON string
var jsonStr = ResultMessageSubtype.Success.ToJsonString();  // "success"

// Parse string to enum
var subtype = EnumStringMappings.ParseResultMessageSubtype("error");  // ResultMessageSubtype.Error

// Safe parsing
if (EnumStringMappings.TryParseSystemMessageSubtype("init", out var result))
{
    // Handle parsed value
}
```

## Ported From

This example is ported from the official TypeScript demo:
`claude-agent-sdk-demos/hello-world/hello-world.ts`

Enhanced with C#-specific features like strongly-typed enum accessors for type-safe message handling.
