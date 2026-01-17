/// <summary>
/// Subagent Test Suite - Diagnoses subagent tool configuration issues
///
/// USAGE:
///   dotnet run                    # Run standard test
///   dotnet run -- --diagnostic    # Run full diagnostic test with hooks and logging
///   dotnet run -- --cli-args      # Show CLI arguments that would be passed
///   dotnet run -- --compare       # Compare C# vs Python SDK arg generation
///
/// KEY QUESTION: Does the main agent's tool configuration affect subagent tools?
///
/// HYPOTHESIS TO TEST:
/// - Python/TypeScript docs show: allowed_tools=["Task", ...]
/// - This uses --allowedTools flag (ADDS to defaults)
/// - C# SDK's Tools property uses --tools flag (REPLACES defaults)
/// - This might be causing the difference in behavior
/// </summary>

using System.Text.Json;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.SubagentTest;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Check for diagnostic modes
        if (args.Contains("--diagnostic"))
        {
            await DiagnosticTest.RunDiagnosticAsync();
            return;
        }

        if (args.Contains("--cli-args") || args.Contains("--compare"))
        {
            ShowCliArgumentAnalysis(args.Contains("--compare"));
            return;
        }

        Console.WriteLine("==============================================");
        Console.WriteLine("   Subagent Tool Test - WebSearch + Write");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("TIP: Run with --diagnostic for full protocol tracing");
        Console.WriteLine("     Run with --cli-args to see CLI arguments");
        Console.WriteLine("     Run with --compare to compare C# vs Python");
        Console.WriteLine();

        string baseDir = Directory.GetCurrentDirectory();
        string outputDir = Path.Combine(baseDir, "output");
        Directory.CreateDirectory(outputDir);

        string testFile = Path.Combine(outputDir, "research_output.txt");
        string testMain = Path.Combine(outputDir, "output.txt");

        // Delete test file if it exists
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
            Console.WriteLine($"Deleted existing test file: {testFile}");
        }

        if (File.Exists(testMain))
        {
            File.Delete(testMain);
            Console.WriteLine($"Deleted existing test file: {testMain}");
        }

        Console.WriteLine($"Working directory: {baseDir}");
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine($"Expected output file: {testFile}");
        Console.WriteLine();

        // Define a researcher subagent with WebSearch + Write tools
        Dictionary<string, AgentDefinition> agents = new()
        {
            ["researcher"] = new AgentDefinition
            {
                Description =
                    "Use this agent to research topics. The researcher can search the web and write findings to files.",
                Tools = ["WebSearch", "Write", "Read"],
                Prompt = $@"You are a research agent. Your job is to:
1. Use WebSearch to find information
2. Use Write to save findings to a file

IMPORTANT:
- When asked to research something, use WebSearch first
- Then use Write to save the results to the specified file path
- Be concise - just save key facts

When you're done, confirm what you found and where you saved it.

OUTPUT FILE: {testFile}",
                Model = "haiku"
            }
        };

        // Main agent - use AllowedTools
        ClaudeAgentOptions options = new()
        {
            WorkingDirectory = baseDir,
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = @"You are a coordinator. You can ONLY use the Task tool to spawn subagents.

CRITICAL: You MUST NOT use WebSearch or Write directly. You MUST spawn the researcher subagent.

You have a 'researcher' subagent that can:
- Search the web (WebSearch)
- Write files (Write)

When asked to research something, spawn the researcher subagent with:
- The topic to research

DO NOT DO THE RESEARCH YOURSELF. SPAWN THE SUBAGENT.",
            // TEST: Main agent restricted to Task only
            // Subagent has WebSearch + Write with FULL PATH in prompt
            // Does the subagent actually work?
            // Tools = new ToolsList(["Task"]),
            AllowedTools = ["Task"],
            Agents = agents,
            Model = "sonnet",
            MaxTurns = 15,
            OnStderr = line =>
            {
                // Only show lines that might be about tool calls or subagents
                if (line.Contains("tool") || line.Contains("Task") || line.Contains("agent") ||
                    line.Contains("subagent") || line.Contains("spawn") || line.Contains("parent")
                    || line.Contains("Write") || line.Contains("Read"))
                {
                    Console.WriteLine($"[STDERR] {line}");
                }
            }
        };

        Console.WriteLine("Configuration:");
        Console.WriteLine("  Main agent: Tools=[Task] (RESTRICTED)");
        Console.WriteLine("  Researcher subagent tools: WebSearch, Write");
        Console.WriteLine("  Using FULL PATH in prompt: " + testFile);
        Console.WriteLine();

        // Debug: Show the agents JSON that will be passed
        string agentsJson = JsonSerializer.Serialize(
            agents.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    description = kv.Value.Description, tools = kv.Value.Tools, prompt = kv.Value.Prompt,
                    model = kv.Value.Model
                }
            ),
            new JsonSerializerOptions { WriteIndented = true }
        );
        Console.WriteLine("[DEBUG] Agents JSON:");
        Console.WriteLine(agentsJson);
        Console.WriteLine();

        try
        {
            await using ClaudeAgentClient client = new(options);

            // Create a session for bidirectional mode
            await using ClaudeAgentSession session = await client.CreateSessionAsync();

            string prompt = $"Research 'what is the capital of France' in one word and save the answer to {testFile}";

            Console.WriteLine($"Prompt: {prompt}");
            Console.WriteLine();
            Console.WriteLine("--- Response ---");

            await session.SendAsync(prompt);

            await foreach (Message message in session.ReceiveAsync())
            {
                // Debug: show message type
                Console.WriteLine($"[MSG] Type: {message.GetType().Name}");
                Console.WriteLine("===============================");
                Console.WriteLine(message);
                Console.WriteLine("===============================");
                switch (message)
                {
                    case AssistantMessage assistant:
                        string? parentId = assistant.MessageContent.ParentToolUseId;
                        string prefix = parentId != null ? $"[SUBAGENT:{parentId[..8]}]" : "[MAIN]";
                        Console.WriteLine($"[MSG] ParentToolUseId: {parentId ?? "(null)"}");

                        foreach (ContentBlock block in assistant.MessageContent.Content)
                        {
                            switch (block)
                            {
                                case TextBlock text:
                                    Console.Write($"{prefix} {text.Text}");
                                    break;
                                case ToolUseBlock toolUse:
                                    Console.WriteLine($"{prefix} Tool: {toolUse.Name}");
                                    if (toolUse.Name == "Task")
                                    {
                                        string? subagentType = GetJsonProperty(toolUse.Input, "subagent_type");
                                        string? description = GetJsonProperty(toolUse.Input, "description");
                                        Console.WriteLine($"  -> Spawning subagent: {subagentType}");
                                        Console.WriteLine($"  -> Description: {description}");
                                    }
                                    else if (toolUse.Name == "Write")
                                    {
                                        string? filePath = GetJsonProperty(toolUse.Input, "file_path");
                                        Console.WriteLine($"  -> Writing to: {filePath}");
                                    }
                                    else if (toolUse.Name == "WebSearch")
                                    {
                                        string? query = GetJsonProperty(toolUse.Input, "query");
                                        Console.WriteLine($"  -> Query: {query}");
                                    }

                                    break;
                                default:
                                    Console.Write($"*** {prefix} {block}");
                                    break;
                            }
                        }

                        break;

                    case ResultMessage result:
                        Console.WriteLine();
                        Console.WriteLine(
                            $"--- Completed in {result.DurationMs / 1000.0:F1}s | Cost: ${result.TotalCostUsd:F4} ---");
                        // exit early no use waiting for this to time out...
                        goto finished;
                }
            }

            finished:
            Console.WriteLine();
            Console.WriteLine("==============================================");
            Console.WriteLine("   Test Result");
            Console.WriteLine("==============================================");

            if (File.Exists(testFile))
            {
                string content = await File.ReadAllTextAsync(testFile);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS: File was created!");
                Console.ResetColor();
                Console.WriteLine($"Content: {content}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILURE: File was NOT created!");
                Console.ResetColor();
                Console.WriteLine("The subagent's tools did not work.");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static string? GetJsonProperty(JsonElement element, string propertyName)
    {
        try
        {
            if (element.TryGetProperty(propertyName, out JsonElement value))
            {
                return value.GetString();
            }
        }
        catch { }

        return null;
    }

    private static void ShowCliArgumentAnalysis(bool includeComparison)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         CLI ARGUMENT ANALYSIS FOR SUBAGENT CONFIG            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        string testFile = Path.Combine(Directory.GetCurrentDirectory(), "output", "test.txt");

        // Define subagent
        Dictionary<string, AgentDefinition> agents = new()
        {
            ["researcher"] = new AgentDefinition
            {
                Description = "Research agent with WebSearch",
                Tools = ["WebSearch", "Write", "Read"],
                Prompt = "You are a research agent.",
                Model = "haiku"
            }
        };

        // Configuration 1: Using Tools property (--tools flag)
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("CONFIGURATION 1: Tools = [\"Task\"] (uses --tools flag)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        ClaudeAgentOptions options1 = new()
        {
            Tools = new ToolsList(["Task"]),
            Agents = agents,
            Model = "sonnet",
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = "You are a coordinator.",
            MaxTurns = 10
        };

        List<string> args1 = CliArgumentCapture.BuildExpectedArguments(options1);
        CliArgumentCapture.PrintCliArguments(args1, Console.WriteLine);

        // Configuration 2: Using AllowedTools property (--allowedTools flag)
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("CONFIGURATION 2: AllowedTools = [\"Task\"] (uses --allowedTools flag)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        ClaudeAgentOptions options2 = new()
        {
            AllowedTools = ["Task"],
            Agents = agents,
            Model = "sonnet",
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = "You are a coordinator.",
            MaxTurns = 10
        };

        List<string> args2 = CliArgumentCapture.BuildExpectedArguments(options2);
        CliArgumentCapture.PrintCliArguments(args2, Console.WriteLine);

        // Configuration 3: Empty Tools + AllowedTools
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("CONFIGURATION 3: Tools = [], AllowedTools = [\"Task\"]");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        ClaudeAgentOptions options3 = new()
        {
            Tools = new ToolsList([]), // Empty = disable defaults
            AllowedTools = ["Task"],
            Agents = agents,
            Model = "sonnet",
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = "You are a coordinator.",
            MaxTurns = 10
        };

        List<string> args3 = CliArgumentCapture.BuildExpectedArguments(options3);
        CliArgumentCapture.PrintCliArguments(args3, Console.WriteLine);

        if (includeComparison)
        {
            CliArgumentCapture.CompareWithPythonExpected(options1, Console.WriteLine);
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("ANALYSIS:");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Config 1 sends: --tools Task");
        Console.WriteLine("  → This REPLACES the default tool set with ONLY Task");
        Console.WriteLine("  → Subagent tools (WebSearch, Write, Read) may not work!");
        Console.WriteLine();
        Console.WriteLine("Config 2 sends: --allowedTools Task");
        Console.WriteLine("  → This ADDS Task to the default tool set");
        Console.WriteLine("  → Subagent should inherit default tools + their own");
        Console.WriteLine();
        Console.WriteLine("Config 3 sends: --tools \"\" --allowedTools Task");
        Console.WriteLine("  → This CLEARS defaults, then adds ONLY Task");
        Console.WriteLine("  → Same problem as Config 1");
        Console.WriteLine();
        Console.WriteLine("⚠️  HYPOTHESIS: The issue is using Tools property instead of AllowedTools");
        Console.WriteLine("    When you use Tools=[\"Task\"], it passes --tools Task to CLI");
        Console.WriteLine("    This might restrict what tools subagents can access.");
        Console.WriteLine();
    }
}
