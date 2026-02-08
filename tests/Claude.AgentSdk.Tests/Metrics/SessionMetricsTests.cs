using Claude.AgentSdk.Metrics;

namespace Claude.AgentSdk.Tests.Metrics;

/// <summary>
///     Tests for session metrics callback integration.
/// </summary>
/// <remarks>
///     Full integration tests with MockTransport require complex control protocol setup.
///     These tests verify the MetricsEvent record and basic callback contract.
///     The actual callback wiring is integration-tested via existing session tests.
/// </remarks>
public class SessionMetricsTests
{
    [Fact]
    public void MetricsEvent_CanBeCreatedWithAllFields()
    {
        var evt = new MetricsEvent
        {
            SessionId = "sess_123456789012345678901",
            InputTokens = 100,
            OutputTokens = 50,
            CacheReadInputTokens = 25,
            CacheCreationInputTokens = 10,
            CostUsd = 0.05,
            DurationMs = 1500,
            ApiDurationMs = 1200,
            IsError = false,
            NumTurns = 3,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.Equal("sess_123456789012345678901", evt.SessionId);
        Assert.Equal(100, evt.InputTokens);
        Assert.Equal(50, evt.OutputTokens);
        Assert.Equal(25, evt.CacheReadInputTokens);
        Assert.Equal(10, evt.CacheCreationInputTokens);
        Assert.Equal(0.05, evt.CostUsd);
        Assert.Equal(1500, evt.DurationMs);
        Assert.Equal(1200, evt.ApiDurationMs);
        Assert.False(evt.IsError);
        Assert.Equal(3, evt.NumTurns);
    }

    [Fact]
    public void MetricsEvent_WithError_SetsIsErrorFlag()
    {
        var evt = new MetricsEvent
        {
            SessionId = "sess_test",
            IsError = true,
            DurationMs = 500,
            ApiDurationMs = 400,
            NumTurns = 1
        };

        Assert.True(evt.IsError);
    }

    [Fact]
    public void MetricsEvent_NullableFields_CanBeNull()
    {
        var evt = new MetricsEvent
        {
            SessionId = "sess_test",
            CostUsd = null,
            CacheReadInputTokens = null,
            CacheCreationInputTokens = null
        };

        Assert.Null(evt.CostUsd);
        Assert.Null(evt.CacheReadInputTokens);
        Assert.Null(evt.CacheCreationInputTokens);
    }

    [Fact]
    public void MetricsEvent_Timestamp_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = new MetricsEvent
        {
            SessionId = "sess_test"
        };
        var after = DateTimeOffset.UtcNow;

        Assert.True(evt.Timestamp >= before);
        Assert.True(evt.Timestamp <= after);
    }

    [Fact]
    public void MetricsEvent_Timestamp_CanBeExplicitlySet()
    {
        var specificTime = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var evt = new MetricsEvent
        {
            SessionId = "sess_test",
            Timestamp = specificTime
        };

        Assert.Equal(specificTime, evt.Timestamp);
    }

    [Fact]
    public void MetricsCallback_CanBeDefinedAsLambda()
    {
        // Verify the callback signature is correct
        Func<MetricsEvent, ValueTask>? callback = async evt =>
        {
            // Simulate saving to database
            await Task.CompletedTask;
            Assert.NotNull(evt.SessionId);
        };

        Assert.NotNull(callback);
    }

    [Fact]
    public async Task MetricsCallback_CanReturnCompletedValueTask()
    {
        var eventReceived = false;
        Func<MetricsEvent, ValueTask> callback = evt =>
        {
            eventReceived = true;
            Assert.Equal("sess_test", evt.SessionId);
            return ValueTask.CompletedTask;
        };

        var evt = new MetricsEvent
        {
            SessionId = "sess_test"
        };

        await callback(evt);
        Assert.True(eventReceived);
    }

    [Fact]
    public async Task MetricsCallback_CanPerformAsyncWork()
    {
        var events = new List<MetricsEvent>();
        Func<MetricsEvent, ValueTask> callback = async evt =>
        {
            await Task.Delay(10); // Simulate async DB write
            lock (events)
            {
                events.Add(evt);
            }
        };

        var evt = new MetricsEvent
        {
            SessionId = "sess_test",
            InputTokens = 100,
            CostUsd = 0.01
        };

        await callback(evt);

        Assert.Single(events);
        Assert.Equal(100, events[0].InputTokens);
        Assert.Equal(0.01, events[0].CostUsd);
    }
}
