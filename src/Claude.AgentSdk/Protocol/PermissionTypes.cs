using Claude.AgentSdk.Types;

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
    ///     Permission suggestions from the CLI. They can be echoed back via
    ///     <see cref="PermissionResultAllow.UpdatedPermissions" />.
    /// </summary>
    public IReadOnlyList<PermissionUpdate>? Suggestions { get; init; }

    /// <summary>
    ///     The file path that triggered the request, if applicable (e.g. a Bash command touching a path outside
    ///     the allowed directories).
    /// </summary>
    public string? BlockedPath { get; init; }

    /// <summary>
    ///     Unique identifier of this tool call. Distinguishes parallel permission requests from the same
    ///     assistant message.
    /// </summary>
    public string? ToolUseId { get; init; }

    /// <summary>
    ///     When running inside a subagent, the subagent's id.
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    ///     Why this permission request was triggered (e.g. the reason a PreToolUse hook returned "ask").
    /// </summary>
    public string? DecisionReason { get; init; }

    /// <summary>
    ///     Full permission prompt sentence (e.g. "Claude wants to read foo.txt"). Prefer this as the prompt text.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     Short noun phrase for the tool action (e.g. "Read file"), suitable for button labels.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    ///     Human-readable subtitle for the permission UI.
    /// </summary>
    public string? Description { get; init; }
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
    ///     Updated input to use instead of the original. When null, the original input is used.
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

    /// <summary>
    ///     Converts this update to the control protocol wire format (camelCase, matching the TypeScript SDK).
    /// </summary>
    public Dictionary<string, object?> ToWireFormat()
    {
        var result = new Dictionary<string, object?> { ["type"] = ToCamelCase(Type.ToString()) };

        if (Destination is { } destination)
        {
            result["destination"] = ToCamelCase(destination.ToString());
        }

        switch (Type)
        {
            case PermissionUpdateType.AddRules or PermissionUpdateType.ReplaceRules or PermissionUpdateType.RemoveRules:
                if (Rules is not null)
                {
                    result["rules"] = Rules.Select(rule => new Dictionary<string, object?>
                    {
                        ["toolName"] = rule.ToolName,
                        ["ruleContent"] = rule.RuleContent
                    }).ToList();
                }

                if (Behavior is { } behavior)
                {
                    result["behavior"] = ToCamelCase(behavior.ToString());
                }

                break;

            case PermissionUpdateType.SetMode:
                if (Mode is { } mode)
                {
                    result["mode"] = mode.ToJsonString();
                }

                break;

            case PermissionUpdateType.AddDirectories or PermissionUpdateType.RemoveDirectories:
                if (Directories is not null)
                {
                    result["directories"] = Directories;
                }

                break;
        }

        return result;
    }

    /// <summary>
    ///     Parses a permission update from the control protocol wire format (inverse of <see cref="ToWireFormat" />).
    /// </summary>
    /// <returns>The parsed update, or null if the type is not recognized.</returns>
    public static PermissionUpdate? FromWireFormat(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetString(element, "type", out var typeString) ||
            !Enum.TryParse<PermissionUpdateType>(typeString, true, out var type))
        {
            return null;
        }

        List<PermissionRuleValue>? rules = null;
        if (element.TryGetProperty("rules", out var rulesElement) && rulesElement.ValueKind == JsonValueKind.Array)
        {
            rules = [];
            foreach (var rule in rulesElement.EnumerateArray())
            {
                if (TryGetString(rule, "toolName", out var toolName))
                {
                    rules.Add(new PermissionRuleValue
                    {
                        ToolName = toolName,
                        RuleContent = TryGetString(rule, "ruleContent", out var content) ? content : null
                    });
                }
            }
        }

        List<string>? directories = null;
        if (element.TryGetProperty("directories", out var dirs) && dirs.ValueKind == JsonValueKind.Array)
        {
            directories = dirs.EnumerateArray()
                .Where(d => d.ValueKind == JsonValueKind.String)
                .Select(d => d.GetString()!)
                .ToList();
        }

        return new PermissionUpdate
        {
            Type = type,
            Rules = rules,
            Behavior = TryGetString(element, "behavior", out var behavior) &&
                       Enum.TryParse<PermissionBehavior>(behavior, true, out var parsedBehavior)
                ? parsedBehavior
                : null,
            Mode = TryGetString(element, "mode", out var mode) &&
                   EnumStringMappings.TryParsePermissionMode(mode, out var parsedMode)
                ? parsedMode
                : null,
            Directories = directories,
            Destination = TryGetString(element, "destination", out var destination) &&
                          Enum.TryParse<PermissionUpdateDestination>(destination, true, out var parsedDestination)
                ? parsedDestination
                : null
        };
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString()!;
            return true;
        }

        value = "";
        return false;
    }

    private static string ToCamelCase(string pascal)
    {
        return pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }
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
