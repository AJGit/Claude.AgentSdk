using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates using hooks to intercept and modify tool execution.
///     Shows usage of strongly-typed enum accessors for hook inputs.
/// </summary>
public class HooksExample : IExample
{
    public string Name => "Hooks (Pre/Post Tool Use)";
    public string Description => "Intercept and log tool usage with hooks";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates using hooks to intercept tool execution.");
        Console.WriteLine("We'll log all tool calls and their results.\n");

        ClaudeAgentOptions options = new()
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
                                    Console.WriteLine("[Hook: PreToolUse]");
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
                                    Console.WriteLine("[Hook: PostToolUse]");
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
                                    Console.WriteLine("[Hook: Stop]");
                                    Console.WriteLine($"  Stop Hook Active: {stopInput.StopHookActive}");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                },

                // SessionStart hook: demonstrates using SourceEnum accessor
                [HookEvent.SessionStart] = new List<HookMatcher>
                {
                    new()
                    {
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is SessionStartHookInput sessionStart)
                                {
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine("[Hook: SessionStart]");

                                    // Use SourceEnum for type-safe source checking
                                    string sourceDescription = sessionStart.SourceEnum switch
                                    {
                                        SessionStartSource.Startup => "Fresh startup",
                                        SessionStartSource.Resume => "Resumed from previous session",
                                        SessionStartSource.Clear => "Session was cleared",
                                        SessionStartSource.Compact => "Session was compacted",
                                        _ => "Unknown"
                                    };
                                    Console.WriteLine($"  Source: {sessionStart.SourceEnum} ({sourceDescription})");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                },

                // SessionEnd hook: demonstrates using ReasonEnum accessor
                [HookEvent.SessionEnd] = new List<HookMatcher>
                {
                    new()
                    {
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is SessionEndHookInput sessionEnd)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                    Console.WriteLine("[Hook: SessionEnd]");

                                    // Use ReasonEnum for type-safe reason checking
                                    string reasonDescription = sessionEnd.ReasonEnum switch
                                    {
                                        SessionEndReason.Clear => "User cleared the session",
                                        SessionEndReason.Logout => "User logged out",
                                        SessionEndReason.PromptInputExit => "User exited at prompt",
                                        SessionEndReason.BypassPermissionsDisabled => "Bypass permissions was disabled",
                                        SessionEndReason.Other => "Other reason",
                                        _ => "Unknown"
                                    };
                                    Console.WriteLine($"  Reason: {sessionEnd.ReasonEnum} ({reasonDescription})");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                },

                // Notification hook: demonstrates using NotificationTypeEnum accessor
                [HookEvent.Notification] = new List<HookMatcher>
                {
                    new()
                    {
                        Hooks =
                        [
                            async (input, toolUseId, context, ct) =>
                            {
                                if (input is NotificationHookInput notification)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                                    Console.WriteLine("[Hook: Notification]");

                                    // Use NotificationTypeEnum for type-safe notification type checking
                                    string icon = notification.NotificationTypeEnum switch
                                    {
                                        NotificationType.PermissionPrompt => "Permission Required",
                                        NotificationType.IdlePrompt => "Idle",
                                        NotificationType.AuthSuccess => "Authenticated",
                                        NotificationType.ElicitationDialog => "Input Needed",
                                        _ => "Unknown"
                                    };
                                    Console.WriteLine($"  Type: {notification.NotificationTypeEnum} ({icon})");
                                    Console.WriteLine($"  Message: {notification.Message}");
                                    Console.ResetColor();
                                }

                                return new SyncHookOutput { Continue = true };
                            }
                        ]
                    }
                }
            }
        };

        await using ClaudeAgentClient client = new(options);

        // Ask Claude to read a file (which will trigger the hooks)
        string prompt = "Read the file at ./README.md and tell me what it's about in one sentence.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Output:");
        Console.WriteLine("-------");

        await foreach (Message message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (ContentBlock block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.WriteLine(text.Text);
                        }
                    }

                    break;

                case ResultMessage result:
                    // Use SubtypeEnum for type-safe result checking
                    string status = result.SubtypeEnum switch
                    {
                        ResultMessageSubtype.Success => "Completed",
                        ResultMessageSubtype.Error => "Error",
                        ResultMessageSubtype.Partial => "Partial",
                        _ => "Unknown"
                    };
                    string context = result.Usage is not null
                        ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k"
                        : "?";
                    Console.WriteLine($"\n[{status} | {result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {context}]");
                    break;
            }
        }
    }
}
