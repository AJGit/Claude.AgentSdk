using Claude.AgentSdk;
using Claude.AgentSdk.DamageControl.Hooks;
using Claude.AgentSdk.DamageControl.Security;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.DamageControl;

/// <summary>
///     Demonstrates the DamageControl hooks for protecting Claude agent sessions.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            Claude AgentSdk - Damage Control Sample             ║");
        Console.WriteLine("║   Defense-in-depth protection for Claude agent sessions        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load security configuration
        var config = SecurityConfigLoader.Load();
        PrintConfigSummary(config);

        // Create hooks
        var damageControl = new DamageControlHooks(config);

        Console.WriteLine("\nSelect an option:");
        Console.WriteLine("  [1] Interactive test mode (test commands without running Claude)");
        Console.WriteLine("  [2] Run Claude agent with Damage Control hooks enabled");
        Console.WriteLine("  [Q] Quit");
        Console.WriteLine();
        Console.Write("Choice: ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await RunInteractiveTest(config);
                break;
            case "2":
                await RunAgentSession(damageControl.GetHooksCompiled());
                break;
            default:
                Console.WriteLine("Goodbye!");
                break;
        }
    }

    private static void PrintConfigSummary(SecurityConfig config)
    {
        Console.WriteLine("Configuration loaded:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  • {config.BashToolPatterns.Count} bash patterns");
        Console.WriteLine($"  • {config.ZeroAccessPaths.Count} zero-access paths");
        Console.WriteLine($"  • {config.ReadOnlyPaths.Count} read-only paths");
        Console.WriteLine($"  • {config.NoDeletePaths.Count} no-delete paths");
        Console.ResetColor();
    }

    private static async Task RunInteractiveTest(SecurityConfig config)
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   Interactive Test Mode                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Test commands against Damage Control patterns.");
        Console.WriteLine("Type 'quit' to exit, 'examples' to see test commands.\n");

        var commandChecker = new CommandChecker(config);
        var pathMatcher = new PathMatcher(config);

        while (true)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("examples", StringComparison.OrdinalIgnoreCase))
            {
                PrintExamples();
                continue;
            }

            // Check if it looks like a path or a command
            if (input.StartsWith("/") || input.StartsWith("~") || input.StartsWith("./") ||
                input.Contains(".env") || input.Contains(".pem"))
            {
                // Test as path
                Console.WriteLine("\nTesting as file path:");
                TestPath(pathMatcher, input);
            }
            else
            {
                // Test as command
                Console.WriteLine("\nTesting as bash command:");
                var result = commandChecker.Check(input);
                PrintResult(result);
            }
        }
    }

    private static void TestPath(PathMatcher matcher, string path)
    {
        Console.WriteLine($"  Path: {path}");
        Console.WriteLine($"  Normalized: {matcher.NormalizePath(path)}");

        var readResult = matcher.CheckForRead(path);
        var editResult = matcher.CheckForEdit(path);
        var deleteResult = matcher.CheckForDelete(path);

        Console.Write("  Read:   ");
        PrintResultShort(readResult);
        Console.Write("  Edit:   ");
        PrintResultShort(editResult);
        Console.Write("  Delete: ");
        PrintResultShort(deleteResult);
    }

    private static void PrintResultShort(CheckResult result)
    {
        if (result.Blocked)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"BLOCKED - {result.Reason}");
        }
        else if (result.Ask)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"ASK - {result.Reason}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ALLOWED");
        }
        Console.ResetColor();
    }

    private static void PrintResult(CheckResult result)
    {
        if (result.Blocked)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [X] BLOCKED: {result.Reason}");
            Console.ResetColor();
        }
        else if (result.Ask)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [?] CONFIRMATION REQUIRED: {result.Reason}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [OK] ALLOWED");
            Console.ResetColor();
        }
    }

    private static void PrintExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                      Example Test Commands                     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        var examples = new (string Category, (string Command, string Expected)[] Commands)[]
        {
            ("Destructive File Operations (BLOCKED)", [
                ("rm -rf /tmp/test", "rm with recursive or force flags"),
                ("rm --force file.txt", "rm with --force flag"),
                ("sudo rm /etc/passwd", "sudo rm"),
            ]),
            ("Git Operations (BLOCKED)", [
                ("git reset --hard HEAD~1", "git reset --hard"),
                ("git push origin main --force", "git push --force"),
                ("git clean -fd", "git clean with force/directory"),
                ("git stash clear", "git stash clear"),
            ]),
            ("Git Operations (ASK)", [
                ("git checkout -- .", "Discards all uncommitted changes"),
                ("git stash drop stash@{0}", "Permanently deletes a stash"),
                ("git branch -D feature", "Force deletes branch"),
            ]),
            ("Cloud Infrastructure (BLOCKED)", [
                ("terraform destroy", "terraform destroy"),
                ("aws s3 rm s3://bucket --recursive", "aws s3 rm --recursive"),
                ("gcloud projects delete my-project", "gcloud projects delete"),
            ]),
            ("Database Operations (BLOCKED)", [
                ("DELETE FROM users;", "DELETE without WHERE clause"),
                ("DROP TABLE customers", "DROP TABLE"),
                ("redis-cli FLUSHALL", "redis-cli FLUSHALL"),
            ]),
            ("Database Operations (ASK)", [
                ("DELETE FROM users WHERE id = 123", "SQL DELETE with specific ID"),
            ]),
            ("Safe Commands (ALLOWED)", [
                ("git status", ""),
                ("ls -la", ""),
                ("npm install", ""),
                ("docker ps", ""),
            ]),
            ("Path Tests (Various)", [
                ("~/.ssh/id_rsa", "zero-access path"),
                (".env", "zero-access path"),
                ("package-lock.json", "read-only path"),
                ("LICENSE", "no-delete path"),
            ]),
        };

        foreach (var (category, commands) in examples)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{category}:");
            Console.ResetColor();

            foreach (var (command, expected) in commands)
            {
                if (string.IsNullOrEmpty(expected))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  {command}");
                }
                else if (category.Contains("ASK"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  {command}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  {command}");
                }
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }

    private static async Task RunAgentSession(
        IReadOnlyDictionary<Claude.AgentSdk.Protocol.HookEvent, IReadOnlyList<Claude.AgentSdk.Protocol.HookMatcher>> hooks)
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║               Claude Agent with Damage Control                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Starting Claude agent session with Damage Control hooks enabled.");
        Console.WriteLine("The hooks will intercept and validate all tool calls.\n");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant. Be careful with file operations.",
            PermissionMode = PermissionMode.Default,
            MaxTurns = 10,
            Hooks = hooks,
            // Only load project-level CLAUDE.md, skip user's global ~/.claude/CLAUDE.md
            SettingSources = [SettingSource.Project]
        };

        try
        {
            await using var client = new ClaudeAgentClient(options);

            Console.WriteLine("Creating session...");
            await using var session = await client.CreateSessionAsync();
            Console.WriteLine("Session created. You can now have a multi-turn conversation.\n");

            while (true)
            {
                Console.WriteLine("Enter your prompt (or 'quit' to exit):");
                Console.Write("> ");
                var prompt = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(prompt))
                    continue;

                if (prompt.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Session ended.");
                    break;
                }

                // Send the message
                await session.SendAsync(prompt);

                Console.WriteLine("\n--- Claude Response ---\n");

                // Receive the response (stops after ResultMessage)
                await foreach (var message in session.ReceiveResponseAsync())
                {
                    switch (message)
                    {
                        case AssistantMessage assistant:
                            foreach (var block in assistant.MessageContent.Content)
                            {
                                if (block is TextBlock text)
                                {
                                    Console.Write(text.Text);
                                }
                            }
                            break;

                        case ResultMessage result:
                            string ctx = result.Usage is not null
                                ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k"
                                : "?";
                            Console.WriteLine($"\n\n--- [{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {ctx}] ---\n");
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("\nMake sure Claude CLI is installed and configured.");
        }
    }
}
