using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions;

/// <summary>
///     Extension methods for working with content blocks in messages.
/// </summary>
/// <remarks>
///     <para>
///         These extensions provide type-checking and type-conversion helpers
///         for content blocks, making it easier to work with polymorphic content.
///     </para>
///     <para>
///         Example usage:
///         <code>
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
    /// <param name="block">The content block.</param>
    extension(ContentBlock block)
    {
        /// <summary>
        ///     Checks if the content block is a text block.
        /// </summary>
        /// <returns>True if this is a text block.</returns>
        public bool IsText()
        {
            return block is TextBlock;
        }

        /// <summary>
        ///     Checks if the content block is a tool use block.
        /// </summary>
        /// <returns>True if this is a tool use block.</returns>
        public bool IsToolUse()
        {
            return block is ToolUseBlock;
        }

        /// <summary>
        ///     Checks if the content block is a tool result block.
        /// </summary>
        /// <returns>True if this is a tool result block.</returns>
        public bool IsToolResult()
        {
            return block is ToolResultBlock;
        }

        /// <summary>
        ///     Checks if the content block is a thinking block.
        /// </summary>
        /// <returns>True if this is a thinking block.</returns>
        public bool IsThinking()
        {
            return block is ThinkingBlock;
        }

        /// <summary>
        ///     Gets the text content from a text block.
        /// </summary>
        /// <returns>The text content, or null if not a text block.</returns>
        public string? AsText()
        {
            return (block as TextBlock)?.Text;
        }

        /// <summary>
        ///     Casts the content block to a tool use block.
        /// </summary>
        /// <returns>The tool use block, or null if not a tool use block.</returns>
        public ToolUseBlock? AsToolUse()
        {
            return block as ToolUseBlock;
        }

        /// <summary>
        ///     Casts the content block to a tool result block.
        /// </summary>
        /// <returns>The tool result block, or null if not a tool result block.</returns>
        public ToolResultBlock? AsToolResult()
        {
            return block as ToolResultBlock;
        }

        /// <summary>
        ///     Casts the content block to a thinking block.
        /// </summary>
        /// <returns>The thinking block, or null if not a thinking block.</returns>
        public ThinkingBlock? AsThinking()
        {
            return block as ThinkingBlock;
        }

        /// <summary>
        ///     Gets the thinking content from a thinking block.
        /// </summary>
        /// <returns>The thinking content, or null if not a thinking block.</returns>
        public string? GetThinkingContent()
        {
            return (block as ThinkingBlock)?.Thinking;
        }
    }

    /// <param name="toolUse">The tool use block.</param>
    extension(ToolUseBlock toolUse)
    {
        /// <summary>
        ///     Deserializes the tool input from a tool use block.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <returns>The deserialized input, or default if deserialization fails.</returns>
        public T? GetInput<T>() where T : class
        {
            try
            {
                return toolUse.Input.Deserialize<T>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Checks if the tool use block is for the specified tool.
        /// </summary>
        /// <param name="toolName">The tool name to check.</param>
        /// <returns>True if this is a tool use for the specified tool.</returns>
        public bool IsTool(string toolName)
        {
            return string.Equals(toolUse.Name, toolName, StringComparison.Ordinal);
        }

        /// <summary>
        ///     Checks if the tool use block is for the specified tool using a strongly-typed name.
        /// </summary>
        /// <param name="toolName">The tool name to check.</param>
        /// <returns>True if this is a tool use for the specified tool.</returns>
        public bool IsTool(ToolName toolName)
        {
            return toolUse.IsTool(toolName.Value);
        }

        /// <summary>
        ///     Checks if the tool use block is for an MCP server tool.
        /// </summary>
        /// <returns>True if this is an MCP server tool.</returns>
        public bool IsMcpTool()
        {
            return toolUse.Name?.StartsWith("mcp__", StringComparison.Ordinal) == true;
        }

        /// <summary>
        ///     Gets the MCP server name from an MCP tool use block.
        /// </summary>
        /// <returns>The MCP server name, or null if not an MCP tool.</returns>
        public string? GetMcpServerName()
        {
            if (!toolUse.IsMcpTool() || toolUse is { Name: null })
            {
                return null;
            }

            var parts = toolUse.Name.Split(["__"], StringSplitOptions.None);
            return parts.Length >= 2 ? parts[1] : null;
        }

        /// <summary>
        ///     Gets the tool name from an MCP tool use block (without the mcp__ prefix).
        /// </summary>
        /// <returns>The tool name portion, or the full name if not an MCP tool.</returns>
        public string? GetMcpToolName()
        {
            if (!toolUse.IsMcpTool() || toolUse is { Name: null })
            {
                return toolUse.Name;
            }

            var parts = toolUse.Name.Split(["__"], StringSplitOptions.None);
            return parts.Length >= 3 ? parts[2] : toolUse.Name;
        }
    }

    /// <param name="toolResult">The tool result block.</param>
    extension(ToolResultBlock toolResult)
    {
        /// <summary>
        ///     Checks if the tool result indicates an error.
        /// </summary>
        /// <returns>True if this is an error result.</returns>
        public bool IsError()
        {
            return toolResult.IsError == true;
        }

        /// <summary>
        ///     Gets the content as a string from a tool result block.
        /// </summary>
        /// <returns>The content as a string, or empty if no content.</returns>
        public string GetContentAsString()
        {
            if (toolResult.Content is null)
            {
                return string.Empty;
            }

            return toolResult.Content.Value.ToString();
        }
    }
}
