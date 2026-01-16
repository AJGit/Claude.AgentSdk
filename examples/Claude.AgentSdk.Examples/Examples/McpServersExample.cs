using System.Text.Json;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Demonstrates different MCP server configurations.
/// </summary>
public class McpServersExample : IExample
{
    public string Name => "MCP Servers (External & SDK)";
    public string Description => "Configure MCP servers: stdio, SSE, HTTP, and in-process SDK";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates different MCP server configurations.\n");
        Console.WriteLine("MCP (Model Context Protocol) allows Claude to use external tools.\n");

        // Show configuration examples
        Console.WriteLine("1. STDIO Server Configuration");
        Console.WriteLine("------------------------------");
        Console.WriteLine("Use for external process-based MCP servers:");
        Console.WriteLine();
        Console.WriteLine(@"McpServers = new Dictionary<string, McpServerConfig>
{
    [""filesystem""] = new McpStdioServerConfig
    {
        Command = ""npx"",
        Args = [""-y"", ""@anthropic/mcp-server-filesystem"", ""./data""],
        Env = new Dictionary<string, string>
        {
            [""NODE_ENV""] = ""production""
        }
    }
}");

        Console.WriteLine("\n2. SSE Server Configuration");
        Console.WriteLine("----------------------------");
        Console.WriteLine("Use for Server-Sent Events based remote servers:");
        Console.WriteLine();
        Console.WriteLine(@"McpServers = new Dictionary<string, McpServerConfig>
{
    [""remote-tools""] = new McpSseServerConfig
    {
        Url = ""https://api.example.com/mcp/sse"",
        Headers = new Dictionary<string, string>
        {
            [""Authorization""] = ""Bearer token123""
        }
    }
}");

        Console.WriteLine("\n3. HTTP Server Configuration");
        Console.WriteLine("-----------------------------");
        Console.WriteLine("Use for HTTP-based remote servers:");
        Console.WriteLine();
        Console.WriteLine(@"McpServers = new Dictionary<string, McpServerConfig>
{
    [""api-tools""] = new McpHttpServerConfig
    {
        Url = ""https://api.example.com/mcp"",
        Headers = new Dictionary<string, string>
        {
            [""X-API-Key""] = ""api-key-123""
        }
    }
}");

        Console.WriteLine("\n4. In-Process SDK Server (Live Demo)");
        Console.WriteLine("-------------------------------------");
        Console.WriteLine("Use for tools defined in your C# code:");
        Console.WriteLine();

        // Create an in-process MCP server
        var toolServer = new McpToolServer("time-tools");
        var timeTools = new TimeTools();
        // Use compile-time generated registration (no reflection)
        toolServer.RegisterToolsCompiled(timeTools);

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["time-tools"] = new McpSdkServerConfig
                {
                    Name = "time-tools",
                    Instance = toolServer
                }
            },
            AllowedTools = ["mcp__time-tools__current_time", "mcp__time-tools__time_difference"],
            SystemPrompt = "You are a helpful assistant with access to time-related tools.",
            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 3
        };

        await using var client = new ClaudeAgentClient(options);

        var prompt = "What is the current time? Also, how many days are between January 1, 2024 and December 31, 2024?";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");
        Console.WriteLine("---------");

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
                                Console.WriteLine(text.Text);
                                break;

                            case ToolUseBlock toolUse:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"\n[Tool: {toolUse.Name}]");
                                Console.ResetColor();
                                break;

                            case ToolResultBlock toolResult:
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[Result: {toolResult.Content}]");
                                Console.ResetColor();
                                break;
                        }
                    }
                    break;

                case ResultMessage result:
                    Console.WriteLine($"\n[Cost: ${result.TotalCostUsd:F4}]");
                    break;
            }
        }

        Console.WriteLine("\n\n5. Multiple Servers");
        Console.WriteLine("-------------------");
        Console.WriteLine("You can configure multiple MCP servers together:");
        Console.WriteLine();
        Console.WriteLine(@"McpServers = new Dictionary<string, McpServerConfig>
{
    // External process
    [""filesystem""] = new McpStdioServerConfig { Command = ""npx"", Args = [...] },

    // Remote server
    [""api""] = new McpHttpServerConfig { Url = ""https://..."" },

    // In-process
    [""custom""] = new McpSdkServerConfig { Name = ""custom"", Instance = toolServer }
}");
    }
}

/// <summary>
/// Class containing time-related tool methods.
/// Uses [GenerateToolRegistration] for compile-time tool registration.
/// </summary>
[GenerateToolRegistration]
public class TimeTools
{
    /// <summary>
    /// Get the current date and time.
    /// </summary>
    [ClaudeTool("current_time", "Get the current date and time in a specified timezone",
        Categories = ["time"],
        TimeoutSeconds = 3)]
    public string GetCurrentTime(
        [ToolParameter(Description = "IANA timezone identifier like 'America/New_York' or 'Europe/London'. If not specified, returns UTC time.",
                       Example = "America/New_York")]
        string? timezone = null)
    {
        var now = DateTime.UtcNow;

        // Simple timezone handling for demo
        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                // Fallback to UTC
            }
        }

        return JsonSerializer.Serialize(new
        {
            datetime = now.ToString("yyyy-MM-dd HH:mm:ss"),
            timezone = timezone ?? "UTC",
            unix_timestamp = ((DateTimeOffset)now).ToUnixTimeSeconds()
        });
    }

    /// <summary>
    /// Calculate the difference between two dates.
    /// </summary>
    [ClaudeTool("time_difference", "Calculate the difference between two dates in days, hours, and minutes",
        Categories = ["time"],
        TimeoutSeconds = 3)]
    public string GetTimeDifference(
        [ToolParameter(Description = "Start date in ISO 8601 format (e.g., '2024-01-01' or '2024-01-01T12:00:00')",
                       Example = "2024-01-01")]
        string startDate,
        [ToolParameter(Description = "End date in ISO 8601 format (e.g., '2024-12-31' or '2024-12-31T23:59:59')",
                       Example = "2024-12-31")]
        string endDate)
    {
        if (!DateTime.TryParse(startDate, out var start))
        {
            return JsonSerializer.Serialize(new { error = $"Invalid start date: {startDate}" });
        }

        if (!DateTime.TryParse(endDate, out var end))
        {
            return JsonSerializer.Serialize(new { error = $"Invalid end date: {endDate}" });
        }

        var diff = end - start;

        return JsonSerializer.Serialize(new
        {
            start_date = start.ToString("yyyy-MM-dd"),
            end_date = end.ToString("yyyy-MM-dd"),
            days = (int)diff.TotalDays,
            hours = (int)diff.TotalHours,
            minutes = (int)diff.TotalMinutes,
            description = $"{(int)diff.TotalDays} days, {diff.Hours} hours, {diff.Minutes} minutes"
        });
    }
}
