using System.Collections.Concurrent;
using System.Text.Json;
using Claude.AgentSdk.Protocol;

#pragma warning disable CA1063

namespace Claude.AgentSdk.ResearchAgent;

/// <summary>
///     Record of a single tool call made by an agent.
/// </summary>
public record ToolCallRecord
{
    public required string Timestamp { get; init; }
    public required string ToolName { get; init; }
    public required JsonElement? ToolInput { get; init; }
    public required string ToolUseId { get; init; }
    public required string AgentType { get; init; }
    public string? ParentToolUseId { get; init; }
    public JsonElement? ToolOutput { get; set; }
    public string? Error { get; set; }
}

/// <summary>
///     Information about a subagent execution session.
/// </summary>
public record SubagentSession
{
    public required string SubagentType { get; init; }
    public required string ParentToolUseId { get; init; }
    public required string SpawnedAt { get; init; }
    public required string Description { get; init; }
    public required string SubagentId { get; init; }
    public List<ToolCallRecord> ToolCalls { get; } = [];
}

/// <summary>
///     Tracks all tool calls made by subagents using hooks.
///     This tracker:
///     1. Monitors Task tool usage to detect subagent spawns
///     2. Uses hooks (PreToolUse/PostToolUse) to capture all tool invocations
///     3. Associates tool calls with their originating subagent
///     4. Logs tool usage to console and JSONL files
/// </summary>
public class SubagentTracker : IDisposable
{
    /// <summary>
    ///     Tools that the main agent is NOT allowed to use directly.
    ///     These must be delegated to subagents.
    /// </summary>
    private static readonly HashSet<string> _forbiddenMainAgentTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "WebSearch", "WebFetch", "Write", "Read", "Glob", "Grep", "Edit", "Bash"
    };

    /// <summary>
    ///     Set of currently active subagent tool_use_ids (from SubagentStart to SubagentStop).
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _activeSubagents = new();

    private readonly Lock _consoleLock = new();
    private readonly ConcurrentDictionary<string, SubagentSession> _sessions = new();
    private readonly ConcurrentDictionary<string, int> _subagentCounters = new();
    private readonly ConcurrentDictionary<string, ToolCallRecord> _toolCallRecords = new();
    private readonly StreamWriter? _toolLogWriter;

    private string? _currentParentId;

    public SubagentTracker(string sessionDir)
    {
        SessionDir = sessionDir;

        // Create session directory if it doesn't exist
        Directory.CreateDirectory(sessionDir);

        // Open tool call log file
        string toolLogPath = Path.Combine(sessionDir, "tool_calls.jsonl");
        _toolLogWriter = new StreamWriter(toolLogPath, false) { AutoFlush = true };
    }

    public string SessionDir { get; }

    /// <summary>
    ///     Returns true if any subagent is currently running.
    /// </summary>
    public bool HasActiveSubagents => !_activeSubagents.IsEmpty;

    /// <summary>
    ///     Whether to enforce tool restrictions (block main agent from using subagent tools).
    /// </summary>
    public bool EnforceToolRestrictions { get; set; } = true;

    public void Dispose()
    {
        _toolLogWriter?.Dispose();
    }

    /// <summary>
    ///     Registers a new subagent spawn detected from a Task tool call.
    /// </summary>
    public string RegisterSubagentSpawn(
        string toolUseId,
        string subagentType,
        string description,
        string? prompt = null)
    {
        // Increment counter for this subagent type
        int count = _subagentCounters.AddOrUpdate(subagentType, 1, (_, c) => c + 1);
        string subagentId = $"{subagentType.ToUpperInvariant()}-{count}";

        SubagentSession session = new()
        {
            SubagentType = subagentType,
            ParentToolUseId = toolUseId,
            SpawnedAt = DateTime.Now.ToString("o"),
            Description = description,
            SubagentId = subagentId
        };

        _sessions[toolUseId] = session;

        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine();
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"SUBAGENT SPAWNED: {subagentId}");
            Console.WriteLine(new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"Task: {description}");
            Console.ResetColor();
        }

        return subagentId;
    }

    /// <summary>
    ///     Updates the current execution context from message stream.
    /// </summary>
    public void SetCurrentContext(string? parentToolUseId)
    {
        _currentParentId = parentToolUseId;
    }

    /// <summary>
    ///     Marks a subagent as active (called when SubagentStart hook fires).
    /// </summary>
    public void MarkSubagentActive(string toolUseId)
    {
        _activeSubagents[toolUseId] = true;
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[TRACKER] Subagent ACTIVE: {toolUseId[..Math.Min(8, toolUseId.Length)]}");
            Console.ResetColor();
        }
    }

    /// <summary>
    ///     Marks a subagent as inactive (called when SubagentStop hook fires).
    /// </summary>
    public void MarkSubagentInactive(string toolUseId)
    {
        _activeSubagents.TryRemove(toolUseId, out _);
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"[TRACKER] Subagent COMPLETED: {toolUseId[..Math.Min(8, toolUseId.Length)]}");
            Console.ResetColor();
        }
    }

    /// <summary>
    ///     SubagentStart hook callback.
    /// </summary>
    public async Task<HookOutput> SubagentStartHookAsync(
        HookInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken)
    {
        if (toolUseId is not null)
        {
            MarkSubagentActive(toolUseId);
        }

        return new SyncHookOutput { Continue = true };
    }

    /// <summary>
    ///     SubagentStop hook callback.
    /// </summary>
    public async Task<HookOutput> SubagentStopHookAsync(
        HookInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken)
    {
        if (toolUseId is not null)
        {
            MarkSubagentInactive(toolUseId);
        }

        return new SyncHookOutput { Continue = true };
    }

    /// <summary>
    ///     PreToolUse hook callback - captures tool calls and optionally blocks main agent tool abuse.
    /// </summary>
    public async Task<HookOutput> PreToolUseHookAsync(
        HookInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken)
    {
        if (input is not PreToolUseHookInput preInput || toolUseId is null)
        {
            return new SyncHookOutput { Continue = true };
        }

        string toolName = preInput.ToolName;
        JsonElement toolInput = preInput.ToolInput;
        string timestamp = DateTime.Now.ToString("o");

        // Determine agent context using multiple signals:
        // 1. Message-based context (ParentToolUseId from AssistantMessage)
        // 2. Hook-based context (between SubagentStart and SubagentStop)
        bool hasMessageContext = _currentParentId is not null && _sessions.ContainsKey(_currentParentId);
        bool hasActiveSubagentContext = HasActiveSubagents;
        bool isSubagent = hasMessageContext || hasActiveSubagentContext;

        // If we have message context with a known session, log with full details
        if (hasMessageContext && _sessions.TryGetValue(_currentParentId!, out SubagentSession? session))
        {
            ToolCallRecord record = new()
            {
                Timestamp = timestamp,
                ToolName = toolName,
                ToolInput = toolInput,
                ToolUseId = toolUseId,
                AgentType = session.SubagentType,
                ParentToolUseId = _currentParentId
            };

            session.ToolCalls.Add(record);
            _toolCallRecords[toolUseId] = record;

            LogToolUse(session.SubagentId, toolName, toolInput);
            LogToJsonl(new
            {
                @event = "tool_call_start",
                timestamp,
                tool_use_id = toolUseId,
                agent_id = session.SubagentId,
                agent_type = session.SubagentType,
                tool_name = toolName,
                tool_input = toolInput,
                parent_tool_use_id = _currentParentId
            });
        }
        else if (hasActiveSubagentContext)
        {
            // We know a subagent is running (from SubagentStart hook) but don't have message context yet
            // Allow the tool call - it's from a subagent
            LogToolUse("[SUBAGENT]", toolName, toolInput);
            LogToJsonl(new
            {
                @event = "tool_call_start",
                timestamp,
                tool_use_id = toolUseId,
                agent_id = "ACTIVE_SUBAGENT",
                agent_type = "subagent",
                tool_name = toolName,
                tool_input = toolInput
            });
        }
        else if (toolName != "Task")
        {
            // Main agent tool call (skip Task calls as they're handled by spawn detection)
            LogToolUse("MAIN AGENT", toolName, toolInput);
            LogToJsonl(new
            {
                @event = "tool_call_start",
                timestamp,
                tool_use_id = toolUseId,
                agent_id = "MAIN_AGENT",
                agent_type = "lead",
                tool_name = toolName,
                tool_input = toolInput
            });

            // ENFORCEMENT: Block main agent from using forbidden tools
            if (EnforceToolRestrictions && _forbiddenMainAgentTools.Contains(toolName))
            {
                lock (_consoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine();
                    Console.WriteLine($"[BLOCKED] Main agent attempted to use {toolName} directly!");
                    Console.WriteLine("          Main agent must delegate to subagents for this tool.");
                    Console.ResetColor();
                }

                LogToJsonl(new
                {
                    @event = "tool_call_blocked",
                    timestamp,
                    tool_use_id = toolUseId,
                    agent_id = "MAIN_AGENT",
                    tool_name = toolName,
                    reason = "Main agent must use Task tool to delegate to subagents"
                });

                return new SyncHookOutput
                {
                    Continue = false,
                    Decision = "block",
                    Reason =
                        $"DENIED: You cannot use {toolName} directly. You MUST spawn a subagent using the Task tool. " +
                        $"Use 'researcher' subagent for WebSearch/Write or 'report-writer' for Read/Write/Glob."
                };
            }
        }

        return new SyncHookOutput { Continue = true };
    }

    /// <summary>
    ///     PostToolUse hook callback - captures tool results.
    /// </summary>
    public async Task<HookOutput> PostToolUseHookAsync(
        HookInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken)
    {
        if (input is not PostToolUseHookInput postInput || toolUseId is null)
        {
            return new SyncHookOutput { Continue = true };
        }

        if (!_toolCallRecords.TryGetValue(toolUseId, out ToolCallRecord? record))
        {
            return new SyncHookOutput { Continue = true };
        }

        // Update record with output
        record.ToolOutput = postInput.ToolResponse;

        // Check for errors
        string? error = null;
        if (postInput.ToolResponse?.ValueKind == JsonValueKind.Object &&
            postInput.ToolResponse.Value.TryGetProperty("error", out JsonElement errorElement))
        {
            error = errorElement.GetString();
            record.Error = error;

            if (_currentParentId is not null && _sessions.TryGetValue(_currentParentId, out SubagentSession? session))
            {
                lock (_consoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{session.SubagentId}] Tool {record.ToolName} error: {error}");
                    Console.ResetColor();
                }
            }
        }

        // Get agent info for logging
        string agentId = "MAIN_AGENT";
        string agentType = "lead";
        if (record.ParentToolUseId is not null &&
            _sessions.TryGetValue(record.ParentToolUseId, out SubagentSession? subSession))
        {
            agentId = subSession.SubagentId;
            agentType = subSession.SubagentType;
        }

        LogToJsonl(new
        {
            @event = "tool_call_complete",
            timestamp = DateTime.Now.ToString("o"),
            tool_use_id = toolUseId,
            agent_id = agentId,
            agent_type = agentType,
            tool_name = record.ToolName,
            success = error is null,
            error,
            output_size = postInput.ToolResponse?.ToString()?.Length ?? 0
        });

        return new SyncHookOutput { Continue = true };
    }

    private void LogToolUse(string agentLabel, string toolName, JsonElement? toolInput)
    {
        string inputSummary = FormatToolInput(toolInput);

        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{agentLabel}]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" -> ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(toolName);
            if (!string.IsNullOrEmpty(inputSummary))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({inputSummary})");
            }

            Console.ResetColor();
            Console.WriteLine();
        }
    }

    private static string FormatToolInput(JsonElement? toolInput, int maxLength = 60)
    {
        if (toolInput is null)
        {
            return "";
        }

        try
        {
            JsonElement element = toolInput.Value;

            // WebSearch: show query
            if (element.TryGetProperty("query", out JsonElement queryElement))
            {
                string query = queryElement.GetString() ?? "";
                return query.Length <= maxLength ? $"query=\"{query}\"" : $"query=\"{query[..maxLength]}...\"";
            }

            // Write: show file path
            if (element.TryGetProperty("file_path", out JsonElement pathElement))
            {
                string path = pathElement.GetString() ?? "";
                string fileName = Path.GetFileName(path);
                if (element.TryGetProperty("content", out JsonElement contentElement))
                {
                    string content = contentElement.GetString() ?? "";
                    return $"file=\"{fileName}\" ({content.Length} chars)";
                }

                return $"path=\"{fileName}\"";
            }

            // Pattern for Glob
            if (element.TryGetProperty("pattern", out JsonElement patternElement))
            {
                return $"pattern=\"{patternElement.GetString()}\"";
            }

            // Task: show subagent spawn
            if (element.TryGetProperty("subagent_type", out JsonElement subagentTypeElement))
            {
                string subagentType = subagentTypeElement.GetString() ?? "";
                string desc = "";
                if (element.TryGetProperty("description", out JsonElement descElement))
                {
                    desc = descElement.GetString() ?? "";
                }

                return $"spawn={subagentType} ({desc})";
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "";
    }

    private void LogToJsonl(object entry)
    {
        try
        {
            string json = JsonSerializer.Serialize(entry);
            _toolLogWriter?.WriteLine(json);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
