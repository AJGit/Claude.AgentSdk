namespace Claude.AgentSdk.Messages;

/// <summary>
///     Base class for all content blocks in messages.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
public abstract record ContentBlock;

/// <summary>
///     Text content block.
/// </summary>
public sealed record TextBlock : ContentBlock
{
    [JsonPropertyName("text")] public required string Text { get; init; }
}

/// <summary>
///     Thinking content block (extended thinking mode).
/// </summary>
public sealed record ThinkingBlock : ContentBlock
{
    [JsonPropertyName("thinking")] public required string Thinking { get; init; }

    [JsonPropertyName("signature")] public required string Signature { get; init; }
}

/// <summary>
///     Tool use content block - represents Claude requesting to use a tool.
/// </summary>
public sealed record ToolUseBlock : ContentBlock
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("input")] public required JsonElement Input { get; init; }
}

/// <summary>
///     Tool result content block - the result of a tool execution.
/// </summary>
public sealed record ToolResultBlock : ContentBlock
{
    [JsonPropertyName("tool_use_id")] public required string ToolUseId { get; init; }

    [JsonPropertyName("content")] public JsonElement? Content { get; init; }

    [JsonPropertyName("is_error")] public bool? IsError { get; init; }
}
