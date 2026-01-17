/// <summary>
/// Research Agent - Multi-Agent Orchestration Example
///
/// USAGE:
///   dotnet run                    # Interactive mode (default)
///   dotnet run -- --auto          # Auto-run with default prompt (for debugging)
///   dotnet run -- "custom prompt" # Run with custom prompt then exit
///
/// This example demonstrates:
/// - Multi-agent orchestration with a lead agent delegating to specialized subagents
/// - Using hooks to track all tool calls across agents
/// - Subagent definitions with specialized prompts and tool restrictions
///
/// Ported from: claude-agent-sdk-demos/research-agent/
/// </summary>

using System.Diagnostics;
using System.Text.Json;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.ResearchAgent;

public static class Program
{
    // Default prompt for auto-run mode
    private const string DefaultPrompt = "What programming language had the highest uptake in 2025?";

    private static int _messageCounter;

    public static async Task Main(string[] args)
    {
        // Interactive mode is default; use --auto for debugging with default prompt
        bool isAutoMode = args.Contains("--auto");
        string? customPrompt = args.FirstOrDefault(a => !a.StartsWith("--"));
        bool isInteractive = !isAutoMode && customPrompt == null;

        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine("     Research Agent - Multi-Agent Orchestration Test");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Setup directories - use FULL ABSOLUTE PATHS
        string baseDir = Directory.GetCurrentDirectory();
        string filesDir = Path.Combine(baseDir, "files");
        string logsDir = Path.Combine(baseDir, "logs");
        string sessionDir = Path.Combine(logsDir, $"session_{DateTime.Now:yyyyMMdd_HHmmss}");

        // FULL ABSOLUTE PATHS for research_notes and reports
        string researchNotesFullPath = Path.GetFullPath(Path.Combine(filesDir, "research_notes"));
        string reportsFullPath = Path.GetFullPath(Path.Combine(filesDir, "reports"));

        // Clear and recreate output directories
        Debug.WriteLine("Clearing output directories...");
        ClearDirectory(researchNotesFullPath);
        ClearDirectory(reportsFullPath);
        Directory.CreateDirectory(sessionDir);

        Debug.WriteLine($"  Research notes: {researchNotesFullPath}");
        Debug.WriteLine($"  Reports: {reportsFullPath}");
        Debug.WriteLine($"  Session logs: {sessionDir}");
        Debug.WriteLine("");

        // Load prompts and inject full paths
        string promptsDir = Path.Combine(AppContext.BaseDirectory, "Prompts");
        string leadAgentPrompt = await LoadPromptAsync(Path.Combine(promptsDir, "LeadAgent.txt"));
        string researcherPrompt = await LoadPromptAsync(Path.Combine(promptsDir, "Researcher.txt"));
        string reportWriterPrompt = await LoadPromptAsync(Path.Combine(promptsDir, "ReportWriter.txt"));

        // IMPORTANT: Replace relative paths with full absolute paths
        // The CLI may not respect WorkingDirectory for file operations
        researcherPrompt = researcherPrompt
            .Replace("files/research_notes/", researchNotesFullPath + Path.DirectorySeparatorChar)
            .Replace("files/research_notes", researchNotesFullPath);

        reportWriterPrompt = reportWriterPrompt
            .Replace("files/research_notes/", researchNotesFullPath + Path.DirectorySeparatorChar)
            .Replace("files/research_notes", researchNotesFullPath)
            .Replace("files/reports/", reportsFullPath + Path.DirectorySeparatorChar)
            .Replace("files/reports", reportsFullPath);

        leadAgentPrompt = leadAgentPrompt
            .Replace("files/research_notes/", researchNotesFullPath + Path.DirectorySeparatorChar)
            .Replace("files/research_notes", researchNotesFullPath)
            .Replace("files/reports/", reportsFullPath + Path.DirectorySeparatorChar)
            .Replace("files/reports", reportsFullPath);

        // Initialize subagent tracker
        using SubagentTracker tracker = new(sessionDir);

        // Define specialized subagents with full paths in descriptions
        // Each subagent gets exactly the tools listed here - independent of main agent's tools
        Dictionary<string, AgentDefinition> agents = new()
        {
            ["researcher"] = new AgentDefinition
            {
                Description = "Use this agent when you need to gather research information on any topic. " +
                              "The researcher uses web search to find relevant information, articles, and sources " +
                              $"from across the internet. Writes research findings to {researchNotesFullPath} " +
                              "for later use by report writers.",
                // Subagent tools are independent - they get exactly these tools
                Tools = ["WebSearch", "Read", "Write"],
                Prompt = researcherPrompt,
                Model = "haiku"
            },
            ["report-writer"] = new AgentDefinition
            {
                Description = "Use this agent when you need to create a formal research report document. " +
                              $"The report-writer reads research findings from {researchNotesFullPath} and synthesizes " +
                              $"them into clear, concise, professionally formatted markdown reports in {reportsFullPath}. " +
                              "Does NOT conduct web searches - only reads existing research notes and creates reports.",
                // Subagent tools are independent - they get exactly these tools
                Tools = ["Glob", "Read", "Write"],
                Prompt = reportWriterPrompt,
                Model = "haiku"
            }
        };

        // Configure hooks for tracking
        Dictionary<HookEvent, IReadOnlyList<HookMatcher>> hooks = new()
        {
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new()
                {
                    // Match all tools
                    Hooks = [tracker.PreToolUseHookAsync]
                }
            },
            [HookEvent.PostToolUse] = new List<HookMatcher>
            {
                new()
                {
                    // Match all tools
                    Hooks = [tracker.PostToolUseHookAsync]
                }
            },
            // Add SubagentStart/SubagentStop hooks - use tracker's hooks for consistent context tracking
            [HookEvent.SubagentStart] = new List<HookMatcher>
            {
                new()
                {
                    Hooks = [tracker.SubagentStartHookAsync]
                }
            },
            [HookEvent.SubagentStop] = new List<HookMatcher>
            {
                new()
                {
                    Hooks = [tracker.SubagentStopHookAsync]
                }
            }
        };

        ClaudeAgentOptions options = new()
        {
            WorkingDirectory = baseDir,
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = leadAgentPrompt,
            // Give main agent all tools - we want to see WHY it doesn't wait for subagents
            AllowedTools = ["Task"],
            // No DisallowedTools - let everything work
            Agents = agents,
            Hooks = hooks,
            Model = "sonnet",
            MaxTurns = 50
        };

        try
        {
            await using ClaudeAgentClient client = new(options);

            // Create a session for bidirectional communication
            await using ClaudeAgentSession session = await client.CreateSessionAsync();

            if (isInteractive)
            {
                // Interactive mode - manual prompts
                Console.WriteLine("Interactive mode. Type 'exit' to quit.");
                Console.WriteLine();

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nYou: ");
                    Console.ResetColor();

                    string? userInput = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(userInput) ||
                        userInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        userInput.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                        userInput.Equals("q", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await RunPromptAsync(session, userInput, tracker);
                }
            }
            else
            {
                // Auto-run mode - use default or custom prompt
                string prompt = customPrompt ?? DefaultPrompt;

                Debug.WriteLine("═══════════════════════════════════════════════════════════════");
                Debug.WriteLine("AUTO-RUN MODE");
                Debug.WriteLine("═══════════════════════════════════════════════════════════════");
                Debug.WriteLine($"Prompt: {prompt}");
                Debug.WriteLine("");

                await RunPromptAsync(session, prompt, tracker);
            }
        }
        finally
        {
            Debug.WriteLine("");
            Debug.WriteLine("═══════════════════════════════════════════════════════════════");
            Debug.WriteLine("SESSION COMPLETE");
            Debug.WriteLine("═══════════════════════════════════════════════════════════════");
            Debug.WriteLine($"Session logs: {sessionDir}");
            Debug.WriteLine($"Tool calls log: {Path.Combine(sessionDir, "tool_calls.jsonl")}");
            Debug.WriteLine("");

            // Show files created
            Debug.WriteLine("Files created:");
            ShowFilesInDirectory(researchNotesFullPath, "  Research notes");
            ShowFilesInDirectory(reportsFullPath, "  Reports");
        }
    }

    private static async Task RunPromptAsync(ClaudeAgentSession session, string prompt, SubagentTracker tracker)
    {
        await session.SendAsync(prompt);

        Debug.WriteLine("\n--- Agent Response ---");

        // Stream and process responses
        await foreach (Message message in session.ReceiveResponseAsync())
        {
            if (ProcessMessage(message, tracker))
            {
                break;
            }
        }

        Debug.WriteLine("");
    }

    private static void ClearDirectory(string fullPath)
    {
        // Safety check - ensure it's an absolute path
        if (!Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException($"Path must be fully qualified: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            foreach (string file in Directory.GetFiles(fullPath))
            {
                File.Delete(file);
                Debug.WriteLine($"  Deleted: {file}");
            }
        }

        // Recreate the directory
        Directory.CreateDirectory(fullPath);
    }

    private static void ShowFilesInDirectory(string fullPath, string label)
    {
        if (!Directory.Exists(fullPath))
        {
            Debug.WriteLine($"{label}: (directory not found)");
            return;
        }

        string[] files = Directory.GetFiles(fullPath);
        if (files.Length == 0)
        {
            Debug.WriteLine($"{label}: (none)");
        }
        else
        {
            Debug.WriteLine($"{label}:");
            foreach (string file in files)
            {
                FileInfo info = new(file);
                Debug.WriteLine($"    - {info.Name} ({info.Length} bytes)");
            }
        }
    }

    private static async Task<string> LoadPromptAsync(string path)
    {
        if (!File.Exists(path))
        {
            Debug.WriteLine($"Warning: Prompt file not found: {path}");
            return "You are a helpful assistant.";
        }

        return await File.ReadAllTextAsync(path);
    }

    private static bool ProcessMessage(Message message, SubagentTracker tracker)
    {
        switch (message)
        {
            case AssistantMessage assistant:
                _messageCounter++;

                // Check for parent_tool_use_id to track subagent context
                // Also consider hook-based tracking (subagents may be active even without message context)
                string? parentId = assistant.MessageContent.ParentToolUseId;
                bool hasMessageContext = parentId is not null;
                bool hasActiveSubagents = tracker.HasActiveSubagents;
                bool isSubagent = hasMessageContext || hasActiveSubagents;

                // Show context label
                string contextLabel;
                if (hasMessageContext)
                {
                    contextLabel = $"[SUBAGENT:{parentId?[..Math.Min(8, parentId.Length)]}]";
                }
                else if (hasActiveSubagents)
                {
                    contextLabel = "[SUBAGENT:ACTIVE]";
                }
                else
                {
                    contextLabel = "[MAIN]";
                }

                if (hasMessageContext)
                {
                    tracker.SetCurrentContext(parentId!);
                }
                else if (!hasActiveSubagents)
                {
                    tracker.SetCurrentContext(null);
                }
                // If hasActiveSubagents but no message context, keep current context

                // Analyze message content - count tool calls
                List<ToolUseBlock> toolUseBlocks = assistant.MessageContent.Content.OfType<ToolUseBlock>().ToList();
                List<ToolUseBlock> taskCalls = toolUseBlocks.Where(t => t.Name == "Task").ToList();
                List<ToolUseBlock> otherCalls = toolUseBlocks.Where(t => t.Name != "Task").ToList();

                if (toolUseBlocks.Count > 0)
                {
                    Debug.WriteLine(
                        $" MESSAGE #{_messageCounter} | {contextLabel} | Tools: {toolUseBlocks.Count} (Task:{taskCalls.Count}, Other:{otherCalls.Count}) ");

                    // KEY DIAGNOSTIC: Are Task and other tools in the same message?
                    Debug.WriteLineIf(taskCalls.Count > 0 && otherCalls.Count > 0 && !isSubagent,
                        $"🚨 PARALLEL CALLS DETECTED: Main agent calling Task AND other tools in same message!\n" +
                        $"   Task calls: {string.Join(", ", taskCalls.Select(t => GetJsonProperty(t.Input, "subagent_type") ?? "?"))}\n" +
                        $"   Other calls: {string.Join(", ", otherCalls.Select(t => t.Name))}");
                }

                foreach (ContentBlock block in assistant.MessageContent.Content)
                {
                    switch (block)
                    {
                        case TextBlock text:
                            // Show truncated text with context
                            string displayText = text.Text.Length > 200
                                ? text.Text[..200] + "..."
                                : text.Text;
                            Debug.WriteLine($"{contextLabel} {displayText}");
                            break;

                        case ToolUseBlock toolUse:
                            // Show ALL tool usage with clear context
                            if (toolUse.Name == "Task")
                            {
                                string? subagentType = GetJsonProperty(toolUse.Input, "subagent_type");
                                string description = GetJsonProperty(toolUse.Input, "description") ?? "Unknown task";

                                Debug.WriteLine(
                                    $"{contextLabel} 🚀 SPAWNING SUBAGENT: {subagentType} (id:{toolUse.Id[..8]})");
                                Debug.WriteLine($"           Description: {description}");

                                if (!string.IsNullOrEmpty(subagentType))
                                {
                                    tracker.RegisterSubagentSpawn(toolUse.Id, subagentType, description);
                                }
                            }
                            else
                            {
                                // Show other tool usage
                                Debug.WriteLine($"{contextLabel} 🔧 TOOL: {toolUse.Name} (id:{toolUse.Id[..8]})");

                                Debug.WriteLineIf(!isSubagent && toolUse.Name != "Task",
                                    $"⚠️  WARNING: Main agent using {toolUse.Name} directly!");
                            }

                            break;

                        case ToolResultBlock toolResult:
                            Debug.WriteLine($"{contextLabel} ← TOOL_RESULT for {toolResult.ToolUseId[..8]}");
                            break;
                    }
                }

                break;

            case ResultMessage result:
                Debug.WriteLine("");
                Debug.WriteLine($"[Completed in {result.DurationMs / 1000.0:F1}s | Cost: ${result.TotalCostUsd:F4}]");
                return true;
        }

        return false;
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
        catch
        {
            // Ignore
        }

        return null;
    }
}
