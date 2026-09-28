using Claude.AgentSdk.Attributes;

namespace Claude.AgentSdk.Messages;

/// <summary>
///     Base class for all content blocks in messages.
/// </summary>
/// <remarks>
///     Use the generated Match extension methods for functional pattern matching:
///     <code>
///     var text = contentBlock.Match(
///         textBlock: t => t.Text,
///         thinkingBlock: t => t.Thinking,
///         toolUseBlock: t => $"Tool: {t.Name}",
///         toolResultBlock: t => t.Content?.ToString() ?? "",
///         serverToolUseBlock: s => $"Server tool: {s.Name}",
///         serverToolResultBlock: r => r.Content.ToString()
///     );
///     </code>
/// </remarks>
[GenerateMatch]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
[JsonDerivedType(typeof(ServerToolUseBlock), "server_tool_use")]
[JsonDerivedType(typeof(ServerToolResultBlock), "advisor_tool_result")]
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

/// <summary>
///     Server-side tool use block (e.g. advisor, web_search, web_fetch, code_execution).
/// </summary>
/// <remarks>
///     These tools are executed by the API on the model's behalf, so they appear alongside regular tool_use
///     blocks but the caller never returns a result. Branch on <see cref="Name" /> to know which tool ran.
/// </remarks>
public sealed record ServerToolUseBlock : ContentBlock
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>
    ///     The server tool name: advisor, web_search, web_fetch, code_execution, bash_code_execution,
    ///     text_editor_code_execution, tool_search_tool_regex, tool_search_tool_bm25, ...
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("input")] public required JsonElement Input { get; init; }
}

/// <summary>
///     Result block returned for a server-side tool call (currently <c>advisor_tool_result</c>).
/// </summary>
public sealed record ServerToolResultBlock : ContentBlock
{
    [JsonPropertyName("tool_use_id")] public required string ToolUseId { get; init; }

    /// <summary>
    ///     The raw result object from the API; inspect <c>Content.GetProperty("type")</c> for its schema.
    /// </summary>
    [JsonPropertyName("content")]
    public required JsonElement Content { get; init; }
}

/// <summary>
///     Deserializes a content block array, skipping block types this SDK version does not recognize so a new
///     block type never drops the whole message.
/// </summary>
internal sealed class ContentBlockListConverter : JsonConverter<IReadOnlyList<ContentBlock>>
{
    private static readonly HashSet<string> _knownTypes =
    [
        "text", "thinking", "tool_use", "tool_result", "server_tool_use", "advisor_tool_result"
    ];

    public override IReadOnlyList<ContentBlock> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected content block array, got {reader.TokenType}");
        }

        var blocks = new List<ContentBlock>();
        using var document = JsonDocument.ParseValue(ref reader);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Invalid content block (expected object, got {element.ValueKind})");
            }

            if (!element.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                !_knownTypes.Contains(type.GetString()!))
            {
                continue;
            }

            if (element.Deserialize<ContentBlock>(options) is { } block)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<ContentBlock> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var block in value)
        {
            JsonSerializer.Serialize(writer, block, options);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
///     Reads <c>origin</c> leniently: anything that is not an object with a string <c>kind</c> is treated as absent.
/// </summary>
internal sealed class LenientMessageOriginConverter : JsonConverter<MessageOrigin?>
{
    public override bool HandleNull => true;

    public override MessageOrigin? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("kind", out var kind) ||
            kind.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return element.Deserialize<MessageOrigin>();
        }
        catch (JsonException)
        {
            // A known field with an unexpected shape: keep the kind and pass everything through raw.
            return new MessageOrigin
            {
                Kind = kind.GetString()!,
                AdditionalProperties = element.EnumerateObject()
                    .Where(p => p.Name != "kind")
                    .ToDictionary(p => p.Name, p => p.Value.Clone())
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, MessageOrigin? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value);
    }
}
