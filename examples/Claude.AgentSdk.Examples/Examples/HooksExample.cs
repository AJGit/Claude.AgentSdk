using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Demonstrates using hooks to intercept and modify tool execution.
/// </summary>
public class HooksExample : IExample
{
    public string Name => "Hooks (Pre/Post Tool Use)";
    public string Description => "Intercept and log tool usage with hooks";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates using hooks to intercept tool execution.");
        Console.WriteLine("We'll log all tool calls and their results.\n");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant. You can read files.",
            AllowedTools = ["Read"],
            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 3,

            // Configure hooks
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                // Pre-tool-use hook: runs before a tool is executed
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new()
                    {
                        // Match all tools (no specific matcher)
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is PreToolUseHookInput preInput)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"[Hook: PreToolUse]");
                                    Console.WriteLine($"  Tool: {preInput.ToolName}");
                                    Console.WriteLine($"  Tool Use ID: {toolUseId}");
                                    Console.ResetColor();
                                }

                                // Allow the tool to proceed
                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                },

                // Post-tool-use hook: runs after a tool completes
                [HookEvent.PostToolUse] = new List<HookMatcher>
                {
                    new()
                    {
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is PostToolUseHookInput postInput)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Hook: PostToolUse]");
                                    Console.WriteLine($"  Tool: {postInput.ToolName}");
                                    Console.WriteLine($"  Has Response: {postInput.ToolResponse != null}");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                },

                // Stop hook: runs when the conversation is about to end
                [HookEvent.Stop] = new List<HookMatcher>
                {
                    new()
                    {
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is StopHookInput stopInput)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine($"[Hook: Stop]");
                                    Console.WriteLine($"  Stop Hook Active: {stopInput.StopHookActive}");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                }
            }
        };

        await using var client = new ClaudeAgentClient(options);

        // Ask Claude to read a file (which will trigger the hooks)
        var prompt = "Read the file at ./README.md and tell me what it's about in one sentence.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Output:");
        Console.WriteLine("-------");

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.WriteLine(text.Text);
                        }
                    }
                    break;

                case ResultMessage result:
                    Console.WriteLine($"\n[Completed - Cost: ${result.TotalCostUsd:F4}]");
                    break;
            }
        }
    }
}
