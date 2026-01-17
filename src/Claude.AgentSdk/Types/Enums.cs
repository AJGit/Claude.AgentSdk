using System.Runtime.Serialization;
using Claude.AgentSdk.Attributes;

namespace Claude.AgentSdk.Types;

/// <summary>
///     Message types in the Claude CLI protocol.
/// </summary>
[GenerateEnumStrings]
public enum MessageType
{
    /// <summary>User message.</summary>
    [EnumMember(Value = "user")] User,

    /// <summary>Assistant (Claude) message.</summary>
    [EnumMember(Value = "assistant")] Assistant,

    /// <summary>System message with metadata.</summary>
    [EnumMember(Value = "system")] System,

    /// <summary>Result message with cost and usage information.</summary>
    [EnumMember(Value = "result")] Result,

    /// <summary>Stream event for partial message updates.</summary>
    [EnumMember(Value = "stream_event")] StreamEvent
}

/// <summary>
///     Content block types in assistant messages.
/// </summary>
[GenerateEnumStrings]
public enum ContentBlockType
{
    /// <summary>Text content block.</summary>
    [EnumMember(Value = "text")] Text,

    /// <summary>Thinking content block (extended thinking mode).</summary>
    [EnumMember(Value = "thinking")] Thinking,

    /// <summary>Tool use request block.</summary>
    [EnumMember(Value = "tool_use")] ToolUse,

    /// <summary>Tool result block.</summary>
    [EnumMember(Value = "tool_result")] ToolResult
}

/// <summary>
///     Subtype values for result messages.
/// </summary>
[GenerateEnumStrings]
public enum ResultMessageSubtype
{
    /// <summary>Successful completion.</summary>
    [EnumMember(Value = "success")] Success,

    /// <summary>Error during execution.</summary>
    [EnumMember(Value = "error")] Error,

    /// <summary>Partial result (streaming).</summary>
    [EnumMember(Value = "partial")] Partial
}

/// <summary>
///     Subtype values for system messages.
/// </summary>
[GenerateEnumStrings]
public enum SystemMessageSubtype
{
    /// <summary>Initialization message with session info.</summary>
    [EnumMember(Value = "init")] Init,

    /// <summary>Compact boundary marker after conversation compaction.</summary>
    [EnumMember(Value = "compact_boundary")]
    CompactBoundary
}

/// <summary>
///     MCP server connection status values.
/// </summary>
[GenerateEnumStrings(DefaultNaming = EnumNamingStrategy.KebabCase)]
public enum McpServerStatusType
{
    /// <summary>Server is connected and ready.</summary>
    [EnumMember(Value = "connected")] Connected,

    /// <summary>Server connection failed.</summary>
    [EnumMember(Value = "failed")] Failed,

    /// <summary>Server requires authentication.</summary>
    [EnumMember(Value = "needs-auth")] NeedsAuth,

    /// <summary>Server connection is pending.</summary>
    [EnumMember(Value = "pending")] Pending
}

/// <summary>
///     Source values for session start events.
/// </summary>
[GenerateEnumStrings]
public enum SessionStartSource
{
    /// <summary>Fresh startup.</summary>
    [EnumMember(Value = "startup")] Startup,

    /// <summary>Resumed from previous session.</summary>
    [EnumMember(Value = "resume")] Resume,

    /// <summary>Session cleared and restarted.</summary>
    [EnumMember(Value = "clear")] Clear,

    /// <summary>Session compacted and continued.</summary>
    [EnumMember(Value = "compact")] Compact
}

/// <summary>
///     Reason values for session end events.
/// </summary>
[GenerateEnumStrings]
public enum SessionEndReason
{
    /// <summary>Session explicitly cleared.</summary>
    [EnumMember(Value = "clear")] Clear,

    /// <summary>User logged out.</summary>
    [EnumMember(Value = "logout")] Logout,

    /// <summary>User exited at prompt.</summary>
    [EnumMember(Value = "prompt_input_exit")]
    PromptInputExit,

    /// <summary>Bypass permissions was disabled.</summary>
    [EnumMember(Value = "bypass_permissions_disabled")]
    BypassPermissionsDisabled,

    /// <summary>Other reason.</summary>
    [EnumMember(Value = "other")] Other
}

/// <summary>
///     Notification type values for notification hooks.
/// </summary>
[GenerateEnumStrings]
public enum NotificationType
{
    /// <summary>Permission prompt notification.</summary>
    [EnumMember(Value = "permission_prompt")]
    PermissionPrompt,

    /// <summary>Idle prompt notification.</summary>
    [EnumMember(Value = "idle_prompt")] IdlePrompt,

    /// <summary>Authentication success notification.</summary>
    [EnumMember(Value = "auth_success")] AuthSuccess,

    /// <summary>Elicitation dialog notification.</summary>
    [EnumMember(Value = "elicitation_dialog")]
    ElicitationDialog
}

/// <summary>
///     Control request subtype values.
/// </summary>
[GenerateEnumStrings]
public enum ControlSubtypeEnum
{
    /// <summary>Interrupt the current operation.</summary>
    [EnumMember(Value = "interrupt")] Interrupt,

    /// <summary>Check if a tool can be used.</summary>
    [EnumMember(Value = "can_use_tool")] CanUseTool,

    /// <summary>Initialize the session.</summary>
    [EnumMember(Value = "initialize")] Initialize,

    /// <summary>Set the permission mode.</summary>
    [EnumMember(Value = "set_permission_mode")]
    SetPermissionMode,

    /// <summary>Hook callback request.</summary>
    [EnumMember(Value = "hook_callback")] HookCallback,

    /// <summary>MCP message request.</summary>
    [EnumMember(Value = "mcp_message")] McpMessage,

    /// <summary>Rewind files to a previous state.</summary>
    [EnumMember(Value = "rewind_files")] RewindFiles,

    /// <summary>Set the model to use.</summary>
    [EnumMember(Value = "set_model")] SetModel,

    /// <summary>Set the maximum thinking tokens.</summary>
    [EnumMember(Value = "set_max_thinking_tokens")]
    SetMaxThinkingTokens,

    /// <summary>Query supported commands.</summary>
    [EnumMember(Value = "supported_commands")]
    SupportedCommands,

    /// <summary>Query supported models.</summary>
    [EnumMember(Value = "supported_models")]
    SupportedModels,

    /// <summary>Query MCP server status.</summary>
    [EnumMember(Value = "mcp_server_status")]
    McpServerStatus,

    /// <summary>Query account information.</summary>
    [EnumMember(Value = "account_info")] AccountInfo
}

/// <summary>
///     Decision values for hook responses.
/// </summary>
[GenerateEnumStrings]
public enum HookDecision
{
    /// <summary>Block the operation.</summary>
    [EnumMember(Value = "block")] Block,

    /// <summary>Allow the operation.</summary>
    [EnumMember(Value = "allow")] Allow
}

/// <summary>
///     Permission behavior values.
/// </summary>
[GenerateEnumStrings]
public enum PermissionBehavior
{
    /// <summary>Allow the operation.</summary>
    [EnumMember(Value = "allow")] Allow,

    /// <summary>Deny the operation.</summary>
    [EnumMember(Value = "deny")] Deny
}

/// <summary>
///     Helper class for converting between enum values and their JSON string representations.
/// </summary>
/// <remarks>
///     This partial class contains manually written methods for backward compatibility.
///     Generated extension methods are added to this class by the EnumStringMappingGenerator.
/// </remarks>
public static partial class EnumStringMappings
{
    // NOTE: The methods below are retained for backward compatibility.
    // Generated methods with identical signatures will be in a separate partial class file.
    // Once migration is verified, these can be marked [Obsolete] and eventually removed.

    /// <summary>
    ///     Converts a MessageType enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this MessageType value) => value switch
    {
        MessageType.User => "user",
        MessageType.Assistant => "assistant",
        MessageType.System => "system",
        MessageType.Result => "result",
        MessageType.StreamEvent => "stream_event",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a MessageType enum value.
    /// </summary>
    [Obsolete("Use the generated ParseMessageType method instead.")]
    public static MessageType ParseMessageTypeLegacy(string value) => value switch
    {
        "user" => MessageType.User,
        "assistant" => MessageType.Assistant,
        "system" => MessageType.System,
        "result" => MessageType.Result,
        "stream_event" => MessageType.StreamEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a ContentBlockType enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this ContentBlockType value) => value switch
    {
        ContentBlockType.Text => "text",
        ContentBlockType.Thinking => "thinking",
        ContentBlockType.ToolUse => "tool_use",
        ContentBlockType.ToolResult => "tool_result",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a ContentBlockType enum value.
    /// </summary>
    [Obsolete("Use the generated ParseContentBlockType method instead.")]
    public static ContentBlockType ParseContentBlockTypeLegacy(string value) => value switch
    {
        "text" => ContentBlockType.Text,
        "thinking" => ContentBlockType.Thinking,
        "tool_use" => ContentBlockType.ToolUse,
        "tool_result" => ContentBlockType.ToolResult,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a ResultMessageSubtype enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this ResultMessageSubtype value) => value switch
    {
        ResultMessageSubtype.Success => "success",
        ResultMessageSubtype.Error => "error",
        ResultMessageSubtype.Partial => "partial",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a ResultMessageSubtype enum value.
    /// </summary>
    [Obsolete("Use the generated ParseResultMessageSubtype method instead.")]
    public static ResultMessageSubtype ParseResultMessageSubtypeLegacy(string value) => value switch
    {
        "success" => ResultMessageSubtype.Success,
        "error" => ResultMessageSubtype.Error,
        "partial" => ResultMessageSubtype.Partial,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a SystemMessageSubtype enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this SystemMessageSubtype value) => value switch
    {
        SystemMessageSubtype.Init => "init",
        SystemMessageSubtype.CompactBoundary => "compact_boundary",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a SystemMessageSubtype enum value.
    /// </summary>
    [Obsolete("Use the generated ParseSystemMessageSubtype method instead.")]
    public static SystemMessageSubtype ParseSystemMessageSubtypeLegacy(string value) => value switch
    {
        "init" => SystemMessageSubtype.Init,
        "compact_boundary" => SystemMessageSubtype.CompactBoundary,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a McpServerStatusType enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this McpServerStatusType value) => value switch
    {
        McpServerStatusType.Connected => "connected",
        McpServerStatusType.Failed => "failed",
        McpServerStatusType.NeedsAuth => "needs-auth",
        McpServerStatusType.Pending => "pending",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a McpServerStatusType enum value.
    /// </summary>
    [Obsolete("Use the generated ParseMcpServerStatusType method instead.")]
    public static McpServerStatusType ParseMcpServerStatusTypeLegacy(string value) => value switch
    {
        "connected" => McpServerStatusType.Connected,
        "failed" => McpServerStatusType.Failed,
        "needs-auth" => McpServerStatusType.NeedsAuth,
        "pending" => McpServerStatusType.Pending,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a SessionStartSource enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this SessionStartSource value) => value switch
    {
        SessionStartSource.Startup => "startup",
        SessionStartSource.Resume => "resume",
        SessionStartSource.Clear => "clear",
        SessionStartSource.Compact => "compact",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a SessionStartSource enum value.
    /// </summary>
    [Obsolete("Use the generated ParseSessionStartSource method instead.")]
    public static SessionStartSource ParseSessionStartSourceLegacy(string value) => value switch
    {
        "startup" => SessionStartSource.Startup,
        "resume" => SessionStartSource.Resume,
        "clear" => SessionStartSource.Clear,
        "compact" => SessionStartSource.Compact,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a SessionEndReason enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this SessionEndReason value) => value switch
    {
        SessionEndReason.Clear => "clear",
        SessionEndReason.Logout => "logout",
        SessionEndReason.PromptInputExit => "prompt_input_exit",
        SessionEndReason.BypassPermissionsDisabled => "bypass_permissions_disabled",
        SessionEndReason.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a SessionEndReason enum value.
    /// </summary>
    [Obsolete("Use the generated ParseSessionEndReason method instead.")]
    public static SessionEndReason ParseSessionEndReasonLegacy(string value) => value switch
    {
        "clear" => SessionEndReason.Clear,
        "logout" => SessionEndReason.Logout,
        "prompt_input_exit" => SessionEndReason.PromptInputExit,
        "bypass_permissions_disabled" => SessionEndReason.BypassPermissionsDisabled,
        "other" => SessionEndReason.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Converts a NotificationType enum to its JSON string representation.
    /// </summary>
    [Obsolete("Use the generated ToJsonString extension method instead.")]
    public static string ToJsonStringLegacy(this NotificationType value) => value switch
    {
        NotificationType.PermissionPrompt => "permission_prompt",
        NotificationType.IdlePrompt => "idle_prompt",
        NotificationType.AuthSuccess => "auth_success",
        NotificationType.ElicitationDialog => "elicitation_dialog",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>
    ///     Parses a JSON string to a NotificationType enum value.
    /// </summary>
    [Obsolete("Use the generated ParseNotificationType method instead.")]
    public static NotificationType ParseNotificationTypeLegacy(string value) => value switch
    {
        "permission_prompt" => NotificationType.PermissionPrompt,
        "idle_prompt" => NotificationType.IdlePrompt,
        "auth_success" => NotificationType.AuthSuccess,
        "elicitation_dialog" => NotificationType.ElicitationDialog,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
