using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Builders;

/// <summary>
///     Fluent builder for configuring hook collections.
/// </summary>
/// <remarks>
///     <para>
///         This builder provides a more ergonomic way to configure hooks
///         compared to manually constructing dictionaries.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     var hooks = new HookConfigurationBuilder()
///         .OnPreToolUse(myHandler, matcher: "Bash|Write")
///         .OnPostToolUse(logHandler)
///         .OnStop(cleanupHandler)
///         .Build();
///     </code>
///     </para>
/// </remarks>
public sealed class HookConfigurationBuilder
{
    private readonly Dictionary<HookEvent, List<HookMatcher>> _hooks = new();

    /// <summary>
    ///     Adds a hook matcher for an event.
    /// </summary>
    /// <param name="hookEvent">The hook event type.</param>
    /// <param name="matcher">The hook matcher.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder AddHook(HookEvent hookEvent, HookMatcher matcher)
    {
        if (!_hooks.TryGetValue(hookEvent, out var matchers))
        {
            matchers = [];
            _hooks[hookEvent] = matchers;
        }

        matchers.Add(matcher);
        return this;
    }

    /// <summary>
    ///     Adds a PreToolUse hook that fires before a tool executes.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match (e.g., "Bash|Write").</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPreToolUse(
        HookCallback handler,
        string? matcher = null,
        double? timeout = null)
    {
        return AddHook(HookEvent.PreToolUse, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds multiple handlers for PreToolUse events.
    /// </summary>
    /// <param name="handlers">The hook callback handlers.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPreToolUse(
        IReadOnlyList<HookCallback> handlers,
        string? matcher = null,
        double? timeout = null)
    {
        return AddHook(HookEvent.PreToolUse, new HookMatcher
        {
            Matcher = matcher,
            Hooks = handlers,
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a PostToolUse hook that fires after a tool executes successfully.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPostToolUse(
        HookCallback handler,
        string? matcher = null,
        double? timeout = null)
    {
        return AddHook(HookEvent.PostToolUse, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a PostToolUseFailure hook that fires when a tool execution fails.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPostToolUseFailure(
        HookCallback handler,
        string? matcher = null,
        double? timeout = null)
    {
        return AddHook(HookEvent.PostToolUseFailure, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a UserPromptSubmit hook that fires when a user prompt is submitted.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnUserPromptSubmit(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.UserPromptSubmit, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a Stop hook that fires when agent execution stops.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnStop(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.Stop, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a SubagentStart hook that fires when a subagent starts.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnSubagentStart(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.SubagentStart, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a SubagentStop hook that fires when a subagent completes.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnSubagentStop(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.SubagentStop, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a PreCompact hook that fires before conversation compaction.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPreCompact(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.PreCompact, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a PermissionRequest hook that fires when a permission dialog would be displayed.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnPermissionRequest(
        HookCallback handler,
        string? matcher = null,
        double? timeout = null)
    {
        return AddHook(HookEvent.PermissionRequest, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a SessionStart hook that fires when a session starts.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnSessionStart(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.SessionStart, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a SessionEnd hook that fires when a session ends.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnSessionEnd(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.SessionEnd, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a Notification hook that fires for agent status notifications.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public HookConfigurationBuilder OnNotification(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.Notification, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Builds the hook configuration dictionary.
    /// </summary>
    /// <returns>A dictionary of hook matchers keyed by event type.</returns>
    public IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>> Build()
    {
        return _hooks.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<HookMatcher>)kvp.Value);
    }
}
