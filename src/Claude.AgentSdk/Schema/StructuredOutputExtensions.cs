using System.Text;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Schema;

/// <summary>
///     Extension methods for working with structured outputs.
/// </summary>
public static class StructuredOutputExtensions
{
    /// <summary>
    ///     Creates a ClaudeAgentOptions with a typed structured output schema.
    /// </summary>
    /// <typeparam name="TOutput">The type to use as the output schema.</typeparam>
    /// <param name="options">The base options to extend.</param>
    /// <param name="schemaName">The name for the schema (defaults to type name in snake_case).</param>
    /// <returns>A new options instance with the OutputFormat set.</returns>
    public static ClaudeAgentOptions WithStructuredOutput<TOutput>(
        this ClaudeAgentOptions options,
        string? schemaName = null)
    {
        var name = schemaName ?? ToSnakeCase(typeof(TOutput).Name);
        var schema = SchemaGenerator.Generate<TOutput>(name);

        return options with { OutputFormat = schema };
    }

    /// <summary>
    ///     Parses a message's structured output as the specified type.
    ///     Structured output comes via a ToolUseBlock named "StructuredOutput".
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="message">The assistant message containing the structured output tool use.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized object, or null if not found or parsing fails.</returns>
    public static T? ParseStructuredOutput<T>(
        this AssistantMessage message,
        JsonSerializerOptions? options = null)
    {
        // Structured output comes via a tool use block named "StructuredOutput"
        var toolUse = message.MessageContent.Content
            .OfType<ToolUseBlock>()
            .FirstOrDefault(t => t.Name == "StructuredOutput");

        if (toolUse is not null)
        {
            options ??= new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            try
            {
                return JsonSerializer.Deserialize<T>(toolUse.Input.GetRawText(), options);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        // Fallback: try parsing from text content (for backward compatibility)
        var textContent = message.MessageContent.Content
            .OfType<TextBlock>()
            .FirstOrDefault();

        if (textContent is null)
        {
            return default;
        }

        options ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        try
        {
            return JsonSerializer.Deserialize<T>(textContent.Text, options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Parses a ResultMessage's structured output as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="message">The result message with structured output.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized object, or null if not present or parsing fails.</returns>
    public static T? ParseStructuredOutput<T>(
        this ResultMessage message,
        JsonSerializerOptions? options = null)
    {
        if (!message.StructuredOutput.HasValue)
        {
            return default;
        }

        options ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        try
        {
            return JsonSerializer.Deserialize<T>(
                message.StructuredOutput.Value.GetRawText(),
                options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new StringBuilder();
        result.Append(char.ToLowerInvariant(name[0]));

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
