/// <summary>
/// Hello World - Claude Agent SDK Example
///
/// This example demonstrates:
/// - Basic one-shot query using QueryAsync
/// - Using PreToolUse hooks to restrict file operations
/// - Restricting .js and .ts files to a specific custom_scripts directory
///
/// Ported from: claude-agent-sdk-demos/hello-world/hello-world.ts
/// </summary>

using System.Text.Json;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.HelloWorld;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Hello World - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Setup working directory for the agent
        var workingDir = Path.Combine(Directory.GetCurrentDirectory(), "agent");
        var customScriptsDir = Path.Combine(workingDir, "custom_scripts");

        // Ensure directories exist
        Directory.CreateDirectory(workingDir);
        Directory.CreateDirectory(customScriptsDir);

        Console.WriteLine($"Working directory: {workingDir}");
        Console.WriteLine($"Custom scripts directory: {customScriptsDir}");
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = "opus",
            MaxTurns = 100,
            PermissionMode = PermissionMode.AcceptEdits,

            // Allow a comprehensive set of tools
            AllowedTools =
            [
                "Task", "Bash", "Glob", "Grep", "Read", "Edit", "MultiEdit", "Write",
                "NotebookEdit", "WebFetch", "TodoWrite", "WebSearch"
            ],

            // Configure PreToolUse hook to restrict file operations
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new()
                    {
                        // Match Write, Edit, and MultiEdit tools
                        Matcher = "Write|Edit|MultiEdit",
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is not PreToolUseHookInput preInput)
                                    return new SyncHookOutput { Continue = true };

                                var toolName = preInput.ToolName;
                                if (toolName is not ("Write" or "Edit" or "MultiEdit"))
                                    return new SyncHookOutput { Continue = true };

                                // Extract file path from tool input
                                var filePath = ExtractFilePath(preInput.ToolInput, toolName);
                                if (string.IsNullOrEmpty(filePath))
                                    return new SyncHookOutput { Continue = true };

                                var ext = Path.GetExtension(filePath).ToLowerInvariant();

                                // Restrict .js and .ts files to custom_scripts directory
                                if (ext is ".js" or ".ts")
                                {
                                    var normalizedPath = Path.GetFullPath(filePath);
                                    var normalizedScriptsDir = Path.GetFullPath(customScriptsDir);

                                    if (!normalizedPath.StartsWith(normalizedScriptsDir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var fileName = Path.GetFileName(filePath);
                                        var suggestedPath = Path.Combine(customScriptsDir, fileName);

                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine($"\n[Hook: Blocked file write]");
                                        Console.WriteLine($"  Path: {filePath}");
                                        Console.WriteLine($"  Reason: Script files must be in custom_scripts directory");
                                        Console.ResetColor();

                                        return new SyncHookOutput
                                        {
                                            Continue = false,
                                            Decision = "block",
                                            StopReason = $"Script files (.js and .ts) must be written to the custom_scripts directory. Please use the path: {suggestedPath}"
                                        };
                                    }
                                }

                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"[Hook: Allowed] {toolName} -> {filePath}");
                                Console.ResetColor();

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                }
            }
        };

        await using var client = new ClaudeAgentClient(options);

        var prompt = args.Length > 0
            ? string.Join(" ", args)
            : "Hello, Claude! Please introduce yourself in one sentence.";

        Console.WriteLine($"Prompt: {prompt}");
        Console.WriteLine();
        Console.WriteLine("Response:");
        Console.WriteLine(new string('-', 50));

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
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
                                Console.WriteLine($"\n[Using tool: {toolUse.Name}]");
                                Console.ResetColor();
                                break;
                        }
                    }
                    break;

                case ResultMessage result:
                    Console.WriteLine();
                    Console.WriteLine(new string('-', 50));
                    Console.WriteLine($"Completed in {result.DurationMs / 1000.0:F2}s | Cost: ${result.TotalCostUsd:F4}");
                    break;
            }
        }
    }

    /// <summary>
    /// Extracts the file path from tool input based on tool name.
    /// </summary>
    private static string? ExtractFilePath(JsonElement? toolInput, string toolName)
    {
        if (toolInput is null)
            return null;

        try
        {
            // Write, Edit, and MultiEdit all use "file_path" property
            if (toolInput.Value.TryGetProperty("file_path", out var filePathElement))
            {
                return filePathElement.GetString();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return null;
    }
}
