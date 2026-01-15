namespace Claude.AgentSdk.EmailAgent;

/// <summary>
/// Represents an email in the mock email store.
/// </summary>
public record Email
{
    public required string Id { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTime Date { get; init; }
    public bool IsRead { get; set; }
    public bool IsStarred { get; set; }
    public bool IsArchived { get; set; }
    public List<string> Labels { get; init; } = [];
    public bool HasAttachments { get; init; }
    public string? Snippet => Body.Length > 100 ? Body[..100] + "..." : Body;
}

/// <summary>
/// Mock email store with sample data for demonstration.
/// </summary>
public class MockEmailStore
{
    private readonly List<Email> _emails = [];

    public MockEmailStore()
    {
        // Generate sample emails
        SeedSampleEmails();
    }

    public IReadOnlyList<Email> GetInbox(int limit = 50)
    {
        return _emails
            .Where(e => !e.IsArchived)
            .OrderByDescending(e => e.Date)
            .Take(limit)
            .ToList();
    }

    public IReadOnlyList<Email> Search(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // Parse Gmail-like search operators
        var results = _emails.AsEnumerable();

        // Handle from: operator
        if (lowerQuery.Contains("from:"))
        {
            var fromMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"from:(\S+)");
            if (fromMatch.Success)
            {
                var fromValue = fromMatch.Groups[1].Value;
                results = results.Where(e => e.From.Contains(fromValue, StringComparison.OrdinalIgnoreCase));
                lowerQuery = lowerQuery.Replace(fromMatch.Value, "").Trim();
            }
        }

        // Handle to: operator
        if (lowerQuery.Contains("to:"))
        {
            var toMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"to:(\S+)");
            if (toMatch.Success)
            {
                var toValue = toMatch.Groups[1].Value;
                results = results.Where(e => e.To.Contains(toValue, StringComparison.OrdinalIgnoreCase));
                lowerQuery = lowerQuery.Replace(toMatch.Value, "").Trim();
            }
        }

        // Handle is:unread operator
        if (lowerQuery.Contains("is:unread"))
        {
            results = results.Where(e => !e.IsRead);
            lowerQuery = lowerQuery.Replace("is:unread", "").Trim();
        }

        // Handle is:starred operator
        if (lowerQuery.Contains("is:starred"))
        {
            results = results.Where(e => e.IsStarred);
            lowerQuery = lowerQuery.Replace("is:starred", "").Trim();
        }

        // Handle has:attachment operator
        if (lowerQuery.Contains("has:attachment"))
        {
            results = results.Where(e => e.HasAttachments);
            lowerQuery = lowerQuery.Replace("has:attachment", "").Trim();
        }

        // Handle label: operator
        if (lowerQuery.Contains("label:"))
        {
            var labelMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"label:(\S+)");
            if (labelMatch.Success)
            {
                var labelValue = labelMatch.Groups[1].Value;
                results = results.Where(e => e.Labels.Any(l => l.Contains(labelValue, StringComparison.OrdinalIgnoreCase)));
                lowerQuery = lowerQuery.Replace(labelMatch.Value, "").Trim();
            }
        }

        // Handle newer_than: operator (e.g., newer_than:7d)
        if (lowerQuery.Contains("newer_than:"))
        {
            var newerMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"newer_than:(\d+)d");
            if (newerMatch.Success && int.TryParse(newerMatch.Groups[1].Value, out var days))
            {
                var cutoff = DateTime.Now.AddDays(-days);
                results = results.Where(e => e.Date >= cutoff);
                lowerQuery = lowerQuery.Replace(newerMatch.Value, "").Trim();
            }
        }

        // Full-text search on remaining query
        if (!string.IsNullOrWhiteSpace(lowerQuery))
        {
            results = results.Where(e =>
                e.Subject.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Body.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                e.From.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
        }

        return results.OrderByDescending(e => e.Date).ToList();
    }

    public Email? GetById(string id)
    {
        return _emails.FirstOrDefault(e => e.Id == id);
    }

    public IReadOnlyList<Email> GetByIds(IEnumerable<string> ids)
    {
        var idSet = ids.ToHashSet();
        return _emails.Where(e => idSet.Contains(e.Id)).ToList();
    }

    public void MarkAsRead(string id)
    {
        var email = GetById(id);
        if (email != null) email.IsRead = true;
    }

    public void MarkAsUnread(string id)
    {
        var email = GetById(id);
        if (email != null) email.IsRead = false;
    }

    public void Star(string id)
    {
        var email = GetById(id);
        if (email != null) email.IsStarred = true;
    }

    public void Unstar(string id)
    {
        var email = GetById(id);
        if (email != null) email.IsStarred = false;
    }

    public void Archive(string id)
    {
        var email = GetById(id);
        if (email != null) email.IsArchived = true;
    }

    public void AddLabel(string id, string label)
    {
        var email = GetById(id);
        if (email != null && !email.Labels.Contains(label))
        {
            email.Labels.Add(label);
        }
    }

    public void RemoveLabel(string id, string label)
    {
        var email = GetById(id);
        email?.Labels.Remove(label);
    }

    private void SeedSampleEmails()
    {
        var now = DateTime.Now;

        _emails.AddRange([
            // Work emails
            new Email
            {
                Id = "email_001",
                From = "john.smith@company.com",
                To = "me@example.com",
                Subject = "Q4 Budget Review Meeting",
                Body = """
                    Hi team,

                    I'd like to schedule a meeting to review our Q4 budget projections.
                    We're currently tracking 15% under budget on operational expenses,
                    but our marketing spend is up 8% from last quarter.

                    Key items to discuss:
                    - Revenue projections ($2.4M expected)
                    - Marketing ROI analysis
                    - Headcount planning for Q1

                    Please review the attached spreadsheet before the meeting.

                    Best,
                    John
                    """,
                Date = now.AddHours(-2),
                IsRead = false,
                HasAttachments = true,
                Labels = ["Work", "Finance"]
            },
            new Email
            {
                Id = "email_002",
                From = "sarah.johnson@company.com",
                To = "me@example.com",
                Subject = "RE: Project Alpha Status Update",
                Body = """
                    Thanks for the update! The client is very happy with the progress.

                    A few notes:
                    - Phase 2 milestone completed on schedule
                    - Testing team reports 95% test coverage
                    - Deployment to staging planned for Friday

                    Can you send me the updated timeline for Phase 3?

                    Sarah
                    """,
                Date = now.AddHours(-5),
                IsRead = true,
                Labels = ["Work", "Projects"]
            },
            new Email
            {
                Id = "email_003",
                From = "hr@company.com",
                To = "all@company.com",
                Subject = "Benefits Enrollment Reminder - Deadline Dec 15",
                Body = """
                    Dear Employees,

                    This is a reminder that the annual benefits enrollment period ends on December 15th.

                    Changes for 2025:
                    - New dental plan option with lower deductible
                    - Increased 401(k) match to 5%
                    - Expanded mental health coverage

                    Please log in to the HR portal to make your selections.

                    Questions? Contact benefits@company.com

                    HR Team
                    """,
                Date = now.AddDays(-1),
                IsRead = false,
                Labels = ["Work", "HR"]
            },

            // Personal emails
            new Email
            {
                Id = "email_004",
                From = "newsletter@techweekly.com",
                To = "me@example.com",
                Subject = "This Week in AI: GPT-5 Rumors and More",
                Body = """
                    TECH WEEKLY DIGEST

                    TOP STORIES:
                    1. OpenAI reportedly testing GPT-5 internally
                    2. Apple announces new M4 chip lineup
                    3. Google's Gemini 2.0 benchmarks leak
                    4. EU AI Act enforcement begins January 2025

                    DEVELOPER CORNER:
                    - New TypeScript 6.0 features preview
                    - Rust adoption hits all-time high
                    - WebAssembly 3.0 draft specification released

                    Read more at techweekly.com/digest

                    Unsubscribe | Manage Preferences
                    """,
                Date = now.AddDays(-2),
                IsRead = true,
                Labels = ["Newsletter"]
            },
            new Email
            {
                Id = "email_005",
                From = "amazon@amazon.com",
                To = "me@example.com",
                Subject = "Your order has shipped!",
                Body = """
                    Your Amazon order #112-3456789 has shipped!

                    Items:
                    - Mechanical Keyboard (Cherry MX Blue) - $129.99
                    - USB-C Hub 7-in-1 - $45.99

                    Estimated delivery: December 12, 2024
                    Carrier: UPS

                    Track your package: amazon.com/track/ABC123

                    Thank you for shopping with Amazon!
                    """,
                Date = now.AddDays(-1).AddHours(-3),
                IsRead = true,
                Labels = ["Shopping"]
            },
            new Email
            {
                Id = "email_006",
                From = "bank@chase.com",
                To = "me@example.com",
                Subject = "Your December Statement is Ready",
                Body = """
                    Your Chase account statement for November 2024 is now available.

                    Account Summary:
                    - Checking (*1234): $5,432.10
                    - Savings (*5678): $12,500.00
                    - Credit Card (*9012): Balance $1,234.56, Payment Due Dec 15

                    Log in to view your full statement at chase.com

                    This is an automated message. Please do not reply.
                    """,
                Date = now.AddDays(-3),
                IsRead = false,
                HasAttachments = true,
                Labels = ["Finance", "Banking"]
            },
            new Email
            {
                Id = "email_007",
                From = "mom@family.com",
                To = "me@example.com",
                Subject = "Holiday dinner plans",
                Body = """
                    Hi sweetie!

                    Just confirming - we're doing Christmas dinner at our place this year.
                    Everyone's arriving around 2pm on the 25th.

                    Can you bring that amazing apple pie you made last year?
                    Also, your cousin Mike is bringing his new girlfriend, so we'll be 12 people total.

                    Let me know if you need any help with directions or anything!

                    Love,
                    Mom
                    """,
                Date = now.AddDays(-4),
                IsRead = true,
                IsStarred = true,
                Labels = ["Family"]
            },
            new Email
            {
                Id = "email_008",
                From = "github@github.com",
                To = "me@example.com",
                Subject = "[claude-agent-sdk] Issue #142: Memory leak in query handler",
                Body = """
                    @developer opened a new issue in anthropics/claude-agent-sdk:

                    **Issue #142: Memory leak in query handler**

                    Description:
                    When running long-running queries with many tool calls, memory usage
                    grows unbounded. Profiling shows accumulation in the message queue.

                    Steps to reproduce:
                    1. Run query with 100+ tool calls
                    2. Monitor memory usage
                    3. Observe 50MB+ memory growth

                    Environment:
                    - Node.js 22.1.0
                    - SDK version 0.1.28

                    ---
                    Reply to this email or view the issue at:
                    https://github.com/anthropics/claude-agent-sdk/issues/142
                    """,
                Date = now.AddHours(-8),
                IsRead = false,
                Labels = ["GitHub", "Work"]
            },
            new Email
            {
                Id = "email_009",
                From = "support@netflix.com",
                To = "me@example.com",
                Subject = "Your Netflix subscription renewal",
                Body = """
                    Hi there,

                    Your Netflix subscription will automatically renew on December 15, 2024.

                    Plan: Premium (4K + HDR)
                    Amount: $22.99/month

                    Your payment method: Visa ending in 4242

                    To update your payment method or cancel, visit netflix.com/account

                    Happy streaming!
                    The Netflix Team
                    """,
                Date = now.AddDays(-5),
                IsRead = true,
                Labels = ["Subscriptions"]
            },
            new Email
            {
                Id = "email_010",
                From = "recruiter@linkedin.com",
                To = "me@example.com",
                Subject = "New job matches for you: Senior Software Engineer",
                Body = """
                    Based on your profile, here are new job matches:

                    1. Senior Software Engineer - Google
                       Mountain View, CA | $200K-$350K
                       Posted 2 days ago | 45 applicants

                    2. Staff Engineer - Stripe
                       San Francisco, CA | $250K-$400K
                       Posted 1 week ago | 120 applicants

                    3. Principal Engineer - Anthropic
                       San Francisco, CA | $300K-$450K
                       Posted 3 days ago | 30 applicants

                    View all matches: linkedin.com/jobs

                    Update your preferences: linkedin.com/settings
                    """,
                Date = now.AddDays(-2).AddHours(-6),
                IsRead = false,
                Labels = ["Jobs"]
            },
        ]);
    }
}
