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
///     Typed data for system messages with subtype "task_notification".
/// </summary>
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
