namespace Claude.AgentSdk.Messages;

/// <summary>
///     Typed data for system messages with subtype "status".
/// </summary>
public sealed record StatusMessageData
{
    [JsonPropertyName("status")] public string? Status { get; init; }

    [JsonPropertyName("permissionMode")] public string? PermissionMode { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "hook_started".
/// </summary>
public sealed record HookStartedData
{
    [JsonPropertyName("hook_id")] public required string HookId { get; init; }

    [JsonPropertyName("hook_name")] public required string HookName { get; init; }

    [JsonPropertyName("hook_event")] public required string HookEvent { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "hook_progress".
/// </summary>
public sealed record HookProgressData
{
    [JsonPropertyName("hook_id")] public required string HookId { get; init; }

    [JsonPropertyName("hook_name")] public required string HookName { get; init; }

    [JsonPropertyName("hook_event")] public required string HookEvent { get; init; }

    [JsonPropertyName("stdout")] public string? Stdout { get; init; }

    [JsonPropertyName("stderr")] public string? Stderr { get; init; }

    [JsonPropertyName("output")] public string? Output { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "hook_response".
/// </summary>
public sealed record HookResponseData
{
    [JsonPropertyName("hook_id")] public required string HookId { get; init; }

    [JsonPropertyName("hook_name")] public required string HookName { get; init; }

    [JsonPropertyName("hook_event")] public required string HookEvent { get; init; }

    [JsonPropertyName("output")] public string? Output { get; init; }

    [JsonPropertyName("stdout")] public string? Stdout { get; init; }

    [JsonPropertyName("stderr")] public string? Stderr { get; init; }

    [JsonPropertyName("exit_code")] public int? ExitCode { get; init; }

    /// <summary>
    ///     The hook outcome: "success", "error", or "cancelled".
    /// </summary>
    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "task_notification" (a task completed, failed, or stopped).
/// </summary>
/// <remarks>
///     Not every terminal task emits this message: background tasks may report completion only via a
///     "task_updated" message whose patch status is terminal. Clear active-task tracking on a terminal status
///     from either message (see <see cref="TaskStatuses.IsTerminal" />).
/// </remarks>
public sealed record TaskNotificationData
{
    [JsonPropertyName("task_id")] public required string TaskId { get; init; }

    /// <summary>
    ///     The task status: "completed", "failed", or "stopped".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("output_file")] public required string OutputFile { get; init; }

    [JsonPropertyName("summary")] public required string Summary { get; init; }

    [JsonPropertyName("tool_use_id")] public string? ToolUseId { get; init; }

    [JsonPropertyName("usage")] public TaskUsage? Usage { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "mirror_error": a batch could not be appended to
///     <see cref="ClaudeAgentOptions.SessionStore" /> after retries (or a single attempt timed out).
/// </summary>
/// <remarks>
///     Non-fatal: the local transcript is already durable and the session continues. The external store may be
///     missing the batch (a timed-out append may still land); use
///     <see cref="Sessions.ClaudeSessions.ImportSessionToStoreAsync" /> to catch it up.
/// </remarks>
public sealed record MirrorErrorData
{
    [JsonPropertyName("key")] public Sessions.SessionKey? Key { get; init; }

    [JsonPropertyName("error")] public string Error { get; init; } = "";
}

/// <summary>
///     Usage statistics reported in task_progress and task_notification messages.
/// </summary>
public sealed record TaskUsage
{
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; init; }

    [JsonPropertyName("tool_uses")] public int ToolUses { get; init; }

    [JsonPropertyName("duration_ms")] public long DurationMs { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "task_started".
/// </summary>
public sealed record TaskStartedData
{
    [JsonPropertyName("task_id")] public required string TaskId { get; init; }

    [JsonPropertyName("description")] public required string Description { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    [JsonPropertyName("tool_use_id")] public string? ToolUseId { get; init; }

    /// <summary>
    ///     The task type, e.g. "local_agent", "local_workflow", or a background shell type.
    /// </summary>
    [JsonPropertyName("task_type")]
    public string? TaskType { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "task_progress".
/// </summary>
public sealed record TaskProgressData
{
    [JsonPropertyName("task_id")] public required string TaskId { get; init; }

    [JsonPropertyName("description")] public required string Description { get; init; }

    [JsonPropertyName("usage")] public TaskUsage? Usage { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    [JsonPropertyName("tool_use_id")] public string? ToolUseId { get; init; }

    [JsonPropertyName("last_tool_name")] public string? LastToolName { get; init; }
}

/// <summary>
///     Typed data for system messages with subtype "task_updated" (a background task's state changed).
/// </summary>
/// <remarks>
///     A task's terminal state can arrive only as this message (e.g. a task stopped via TaskStop reports
///     status "killed" here, with no matching task_notification).
/// </remarks>
public sealed record TaskUpdatedData
{
    [JsonPropertyName("task_id")] public string TaskId { get; init; } = "";

    /// <summary>
    ///     The changed fields (e.g. status, end_time).
    /// </summary>
    [JsonPropertyName("patch")]
    public JsonElement? Patch { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    /// <summary>
    ///     The patch status: pending, running, paused, completed, failed, or killed. Null when the patch
    ///     carries no status.
    /// </summary>
    [JsonIgnore]
    public string? Status =>
        Patch is { ValueKind: JsonValueKind.Object } patch &&
        patch.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String
            ? status.GetString()
            : null;
}

/// <summary>
///     Typed data for system messages with subtype "session_state_changed".
/// </summary>
public sealed record SessionStateChangedData
{
    /// <summary>
    ///     The CLI session state: "idle", "running", or "requires_action".
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }
}

/// <summary>
///     Task status helpers spanning both lifecycle vocabularies (task_notification reports "stopped",
///     task_updated reports the raw "killed").
/// </summary>
public static class TaskStatuses
{
    /// <summary>
    ///     Statuses meaning the task has finished and should be cleared from active-task tracking.
    /// </summary>
    public static IReadOnlySet<string> Terminal { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "completed", "failed", "stopped", "killed" };

    /// <summary>
    ///     Returns true when <paramref name="status" /> is a terminal task status.
    /// </summary>
    public static bool IsTerminal(string? status)
    {
        return status is not null && Terminal.Contains(status);
    }
}

/// <summary>
///     Typed data for system messages with subtype "files_persisted".
/// </summary>
public sealed record FilesPersistedData
{
    [JsonPropertyName("files")] public required IReadOnlyList<PersistedFile> Files { get; init; }

    [JsonPropertyName("failed")] public required IReadOnlyList<FailedFile> Failed { get; init; }

    public sealed record PersistedFile
    {
        [JsonPropertyName("filename")] public required string Filename { get; init; }

        [JsonPropertyName("file_id")] public required string FileId { get; init; }
    }

    public sealed record FailedFile
    {
        [JsonPropertyName("filename")] public required string Filename { get; init; }

        [JsonPropertyName("error")] public required string Error { get; init; }
    }
}
