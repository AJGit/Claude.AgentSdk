namespace Claude.AgentSdk.Protocol;

/// <summary>
///     Control request from the CLI.
/// </summary>
internal sealed record ControlRequest
{
    [JsonPropertyName("type")] public string Type => "control_request";

    [JsonPropertyName("request_id")] public required string RequestId { get; init; }

    [JsonPropertyName("request")] public required JsonElement Request { get; init; }
}

/// <summary>
///     Control response to the CLI.
/// </summary>
internal sealed record ControlResponse
{
    [JsonPropertyName("type")] public string Type => "control_response";

    [JsonPropertyName("response")] public required ControlResponsePayload Response { get; init; }
}

[JsonDerivedType(typeof(ControlSuccessResponse))]
[JsonDerivedType(typeof(ControlErrorResponse))]
internal abstract record ControlResponsePayload
{
    [JsonPropertyName("request_id")] public required string RequestId { get; init; }
}

internal sealed record ControlSuccessResponse : ControlResponsePayload
{
    [JsonPropertyName("subtype")] public string Subtype => "success";

    [JsonPropertyName("response")] public object? ResponseData { get; init; }
}

internal sealed record ControlErrorResponse : ControlResponsePayload
{
    [JsonPropertyName("subtype")] public string Subtype => "error";

    [JsonPropertyName("error")] public required string Error { get; init; }
}

/// <summary>
///     Control request subtypes.
/// </summary>
internal static class ControlSubtype
{
    public const string Interrupt = "interrupt";
    public const string CanUseTool = "can_use_tool";
    public const string Initialize = "initialize";
    public const string SetPermissionMode = "set_permission_mode";
    public const string HookCallback = "hook_callback";
    public const string McpMessage = "mcp_message";
    public const string RewindFiles = "rewind_files";
    public const string SetModel = "set_model";
    public const string SetMaxThinkingTokens = "set_max_thinking_tokens";
    public const string SupportedCommands = "supported_commands";
    public const string SupportedModels = "supported_models";
    public const string McpServerStatus = "mcp_server_status";
    public const string AccountInfo = "account_info";
    public const string ReconnectMcpServer = "reconnect_mcp_server";
    public const string ToggleMcpServer = "toggle_mcp_server";
    public const string SetMcpServers = "set_mcp_servers";
}

/// <summary>
///     Initialize request payload.
/// </summary>
internal sealed record InitializeRequest
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.Initialize;

    [JsonPropertyName("hooks")] public JsonElement? Hooks { get; init; }
}

/// <summary>
///     Permission request payload.
/// </summary>
internal sealed record CanUseToolRequest
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.CanUseTool;

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("input")] public required JsonElement Input { get; init; }

    [JsonPropertyName("permission_suggestions")]
    public JsonElement? PermissionSuggestions { get; init; }

    [JsonPropertyName("blocked_path")] public string? BlockedPath { get; init; }
}

/// <summary>
///     Hook callback request payload.
/// </summary>
internal sealed record HookCallbackRequest
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.HookCallback;

    [JsonPropertyName("callback_id")] public required string CallbackId { get; init; }

    [JsonPropertyName("input")] public required JsonElement Input { get; init; }

    [JsonPropertyName("tool_use_id")] public string? ToolUseId { get; init; }
}

/// <summary>
///     MCP message request payload.
/// </summary>
internal sealed record McpMessageRequest
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.McpMessage;

    [JsonPropertyName("server_name")] public required string ServerName { get; init; }

    [JsonPropertyName("message")] public required JsonElement Message { get; init; }
}

// ============================================================================
// Control Request Body DTOs (SDK -> CLI)
// ============================================================================

/// <summary>
///     Interrupt request body.
/// </summary>
internal sealed record InterruptRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.Interrupt;
}

/// <summary>
///     Set permission mode request body.
/// </summary>
internal sealed record SetPermissionModeRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SetPermissionMode;

    [JsonPropertyName("mode")] public required string Mode { get; init; }
}

/// <summary>
///     Set model request body.
/// </summary>
internal sealed record SetModelRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SetModel;

    [JsonPropertyName("model")] public required string Model { get; init; }
}

/// <summary>
///     Rewind files request body.
/// </summary>
internal sealed record RewindFilesRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.RewindFiles;

    [JsonPropertyName("user_message_id")] public required string UserMessageId { get; init; }
}

/// <summary>
///     Set max thinking tokens request body.
/// </summary>
internal sealed record SetMaxThinkingTokensRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SetMaxThinkingTokens;

    [JsonPropertyName("max_thinking_tokens")]
    public required int MaxThinkingTokens { get; init; }
}

/// <summary>
///     Supported commands request body.
/// </summary>
internal sealed record SupportedCommandsRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SupportedCommands;
}

/// <summary>
///     Supported models request body.
/// </summary>
internal sealed record SupportedModelsRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SupportedModels;
}

/// <summary>
///     MCP server status request body.
/// </summary>
internal sealed record McpServerStatusRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.McpServerStatus;
}

/// <summary>
///     Account info request body.
/// </summary>
internal sealed record AccountInfoRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.AccountInfo;
}

/// <summary>
///     Reconnect MCP server request body.
/// </summary>
internal sealed record ReconnectMcpServerRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.ReconnectMcpServer;

    [JsonPropertyName("server_name")] public required string ServerName { get; init; }
}

/// <summary>
///     Toggle MCP server request body.
/// </summary>
internal sealed record ToggleMcpServerRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.ToggleMcpServer;

    [JsonPropertyName("server_name")] public required string ServerName { get; init; }

    [JsonPropertyName("enabled")] public required bool Enabled { get; init; }
}

/// <summary>
///     Set MCP servers request body.
/// </summary>
internal sealed record SetMcpServersRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.SetMcpServers;

    [JsonPropertyName("servers")] public required object Servers { get; init; }
}

/// <summary>
///     Initialize request body with hooks configuration.
/// </summary>
internal sealed record InitializeRequestBody
{
    [JsonPropertyName("subtype")] public string Subtype => ControlSubtype.Initialize;

    [JsonPropertyName("hooks")] public object? Hooks { get; init; }
}

// ============================================================================
// Control Request Envelope (SDK -> CLI)
// ============================================================================

/// <summary>
///     Control request envelope sent from SDK to CLI.
/// </summary>
internal sealed record ControlRequestEnvelope
{
    [JsonPropertyName("type")] public string Type => "control_request";

    [JsonPropertyName("request_id")] public required string RequestId { get; init; }

    [JsonPropertyName("request")] public required object Request { get; init; }
}

// ============================================================================
// Permission Response DTOs (SDK -> CLI)
// ============================================================================

/// <summary>
///     Base class for permission responses.
/// </summary>
internal abstract record PermissionResponseDto;

/// <summary>
///     Permission allowed response.
/// </summary>
internal sealed record PermissionAllowResponse : PermissionResponseDto
{
    [JsonPropertyName("behavior")] public string Behavior => "allow";

    [JsonPropertyName("updated_input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? UpdatedInput { get; init; }

    [JsonPropertyName("updated_permissions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PermissionUpdate>? UpdatedPermissions { get; init; }
}

/// <summary>
///     Permission denied response.
/// </summary>
internal sealed record PermissionDenyResponse : PermissionResponseDto
{
    [JsonPropertyName("behavior")] public string Behavior => "deny";

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("interrupt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Interrupt { get; init; }
}

// ============================================================================
// Hook Response DTOs (SDK -> CLI)
// ============================================================================

/// <summary>
///     Hook continue response (synchronous).
/// </summary>
internal sealed record HookContinueResponse
{
    [JsonPropertyName("continue")] public bool Continue { get; init; } = true;

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

// ============================================================================
// User Message Envelope (SDK -> CLI)
// ============================================================================

/// <summary>
///     User message envelope sent to the CLI.
/// </summary>
internal sealed record UserMessageEnvelope
{
    [JsonPropertyName("type")] public string Type => "user";

    [JsonPropertyName("message")] public required UserMessagePayload Message { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }

    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }
}

/// <summary>
///     User message payload within an envelope.
/// </summary>
internal sealed record UserMessagePayload
{
    [JsonPropertyName("role")] public string Role => "user";

    [JsonPropertyName("content")] public required string Content { get; init; }
}

// ============================================================================
// MCP Response Wrapper (SDK -> CLI)
// ============================================================================

/// <summary>
///     Wrapper for MCP responses.
/// </summary>
internal sealed record McpResponseWrapper
{
    [JsonPropertyName("mcp_response")] public object? McpResponse { get; init; }
}

// ============================================================================
// Public Result Types
// ============================================================================

/// <summary>
///     Result of setting MCP servers.
/// </summary>
public sealed record McpSetServersResult
{
    /// <summary>
    ///     Servers that were added.
    /// </summary>
    [JsonPropertyName("added")]
    public IReadOnlyList<string> Added { get; init; } = [];

    /// <summary>
    ///     Servers that were removed.
    /// </summary>
    [JsonPropertyName("removed")]
    public IReadOnlyList<string> Removed { get; init; } = [];

    /// <summary>
    ///     Errors that occurred during configuration.
    /// </summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
///     Result of rewinding files to a previous state.
/// </summary>
public sealed record RewindFilesResult
{
    /// <summary>
    ///     Whether the rewind operation is possible.
    /// </summary>
    [JsonPropertyName("can_rewind")]
    public bool CanRewind { get; init; }

    /// <summary>
    ///     Error message if rewind failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    ///     Number of files changed during rewind.
    /// </summary>
    [JsonPropertyName("files_changed")]
    public int? FilesChanged { get; init; }

    /// <summary>
    ///     Number of line insertions.
    /// </summary>
    [JsonPropertyName("insertions")]
    public int? Insertions { get; init; }

    /// <summary>
    ///     Number of line deletions.
    /// </summary>
    [JsonPropertyName("deletions")]
    public int? Deletions { get; init; }
}
