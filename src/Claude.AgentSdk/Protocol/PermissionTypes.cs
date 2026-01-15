namespace Claude.AgentSdk.Protocol;

/// <summary>
///     Context for tool permission requests.
/// </summary>
public sealed record ToolPermissionRequest
{
    /// <summary>
    ///     Name of the tool requesting permission.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    ///     Input arguments for the tool.
    /// </summary>
    public required JsonElement Input { get; init; }

    /// <summary>
    ///     Permission suggestions from the CLI.
    /// </summary>
    public IReadOnlyList<PermissionUpdate>? Suggestions { get; init; }

    /// <summary>
    ///     Path that was blocked, if applicable.
    /// </summary>
    public string? BlockedPath { get; init; }
}

/// <summary>
///     Base class for permission results.
/// </summary>
public abstract record PermissionResult;

/// <summary>
///     Allow the tool to execute.
/// </summary>
public sealed record PermissionResultAllow : PermissionResult
{
    /// <summary>
    ///     Updated input to use instead of the original.
    /// </summary>
    public JsonElement? UpdatedInput { get; init; }

    /// <summary>
    ///     Permission updates to apply.
    /// </summary>
    public IReadOnlyList<PermissionUpdate>? UpdatedPermissions { get; init; }
}

/// <summary>
///     Deny the tool execution.
/// </summary>
public sealed record PermissionResultDeny : PermissionResult
{
    /// <summary>
    ///     Message explaining why the tool was denied.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    ///     Whether to interrupt the agent.
    /// </summary>
    public bool Interrupt { get; init; }
}

/// <summary>
///     Permission update configuration.
/// </summary>
public sealed record PermissionUpdate
{
    public required PermissionUpdateType Type { get; init; }
    public IReadOnlyList<PermissionRuleValue>? Rules { get; init; }
    public PermissionBehavior? Behavior { get; init; }
    public PermissionMode? Mode { get; init; }
    public IReadOnlyList<string>? Directories { get; init; }
    public PermissionUpdateDestination? Destination { get; init; }
}

public enum PermissionUpdateType
{
    AddRules,
    ReplaceRules,
    RemoveRules,
    SetMode,
    AddDirectories,
    RemoveDirectories
}

public enum PermissionBehavior
{
    Allow,
    Deny,
    Ask
}

public enum PermissionUpdateDestination
{
    UserSettings,
    ProjectSettings,
    LocalSettings,
    Session
}

public sealed record PermissionRuleValue
{
    public required string ToolName { get; init; }
    public string? RuleContent { get; init; }
}
