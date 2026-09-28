using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Messages;

/// <summary>
///     Base class for all message types from the Claude CLI.
/// </summary>
/// <remarks>
///     Use the generated Match extension methods for functional pattern matching:
///     <code>
///     var result = message.Match(
///         userMessage: u => $"User: {u.MessageContent.Content}",
///         assistantMessage: a => $"Assistant: {a.MessageContent.Model}",
///         systemMessage: s => $"System: {s.Subtype}",
///         resultMessage: r => $"Result: {r.SessionId}",
///         streamEvent: e => $"Event: {e.Uuid}",
///         ...
///     );
///     </code>
///     Unknown message types from newer CLI versions are skipped rather than failing the stream.
/// </remarks>
[GenerateMatch]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ResultMessage), "result")]
[JsonDerivedType(typeof(StreamEvent), "stream_event")]
[JsonDerivedType(typeof(ToolProgressMessage), "tool_progress")]
[JsonDerivedType(typeof(ToolUseSummaryMessage), "tool_use_summary")]
[JsonDerivedType(typeof(AuthStatusMessage), "auth_status")]
[JsonDerivedType(typeof(RateLimitEvent), "rate_limit_event")]
[JsonDerivedType(typeof(ConversationResetMessage), "conversation_reset")]
public abstract record Message
{
    /// <summary>
    ///     The raw JSON of the message as received from the CLI, including fields this SDK version does not model.
    /// </summary>
    /// <remarks>
    ///     Populated for messages parsed from the CLI stream; <c>default</c> for messages constructed in code.
    /// </remarks>
    [JsonIgnore]
    public JsonElement RawJson { get; init; }
}

/// <summary>
///     User message in the conversation.
/// </summary>
public sealed record UserMessage : Message
{
    [JsonPropertyName("message")] public required UserMessageContent MessageContent { get; init; }

    /// <summary>
    ///     Unique identifier of this transcript entry. Can be passed to
    ///     <see cref="ClaudeAgentSession.RewindFilesAsync" /> or <see cref="ClaudeAgentOptions.ResumeSessionAt" />.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>
    ///     For subagent messages, the id of the Agent tool_use block that spawned the subagent.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    ///     The structured tool result attached to a tool-result user message, if any.
    /// </summary>
    [JsonPropertyName("tool_use_result")]
    public JsonElement? ToolUseResult { get; init; }

    /// <summary>
    ///     Session this message belongs to.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    ///     Provenance of this message (see <see cref="MessageOrigin" />). Null when the CLI did not attribute it.
    ///     Populated on injected turns (task notifications, channel/peer messages, ...); tool-result messages
    ///     never carry it.
    /// </summary>
    [JsonPropertyName("origin")]
    [JsonConverter(typeof(LenientMessageOriginConverter))]
    public MessageOrigin? Origin { get; init; }
}

/// <summary>
///     User message content wrapper.
/// </summary>
public sealed record UserMessageContent
{
    /// <summary>
    ///     The content: either a string or an array of content blocks.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement Content { get; init; }

    /// <summary>
    ///     Legacy location for the uuid. The CLI sends it at the top level; prefer <see cref="UserMessage.Uuid" />.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>
    ///     Legacy location. The CLI sends it at the top level; prefer <see cref="UserMessage.ParentToolUseId" />.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    ///     Legacy location. The CLI sends it at the top level; prefer <see cref="UserMessage.ToolUseResult" />.
    /// </summary>
    [JsonPropertyName("tool_use_result")]
    public JsonElement? ToolUseResult { get; init; }
}

/// <summary>
///     Assistant message with content blocks.
/// </summary>
public sealed record AssistantMessage : Message
{
    [JsonPropertyName("message")] public required AssistantMessageContent MessageContent { get; init; }

    /// <summary>
    ///     For subagent messages, the id of the Agent tool_use block that spawned the subagent.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    ///     Error category when the turn failed: authentication_failed, billing_error, rate_limit,
    ///     invalid_request, server_error, or unknown.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    ///     Session this message belongs to.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    ///     Unique identifier of this transcript entry.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

/// <summary>
///     Assistant message content wrapper (the Anthropic API message).
/// </summary>
public sealed record AssistantMessageContent
{
    /// <summary>
    ///     Content blocks. Block types this SDK version does not recognize are skipped.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonConverter(typeof(ContentBlockListConverter))]
    public required IReadOnlyList<ContentBlock> Content { get; init; }

    [JsonPropertyName("model")] public required string Model { get; init; }

    /// <summary>
    ///     The API message id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    ///     Why the model stopped generating (e.g. "end_turn", "tool_use").
    /// </summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    /// <summary>
    ///     Per-turn token usage.
    /// </summary>
    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; init; }

    /// <summary>
    ///     Legacy location. The CLI sends it at the top level; prefer <see cref="AssistantMessage.ParentToolUseId" />.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    ///     Legacy location. The CLI sends it at the top level; prefer <see cref="AssistantMessage.Error" />.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
///     System message with metadata.
/// </summary>
/// <remarks>
///     Subtype-specific payloads (task lifecycle, hook events, session state, ...) can be read with
///     <c>GetData&lt;T&gt;()</c>, e.g. <c>message.GetData&lt;TaskStartedData&gt;()</c>.
/// </remarks>
public sealed record SystemMessage : Message
{
    [JsonPropertyName("subtype")] public required string Subtype { get; init; }

    /// <summary>
    ///     Gets the strongly-typed subtype enum value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The subtype is not known to this SDK version.</exception>
    [JsonIgnore]
    public SystemMessageSubtype SubtypeEnum => EnumStringMappings.ParseSystemMessageSubtype(Subtype);

    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

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

    /// <summary>
    ///     Returns true for hook lifecycle events (emitted when <see cref="ClaudeAgentOptions.IncludeHookEvents" /> is set).
    /// </summary>
    public bool IsHookEvent => Subtype is "hook_started" or "hook_response";
}

/// <summary>
///     MCP server connection status from init message.
/// </summary>
public sealed record McpServerStatus
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("status")] public required string Status { get; init; }

    /// <summary>
    ///     Gets the strongly-typed status enum value.
    /// </summary>
    [JsonIgnore]
    public McpServerStatusType StatusEnum => EnumStringMappings.ParseMcpServerStatusType(Status);

    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("config")] public JsonElement? Config { get; init; }

    [JsonPropertyName("scope")] public string? Scope { get; init; }

    [JsonPropertyName("tools")] public IReadOnlyList<string>? Tools { get; init; }
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
///     Token usage information from the API response.
/// </summary>
public sealed record UsageInfo
{
    /// <summary>
    ///     Total input tokens sent to the API for this turn.
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    /// <summary>
    ///     Total output tokens generated by Claude.
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    /// <summary>
    ///     Input tokens served from Anthropic's prompt cache.
    /// </summary>
    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; init; }

    /// <summary>
    ///     Input tokens written to the prompt cache for future reuse.
    /// </summary>
    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; init; }

    /// <summary>
    ///     Number of web search requests made.
    /// </summary>
    [JsonPropertyName("web_search_requests")]
    public int? WebSearchRequests { get; init; }

    /// <summary>
    ///     Calculates the total context size (cache + input + output).
    /// </summary>
    public int TotalContextTokens => (CacheReadInputTokens ?? 0) + InputTokens + OutputTokens;
}

/// <summary>
///     Per-model token usage and cost breakdown (from <c>modelUsage</c> on result messages).
/// </summary>
public sealed record ModelUsage
{
    [JsonPropertyName("inputTokens")] public long InputTokens { get; init; }

    [JsonPropertyName("outputTokens")] public long OutputTokens { get; init; }

    [JsonPropertyName("cacheReadInputTokens")]
    public long CacheReadInputTokens { get; init; }

    [JsonPropertyName("cacheCreationInputTokens")]
    public long CacheCreationInputTokens { get; init; }

    [JsonPropertyName("webSearchRequests")]
    public long WebSearchRequests { get; init; }

    [JsonPropertyName("costUSD")] public double CostUsd { get; init; }

    [JsonPropertyName("contextWindow")] public long ContextWindow { get; init; }

    [JsonPropertyName("maxOutputTokens")] public long MaxOutputTokens { get; init; }

    /// <summary>
    ///     Canonical model id used for the pricing lookup. May differ from the raw model key
    ///     (provider-specific ids, aliases).
    /// </summary>
    [JsonPropertyName("canonicalModel")]
    public string? CanonicalModel { get; init; }

    /// <summary>
    ///     API provider that served this model (e.g. firstParty, bedrock, vertex, foundry).
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }
}

/// <summary>
///     A tool use that was deferred by a PreToolUse hook returning <c>permissionDecision: "defer"</c>.
/// </summary>
public sealed record DeferredToolUse
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("input")] public JsonElement Input { get; init; }
}

/// <summary>
///     Result message with cost and usage information.
/// </summary>
public sealed record ResultMessage : Message
{
    [JsonPropertyName("subtype")] public required string Subtype { get; init; }

    /// <summary>
    ///     Gets the strongly-typed subtype enum value.
    /// </summary>
    [JsonIgnore]
    public ResultMessageSubtype SubtypeEnum => EnumStringMappings.ParseResultMessageSubtype(Subtype);

    [JsonPropertyName("duration_ms")] public required int DurationMs { get; init; }

    [JsonPropertyName("duration_api_ms")] public required int DurationApiMs { get; init; }

    [JsonPropertyName("is_error")] public required bool IsError { get; init; }

    [JsonPropertyName("num_turns")] public required int NumTurns { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }

    [JsonPropertyName("total_cost_usd")] public double? TotalCostUsd { get; init; }

    [JsonPropertyName("usage")] public UsageInfo? Usage { get; init; }

    [JsonPropertyName("result")] public string? Result { get; init; }

    [JsonPropertyName("structured_output")]
    public JsonElement? StructuredOutput { get; init; }

    [JsonPropertyName("stop_reason")] public string? StopReason { get; init; }

    /// <summary>
    ///     Per-model usage and cost, keyed by model id.
    /// </summary>
    [JsonPropertyName("modelUsage")]
    public IReadOnlyDictionary<string, ModelUsage>? ModelUsage { get; init; }

    /// <summary>
    ///     Tool calls that were denied during this run.
    /// </summary>
    [JsonPropertyName("permission_denials")]
    public JsonElement? PermissionDenials { get; init; }

    /// <summary>
    ///     The tool call deferred by a PreToolUse hook, when the run stopped on a "defer" decision.
    /// </summary>
    [JsonPropertyName("deferred_tool_use")]
    public DeferredToolUse? DeferredToolUse { get; init; }

    /// <summary>
    ///     Error messages for terminal error results (e.g. error_max_turns, error_during_execution).
    /// </summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>
    ///     HTTP status code (e.g. 429, 500, 529) of the failing API call when <see cref="IsError" /> is true.
    ///     Safe to log (no message content).
    /// </summary>
    [JsonPropertyName("api_error_status")]
    public int? ApiErrorStatus { get; init; }

    [JsonPropertyName("uuid")] public string? Uuid { get; init; }

    /// <summary>
    ///     Why the query loop terminated (e.g. "completed", "max_turns", "aborted_streaming", "aborted_tools").
    ///     "aborted_streaming" / "aborted_tools" mean the turn was cancelled via an interrupt.
    ///     Null when the CLI did not report one.
    /// </summary>
    [JsonPropertyName("terminal_reason")]
    public string? TerminalReason { get; init; }

    /// <summary>
    ///     Origin of the user message that triggered this turn. Lets a streaming consumer distinguish the result
    ///     of its own prompt (null, or kind "human") from results of injected turns such as background-task
    ///     notifications.
    /// </summary>
    [JsonPropertyName("origin")]
    [JsonConverter(typeof(LenientMessageOriginConverter))]
    public MessageOrigin? Origin { get; init; }
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

/// <summary>
///     Tool progress update message.
/// </summary>
public sealed record ToolProgressMessage : Message
{
    [JsonPropertyName("tool_use_id")] public required string ToolUseId { get; init; }

    [JsonPropertyName("tool_name")] public required string ToolName { get; init; }

    [JsonPropertyName("parent_tool_use_id")] public string? ParentToolUseId { get; init; }

    [JsonPropertyName("elapsed_time_seconds")] public required double ElapsedTimeSeconds { get; init; }

    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
}

/// <summary>
///     Tool use summary message.
/// </summary>
public sealed record ToolUseSummaryMessage : Message
{
    [JsonPropertyName("summary")] public required string Summary { get; init; }

    [JsonPropertyName("preceding_tool_use_ids")] public required IReadOnlyList<string> PrecedingToolUseIds { get; init; }

    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
}

/// <summary>
///     Authentication status message.
/// </summary>
public sealed record AuthStatusMessage : Message
{
    [JsonPropertyName("isAuthenticating")] public required bool IsAuthenticating { get; init; }

    [JsonPropertyName("output")] public required IReadOnlyList<string> Output { get; init; }

    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
}

/// <summary>
///     Rate limit status, emitted whenever the rate limit state changes.
/// </summary>
public sealed record RateLimitInfo
{
    /// <summary>
    ///     "allowed" (within the limit), "allowed_warning" (approaching it), or "rejected" (limit hit).
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    ///     Unix timestamp (seconds) when the rate limit window resets.
    /// </summary>
    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; init; }

    /// <summary>
    ///     Which rate limit window applies: five_hour, seven_day, seven_day_opus, seven_day_sonnet, or overage.
    /// </summary>
    [JsonPropertyName("rateLimitType")]
    public string? RateLimitType { get; init; }

    /// <summary>
    ///     Fraction of the rate limit consumed (0.0 - 1.0).
    /// </summary>
    [JsonPropertyName("utilization")]
    public double? Utilization { get; init; }

    [JsonPropertyName("overageStatus")] public string? OverageStatus { get; init; }

    [JsonPropertyName("overageResetsAt")] public long? OverageResetsAt { get; init; }

    [JsonPropertyName("overageDisabledReason")]
    public string? OverageDisabledReason { get; init; }

    /// <summary>
    ///     Fields not modeled above.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>
///     Rate limit event emitted when rate limit info changes. Use it to warn users before they hit a hard
///     limit, or to back off when the status is "rejected".
/// </summary>
public sealed record RateLimitEvent : Message
{
    [JsonPropertyName("rate_limit_info")] public required RateLimitInfo RateLimitInfo { get; init; }

    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
}

/// <summary>
///     Emitted when the session's conversation is replaced without ending the connection (e.g. after
///     <c>/clear</c>).
/// </summary>
/// <remarks>
///     A reset clears the conversation history and zeroes the running totals reported on subsequent
///     <see cref="ResultMessage" /> objects (e.g. total cost). Snapshot accumulated totals when this arrives.
/// </remarks>
public sealed record ConversationResetMessage : Message
{
    /// <summary>
    ///     Opaque identifier for the fresh conversation. This is not the session id of subsequent messages.
    /// </summary>
    [JsonPropertyName("new_conversation_id")]
    public required string NewConversationId { get; init; }

    [JsonPropertyName("uuid")] public required string Uuid { get; init; }

    /// <summary>
    ///     The session that was reset (messages after the reset carry a new session id).
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}

/// <summary>
///     Provenance of a user-role message, and (on a <see cref="ResultMessage" />) of the message that triggered
///     that turn.
/// </summary>
/// <remarks>
///     Only <see cref="Kind" /> is always present; other fields depend on the kind. Newer CLI versions may emit
///     kinds not known here — treat anything unrecognized as "not human". Sender fields are sender-asserted:
///     use them for routing/display, never as proof of identity.
/// </remarks>
public sealed record MessageOrigin
{
    /// <summary>
    ///     Discriminator: human, channel, peer, task-notification, coordinator, unclassified, observer,
    ///     auto-continuation, observer-activity, ...
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>kind == "channel": name of the MCP server the message arrived on.</summary>
    [JsonPropertyName("server")]
    public string? Server { get; init; }

    /// <summary>kind == "peer" / "observer": sender address.</summary>
    [JsonPropertyName("from")]
    public string? From { get; init; }

    /// <summary>kind == "peer": sender display name, normalized by the CLI.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>kind == "peer": the sender's host-openable session id, if provided.</summary>
    [JsonPropertyName("fromSession")]
    public string? FromSession { get; init; }

    /// <summary>kind == "peer" / "observer": task id of the in-process background subagent that sent it.</summary>
    [JsonPropertyName("senderTaskId")]
    public string? SenderTaskId { get; init; }

    /// <summary>kind == "peer": decoded message body with the peer envelope stripped.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>kind == "peer": kernel-verified pid of the connecting process, when verifiable.</summary>
    [JsonPropertyName("verifiedPeerPid")]
    public long? VerifiedPeerPid { get; init; }

    /// <summary>kind == "task-notification": "scheduled-trigger" or "peer-send-message", when applicable.</summary>
    [JsonPropertyName("subkind")]
    public string? Subkind { get; init; }

    /// <summary>
    ///     Additional undocumented keys passed through from the CLI.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    /// <summary>
    ///     True when the message was submitted by the application (kind "human").
    /// </summary>
    [JsonIgnore]
    public bool IsHuman => Kind == "human";
}
