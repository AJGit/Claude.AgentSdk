/// <summary>
/// Simple Chat App - Claude Agent SDK Example
///
/// This example demonstrates:
/// - Interactive multi-turn conversation using bidirectional mode
/// - CreateSessionAsync for persistent sessions with clear lifecycle
/// - Streaming responses with SendAsync/ReceiveResponseAsync
/// - Console-based chat interface
///
/// Ported from: claude-agent-sdk-demos/simple-chatapp/
/// </summary>

using System.Text.Json;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.SimpleChatApp;

public static class Program
{
    private const string SystemPrompt = """
                                        You are a helpful AI assistant. You can help users with a wide variety of tasks including:
                                        - Answering questions
                                        - Writing and editing text
                                        - Coding and debugging
                                        - Analysis and research
                                        - Creative tasks

                                        Be concise but thorough in your responses.
                                        """;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Simple Chat App - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("A multi-turn conversational assistant with tool access.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /clear  - Start a new conversation");
        Console.WriteLine("  /exit   - Exit the application");
        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();

        ClaudeAgentOptions options = new()
        {
            SystemPrompt = SystemPrompt,
            Model = "sonnet",
            MaxTurns = 100,
            PermissionMode = PermissionMode.AcceptEdits,
            AllowedTools =
            [
                "Bash", "Read", "Write", "Edit", "Glob", "Grep",
                "WebSearch", "WebFetch", "TodoWrite"
            ]
        };

        await RunChatLoopAsync(options);
    }

    private static async Task RunChatLoopAsync(ClaudeAgentOptions options)
    {
        while (true)
        {
            try
            {
                await using ClaudeAgentClient client = new(options);

                // Create a session for bidirectional communication
                await using ClaudeAgentSession session = await client.CreateSessionAsync();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[Connected - new conversation started]");
                Console.ResetColor();
                Console.WriteLine();

                // Chat loop for this session
                while (true)
                {
                    // Get user input
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("You: ");
                    Console.ResetColor();

                    string? input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                    {
                        continue;
                    }

                    // Handle commands
                    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Goodbye!");
                        return;
                    }

                    if (input.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine();
                        Console.WriteLine("[Starting new conversation...]");
                        Console.WriteLine();
                        break; // Break inner loop to create new session
                    }

                    // Send message to Claude
                    await session.SendAsync(input);

                    // Display response
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Claude: ");
                    Console.ResetColor();

                    MessageResult hasOutput = MessageResult.NoMessage;
                    await foreach (Message message in session.ReceiveResponseAsync())
                    {
                        hasOutput = ProcessMessage(message);
                        if (hasOutput == MessageResult.Completed)
                        {
                            break;
                        }
                    }

                    if (hasOutput == MessageResult.Completed)
                    {
                        Console.WriteLine();
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {ex.Message}");
                Console.ResetColor();

                Console.WriteLine("Press Enter to start a new conversation, or Ctrl+C to exit...");
                Console.ReadLine();
                Console.WriteLine();
            }
        }
    }

    private static MessageResult ProcessMessage(Message message)
    {
        switch (message)
        {
            case AssistantMessage assistant:
                foreach (ContentBlock block in assistant.MessageContent.Content)
                {
                    switch (block)
                    {
                        case TextBlock text:
                            Console.Write(text.Text);
                            return MessageResult.MoreMessages;

                        case ToolUseBlock toolUse:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"\n[Using {toolUse.Name}");

                            // Show brief tool input summary
                            string? summary = GetToolInputSummary(toolUse.Name, toolUse.Input);
                            if (!string.IsNullOrEmpty(summary))
                            {
                                Console.Write($": {summary}");
                            }

                            Console.Write("]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;

                        case ThinkingBlock thinking:
                            // Optionally show thinking (collapsed by default)
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write("[thinking...]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;
                    }
                }

                break;

            case SystemMessage system when system.CompactMetadata is not null:
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"[Context compacted: {system.CompactMetadata.PreTokens:N0} → {system.CompactMetadata.PostTokens:N0} tokens]");
                Console.ResetColor();
                return MessageResult.MoreMessages;

            case ResultMessage result:
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                string context = result.Usage is not null
                    ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k"
                    : "?";
                Console.Write($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {context}]");
                Console.ResetColor();
                return MessageResult.Completed;
        }

        return MessageResult.NoMessage;
    }

    private static string? GetToolInputSummary(string toolName, JsonElement? input)
    {
        if (input is null)
        {
            return null;
        }

        try
        {
            JsonElement element = input.Value;

            return toolName switch
            {
                "WebSearch" when element.TryGetProperty("query", out JsonElement q) =>
                    $"\"{Truncate(q.GetString(), 40)}\"",

                "Read" when element.TryGetProperty("file_path", out JsonElement p) =>
                    Path.GetFileName(p.GetString()),

                "Write" when element.TryGetProperty("file_path", out JsonElement p) =>
                    Path.GetFileName(p.GetString()),

                "Edit" when element.TryGetProperty("file_path", out JsonElement p) =>
                    Path.GetFileName(p.GetString()),

                "Bash" when element.TryGetProperty("command", out JsonElement c) =>
                    Truncate(c.GetString(), 30),

                "Glob" when element.TryGetProperty("pattern", out JsonElement p) =>
                    p.GetString(),

                "Grep" when element.TryGetProperty("pattern", out JsonElement p) =>
                    $"\"{Truncate(p.GetString(), 30)}\"",

                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private enum MessageResult
    {
        MoreMessages,
        NoMessage,
        Completed
    }
}
