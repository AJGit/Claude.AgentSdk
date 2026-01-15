using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Demonstrates custom permission handling for tool execution.
/// </summary>
public class PermissionHandlerExample : IExample
{
    public string Name => "Permission Handler (CanUseTool)";
    public string Description => "Control which tools can be used and with what inputs";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates custom permission handling.");
        Console.WriteLine("We'll allow reading files but block writing to certain paths.\n");

        // Track permission decisions for display
        var permissionLog = new List<string>();

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant with file access.",
            AllowedTools = ["Read", "Write", "Bash"],
            MaxTurns = 5,

            // Custom permission callback
            CanUseTool = async (request, ct) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Permission Request]");
                Console.WriteLine($"  Tool: {request.ToolName}");
                Console.ResetColor();

                // Always allow Read tool
                if (request.ToolName == "Read")
                {
                    permissionLog.Add($"ALLOWED: Read");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  Decision: ALLOWED");
                    Console.ResetColor();
                    return new PermissionResultAllow();
                }

                // Block Write to sensitive paths
                if (request.ToolName == "Write")
                {
                    // Check the file path in the input
                    if (request.Input.TryGetProperty("file_path", out var pathElement))
                    {
                        var path = pathElement.GetString() ?? "";

                        // Block writes to system directories or sensitive files
                        var blockedPatterns = new[] { "/etc/", "/sys/", ".env", "password", "secret" };

                        foreach (var pattern in blockedPatterns)
                        {
                            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                permissionLog.Add($"DENIED: Write to {path} (blocked pattern: {pattern})");
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"  Decision: DENIED (blocked path pattern: {pattern})");
                                Console.ResetColor();

                                return new PermissionResultDeny
                                {
                                    Message = $"Writing to paths containing '{pattern}' is not allowed"
                                };
                            }
                        }
                    }

                    permissionLog.Add($"ALLOWED: Write");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  Decision: ALLOWED");
                    Console.ResetColor();
                    return new PermissionResultAllow();
                }

                // Block Bash entirely for this demo
                if (request.ToolName == "Bash")
                {
                    permissionLog.Add($"DENIED: Bash (disabled for demo)");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Decision: DENIED (Bash disabled for this demo)");
                    Console.ResetColor();

                    return new PermissionResultDeny
                    {
                        Message = "Bash commands are disabled in this example"
                    };
                }

                // Allow other tools by default
                permissionLog.Add($"ALLOWED: {request.ToolName} (default)");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Decision: ALLOWED (default)");
                Console.ResetColor();
                return new PermissionResultAllow();
            }
        };

        await using var client = new ClaudeAgentClient(options);

        // Try different operations to trigger permission checks
        var prompt = @"Please try the following:
1. Read the file ./README.md
2. Create a file called ./test.txt with content 'Hello World'
3. Run the command 'echo test'
Tell me what succeeded and what failed.";

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

        // Show permission log summary
        Console.WriteLine("\nPermission Log Summary:");
        Console.WriteLine("-----------------------");
        foreach (var entry in permissionLog)
        {
            Console.WriteLine($"  {entry}");
        }
    }
}
