/// <summary>
/// Diagnostic Subagent Test - Captures everything to prove where the issue is
///
/// This test:
/// 1. Logs the exact CLI arguments being passed
/// 2. Tracks SubagentStart/SubagentStop events via hooks
/// 3. Tracks all PreToolUse/PostToolUse events
/// 4. Captures ALL stderr output
/// 5. Logs all control protocol messages
/// </summary>

using System.Text.Json;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.SubagentTest;

public static class DiagnosticTest
{
    private static readonly List<string> _eventLog = [];
    private static readonly Lock _logLock = new();

    private static void Log(string message)
    {
        lock (_logLock)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timestamp}] {message}";
            _eventLog.Add(entry);
            Console.WriteLine(entry);
        }
    }

    public static async Task RunDiagnosticAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     SUBAGENT DIAGNOSTIC TEST - Full Protocol Capture         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        string baseDir = Directory.GetCurrentDirectory();
        string outputDir = Path.Combine(baseDir, "diagnostic_output");
        Directory.CreateDirectory(outputDir);

        string testFile = Path.Combine(outputDir, "test_output.txt");
        string logFile = Path.Combine(outputDir, "diagnostic_log.txt");

        // Clean up
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
        }

        Log($"Working directory: {baseDir}");
        Log($"Output file: {testFile}");
        Log($"Log file: {logFile}");

        // Define hooks to track everything
        Dictionary<HookEvent, IReadOnlyList<HookMatcher>> hooks = new()
        {
            [HookEvent.SubagentStart] = new List<HookMatcher>
            {
                new()
                {
                    Hooks =
                    [
                        async (input, toolUseId, context, ct) =>
                        {
                            Log($"🚀 SUBAGENT_START: toolUseId={toolUseId}");
                            Log($"   Input: {JsonSerializer.Serialize(input)}");
                            return new SyncHookOutput { Continue = true };
                        }
                    ]
                }
            },
            [HookEvent.SubagentStop] = new List<HookMatcher>
            {
                new()
                {
                    Hooks =
                    [
                        async (input, toolUseId, context, ct) =>
                        {
                            Log($"🛑 SUBAGENT_STOP: toolUseId={toolUseId}");
                            Log($"   Input: {JsonSerializer.Serialize(input)}");
                            return new SyncHookOutput { Continue = true };
                        }
                    ]
                }
            },
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new()
                {
                    Hooks =
                    [
                        async (input, toolUseId, context, ct) =>
                        {
                            if (input is PreToolUseHookInput preInput)
                            {
                                Log($"🔧 PRE_TOOL_USE: {preInput.ToolName} (id={toolUseId})");
                                Log(
                                    $"   Input: {JsonSerializer.Serialize(preInput.ToolInput).Substring(0, Math.Min(200, JsonSerializer.Serialize(preInput.ToolInput).Length))}...");
                            }

                            return new SyncHookOutput { Continue = true };
                        }
                    ]
                }
            },
            [HookEvent.PostToolUse] = new List<HookMatcher>
            {
                new()
                {
                    Hooks =
                    [
                        async (input, toolUseId, context, ct) =>
                        {
                            if (input is PostToolUseHookInput postInput)
                            {
                                Log($"✅ POST_TOOL_USE: {postInput.ToolName} (id={toolUseId})");
                            }

                            return new SyncHookOutput { Continue = true };
                        }
                    ]
                }
            }
        };

        // Define subagent
        Dictionary<string, AgentDefinition> agents = new()
        {
            ["researcher"] = new AgentDefinition
            {
                Description = "Use this agent to research topics. It can search the web and write files.",
                Tools = ["WebSearch", "Write", "Read"],
                Prompt = $@"You are a research agent.
1. Use WebSearch to find information about the topic
2. Use Write to save the answer to: {testFile}
Be concise - just save the key answer.",
                Model = "haiku"
            }
        };

        // === TEST CONFIGURATION 1: Using Tools property (--tools flag) ===
        Log("");
        Log("═══════════════════════════════════════════════════════════");
        Log("TEST CONFIG: Using 'Tools' property (maps to --tools CLI flag)");
        Log("═══════════════════════════════════════════════════════════");

        ClaudeAgentOptions optionsWithTools = new()
        {
            WorkingDirectory = baseDir,
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = @"You are a coordinator that ONLY uses the Task tool to spawn subagents.
You have a 'researcher' subagent available.
DO NOT use any tool other than Task.",
            // Using Tools property - this maps to --tools CLI flag
            Tools = new ToolsList(["Task"]), // <-- JUST Task
            Agents = agents,
            Hooks = hooks,
            Model = "sonnet",
            MaxTurns = 10,
            OnStderr = line => Log($"[STDERR] {line}")
        };

        await RunTestWithConfigAsync("TEST 1: Tools=[Task]", optionsWithTools, testFile);

        // === TEST CONFIGURATION 2: Using AllowedTools property (--allowedTools flag) ===
        Log("");
        Log("═══════════════════════════════════════════════════════════");
        Log("TEST CONFIG: Using 'AllowedTools' property (maps to --allowedTools CLI flag)");
        Log("═══════════════════════════════════════════════════════════");

        // Clean up for next test
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
        }

        ClaudeAgentOptions optionsWithAllowedTools = new()
        {
            WorkingDirectory = baseDir,
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = @"You are a coordinator that ONLY uses the Task tool to spawn subagents.
You have a 'researcher' subagent available.
DO NOT use any tool other than Task.",
            // Using AllowedTools property - this maps to --allowedTools CLI flag
            AllowedTools = ["Task"], // <-- JUST Task via AllowedTools
            Agents = agents,
            Hooks = hooks,
            Model = "sonnet",
            MaxTurns = 10,
            OnStderr = line => Log($"[STDERR] {line}")
        };

        await RunTestWithConfigAsync("TEST 2: AllowedTools=[Task]", optionsWithAllowedTools, testFile);

        // === TEST CONFIGURATION 3: Tools + AllowedTools combined ===
        Log("");
        Log("═══════════════════════════════════════════════════════════");
        Log("TEST CONFIG: Empty Tools + AllowedTools=[Task] (disable defaults, add Task)");
        Log("═══════════════════════════════════════════════════════════");

        // Clean up for next test
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
        }

        ClaudeAgentOptions optionsWithBoth = new()
        {
            WorkingDirectory = baseDir,
            PermissionMode = PermissionMode.BypassPermissions,
            SystemPrompt = @"You are a coordinator that ONLY uses the Task tool to spawn subagents.
You have a 'researcher' subagent available.
DO NOT use any tool other than Task.",
            // Disable default tools, then add just Task
            Tools = new ToolsList([]), // Empty = disable defaults
            AllowedTools = ["Task"], // Add only Task
            Agents = agents,
            Hooks = hooks,
            Model = "sonnet",
            MaxTurns = 10,
            OnStderr = line => Log($"[STDERR] {line}")
        };

        await RunTestWithConfigAsync("TEST 3: Tools=[], AllowedTools=[Task]", optionsWithBoth, testFile);

        // Save complete log
        await File.WriteAllLinesAsync(logFile, _eventLog);
        Log("");
        Log($"Complete diagnostic log saved to: {logFile}");
    }

    private static async Task RunTestWithConfigAsync(string testName, ClaudeAgentOptions options, string testFile)
    {
        Log($"Running: {testName}");
        Log("");

        // Log the configuration
        if (options.Tools is ToolsList toolsList)
        {
            Log($"  Tools (--tools): [{string.Join(", ", toolsList.Tools)}]");
        }
        else
        {
            Log("  Tools (--tools): (not set)");
        }

        Log($"  AllowedTools (--allowedTools): [{string.Join(", ", options.AllowedTools)}]");

        string agentsJson = JsonSerializer.Serialize(
            options.Agents!.ToDictionary(
                kv => kv.Key,
                kv => new { description = kv.Value.Description, tools = kv.Value.Tools, model = kv.Value.Model }
            ),
            new JsonSerializerOptions { WriteIndented = false }
        );
        Log($"  Agents (--agents): {agentsJson}");
        Log("");

        try
        {
            await using ClaudeAgentClient client = new(options);
            await using ClaudeAgentSession session = await client.CreateSessionAsync();

            string prompt = $"Research 'what is the capital of France' and save the one-word answer to {testFile}";
            Log($"Sending prompt: {prompt}");
            Log("");

            await session.SendAsync(prompt);

            int toolCallCount = 0;
            bool subagentSpawned = false;
            bool taskToolUsed = false;

            await foreach (Message message in session.ReceiveAsync())
            {
                switch (message)
                {
                    case AssistantMessage assistant:
                        string? parentId = assistant.MessageContent.ParentToolUseId;
                        string context = parentId != null
                            ? $"[SUBAGENT:{parentId[..Math.Min(8, parentId.Length)]}]"
                            : "[MAIN]";

                        foreach (ContentBlock block in assistant.MessageContent.Content)
                        {
                            switch (block)
                            {
                                case TextBlock text:
                                    Log(
                                        $"{context} Text: {text.Text.Substring(0, Math.Min(100, text.Text.Length))}...");
                                    break;
                                case ToolUseBlock toolUse:
                                    toolCallCount++;
                                    Log(
                                        $"{context} TOOL_USE: {toolUse.Name} (id={toolUse.Id[..Math.Min(8, toolUse.Id.Length)]})");

                                    if (toolUse.Name == "Task")
                                    {
                                        taskToolUsed = true;
                                        string? subagentType = GetJsonProperty(toolUse.Input, "subagent_type");
                                        Log($"  → Spawning subagent: {subagentType}");
                                        if (subagentType == "researcher")
                                        {
                                            subagentSpawned = true;
                                        }
                                    }

                                    break;
                            }
                        }

                        break;

                    case ResultMessage result:
                        Log(
                            $"RESULT: Completed in {result.DurationMs / 1000.0:F1}s, Cost: ${result.TotalCostUsd:F4}, Error: {result.IsError}");
                        goto done;
                }
            }

            done:
            Log("");
            Log($"═══ {testName} SUMMARY ═══");
            Log($"  Tool calls made: {toolCallCount}");
            Log($"  Task tool used: {taskToolUsed}");
            Log($"  Subagent spawned: {subagentSpawned}");
            Log($"  Output file exists: {File.Exists(testFile)}");

            if (File.Exists(testFile))
            {
                string content = await File.ReadAllTextAsync(testFile);
                Log($"  File content: {content}");
                Log("  ✅ TEST PASSED - Subagent successfully wrote file");
            }
            else
            {
                Log("  ❌ TEST FAILED - File was NOT created");
                if (!taskToolUsed)
                {
                    Log("     DIAGNOSIS: Main agent did NOT use Task tool at all");
                }
                else if (!subagentSpawned)
                {
                    Log("     DIAGNOSIS: Task tool was used but researcher subagent was not spawned");
                }
                else
                {
                    Log("     DIAGNOSIS: Subagent was spawned but failed to write file (permission/tool issue?)");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack: {ex.StackTrace}");
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
}
