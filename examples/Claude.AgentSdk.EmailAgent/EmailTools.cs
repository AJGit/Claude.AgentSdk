using System.Text.Json;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.EmailAgent;

/// <summary>
/// Custom MCP tools for email operations.
/// </summary>
public class EmailTools
{
    private readonly MockEmailStore _store;

    public EmailTools(MockEmailStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Registers all email tools with the MCP server.
    /// </summary>
    public void RegisterTools(McpToolServer server)
    {
        server.RegisterTool<SearchInboxInput>(
            "search_inbox",
            """
            Search emails using Gmail-like query syntax.

            Supported operators:
            - from:sender@example.com - Filter by sender
            - to:recipient@example.com - Filter by recipient
            - is:unread - Only unread emails
            - is:starred - Only starred emails
            - has:attachment - Only emails with attachments
            - label:category - Filter by label
            - newer_than:7d - Emails from last N days
            - Plain text - Full-text search in subject/body

            Examples:
            - "from:boss is:unread" - Unread emails from boss
            - "budget newer_than:30d" - Emails mentioning budget in last 30 days
            - "label:finance has:attachment" - Finance emails with attachments
            """,
            SearchInboxAsync);

        server.RegisterTool<ReadEmailsInput>(
            "read_emails",
            "Read full email content by IDs. Returns complete email details including body.",
            ReadEmailsAsync);

        server.RegisterTool<GetInboxInput>(
            "get_inbox",
            "Get recent emails from inbox. Returns up to 'limit' most recent non-archived emails.",
            GetInboxAsync);

        server.RegisterTool<MarkAsReadInput>(
            "mark_as_read",
            "Mark one or more emails as read.",
            MarkAsReadAsync);

        server.RegisterTool<MarkAsUnreadInput>(
            "mark_as_unread",
            "Mark one or more emails as unread.",
            MarkAsUnreadAsync);

        server.RegisterTool<StarEmailInput>(
            "star_email",
            "Star one or more emails.",
            StarEmailAsync);

        server.RegisterTool<UnstarEmailInput>(
            "unstar_email",
            "Remove star from one or more emails.",
            UnstarEmailAsync);

        server.RegisterTool<ArchiveEmailInput>(
            "archive_email",
            "Archive one or more emails (remove from inbox).",
            ArchiveEmailAsync);

        server.RegisterTool<AddLabelInput>(
            "add_label",
            "Add a label to one or more emails.",
            AddLabelAsync);

        server.RegisterTool<RemoveLabelInput>(
            "remove_label",
            "Remove a label from one or more emails.",
            RemoveLabelAsync);
    }

    private Task<ToolResult> SearchInboxAsync(SearchInboxInput input, CancellationToken ct)
    {
        try
        {
            var results = _store.Search(input.Query);

            if (results.Count == 0)
            {
                return Task.FromResult(ToolResult.Text($"No emails found matching: {input.Query}"));
            }

            var summary = results.Take(input.Limit ?? 20).Select(e => new
            {
                id = e.Id,
                from = e.From,
                subject = e.Subject,
                date = e.Date.ToString("g"),
                is_read = e.IsRead,
                is_starred = e.IsStarred,
                labels = e.Labels,
                snippet = e.Snippet
            });

            var json = JsonSerializer.Serialize(new
            {
                total_results = results.Count,
                showing = Math.Min(results.Count, input.Limit ?? 20),
                emails = summary
            }, new JsonSerializerOptions { WriteIndented = true });

            return Task.FromResult(ToolResult.Text(json));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Search failed: {ex.Message}"));
        }
    }

    private Task<ToolResult> ReadEmailsAsync(ReadEmailsInput input, CancellationToken ct)
    {
        try
        {
            var emails = _store.GetByIds(input.Ids);

            if (emails.Count == 0)
            {
                return Task.FromResult(ToolResult.Text("No emails found with the specified IDs."));
            }

            var fullEmails = emails.Select(e => new
            {
                id = e.Id,
                from = e.From,
                to = e.To,
                subject = e.Subject,
                body = e.Body,
                date = e.Date.ToString("o"),
                is_read = e.IsRead,
                is_starred = e.IsStarred,
                is_archived = e.IsArchived,
                has_attachments = e.HasAttachments,
                labels = e.Labels
            });

            var json = JsonSerializer.Serialize(new { emails = fullEmails },
                new JsonSerializerOptions { WriteIndented = true });

            // Mark as read when reading
            foreach (var email in emails)
            {
                _store.MarkAsRead(email.Id);
            }

            return Task.FromResult(ToolResult.Text(json));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to read emails: {ex.Message}"));
        }
    }

    private Task<ToolResult> GetInboxAsync(GetInboxInput input, CancellationToken ct)
    {
        try
        {
            var emails = _store.GetInbox(input.Limit ?? 20);

            var summary = emails.Select(e => new
            {
                id = e.Id,
                from = e.From,
                subject = e.Subject,
                date = e.Date.ToString("g"),
                is_read = e.IsRead,
                is_starred = e.IsStarred,
                labels = e.Labels,
                snippet = e.Snippet
            });

            var unreadCount = emails.Count(e => !e.IsRead);

            var json = JsonSerializer.Serialize(new
            {
                total_in_inbox = emails.Count,
                unread_count = unreadCount,
                emails = summary
            }, new JsonSerializerOptions { WriteIndented = true });

            return Task.FromResult(ToolResult.Text(json));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to get inbox: {ex.Message}"));
        }
    }

    private Task<ToolResult> MarkAsReadAsync(MarkAsReadInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.MarkAsRead(id);
            }
            return Task.FromResult(ToolResult.Text($"Marked {input.Ids.Length} email(s) as read."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to mark as read: {ex.Message}"));
        }
    }

    private Task<ToolResult> MarkAsUnreadAsync(MarkAsUnreadInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.MarkAsUnread(id);
            }
            return Task.FromResult(ToolResult.Text($"Marked {input.Ids.Length} email(s) as unread."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to mark as unread: {ex.Message}"));
        }
    }

    private Task<ToolResult> StarEmailAsync(StarEmailInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.Star(id);
            }
            return Task.FromResult(ToolResult.Text($"Starred {input.Ids.Length} email(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to star emails: {ex.Message}"));
        }
    }

    private Task<ToolResult> UnstarEmailAsync(UnstarEmailInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.Unstar(id);
            }
            return Task.FromResult(ToolResult.Text($"Unstarred {input.Ids.Length} email(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to unstar emails: {ex.Message}"));
        }
    }

    private Task<ToolResult> ArchiveEmailAsync(ArchiveEmailInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.Archive(id);
            }
            return Task.FromResult(ToolResult.Text($"Archived {input.Ids.Length} email(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to archive emails: {ex.Message}"));
        }
    }

    private Task<ToolResult> AddLabelAsync(AddLabelInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.AddLabel(id, input.Label);
            }
            return Task.FromResult(ToolResult.Text($"Added label '{input.Label}' to {input.Ids.Length} email(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to add label: {ex.Message}"));
        }
    }

    private Task<ToolResult> RemoveLabelAsync(RemoveLabelInput input, CancellationToken ct)
    {
        try
        {
            foreach (var id in input.Ids)
            {
                _store.RemoveLabel(id, input.Label);
            }
            return Task.FromResult(ToolResult.Text($"Removed label '{input.Label}' from {input.Ids.Length} email(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to remove label: {ex.Message}"));
        }
    }
}

// Input types for email tools

public record SearchInboxInput
{
    public required string Query { get; init; }
    public int? Limit { get; init; }
}

public record ReadEmailsInput
{
    public required string[] Ids { get; init; }
}

public record GetInboxInput
{
    public int? Limit { get; init; }
}

public record MarkAsReadInput
{
    public required string[] Ids { get; init; }
}

public record MarkAsUnreadInput
{
    public required string[] Ids { get; init; }
}

public record StarEmailInput
{
    public required string[] Ids { get; init; }
}

public record UnstarEmailInput
{
    public required string[] Ids { get; init; }
}

public record ArchiveEmailInput
{
    public required string[] Ids { get; init; }
}

public record AddLabelInput
{
    public required string[] Ids { get; init; }
    public required string Label { get; init; }
}

public record RemoveLabelInput
{
    public required string[] Ids { get; init; }
    public required string Label { get; init; }
}
