using Microsoft.CodeAnalysis;

namespace Claude.AgentSdk.Analyzers;

/// <summary>
///     Diagnostic descriptors for Claude Agent SDK analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    /// <summary>
    ///     Category for all Claude Agent SDK diagnostics.
    /// </summary>
    public const string Category = "Claude.AgentSdk";

    /// <summary>
    ///     CLAUDE001: Task tool should not be included in subagent tools.
    ///     Subagents cannot spawn their own subagents.
    /// </summary>
    public static readonly DiagnosticDescriptor TaskToolInSubagent = new(
        id: "CLAUDE001",
        title: "Task tool in subagent tools",
        messageFormat: "Subagents cannot spawn their own subagents. Remove 'Task' from the tools list.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The Task tool allows spawning subagents, but subagents themselves cannot spawn further subagents. Including Task in a subagent's tools list will have no effect.",
        helpLinkUri: "https://github.com/ajgit/Claude.AgentSdk/docs/analyzers/CLAUDE001.md");

    /// <summary>
    ///     CLAUDE002: AllowedTools contains duplicate entries.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateAllowedTools = new(
        id: "CLAUDE002",
        title: "Duplicate tools in AllowedTools",
        messageFormat: "AllowedTools contains duplicate entry: '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Specifying the same tool multiple times in AllowedTools has no effect and may indicate a copy-paste error.");

    /// <summary>
    ///     CLAUDE003: Tool is in both AllowedTools and DisallowedTools.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingToolPermissions = new(
        id: "CLAUDE003",
        title: "Tool in both AllowedTools and DisallowedTools",
        messageFormat: "Tool '{0}' is in both AllowedTools and DisallowedTools. DisallowedTools takes precedence.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a tool appears in both AllowedTools and DisallowedTools, it will be disallowed. This may not be the intended behavior.");

    /// <summary>
    ///     CLAUDE004: MaxTurns set to 0 or negative.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMaxTurns = new(
        id: "CLAUDE004",
        title: "Invalid MaxTurns value",
        messageFormat: "MaxTurns must be a positive integer. Value '{0}' will cause the agent to exit immediately.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Setting MaxTurns to 0 or a negative value will cause the agent to exit without processing any prompts.");

    /// <summary>
    ///     CLAUDE005: MaxBudgetUsd set to 0 or negative.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMaxBudget = new(
        id: "CLAUDE005",
        title: "Invalid MaxBudgetUsd value",
        messageFormat: "MaxBudgetUsd must be positive. Value '{0}' will prevent any API calls.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Setting MaxBudgetUsd to 0 or a negative value will prevent the agent from making any API calls.");

    /// <summary>
    ///     CLAUDE006: DangerouslySkipPermissions is enabled.
    /// </summary>
    public static readonly DiagnosticDescriptor DangerousPermissionSkip = new(
        id: "CLAUDE006",
        title: "DangerouslySkipPermissions is enabled",
        messageFormat: "DangerouslySkipPermissions bypasses all safety checks. Use with extreme caution in production.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Enabling DangerouslySkipPermissions allows the agent to execute any tool without user approval, including file writes and bash commands. This should only be used in controlled environments.");

    /// <summary>
    ///     CLAUDE007: MCP server name contains invalid characters.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMcpServerName = new(
        id: "CLAUDE007",
        title: "Invalid MCP server name",
        messageFormat: "MCP server name '{0}' contains invalid characters. Use alphanumeric characters, hyphens, and underscores only.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "MCP server names are used in tool identifiers (mcp__server__tool) and must be valid identifiers.");

    /// <summary>
    ///     CLAUDE008: Empty system prompt.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptySystemPrompt = new(
        id: "CLAUDE008",
        title: "Empty system prompt",
        messageFormat: "System prompt is empty or whitespace. This may not be the intended behavior.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "An empty system prompt means the agent will use default behavior without custom instructions.");

    /// <summary>
    ///     CLAUDE009: Using deprecated API.
    /// </summary>
    public static readonly DiagnosticDescriptor DeprecatedApi = new(
        id: "CLAUDE009",
        title: "Using deprecated API",
        messageFormat: "'{0}' is deprecated: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "This API is deprecated and may be removed in a future version.");

    /// <summary>
    ///     CLAUDE010: Result not checked.
    /// </summary>
    public static readonly DiagnosticDescriptor ResultNotChecked = new(
        id: "CLAUDE010",
        title: "Result not checked",
        messageFormat: "Return value of type Result<T> is not checked. This may hide errors.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using the functional Result type, you should check whether the operation succeeded or failed.");

    /// <summary>
    ///     CLAUDE011: Option value accessed without check.
    /// </summary>
    public static readonly DiagnosticDescriptor OptionValueUnchecked = new(
        id: "CLAUDE011",
        title: "Option.Value accessed without checking IsSome",
        messageFormat: "Option.Value accessed without checking IsSome. This may throw InvalidOperationException.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Accessing Option.Value when IsSome is false throws InvalidOperationException. Use Match, GetValueOrDefault, or check IsSome first.");
}
