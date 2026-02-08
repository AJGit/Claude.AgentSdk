using Claude.AgentSdk.Metrics;

namespace Claude.AgentSdk.Tests.Metrics;

public class MetricsEventTests
{
    [Fact]
    public void MetricsEvent_HasRequiredProperties()
    {
        var evt = new MetricsEvent
        {
            SessionId = "sess_123",
            InputTokens = 100,
            OutputTokens = 50,
            CacheReadInputTokens = 25,
            CacheCreationInputTokens = 10,
            CostUsd = 0.05,
            DurationMs = 1500,
            ApiDurationMs = 1200,
            IsError = false,
            NumTurns = 3
        };

        Assert.Equal("sess_123", evt.SessionId);
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
    public void MetricsEvent_Timestamp_DefaultsToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = new MetricsEvent
        {
            SessionId = "sess_123"
        };
        var after = DateTimeOffset.UtcNow;

        Assert.True(evt.Timestamp >= before);
        Assert.True(evt.Timestamp <= after);
    }

    [Fact]
    public void MetricsEvent_NullableCostAndCacheTokens()
    {
        var evt = new MetricsEvent
        {
            SessionId = "sess_123",
            InputTokens = 100,
            OutputTokens = 50,
            CostUsd = null,
            CacheReadInputTokens = null,
            CacheCreationInputTokens = null
        };

        Assert.Null(evt.CostUsd);
        Assert.Null(evt.CacheReadInputTokens);
        Assert.Null(evt.CacheCreationInputTokens);
    }
}
