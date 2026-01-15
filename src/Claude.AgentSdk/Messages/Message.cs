namespace Claude.AgentSdk.Messages;

/// <summary>
///     Base class for all message types from the Claude CLI.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ResultMessage), "result")]
[JsonDerivedType(typeof(StreamEvent), "stream_event")]
public abstract record Message;

/// <summary>
///     User message in the conversation.
/// </summary>
public sealed record UserMessage : Message
{
    [JsonPropertyName("message")] public required UserMessageContent MessageContent { get; init; }
}

/// <summary>
///     User message content wrapper.
/// </summary>
public sealed record UserMessageContent
{
    [JsonPropertyName("content")] public JsonElement Content { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}

/// <summary>
///     Assistant message with content blocks.
/// </summary>
public sealed record AssistantMessage : Message
{
    [JsonPropertyName("message")] public required AssistantMessageContent MessageContent { get; init; }
}

/// <summary>
///     Assistant message content wrapper.
/// </summary>
public sealed record AssistantMessageContent
{
    [JsonPropertyName("content")] public required IReadOnlyList<ContentBlock> Content { get; init; }

    [JsonPropertyName("model")] public required string Model { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    [JsonPropertyName("error")] public string? Error { get; init; }
}

/// <summary>
///     System message with metadata.
/// </summary>
public sealed record SystemMessage : Message
{
    [JsonPropertyName("subtype")] public required string Subtype { get; init; }

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    [JsonPropertyName("cwd")] public string? Cwd { get; init; }

    [JsonPropertyName("slash_commands")] public IReadOnlyList<string>? SlashCommands { get; init; }

    [JsonPropertyName("tools")] public IReadOnlyList<string>? Tools { get; init; }

    [JsonPropertyName("mcp_servers")] public IReadOnlyList<McpServerStatus>? McpServers { get; init; }

    [JsonPropertyName("model")] public string? Model { get; init; }

    [JsonPropertyName("permission_mode")] public string? PermissionMode { get; init; }

    [JsonPropertyName("compact_metadata")] public CompactMetadata? CompactMetadata { get; init; }

    [JsonPropertyName("data")] public JsonElement? Data { get; init; }

    /// <summary>
    ///     Returns true if this is an init message (subtype == "init").
    /// </summary>
    public bool IsInit => Subtype == "init";

    /// <summary>
    ///     Returns true if this is a compact boundary message.
    /// </summary>
    public bool IsCompactBoundary => Subtype == "compact_boundary";
}

/// <summary>
///     MCP server connection status from init message.
/// </summary>
public sealed record McpServerStatus
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("status")] public required string Status { get; init; }

    [JsonPropertyName("error")] public string? Error { get; init; }
}

/// <summary>
///     Metadata from conversation compaction.
/// </summary>
public sealed record CompactMetadata
{
    [JsonPropertyName("pre_tokens")] public int PreTokens { get; init; }

    [JsonPropertyName("post_tokens")] public int PostTokens { get; init; }

    [JsonPropertyName("trigger")] public string? Trigger { get; init; }
}

/// <summary>
///     Result message with cost and usage information.
/// </summary>
public sealed record ResultMessage : Message
{
    [JsonPropertyName("subtype")] public required string Subtype { get; init; }

    [JsonPropertyName("duration_ms")] public required int DurationMs { get; init; }

    [JsonPropertyName("duration_api_ms")] public required int DurationApiMs { get; init; }

    [JsonPropertyName("is_error")] public required bool IsError { get; init; }

    [JsonPropertyName("num_turns")] public required int NumTurns { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }

    [JsonPropertyName("total_cost_usd")] public double? TotalCostUsd { get; init; }

    [JsonPropertyName("usage")] public JsonElement? Usage { get; init; }

    [JsonPropertyName("result")] public string? Result { get; init; }

    [JsonPropertyName("structured_output")]
    public JsonElement? StructuredOutput { get; init; }
}

/// <summary>
///     Stream event for partial message updates during streaming.
/// </summary>
public sealed record StreamEvent : Message
{
    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }

    [JsonPropertyName("event")] public required JsonElement Event { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}
