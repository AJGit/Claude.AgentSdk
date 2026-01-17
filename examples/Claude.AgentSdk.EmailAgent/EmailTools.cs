using System.Text.Json;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.EmailAgent;

/// <summary>
///     Custom MCP tools for email operations.
///     Uses [GenerateToolRegistration] for compile-time tool registration.
/// </summary>
[GenerateToolRegistration]
public class EmailTools(MockEmailStore store)
{
    private readonly MockEmailStore _store = store;

    /// <summary>
    ///     Search emails using Gmail-like query syntax.
    /// </summary>
    [ClaudeTool("search_inbox",
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
        Categories = ["email"])]
    public string SearchInbox(
        [ToolParameter(Description = "Gmail-style search query")]
        string query,
        [ToolParameter(Description = "Maximum number of results to return")]
        int? limit = 20)
    {
        try
        {
            IReadOnlyList<Email> results = _store.Search(query);

            if (results.Count == 0)
            {
                return $"No emails found matching: {query}";
            }

            var summary = results.Take(limit ?? 20).Select(e => new
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

            return JsonSerializer.Serialize(new
            {
                total_results = results.Count,
                showing = Math.Min(results.Count, limit ?? 20),
                emails = summary
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    /// <summary>
    ///     Read full email content by IDs.
    /// </summary>
    [ClaudeTool("read_emails",
        "Read full email content by IDs. Returns complete email details including body.",
        Categories = ["email"])]
    public string ReadEmails(
        [ToolParameter(Description = "Array of email IDs to read")]
        string[] ids)
    {
        try
        {
            IReadOnlyList<Email> emails = _store.GetByIds(ids);

            if (emails.Count == 0)
            {
                return "No emails found with the specified IDs.";
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

            // Mark as read when reading
            foreach (Email email in emails)
            {
                _store.MarkAsRead(email.Id);
            }

            return JsonSerializer.Serialize(new { emails = fullEmails },
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Failed to read emails: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get recent emails from inbox.
    /// </summary>
    [ClaudeTool("get_inbox",
        "Get recent emails from inbox. Returns up to 'limit' most recent non-archived emails.",
        Categories = ["email"])]
    public string GetInbox(
        [ToolParameter(Description = "Maximum number of emails to return")]
        int? limit = 20)
    {
        try
        {
            IReadOnlyList<Email> emails = _store.GetInbox(limit ?? 20);

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

            int unreadCount = emails.Count(e => !e.IsRead);

            return JsonSerializer.Serialize(new
            {
                total_in_inbox = emails.Count,
                unread_count = unreadCount,
                emails = summary
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Failed to get inbox: {ex.Message}";
        }
    }

    /// <summary>
    ///     Mark one or more emails as read.
    /// </summary>
    [ClaudeTool("mark_as_read",
        "Mark one or more emails as read.",
        Categories = ["email"])]
    public string MarkAsRead(
        [ToolParameter(Description = "Array of email IDs to mark as read")]
        string[] ids)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.MarkAsRead(id);
            }

            return $"Marked {ids.Length} email(s) as read.";
        }
        catch (Exception ex)
        {
            return $"Failed to mark as read: {ex.Message}";
        }
    }

    /// <summary>
    ///     Mark one or more emails as unread.
    /// </summary>
    [ClaudeTool("mark_as_unread",
        "Mark one or more emails as unread.",
        Categories = ["email"])]
    public string MarkAsUnread(
        [ToolParameter(Description = "Array of email IDs to mark as unread")]
        string[] ids)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.MarkAsUnread(id);
            }

            return $"Marked {ids.Length} email(s) as unread.";
        }
        catch (Exception ex)
        {
            return $"Failed to mark as unread: {ex.Message}";
        }
    }

    /// <summary>
    ///     Star one or more emails.
    /// </summary>
    [ClaudeTool("star_email",
        "Star one or more emails.",
        Categories = ["email"])]
    public string StarEmail(
        [ToolParameter(Description = "Array of email IDs to star")]
        string[] ids)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.Star(id);
            }

            return $"Starred {ids.Length} email(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to star emails: {ex.Message}";
        }
    }

    /// <summary>
    ///     Remove star from one or more emails.
    /// </summary>
    [ClaudeTool("unstar_email",
        "Remove star from one or more emails.",
        Categories = ["email"])]
    public string UnstarEmail(
        [ToolParameter(Description = "Array of email IDs to unstar")]
        string[] ids)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.Unstar(id);
            }

            return $"Unstarred {ids.Length} email(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to unstar emails: {ex.Message}";
        }
    }

    /// <summary>
    ///     Archive one or more emails (remove from inbox).
    /// </summary>
    [ClaudeTool("archive_email",
        "Archive one or more emails (remove from inbox).",
        Categories = ["email"])]
    public string ArchiveEmail(
        [ToolParameter(Description = "Array of email IDs to archive")]
        string[] ids)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.Archive(id);
            }

            return $"Archived {ids.Length} email(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to archive emails: {ex.Message}";
        }
    }

    /// <summary>
    ///     Add a label to one or more emails.
    /// </summary>
    [ClaudeTool("add_label",
        "Add a label to one or more emails.",
        Categories = ["email"])]
    public string AddLabel(
        [ToolParameter(Description = "Array of email IDs to label")]
        string[] ids,
        [ToolParameter(Description = "Label name to add")]
        string label)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.AddLabel(id, label);
            }

            return $"Added label '{label}' to {ids.Length} email(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to add label: {ex.Message}";
        }
    }

    /// <summary>
    ///     Remove a label from one or more emails.
    /// </summary>
    [ClaudeTool("remove_label",
        "Remove a label from one or more emails.",
        Categories = ["email"])]
    public string RemoveLabel(
        [ToolParameter(Description = "Array of email IDs to unlabel")]
        string[] ids,
        [ToolParameter(Description = "Label name to remove")]
        string label)
    {
        try
        {
            foreach (string id in ids)
            {
                _store.RemoveLabel(id, label);
            }

            return $"Removed label '{label}' from {ids.Length} email(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to remove label: {ex.Message}";
        }
    }
}

// Input types for email tools - marked with [GenerateSchema] for compile-time schema generation

/// <summary>Input for searching the inbox.</summary>
[GenerateSchema]
public record SearchInboxInput
{
    [ToolParameter(Description = "Gmail-style search query")]
    public required string Query { get; init; }

    [ToolParameter(Description = "Maximum number of results to return")]
    public int? Limit { get; init; }
}

/// <summary>Input for reading emails.</summary>
[GenerateSchema]
public record ReadEmailsInput
{
    [ToolParameter(Description = "Array of email IDs to read")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for getting inbox.</summary>
[GenerateSchema]
public record GetInboxInput
{
    [ToolParameter(Description = "Maximum number of emails to return")]
    public int? Limit { get; init; }
}

/// <summary>Input for marking emails as read.</summary>
[GenerateSchema]
public record MarkAsReadInput
{
    [ToolParameter(Description = "Array of email IDs to mark as read")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for marking emails as unread.</summary>
[GenerateSchema]
public record MarkAsUnreadInput
{
    [ToolParameter(Description = "Array of email IDs to mark as unread")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for starring emails.</summary>
[GenerateSchema]
public record StarEmailInput
{
    [ToolParameter(Description = "Array of email IDs to star")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for unstarring emails.</summary>
[GenerateSchema]
public record UnstarEmailInput
{
    [ToolParameter(Description = "Array of email IDs to unstar")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for archiving emails.</summary>
[GenerateSchema]
public record ArchiveEmailInput
{
    [ToolParameter(Description = "Array of email IDs to archive")]
    public required string[] Ids { get; init; }
}

/// <summary>Input for adding labels to emails.</summary>
[GenerateSchema]
public record AddLabelInput
{
    [ToolParameter(Description = "Array of email IDs to label")]
    public required string[] Ids { get; init; }

    [ToolParameter(Description = "Label name to add")]
    public required string Label { get; init; }
}

/// <summary>Input for removing labels from emails.</summary>
[GenerateSchema]
public record RemoveLabelInput
{
    [ToolParameter(Description = "Array of email IDs to unlabel")]
    public required string[] Ids { get; init; }

    [ToolParameter(Description = "Label name to remove")]
    public required string Label { get; init; }
}
