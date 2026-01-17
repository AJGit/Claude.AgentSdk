using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions;

/// <summary>
///     Extension methods for working with messages from the Claude CLI.
/// </summary>
/// <remarks>
///     <para>
///         These extensions provide convenient methods for extracting content from messages,
///         reducing boilerplate when processing conversation responses.
///     </para>
///     <para>
///         Example usage:
///         <code>
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
    /// <param name="message">The assistant message.</param>
    extension(AssistantMessage message)
    {
        /// <summary>
        ///     Gets all text content from an assistant message.
        /// </summary>
        /// <returns>The concatenated text from all text blocks, separated by newlines.</returns>
        public string GetText()
        {
            if (message?.MessageContent.Content is null)
            {
                return string.Empty;
            }

            var textBlocks = message.MessageContent.Content
                .OfType<TextBlock>()
                .Select(t => t.Text);

            return string.Join("\n", textBlocks);
        }

        /// <summary>
        ///     Gets all tool use blocks from an assistant message.
        /// </summary>
        /// <returns>An enumerable of tool use blocks.</returns>
        public IEnumerable<ToolUseBlock> GetToolUses()
        {
            if (message?.MessageContent?.Content is null)
            {
                return [];
            }

            return message.MessageContent.Content.OfType<ToolUseBlock>();
        }

        /// <summary>
        ///     Checks if the assistant message contains a tool use with the specified name.
        /// </summary>
        /// <param name="toolName">The tool name to check for.</param>
        /// <returns>True if the message contains a tool use with the specified name.</returns>
        public bool HasToolUse(string toolName)
        {
            return message.GetToolUses().Any(t => t.Name == toolName);
        }

        /// <summary>
        ///     Checks if the assistant message contains a tool use with the specified strongly-typed name.
        /// </summary>
        /// <param name="toolName">The tool name to check for.</param>
        /// <returns>True if the message contains a tool use with the specified name.</returns>
        public bool HasToolUse(ToolName toolName)
        {
            return message.HasToolUse(toolName.Value);
        }

        /// <summary>
        ///     Gets the first tool use block with the specified name.
        /// </summary>
        /// <param name="toolName">The tool name to find.</param>
        /// <returns>The tool use block, or null if not found.</returns>
        public ToolUseBlock? GetToolUse(string toolName)
        {
            return message.GetToolUses().FirstOrDefault(t => t.Name == toolName);
        }

        /// <summary>
        ///     Gets the first tool use block with the specified strongly-typed name.
        /// </summary>
        /// <param name="toolName">The tool name to find.</param>
        /// <returns>The tool use block, or null if not found.</returns>
        public ToolUseBlock? GetToolUse(ToolName toolName)
        {
            return message.GetToolUse(toolName.Value);
        }

        /// <summary>
        ///     Gets all thinking blocks from an assistant message.
        /// </summary>
        /// <returns>An enumerable of thinking blocks.</returns>
        public IEnumerable<ThinkingBlock> GetThinking()
        {
            if (message?.MessageContent?.Content is null)
            {
                return [];
            }

            return message.MessageContent.Content.OfType<ThinkingBlock>();
        }

        /// <summary>
        ///     Gets all thinking text from an assistant message.
        /// </summary>
        /// <returns>The concatenated thinking text from all thinking blocks.</returns>
        public string GetThinkingText()
        {
            var thinkingBlocks = message.GetThinking().Select(t => t.Thinking);
            return string.Join("\n", thinkingBlocks);
        }

        /// <summary>
        ///     Checks if the assistant message contains any thinking blocks.
        /// </summary>
        /// <returns>True if the message contains thinking blocks.</returns>
        public bool HasThinking()
        {
            return message.GetThinking().Any();
        }

        /// <summary>
        ///     Gets all text blocks from an assistant message.
        /// </summary>
        /// <returns>An enumerable of text blocks.</returns>
        public IEnumerable<TextBlock> GetTextBlocks()
        {
            if (message?.MessageContent?.Content is null)
            {
                return [];
            }

            return message.MessageContent.Content.OfType<TextBlock>();
        }

        /// <summary>
        ///     Checks if the assistant message contains an error.
        /// </summary>
        /// <returns>True if the message contains an error.</returns>
        public bool HasError()
        {
            return !string.IsNullOrEmpty(message?.MessageContent?.Error);
        }

        /// <summary>
        ///     Gets the error message from an assistant message.
        /// </summary>
        /// <returns>The error message, or null if no error.</returns>
        public string? GetError()
        {
            return message?.MessageContent?.Error;
        }

        /// <summary>
        ///     Gets the model that generated the assistant message.
        /// </summary>
        /// <returns>The model identifier string.</returns>
        public string GetModel()
        {
            return message?.MessageContent?.Model ?? string.Empty;
        }
    }

    /// <summary>
    ///     Checks if the result message indicates success.
    /// </summary>
    /// <param name="message">The result message.</param>
    /// <returns>True if the result indicates success.</returns>
    public static bool IsSuccess(this ResultMessage message)
    {
        return message is { Subtype: "success", IsError: false };
    }

    /// <param name="message">The system message.</param>
    extension(SystemMessage message)
    {
        /// <summary>
        ///     Checks if the system message is an initialization message.
        /// </summary>
        /// <returns>True if this is an init message.</returns>
        public bool IsInitMessage()
        {
            return message?.Subtype == "init";
        }

        /// <summary>
        ///     Gets the available tools from an init system message.
        /// </summary>
        /// <returns>The list of tool names, or empty if not an init message.</returns>
        public IReadOnlyList<string> GetTools()
        {
            if (!message.IsInitMessage() || message.Tools is null)
            {
                return [];
            }

            return message.Tools;
        }

        /// <summary>
        ///     Gets the MCP servers from an init system message.
        /// </summary>
        /// <returns>The list of MCP server statuses, or empty if not an init message.</returns>
        public IReadOnlyList<McpServerStatus> GetMcpServers()
        {
            if (!message.IsInitMessage() || message.McpServers is null)
            {
                return [];
            }

            return message.McpServers;
        }
    }
}
