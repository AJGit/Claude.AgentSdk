using Claude.AgentSdk.Messages;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk;

/// <summary>
///     Static API for executing queries to Claude - an alias for <see cref="Query" />.
/// </summary>
/// <remarks>
///     <para>
///         This class provides the same functionality as <see cref="Query" /> with a more
///         descriptive name. Use whichever name you prefer - both are equivalent.
///     </para>
///     <para>
///         For interactive sessions or multiple related queries, use
///         <see cref="ClaudeAgentClient" /> instead.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Simple one-shot query
/// await foreach (var message in ClaudeAgent.RunAsync("What is 2 + 2?"))
/// {
///     // Process messages
/// }
/// 
/// // Get just the text response
/// var response = await ClaudeAgent.GetTextAsync("Explain quantum computing");
/// Console.WriteLine(response);
/// </code>
/// </example>
public static class ClaudeAgent
{
    /// <summary>
    ///     Execute a one-shot query and stream the responses.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of messages from Claude.</returns>
    public static IAsyncEnumerable<Message> RunAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        return Query.RunAsync(prompt, options, loggerFactory, cancellationToken);
    }

    /// <summary>
    ///     Execute a one-shot query and wait for the final result.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result message, or null if the query failed.</returns>
    public static Task<ResultMessage?> RunToCompletionAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        return Query.RunToCompletionAsync(prompt, options, loggerFactory, cancellationToken);
    }

    /// <summary>
    ///     Execute a one-shot query and get the text response.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration for this query.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The concatenated text from all assistant messages.</returns>
    public static Task<string> GetTextAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        return Query.GetTextAsync(prompt, options, loggerFactory, cancellationToken);
    }
}
