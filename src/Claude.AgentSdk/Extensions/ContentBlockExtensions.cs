using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions;

/// <summary>
///     Extension methods for working with content blocks in messages.
/// </summary>
/// <remarks>
///     <para>
///     These extensions provide type-checking and type-conversion helpers
///     for content blocks, making it easier to work with polymorphic content.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     foreach (var block in assistantMessage.MessageContent.Content)
///     {
///         if (block.IsText())
///         {
///             Console.WriteLine(block.AsText());
///         }
///         else if (block.IsToolUse())
///         {
///             var tool = block.AsToolUse()!;
///             Console.WriteLine($"Using tool: {tool.Name}");
///         }
///     }
///     </code>
///     </para>
/// </remarks>
public static class ContentBlockExtensions
{
    /// <summary>
    ///     Checks if the content block is a text block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>True if this is a text block.</returns>
    public static bool IsText(this ContentBlock block)
    {
        return block is TextBlock;
    }

    /// <summary>
    ///     Checks if the content block is a tool use block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>True if this is a tool use block.</returns>
    public static bool IsToolUse(this ContentBlock block)
    {
        return block is ToolUseBlock;
    }

    /// <summary>
    ///     Checks if the content block is a tool result block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>True if this is a tool result block.</returns>
    public static bool IsToolResult(this ContentBlock block)
    {
        return block is ToolResultBlock;
    }

    /// <summary>
    ///     Checks if the content block is a thinking block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>True if this is a thinking block.</returns>
    public static bool IsThinking(this ContentBlock block)
    {
        return block is ThinkingBlock;
    }

    /// <summary>
    ///     Gets the text content from a text block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>The text content, or null if not a text block.</returns>
    public static string? AsText(this ContentBlock block)
    {
        return (block as TextBlock)?.Text;
    }

    /// <summary>
    ///     Casts the content block to a tool use block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>The tool use block, or null if not a tool use block.</returns>
    public static ToolUseBlock? AsToolUse(this ContentBlock block)
    {
        return block as ToolUseBlock;
    }

    /// <summary>
    ///     Casts the content block to a tool result block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>The tool result block, or null if not a tool result block.</returns>
    public static ToolResultBlock? AsToolResult(this ContentBlock block)
    {
        return block as ToolResultBlock;
    }

    /// <summary>
    ///     Casts the content block to a thinking block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>The thinking block, or null if not a thinking block.</returns>
    public static ThinkingBlock? AsThinking(this ContentBlock block)
    {
        return block as ThinkingBlock;
    }

    /// <summary>
    ///     Gets the thinking content from a thinking block.
    /// </summary>
    /// <param name="block">The content block.</param>
    /// <returns>The thinking content, or null if not a thinking block.</returns>
    public static string? GetThinkingContent(this ContentBlock block)
    {
        return (block as ThinkingBlock)?.Thinking;
    }

    /// <summary>
    ///     Deserializes the tool input from a tool use block.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="toolUse">The tool use block.</param>
    /// <returns>The deserialized input, or default if deserialization fails.</returns>
    public static T? GetInput<T>(this ToolUseBlock toolUse) where T : class
    {
        try
        {
            return toolUse.Input.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    ///     Checks if the tool use block is for the specified tool.
    /// </summary>
    /// <param name="toolUse">The tool use block.</param>
    /// <param name="toolName">The tool name to check.</param>
    /// <returns>True if this is a tool use for the specified tool.</returns>
    public static bool IsTool(this ToolUseBlock toolUse, string toolName)
    {
        return string.Equals(toolUse.Name, toolName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Checks if the tool use block is for the specified tool using a strongly-typed name.
    /// </summary>
    /// <param name="toolUse">The tool use block.</param>
    /// <param name="toolName">The tool name to check.</param>
    /// <returns>True if this is a tool use for the specified tool.</returns>
    public static bool IsTool(this ToolUseBlock toolUse, ToolName toolName)
    {
        return toolUse.IsTool(toolName.Value);
    }

    /// <summary>
    ///     Checks if the tool use block is for an MCP server tool.
    /// </summary>
    /// <param name="toolUse">The tool use block.</param>
    /// <returns>True if this is an MCP server tool.</returns>
    public static bool IsMcpTool(this ToolUseBlock toolUse)
    {
        return toolUse.Name?.StartsWith("mcp__", StringComparison.Ordinal) == true;
    }

    /// <summary>
    ///     Gets the MCP server name from an MCP tool use block.
    /// </summary>
    /// <param name="toolUse">The tool use block.</param>
    /// <returns>The MCP server name, or null if not an MCP tool.</returns>
    public static string? GetMcpServerName(this ToolUseBlock toolUse)
    {
        if (!toolUse.IsMcpTool() || toolUse.Name is null)
            return null;

        var parts = toolUse.Name.Split(new[] { "__" }, StringSplitOptions.None);
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    ///     Gets the tool name from an MCP tool use block (without the mcp__ prefix).
    /// </summary>
    /// <param name="toolUse">The tool use block.</param>
    /// <returns>The tool name portion, or the full name if not an MCP tool.</returns>
    public static string? GetMcpToolName(this ToolUseBlock toolUse)
    {
        if (!toolUse.IsMcpTool() || toolUse.Name is null)
            return toolUse.Name;

        var parts = toolUse.Name.Split(new[] { "__" }, StringSplitOptions.None);
        return parts.Length >= 3 ? parts[2] : toolUse.Name;
    }

    /// <summary>
    ///     Checks if the tool result indicates an error.
    /// </summary>
    /// <param name="toolResult">The tool result block.</param>
    /// <returns>True if this is an error result.</returns>
    public static bool IsError(this ToolResultBlock toolResult)
    {
        return toolResult.IsError == true;
    }

    /// <summary>
    ///     Gets the content as a string from a tool result block.
    /// </summary>
    /// <param name="toolResult">The tool result block.</param>
    /// <returns>The content as a string, or empty if no content.</returns>
    public static string GetContentAsString(this ToolResultBlock toolResult)
    {
        if (toolResult.Content is null || !toolResult.Content.HasValue)
            return string.Empty;

        return toolResult.Content.Value.ToString() ?? string.Empty;
    }
}
