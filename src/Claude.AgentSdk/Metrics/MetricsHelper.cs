using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Metrics;

/// <summary>
///     Helper methods for creating and publishing metrics events.
/// </summary>
public static class MetricsHelper
{
    /// <summary>
    ///     Creates a MetricsEvent from a ResultMessage.
    /// </summary>
    /// <param name="result">The result message containing metrics data.</param>
    /// <param name="sessionIdOverride">Optional session ID override. If null, uses result.SessionId or generates one.</param>
    /// <param name="displayName">Optional display name for the session.</param>
    /// <param name="model">Optional model identifier used for this turn.</param>
    /// <returns>A new MetricsEvent populated from the result.</returns>
    public static MetricsEvent FromResultMessage(
        ResultMessage result,
        string? sessionIdOverride = null,
        string? displayName = null,
        string? model = null)
    {
        return new MetricsEvent
        {
            SessionId = sessionIdOverride ?? result.SessionId ?? $"query-{Guid.NewGuid():N}"[..16],
            InputTokens = result.Usage?.InputTokens ?? 0,
            OutputTokens = result.Usage?.OutputTokens ?? 0,
            CacheReadInputTokens = result.Usage?.CacheReadInputTokens,
            CacheCreationInputTokens = result.Usage?.CacheCreationInputTokens,
            CostUsd = result.TotalCostUsd,
            DurationMs = result.DurationMs,
            ApiDurationMs = result.DurationApiMs,
            IsError = result.IsError,
            NumTurns = result.NumTurns,
            DisplayName = displayName,
            Model = model
        };
    }

    /// <summary>
    ///     Invokes a metrics callback in a fire-and-forget manner without blocking.
    /// </summary>
    /// <param name="evt">The metrics event to publish.</param>
    /// <param name="callback">The callback to invoke.</param>
    /// <remarks>
    ///     Exceptions from the callback are swallowed to prevent affecting message processing.
    /// </remarks>
    public static void FireAndForget(MetricsEvent evt, Func<MetricsEvent, ValueTask> callback)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await callback(evt).ConfigureAwait(false);
            }
            catch
            {
                // Don't let callback errors affect message processing
            }
        });
    }
}
