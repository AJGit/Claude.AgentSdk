/// <summary>
/// Email Agent - Claude Agent SDK Example
///
/// This example demonstrates:
/// - Custom MCP tools for email operations (search, read, archive, label, etc.)
/// - Gmail-like search query syntax
/// - Mock email data store for demonstration
/// - Interactive chat for email management
///
/// Ported from: claude-agent-sdk-demos/email-agent/
/// </summary>

using System.Text.Json;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.EmailAgent;

public static class Program
{
    private const string SystemPrompt = """
                                        You are an intelligent email assistant with access to a user's inbox. You help manage emails efficiently.

                                        ## Your Capabilities
                                        You can:
                                        - Search emails using Gmail-like query syntax
                                        - Read full email contents
                                        - Mark emails as read/unread
                                        - Star/unstar important emails
                                        - Archive emails to clean up the inbox
                                        - Add/remove labels for organization

                                        ## Available Tools
                                        - `get_inbox`: Get recent emails from inbox
                                        - `search_inbox`: Search with query syntax (from:, to:, is:unread, label:, newer_than:, etc.)
                                        - `read_emails`: Read full content of specific emails by ID
                                        - `mark_as_read` / `mark_as_unread`: Update read status
                                        - `star_email` / `unstar_email`: Manage starred emails
                                        - `archive_email`: Remove emails from inbox
                                        - `add_label` / `remove_label`: Organize with labels

                                        ## Search Syntax Examples
                                        - `from:boss@company.com is:unread` - Unread emails from boss
                                        - `label:Finance newer_than:7d` - Finance emails from last week
                                        - `has:attachment budget` - Emails with attachments mentioning budget
                                        - `is:starred` - All starred emails

                                        ## Best Practices
                                        1. When asked about emails, first search to find relevant ones
                                        2. Provide concise summaries with key information
                                        3. Offer to take actions (archive, label, etc.) when appropriate
                                        4. Always reference emails by their subject/sender for clarity
                                        5. Use markdown formatting for readable summaries

                                        ## Response Format
                                        When summarizing emails, use this format:
                                        - **From**: sender
                                        - **Subject**: subject line
                                        - **Date**: when received
                                        - **Summary**: brief description of content
                                        - **Action needed**: yes/no with explanation

                                        Be helpful, concise, and proactive in suggesting email management actions.
                                        """;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Email Agent - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("Manage your emails with AI assistance.");
        Console.WriteLine();
        Console.WriteLine("Example commands:");
        Console.WriteLine("  - Show me my unread emails");
        Console.WriteLine("  - Find emails about the budget");
        Console.WriteLine("  - Archive all newsletters");
        Console.WriteLine("  - Star important emails from my boss");
        Console.WriteLine();
        Console.WriteLine("Type 'exit' to quit, 'inbox' to see raw inbox.");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();

        // Create mock email store with sample data
        MockEmailStore emailStore = new();

        // Create MCP tool server with email tools
        // Uses compile-time generated registration (no reflection)
        McpToolServer toolServer = new("email-tools");
        EmailTools emailTools = new(emailStore);
        toolServer.RegisterToolsCompiled(emailTools);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Loaded {emailStore.GetInbox().Count} emails in mock inbox");
        Console.ResetColor();
        Console.WriteLine();

        ClaudeAgentOptions options = new()
        {
            SystemPrompt = SystemPrompt,
            Model = "sonnet",
            MaxTurns = 30,
            PermissionMode = PermissionMode.AcceptEdits,

            // Register the email tools MCP server
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["email-tools"] = new McpSdkServerConfig { Name = "email-tools", Instance = toolServer }
            },

            // Allow MCP email tools
            AllowedTools =
            [
                "mcp__email-tools__get_inbox",
                "mcp__email-tools__search_inbox",
                "mcp__email-tools__read_emails",
                "mcp__email-tools__mark_as_read",
                "mcp__email-tools__mark_as_unread",
                "mcp__email-tools__star_email",
                "mcp__email-tools__unstar_email",
                "mcp__email-tools__archive_email",
                "mcp__email-tools__add_label",
                "mcp__email-tools__remove_label"
            ]
        };

        await RunChatLoopAsync(options, emailStore);
    }

    private static async Task RunChatLoopAsync(ClaudeAgentOptions options, MockEmailStore store)
    {
        while (true)
        {
            try
            {
                await using ClaudeAgentClient client = new(options);
                await using ClaudeAgentSession session = await client.CreateSessionAsync();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[Connected - ready to manage your emails]");
                Console.ResetColor();
                Console.WriteLine();

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("You: ");
                    Console.ResetColor();

                    string? input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                    {
                        continue;
                    }

                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Goodbye!");
                        return;
                    }

                    if (input.Equals("inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowInbox(store);
                        continue;
                    }

                    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[Starting new conversation...]");
                        Console.WriteLine();
                        break;
                    }

                    await session.SendAsync(input);

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Email Agent: ");
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

                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                Console.WriteLine();
            }
        }
    }

    private static void ShowInbox(MockEmailStore store)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Current Inbox:");
        Console.ResetColor();

        IReadOnlyList<Email> emails = store.GetInbox(15);
        foreach (Email email in emails)
        {
            string readIcon = email.IsRead ? " " : "*";
            string starIcon = email.IsStarred ? "[*]" : "   ";
            string truncSubject = email.Subject.Length > 40
                ? email.Subject[..37] + "..."
                : email.Subject;

            Console.WriteLine($"{readIcon} {starIcon} {email.Date:MM/dd HH:mm} | {email.From,-25} | {truncSubject}");
        }

        int unread = emails.Count(e => !e.IsRead);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Total: {emails.Count} emails, {unread} unread");
        Console.ResetColor();
        Console.WriteLine();
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
                            string toolName = toolUse.Name.Replace("mcp__email-tools__", "");
                            Console.Write($"\n[{toolName}");

                            string? summary = GetToolInputSummary(toolName, toolUse.Input);
                            if (!string.IsNullOrEmpty(summary))
                            {
                                Console.Write($": {summary}");
                            }

                            Console.Write("]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;

                        case ThinkingBlock:
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write("[thinking...]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;
                    }
                }

                break;

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
                "search_inbox" when element.TryGetProperty("query", out JsonElement q) =>
                    $"\"{Truncate(q.GetString(), 30)}\"",
                "read_emails" when element.TryGetProperty("ids", out JsonElement ids) =>
                    $"{ids.GetArrayLength()} email(s)",
                "mark_as_read" or "mark_as_unread" or "star_email" or "unstar_email" or "archive_email"
                    when element.TryGetProperty("ids", out JsonElement ids) =>
                    $"{ids.GetArrayLength()} email(s)",
                "add_label" or "remove_label" when element.TryGetProperty("label", out JsonElement l) =>
                    l.GetString(),
                "get_inbox" when element.TryGetProperty("limit", out JsonElement l) =>
                    $"limit={l.GetInt32()}",
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
