using System.Diagnostics;
using System.Diagnostics.Metrics;
using Claude.AgentSdk;
using Claude.AgentSdk.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     OpenTelemetry instrumentation for Claude Agent SDK.
///     Provides metrics and activity tracing for agent operations.
/// </summary>
/// <remarks>
///     <para>
///     This class exposes standardized telemetry for Claude agent operations,
///     compatible with any OpenTelemetry-compatible backend (Prometheus, Jaeger, etc.).
///     </para>
///     <para>
///     Metrics exposed:
///     - claude_agent_sessions_total: Total sessions created
///     - claude_agent_messages_total: Total messages by type
///     - claude_agent_tool_executions_total: Total tool executions by tool name
///     - claude_agent_session_duration_seconds: Session duration histogram
///     - claude_agent_tokens_total: Token usage (input/output)
///     - claude_agent_errors_total: Error count by type
///     </para>
///     <para>
///     Activity sources:
///     - Claude.AgentSdk: Root activity source for all agent operations
///     </para>
/// </remarks>
public sealed class ClaudeAgentTelemetry : IDisposable
{
    /// <summary>
    ///     The meter name for Claude Agent SDK metrics.
    /// </summary>
    public const string MeterName = "Claude.AgentSdk";

    /// <summary>
    ///     The activity source name for Claude Agent SDK tracing.
    /// </summary>
    public const string ActivitySourceName = "Claude.AgentSdk";

    /// <summary>
    ///     The singleton instance.
    /// </summary>
    public static ClaudeAgentTelemetry Instance { get; } = new();

    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;

    // Counters
    private readonly Counter<long> _sessionsTotal;
    private readonly Counter<long> _messagesTotal;
    private readonly Counter<long> _toolExecutionsTotal;
    private readonly Counter<long> _errorsTotal;
    private readonly Counter<long> _tokensTotal;

    // Histograms
    private readonly Histogram<double> _sessionDuration;
    private readonly Histogram<double> _toolExecutionDuration;

    private ClaudeAgentTelemetry()
    {
        _meter = new Meter(MeterName, "1.0.0");
        _activitySource = new ActivitySource(ActivitySourceName, "1.0.0");

        // Initialize counters
        _sessionsTotal = _meter.CreateCounter<long>(
            "claude_agent_sessions_total",
            description: "Total number of Claude agent sessions created");

        _messagesTotal = _meter.CreateCounter<long>(
            "claude_agent_messages_total",
            description: "Total number of messages by type");

        _toolExecutionsTotal = _meter.CreateCounter<long>(
            "claude_agent_tool_executions_total",
            description: "Total number of tool executions by tool name");

        _errorsTotal = _meter.CreateCounter<long>(
            "claude_agent_errors_total",
            description: "Total number of errors by type");

        _tokensTotal = _meter.CreateCounter<long>(
            "claude_agent_tokens_total",
            description: "Total token usage (input/output)");

        // Initialize histograms
        _sessionDuration = _meter.CreateHistogram<double>(
            "claude_agent_session_duration_seconds",
            unit: "s",
            description: "Duration of Claude agent sessions in seconds");

        _toolExecutionDuration = _meter.CreateHistogram<double>(
            "claude_agent_tool_execution_duration_seconds",
            unit: "s",
            description: "Duration of tool executions in seconds");
    }

    /// <summary>
    ///     Records a session creation.
    /// </summary>
    public void RecordSessionCreated(string? model = null)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(model))
            tags.Add("model", model);

        _sessionsTotal.Add(1, tags);
    }

    /// <summary>
    ///     Records a message received.
    /// </summary>
    public void RecordMessage(Message message)
    {
        var messageType = message switch
        {
            AssistantMessage => "assistant",
            UserMessage => "user",
            SystemMessage => "system",
            ResultMessage => "result",
            _ => "unknown"
        };

        _messagesTotal.Add(1, new TagList { { "type", messageType } });

        // Record model used for assistant messages
        if (message is AssistantMessage { MessageContent: { } content })
        {
            // Model is available in content.Model if needed for tagging
            // Token usage is tracked separately via result messages if needed
        }
    }

    /// <summary>
    ///     Records a tool execution.
    /// </summary>
    public void RecordToolExecution(string toolName, TimeSpan duration, bool success = true)
    {
        var tags = new TagList
        {
            { "tool", toolName },
            { "success", success.ToString().ToLowerInvariant() }
        };

        _toolExecutionsTotal.Add(1, tags);
        _toolExecutionDuration.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    ///     Records an error.
    /// </summary>
    public void RecordError(string errorType, string? message = null)
    {
        var tags = new TagList { { "type", errorType } };
        _errorsTotal.Add(1, tags);
    }

    /// <summary>
    ///     Records session duration.
    /// </summary>
    public void RecordSessionDuration(TimeSpan duration, string? model = null)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(model))
            tags.Add("model", model);

        _sessionDuration.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    ///     Starts a new activity for a session.
    /// </summary>
    public Activity? StartSessionActivity(string? sessionId = null)
    {
        var activity = _activitySource.StartActivity("claude.session", ActivityKind.Client);
        if (activity is not null && !string.IsNullOrEmpty(sessionId))
        {
            activity.SetTag("session.id", sessionId);
        }
        return activity;
    }

    /// <summary>
    ///     Starts a new activity for a tool execution.
    /// </summary>
    public Activity? StartToolActivity(string toolName, string? toolUseId = null)
    {
        var activity = _activitySource.StartActivity($"claude.tool.{toolName}", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("tool.name", toolName);
            if (!string.IsNullOrEmpty(toolUseId))
                activity.SetTag("tool.use_id", toolUseId);
        }
        return activity;
    }

    /// <summary>
    ///     Starts a new activity for sending a prompt.
    /// </summary>
    public Activity? StartPromptActivity(string? promptPreview = null)
    {
        var activity = _activitySource.StartActivity("claude.prompt", ActivityKind.Client);
        if (activity is not null && !string.IsNullOrEmpty(promptPreview))
        {
            // Only include first 100 chars to avoid huge traces
            var preview = promptPreview.Length > 100
                ? promptPreview[..100] + "..."
                : promptPreview;
            activity.SetTag("prompt.preview", preview);
        }
        return activity;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
        _activitySource.Dispose();
    }
}

/// <summary>
///     Extension methods for adding telemetry to Claude agent.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    ///     Adds telemetry instrumentation to the Claude agent builder.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IClaudeAgentBuilder AddTelemetry(this IClaudeAgentBuilder builder)
    {
        // Register telemetry as singleton
        builder.Services.AddSingleton(ClaudeAgentTelemetry.Instance);
        return builder;
    }

    /// <summary>
    ///     Gets the meter name for configuring OpenTelemetry.
    /// </summary>
    public static string GetMeterName() => ClaudeAgentTelemetry.MeterName;

    /// <summary>
    ///     Gets the activity source name for configuring OpenTelemetry.
    /// </summary>
    public static string GetActivitySourceName() => ClaudeAgentTelemetry.ActivitySourceName;
}

/// <summary>
///     Helper for timing operations and recording metrics.
/// </summary>
public readonly struct TelemetryScope : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly Action<TimeSpan> _onComplete;

    /// <summary>
    ///     Creates a new telemetry scope.
    /// </summary>
    public TelemetryScope(Action<TimeSpan> onComplete)
    {
        _onComplete = onComplete;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopwatch.Stop();
        _onComplete(_stopwatch.Elapsed);
    }

    /// <summary>
    ///     Creates a scope for timing session duration.
    /// </summary>
    public static TelemetryScope Session(string? model = null) =>
        new(duration => ClaudeAgentTelemetry.Instance.RecordSessionDuration(duration, model));

    /// <summary>
    ///     Creates a scope for timing tool execution.
    /// </summary>
    public static TelemetryScope Tool(string toolName) =>
        new(duration => ClaudeAgentTelemetry.Instance.RecordToolExecution(toolName, duration));
}
