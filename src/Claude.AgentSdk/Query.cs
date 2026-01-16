using System.Runtime.CompilerServices;
using Claude.AgentSdk.Messages;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk;

/// <summary>
///     Static API for executing one-shot queries to Claude without managing client instances.
/// </summary>
/// <remarks>
///     <para>
///         Use this class for simple, fire-and-forget queries where you don't need session management
///         or multiple queries. For interactive sessions or multiple related queries, use
///         <see cref="ClaudeAgentClient" /> instead.
///     </para>
///     <para>
///         Each call to RunAsync creates an independent connection to the Claude CLI,
///         executes the query, and cleans up automatically.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Simple one-shot query
/// await foreach (var message in Query.RunAsync("What is 2 + 2?"))
/// {
///     if (message is AssistantMessage assistant)
///     {
///         foreach (var block in assistant.MessageContent.Content)
///         {
///             if (block is TextBlock text)
///             {
///                 Console.Write(text.Text);
///             }
///         }
///     }
/// }
///
/// // With options
/// var options = new ClaudeAgentOptions
/// {
///     Model = "claude-sonnet-4-20250514",
///     MaxTurns = 1
/// };
/// await foreach (var message in Query.RunAsync("Hello!", options))
/// {
///     // Process messages
/// }
/// </code>
/// </example>
public static class Query
{
    /// <summary>
    ///     Execute a one-shot query and stream the responses.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of messages from Claude.</returns>
    /// <example>
    ///     <code>
    /// await foreach (var message in Query.RunAsync("What is the weather?"))
    /// {
    ///     Console.WriteLine(message);
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<Message> RunAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var client = new ClaudeAgentClient(options, loggerFactory);

        await foreach (var message in client.QueryAsync(prompt, null, cancellationToken).ConfigureAwait(false))
        {
            yield return message;
        }
    }

    /// <summary>
    ///     Execute a one-shot query and wait for the final result.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result message, or null if the query failed.</returns>
    /// <example>
    ///     <code>
    /// var result = await Query.RunToCompletionAsync("Calculate 2 + 2");
    /// Console.WriteLine($"Session: {result?.SessionId}, Cost: ${result?.TotalCostUsd}");
    /// </code>
    /// </example>
    public static async Task<ResultMessage?> RunToCompletionAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        await using var client = new ClaudeAgentClient(options, loggerFactory);
        return await client.QueryToCompletionAsync(prompt, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Execute a one-shot query and get the text response.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The concatenated text from all assistant messages.</returns>
    /// <example>
    ///     <code>
    /// var response = await Query.GetTextAsync("What is the meaning of life?");
    /// Console.WriteLine(response);
    /// </code>
    /// </example>
    public static async Task<string> GetTextAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var textParts = new List<string>();

        await foreach (var message in RunAsync(prompt, options, loggerFactory, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.MessageContent.Content)
                {
                    if (block is TextBlock text)
                    {
                        textParts.Add(text.Text);
                    }
                }
            }
        }

        return string.Join("", textParts);
    }

    #region Convenience Overloads

    /// <summary>
    ///     Execute a one-shot query with common options specified inline.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="model">The model to use (e.g., "claude-sonnet-4-20250514").</param>
    /// <param name="maxTurns">Maximum number of conversation turns.</param>
    /// <param name="systemPrompt">Custom system prompt.</param>
    /// <param name="permissionMode">Permission mode for tool execution.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of messages from Claude.</returns>
    /// <example>
    ///     <code>
    /// await foreach (var message in Query.RunAsync(
    ///     "Write a haiku",
    ///     model: "claude-sonnet-4-20250514",
    ///     maxTurns: 1))
    /// {
    ///     // Process messages
    /// }
    /// </code>
    /// </example>
    public static IAsyncEnumerable<Message> RunAsync(
        string prompt,
        string? model = null,
        int? maxTurns = null,
        string? systemPrompt = null,
        PermissionMode? permissionMode = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var options = new ClaudeAgentOptions
        {
            Model = model,
            MaxTurns = maxTurns,
            SystemPrompt = systemPrompt is not null ? new CustomSystemPrompt(systemPrompt) : null,
            PermissionMode = permissionMode
        };

        return RunAsync(prompt, options, loggerFactory, cancellationToken);
    }

    /// <summary>
    ///     Execute a one-shot query with a specific model.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The concatenated text from all assistant messages.</returns>
    /// <example>
    ///     <code>
    /// var response = await Query.GetTextAsync("Explain AI", model: "claude-sonnet-4-20250514");
    /// </code>
    /// </example>
    public static Task<string> GetTextAsync(
        string prompt,
        string model,
        CancellationToken cancellationToken = default)
    {
        var options = new ClaudeAgentOptions { Model = model };
        return GetTextAsync(prompt, options, null, cancellationToken);
    }

    /// <summary>
    ///     Execute a one-shot query with common options specified inline.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="maxTurns">Maximum number of conversation turns.</param>
    /// <param name="systemPrompt">Custom system prompt.</param>
    /// <param name="permissionMode">Permission mode for tool execution.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The concatenated text from all assistant messages.</returns>
    public static Task<string> GetTextAsync(
        string prompt,
        string? model = null,
        int? maxTurns = null,
        string? systemPrompt = null,
        PermissionMode? permissionMode = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        var options = new ClaudeAgentOptions
        {
            Model = model,
            MaxTurns = maxTurns,
            SystemPrompt = systemPrompt is not null ? new CustomSystemPrompt(systemPrompt) : null,
            PermissionMode = permissionMode
        };

        return GetTextAsync(prompt, options, loggerFactory, cancellationToken);
    }

    #endregion
}
