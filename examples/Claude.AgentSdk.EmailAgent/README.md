# Email Agent Example

An intelligent email assistant with custom MCP tools for searching, reading, and managing emails.

## What This Example Demonstrates

- **Custom MCP tools** for email operations
- **Gmail-like search syntax** with operators (from:, is:unread, label:, etc.)
- **Mock email store** with realistic sample data
- **Email management actions** (star, archive, label, mark read/unread)

## Features

The Email Agent can:
- Search emails using powerful query syntax
- Read full email contents
- Mark emails as read/unread
- Star/unstar important emails
- Archive emails to clean up inbox
- Add/remove labels for organization

## Running the Example

```bash
cd examples/Claude.AgentSdk.EmailAgent
dotnet run
```

## Example Prompts

```
You: Show me my unread emails

You: Find emails about the budget from the last week

You: Star all emails from my boss

You: Archive the newsletters

You: Find finance-related emails with attachments

You: What emails need my attention today?
```

## Search Query Syntax

The email agent supports Gmail-like search operators:

| Operator         | Description             | Example                 |
| ---------------- | ----------------------- | ----------------------- |
| `from:`          | Filter by sender        | `from:boss@company.com` |
| `to:`            | Filter by recipient     | `to:team@company.com`   |
| `is:unread`      | Only unread emails      | `is:unread`             |
| `is:starred`     | Only starred emails     | `is:starred`            |
| `has:attachment` | Emails with attachments | `has:attachment`        |
| `label:`         | Filter by label         | `label:Finance`         |
| `newer_than:`    | Recent emails           | `newer_than:7d`         |
| (text)           | Full-text search        | `quarterly report`      |

Combine operators: `from:hr@company.com is:unread newer_than:30d`

## Available MCP Tools

| Tool             | Description                          |
| ---------------- | ------------------------------------ |
| `get_inbox`      | Get recent emails from inbox         |
| `search_inbox`   | Search with Gmail-like query syntax  |
| `read_emails`    | Read full content of specific emails |
| `mark_as_read`   | Mark emails as read                  |
| `mark_as_unread` | Mark emails as unread                |
| `star_email`     | Star important emails                |
| `unstar_email`   | Remove star from emails              |
| `archive_email`  | Archive emails (remove from inbox)   |
| `add_label`      | Add a label to emails                |
| `remove_label`   | Remove a label from emails           |

## Key Code Patterns

### Implementing Search with Operators

```csharp
public IReadOnlyList<Email> Search(string query)
{
    var results = _emails.AsEnumerable();

    // Handle from: operator
    if (query.Contains("from:"))
    {
        var match = Regex.Match(query, @"from:(\S+)");
        if (match.Success)
        {
            var fromValue = match.Groups[1].Value;
            results = results.Where(e =>
                e.From.Contains(fromValue, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Handle is:unread operator
    if (query.Contains("is:unread"))
    {
        results = results.Where(e => !e.IsRead);
    }

    // ... more operators

    return results.OrderByDescending(e => e.Date).ToList();
}
```

### Registering Email Tools

```csharp
// Tools use multi-line descriptions for comprehensive documentation
server.RegisterTool<SearchInboxInput>(
    "search_inbox",
    """
    Search emails using Gmail-like query syntax.
    Supported operators: from:, to:, is:unread, is:starred,
    has:attachment, label:, newer_than:, and full-text search.
    """,
    SearchInboxAsync);

server.RegisterTool<ArchiveEmailInput>(
    "archive_email",
    "Archive one or more emails (remove from inbox).",
    ArchiveEmailAsync);
```

### Configuration with Session-Based API

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = "You are an email assistant...",
    Model = "sonnet",
    PermissionMode = PermissionMode.AcceptEdits,
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["email-tools"] = new McpSdkServerConfig { Name = "email-tools", Instance = toolServer }
    },
    // MCP tools use format: mcp__<server-name>__<tool-name>
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

await using var client = new ClaudeAgentClient(options);

// Create session for bidirectional communication
await using var session = await client.CreateSessionAsync();

// Send and receive messages
await session.SendAsync("Show me unread emails");
await foreach (var message in session.ReceiveResponseAsync())
{
    ProcessMessage(message);
}
```

## Mock Email Data

The example includes 10 realistic sample emails across categories:
- **Work**: Budget reviews, project updates, HR announcements
- **Personal**: Family emails, newsletters
- **Finance**: Bank statements, subscription renewals
- **Shopping**: Order confirmations
- **Developer**: GitHub notifications, job alerts

## Project Structure

```
Claude.AgentSdk.EmailAgent/
├── Program.cs              # Main entry point and chat loop
├── EmailTools.cs           # Custom MCP tool implementations
├── MockEmailStore.cs       # Mock email data and search logic
└── Claude.AgentSdk.EmailAgent.csproj
```

## Example Session

```
You: Show me unread work emails

Email Agent: I'll search for unread emails related to work.

[search_inbox: "is:unread label:Work"]

I found 3 unread work emails:

1. **From**: john.smith@company.com
   **Subject**: Q4 Budget Review Meeting
   **Date**: Today at 2:00 PM
   **Summary**: Request to schedule a budget review meeting...
   **Action needed**: Yes - respond with availability

2. **From**: hr@company.com
   **Subject**: Benefits Enrollment Reminder - Deadline Dec 15
   **Date**: Yesterday
   **Summary**: Annual benefits enrollment period ending...
   **Action needed**: Yes - complete enrollment by Dec 15

3. **From**: github@github.com
   **Subject**: [claude-agent-sdk] Issue #142: Memory leak
   **Date**: 8 hours ago
   **Summary**: New issue reported in repository...
   **Action needed**: Optional - review when time permits

Would you like me to mark any of these as read, star them, or take other actions?

[2.1s | $0.0052]
```

## Ported From

This example is ported from the official TypeScript demo:
`claude-agent-sdk-demos/email-agent/`

Note: The original uses a full web UI with IMAP integration. This C# port provides a console-based interface with mock data while demonstrating the same MCP tool patterns and search functionality.
