using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Tests.Builders;

/// <summary>
///     Tests for the HookConfigurationBuilder fluent builder.
/// </summary>
[UnitTest]
public class HookConfigurationBuilderTests
{
    #region Helper Methods

    private static HookCallback CreateMockHandler() =>
        (input, toolUseId, context, ct) => Task.FromResult<HookOutput>(
            new SyncHookOutput { Continue = true });

    #endregion

    #region PreToolUse Hook Tests

    [Fact]
    public void OnPreToolUse_AddsPreToolUseHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.PreToolUse));
        Assert.Single(hooks[HookEvent.PreToolUse]);
        Assert.Contains(handler, hooks[HookEvent.PreToolUse][0].Hooks);
    }

    [Fact]
    public void OnPreToolUse_WithMatcher_SetsMatcher()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(handler, matcher: "Bash|Write")
            .Build();

        // Assert
        Assert.Equal("Bash|Write", hooks[HookEvent.PreToolUse][0].Matcher);
    }

    [Fact]
    public void OnPreToolUse_WithTimeout_SetsTimeout()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(handler, timeout: 30.0)
            .Build();

        // Assert
        Assert.Equal(30.0, hooks[HookEvent.PreToolUse][0].Timeout);
    }

    [Fact]
    public void OnPreToolUse_WithMultipleHandlers_SetsHandlers()
    {
        // Arrange
        var handler1 = CreateMockHandler();
        var handler2 = CreateMockHandler();
        var handlers = new List<HookCallback> { handler1, handler2 };

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(handlers, matcher: "Bash")
            .Build();

        // Assert
        Assert.Equal(2, hooks[HookEvent.PreToolUse][0].Hooks.Count);
    }

    #endregion

    #region PostToolUse Hook Tests

    [Fact]
    public void OnPostToolUse_AddsPostToolUseHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPostToolUse(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.PostToolUse));
        Assert.Single(hooks[HookEvent.PostToolUse]);
    }

    [Fact]
    public void OnPostToolUse_WithMatcher_SetsMatcher()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPostToolUse(handler, matcher: "Edit")
            .Build();

        // Assert
        Assert.Equal("Edit", hooks[HookEvent.PostToolUse][0].Matcher);
    }

    [Fact]
    public void OnPostToolUseFailure_AddsPostToolUseFailureHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPostToolUseFailure(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.PostToolUseFailure));
    }

    #endregion

    #region Other Hook Event Tests

    [Fact]
    public void OnUserPromptSubmit_AddsUserPromptSubmitHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnUserPromptSubmit(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.UserPromptSubmit));
    }

    [Fact]
    public void OnStop_AddsStopHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnStop(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.Stop));
    }

    [Fact]
    public void OnSubagentStart_AddsSubagentStartHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnSubagentStart(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.SubagentStart));
    }

    [Fact]
    public void OnSubagentStop_AddsSubagentStopHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnSubagentStop(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.SubagentStop));
    }

    [Fact]
    public void OnPreCompact_AddsPreCompactHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreCompact(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.PreCompact));
    }

    [Fact]
    public void OnPermissionRequest_AddsPermissionRequestHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPermissionRequest(handler, matcher: "Bash")
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.PermissionRequest));
        Assert.Equal("Bash", hooks[HookEvent.PermissionRequest][0].Matcher);
    }

    [Fact]
    public void OnSessionStart_AddsSessionStartHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnSessionStart(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.SessionStart));
    }

    [Fact]
    public void OnSessionEnd_AddsSessionEndHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnSessionEnd(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.SessionEnd));
    }

    [Fact]
    public void OnNotification_AddsNotificationHook()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnNotification(handler)
            .Build();

        // Assert
        Assert.True(hooks.ContainsKey(HookEvent.Notification));
    }

    #endregion

    #region AddHook Tests

    [Fact]
    public void AddHook_AddsCustomHookMatcher()
    {
        // Arrange
        var handler = CreateMockHandler();
        var matcher = new HookMatcher
        {
            Matcher = "CustomTool",
            Hooks = new[] { handler },
            Timeout = 60.0
        };

        // Act
        var hooks = new HookConfigurationBuilder()
            .AddHook(HookEvent.PreToolUse, matcher)
            .Build();

        // Assert
        Assert.Equal("CustomTool", hooks[HookEvent.PreToolUse][0].Matcher);
        Assert.Equal(60.0, hooks[HookEvent.PreToolUse][0].Timeout);
    }

    #endregion

    #region Multiple Hooks Tests

    [Fact]
    public void MultipleHooksForSameEvent_AccumulatesHooks()
    {
        // Arrange
        var handler1 = CreateMockHandler();
        var handler2 = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(handler1, matcher: "Bash")
            .OnPreToolUse(handler2, matcher: "Write")
            .Build();

        // Assert
        Assert.Equal(2, hooks[HookEvent.PreToolUse].Count);
        Assert.Equal("Bash", hooks[HookEvent.PreToolUse][0].Matcher);
        Assert.Equal("Write", hooks[HookEvent.PreToolUse][1].Matcher);
    }

    [Fact]
    public void MultipleEventTypes_BuildsCorrectDictionary()
    {
        // Arrange
        var preHandler = CreateMockHandler();
        var postHandler = CreateMockHandler();
        var stopHandler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnPreToolUse(preHandler)
            .OnPostToolUse(postHandler)
            .OnStop(stopHandler)
            .Build();

        // Assert
        Assert.Equal(3, hooks.Count);
        Assert.True(hooks.ContainsKey(HookEvent.PreToolUse));
        Assert.True(hooks.ContainsKey(HookEvent.PostToolUse));
        Assert.True(hooks.ContainsKey(HookEvent.Stop));
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithNoHooks_ReturnsEmptyDictionary()
    {
        // Act
        var hooks = new HookConfigurationBuilder().Build();

        // Assert
        Assert.Empty(hooks);
    }

    [Fact]
    public void Build_ReturnsReadOnlyDictionary()
    {
        // Arrange
        var handler = CreateMockHandler();

        // Act
        var hooks = new HookConfigurationBuilder()
            .OnStop(handler)
            .Build();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>>(hooks);
    }

    #endregion
}
