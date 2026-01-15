namespace Claude.AgentSdk.Protocol;

/// <summary>
///     Hook event types supported by the SDK.
/// </summary>
public enum HookEvent
{
    /// <summary>
    ///     Fires before a tool executes. Can block or modify the operation.
    /// </summary>
    PreToolUse,

    /// <summary>
    ///     Fires after a tool executes successfully.
    /// </summary>
    PostToolUse,

    /// <summary>
    ///     Fires when a tool execution fails.
    /// </summary>
    PostToolUseFailure,

    /// <summary>
    ///     Fires when a user prompt is submitted.
    /// </summary>
    UserPromptSubmit,

    /// <summary>
    ///     Fires when the agent execution stops.
    /// </summary>
    Stop,

    /// <summary>
    ///     Fires when a subagent starts.
    /// </summary>
    SubagentStart,

    /// <summary>
    ///     Fires when a subagent completes.
    /// </summary>
    SubagentStop,

    /// <summary>
    ///     Fires before conversation compaction.
    /// </summary>
    PreCompact,

    /// <summary>
    ///     Fires when a permission dialog would be displayed.
    /// </summary>
    PermissionRequest,

    /// <summary>
    ///     Fires when a session starts.
    /// </summary>
    SessionStart,

    /// <summary>
    ///     Fires when a session ends.
    /// </summary>
    SessionEnd,

    /// <summary>
    ///     Fires for agent status notifications.
    /// </summary>
    Notification
}

/// <summary>
///     Hook matcher configuration.
/// </summary>
public sealed record HookMatcher
{
    /// <summary>
    ///     Pattern to match (e.g., tool name like "Bash" or "Write|Edit").
    /// </summary>
    public string? Matcher { get; init; }

    /// <summary>
    ///     Hook callbacks for this matcher.
    /// </summary>
    public required IReadOnlyList<HookCallback> Hooks { get; init; }

    /// <summary>
    ///     Timeout in seconds for hooks in this matcher.
    /// </summary>
    public double? Timeout { get; init; }
}

/// <summary>
///     Delegate for hook callbacks.
/// </summary>
public delegate Task<HookOutput> HookCallback(
    HookInput input,
    string? toolUseId,
    HookContext context,
    CancellationToken cancellationToken = default);

/// <summary>
///     Context for hook callbacks.
/// </summary>
public sealed record HookContext
{
    // Reserved for future abort signal support
}

/// <summary>
///     Base class for hook input.
/// </summary>
public abstract record HookInput
{
    [JsonPropertyName("session_id")] public required string SessionId { get; init; }

    [JsonPropertyName("transcript_path")] public required string TranscriptPath { get; init; }

    [JsonPropertyName("cwd")] public required string Cwd { get; init; }

    [JsonPropertyName("permission_mode")] public string? PermissionMode { get; init; }

    [JsonPropertyName("hook_event_name")] public abstract string HookEventName { get; }
}

public sealed record PreToolUseHookInput : HookInput
{
    public override string HookEventName => "PreToolUse";

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")] public required JsonElement ToolInput { get; init; }
}

public sealed record PostToolUseHookInput : HookInput
{
    public override string HookEventName => "PostToolUse";

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")] public required JsonElement ToolInput { get; init; }

    [JsonPropertyName("tool_response")] public JsonElement? ToolResponse { get; init; }
}

public sealed record UserPromptSubmitHookInput : HookInput
{
    public override string HookEventName => "UserPromptSubmit";

    [JsonPropertyName("prompt")] public required string Prompt { get; init; }
}

public sealed record StopHookInput : HookInput
{
    public override string HookEventName => "Stop";

    [JsonPropertyName("stop_hook_active")] public required bool StopHookActive { get; init; }
}

public sealed record SubagentStopHookInput : HookInput
{
    public override string HookEventName => "SubagentStop";

    [JsonPropertyName("stop_hook_active")] public required bool StopHookActive { get; init; }

    [JsonPropertyName("agent_id")] public string? AgentId { get; init; }

    [JsonPropertyName("agent_transcript_path")]
    public string? AgentTranscriptPath { get; init; }
}

public sealed record PreCompactHookInput : HookInput
{
    public override string HookEventName => "PreCompact";

    [JsonPropertyName("trigger")] public required string Trigger { get; init; }

    [JsonPropertyName("custom_instructions")]
    public string? CustomInstructions { get; init; }
}

/// <summary>
///     Input for PostToolUseFailure hooks.
/// </summary>
public sealed record PostToolUseFailureHookInput : HookInput
{
    public override string HookEventName => "PostToolUseFailure";

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")] public required JsonElement ToolInput { get; init; }

    [JsonPropertyName("error")] public required string Error { get; init; }

    [JsonPropertyName("is_interrupt")] public bool IsInterrupt { get; init; }
}

/// <summary>
///     Input for SubagentStart hooks.
/// </summary>
public sealed record SubagentStartHookInput : HookInput
{
    public override string HookEventName => "SubagentStart";

    [JsonPropertyName("agent_id")] public required string AgentId { get; init; }

    [JsonPropertyName("agent_type")] public required string AgentType { get; init; }
}

/// <summary>
///     Input for PermissionRequest hooks.
/// </summary>
public sealed record PermissionRequestHookInput : HookInput
{
    public override string HookEventName => "PermissionRequest";

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")] public required JsonElement ToolInput { get; init; }

    [JsonPropertyName("permission_suggestions")]
    public JsonElement? PermissionSuggestions { get; init; }
}

/// <summary>
///     Input for SessionStart hooks.
/// </summary>
public sealed record SessionStartHookInput : HookInput
{
    public override string HookEventName => "SessionStart";

    /// <summary>
    ///     How the session started: "startup", "resume", "clear", or "compact".
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }
}

/// <summary>
///     Input for SessionEnd hooks.
/// </summary>
public sealed record SessionEndHookInput : HookInput
{
    public override string HookEventName => "SessionEnd";

    /// <summary>
    ///     Why the session ended: "clear", "logout", "prompt_input_exit", "bypass_permissions_disabled", or "other".
    /// </summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}

/// <summary>
///     Input for Notification hooks.
/// </summary>
public sealed record NotificationHookInput : HookInput
{
    public override string HookEventName => "Notification";

    /// <summary>
    ///     Status message from the agent.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    ///     Type of notification: "permission_prompt", "idle_prompt", "auth_success", or "elicitation_dialog".
    /// </summary>
    [JsonPropertyName("notification_type")]
    public required string NotificationType { get; init; }

    /// <summary>
    ///     Optional title set by the agent.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>
///     Base class for hook output.
/// </summary>
public abstract record HookOutput;

/// <summary>
///     Synchronous hook output.
/// </summary>
public sealed record SyncHookOutput : HookOutput
{
    /// <summary>
    ///     Whether to continue execution (default: true).
    /// </summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; init; }

    /// <summary>
    ///     Hide stdout from transcript mode.
    /// </summary>
    [JsonPropertyName("suppressOutput")]
    public bool? SuppressOutput { get; init; }

    /// <summary>
    ///     Message shown when continue is false.
    /// </summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    /// <summary>
    ///     Decision type (e.g., "block").
    /// </summary>
    [JsonPropertyName("decision")]
    public string? Decision { get; init; }

    /// <summary>
    ///     Warning message for the user.
    /// </summary>
    [JsonPropertyName("systemMessage")]
    public string? SystemMessage { get; init; }

    /// <summary>
    ///     Feedback message for Claude.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    ///     Hook-specific output.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    public JsonElement? HookSpecificOutput { get; init; }
}

/// <summary>
///     Async hook output that defers execution.
/// </summary>
public sealed record AsyncHookOutput : HookOutput
{
    /// <summary>
    ///     Timeout in milliseconds for the async operation.
    /// </summary>
    [JsonPropertyName("asyncTimeout")]
    public int? AsyncTimeout { get; init; }
}
