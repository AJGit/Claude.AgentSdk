using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions;

/// <summary>
///     Extension methods for working with messages from the Claude CLI.
/// </summary>
/// <remarks>
///     <para>
///     These extensions provide convenient methods for extracting content from messages,
///     reducing boilerplate when processing conversation responses.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     await foreach (var message in session.StreamAsync(prompt))
///     {
///         if (message is AssistantMessage assistant)
///         {
///             var text = assistant.GetText();
///             var tools = assistant.GetToolUses();
///
///             Console.WriteLine(text);
///             foreach (var tool in tools)
///             {
///                 Console.WriteLine($"Tool: {tool.Name}");
///             }
///         }
///     }
///     </code>
///     </para>
/// </remarks>
public static class MessageExtensions
{
    /// <summary>
    ///     Gets all text content from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>The concatenated text from all text blocks, separated by newlines.</returns>
    public static string GetText(this AssistantMessage message)
    {
        if (message?.MessageContent?.Content is null)
            return string.Empty;

        var textBlocks = message.MessageContent.Content
            .OfType<TextBlock>()
            .Select(t => t.Text);

        return string.Join("\n", textBlocks);
    }

    /// <summary>
    ///     Gets all tool use blocks from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>An enumerable of tool use blocks.</returns>
    public static IEnumerable<ToolUseBlock> GetToolUses(this AssistantMessage message)
    {
        if (message?.MessageContent?.Content is null)
            return Enumerable.Empty<ToolUseBlock>();

        return message.MessageContent.Content.OfType<ToolUseBlock>();
    }

    /// <summary>
    ///     Checks if the assistant message contains a tool use with the specified name.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <param name="toolName">The tool name to check for.</param>
    /// <returns>True if the message contains a tool use with the specified name.</returns>
    public static bool HasToolUse(this AssistantMessage message, string toolName)
    {
        return message.GetToolUses().Any(t => t.Name == toolName);
    }

    /// <summary>
    ///     Checks if the assistant message contains a tool use with the specified strongly-typed name.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <param name="toolName">The tool name to check for.</param>
    /// <returns>True if the message contains a tool use with the specified name.</returns>
    public static bool HasToolUse(this AssistantMessage message, ToolName toolName)
    {
        return message.HasToolUse(toolName.Value);
    }

    /// <summary>
    ///     Gets the first tool use block with the specified name.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <param name="toolName">The tool name to find.</param>
    /// <returns>The tool use block, or null if not found.</returns>
    public static ToolUseBlock? GetToolUse(this AssistantMessage message, string toolName)
    {
        return message.GetToolUses().FirstOrDefault(t => t.Name == toolName);
    }

    /// <summary>
    ///     Gets the first tool use block with the specified strongly-typed name.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <param name="toolName">The tool name to find.</param>
    /// <returns>The tool use block, or null if not found.</returns>
    public static ToolUseBlock? GetToolUse(this AssistantMessage message, ToolName toolName)
    {
        return message.GetToolUse(toolName.Value);
    }

    /// <summary>
    ///     Gets all thinking blocks from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>An enumerable of thinking blocks.</returns>
    public static IEnumerable<ThinkingBlock> GetThinking(this AssistantMessage message)
    {
        if (message?.MessageContent?.Content is null)
            return Enumerable.Empty<ThinkingBlock>();

        return message.MessageContent.Content.OfType<ThinkingBlock>();
    }

    /// <summary>
    ///     Gets all thinking text from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>The concatenated thinking text from all thinking blocks.</returns>
    public static string GetThinkingText(this AssistantMessage message)
    {
        var thinkingBlocks = message.GetThinking().Select(t => t.Thinking);
        return string.Join("\n", thinkingBlocks);
    }

    /// <summary>
    ///     Checks if the assistant message contains any thinking blocks.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>True if the message contains thinking blocks.</returns>
    public static bool HasThinking(this AssistantMessage message)
    {
        return message.GetThinking().Any();
    }

    /// <summary>
    ///     Gets all text blocks from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>An enumerable of text blocks.</returns>
    public static IEnumerable<TextBlock> GetTextBlocks(this AssistantMessage message)
    {
        if (message?.MessageContent?.Content is null)
            return Enumerable.Empty<TextBlock>();

        return message.MessageContent.Content.OfType<TextBlock>();
    }

    /// <summary>
    ///     Checks if the assistant message contains an error.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>True if the message contains an error.</returns>
    public static bool HasError(this AssistantMessage message)
    {
        return !string.IsNullOrEmpty(message?.MessageContent?.Error);
    }

    /// <summary>
    ///     Gets the error message from an assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>The error message, or null if no error.</returns>
    public static string? GetError(this AssistantMessage message)
    {
        return message?.MessageContent?.Error;
    }

    /// <summary>
    ///     Gets the model that generated the assistant message.
    /// </summary>
    /// <param name="message">The assistant message.</param>
    /// <returns>The model identifier string.</returns>
    public static string GetModel(this AssistantMessage message)
    {
        return message?.MessageContent?.Model ?? string.Empty;
    }

    /// <summary>
    ///     Checks if the result message indicates success.
    /// </summary>
    /// <param name="message">The result message.</param>
    /// <returns>True if the result indicates success.</returns>
    public static bool IsSuccess(this ResultMessage message)
    {
        return message?.Subtype == "success" && !message.IsError;
    }

    /// <summary>
    ///     Checks if the system message is an initialization message.
    /// </summary>
    /// <param name="message">The system message.</param>
    /// <returns>True if this is an init message.</returns>
    public static bool IsInitMessage(this SystemMessage message)
    {
        return message?.Subtype == "init";
    }

    /// <summary>
    ///     Gets the available tools from an init system message.
    /// </summary>
    /// <param name="message">The system message.</param>
    /// <returns>The list of tool names, or empty if not an init message.</returns>
    public static IReadOnlyList<string> GetTools(this SystemMessage message)
    {
        if (!message.IsInitMessage() || message.Tools is null)
            return Array.Empty<string>();

        return message.Tools;
    }

    /// <summary>
    ///     Gets the MCP servers from an init system message.
    /// </summary>
    /// <param name="message">The system message.</param>
    /// <returns>The list of MCP server statuses, or empty if not an init message.</returns>
    public static IReadOnlyList<McpServerStatus> GetMcpServers(this SystemMessage message)
    {
        if (!message.IsInitMessage() || message.McpServers is null)
            return Array.Empty<McpServerStatus>();

        return message.McpServers;
    }
}
