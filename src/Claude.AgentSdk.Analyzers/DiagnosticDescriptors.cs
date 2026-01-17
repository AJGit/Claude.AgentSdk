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
        "CLAUDE001",
        "Task tool in subagent tools",
        "Subagents cannot spawn their own subagents. Remove 'Task' from the tools list.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "The Task tool allows spawning subagents, but subagents themselves cannot spawn further subagents. Including Task in a subagent's tools list will have no effect.",
        "https://github.com/ajgit/Claude.AgentSdk/docs/analyzers/CLAUDE001.md");

    /// <summary>
    ///     CLAUDE002: AllowedTools contains duplicate entries.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateAllowedTools = new(
        "CLAUDE002",
        "Duplicate tools in AllowedTools",
        "AllowedTools contains duplicate entry: '{0}'",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Specifying the same tool multiple times in AllowedTools has no effect and may indicate a copy-paste error.");

    /// <summary>
    ///     CLAUDE003: Tool is in both AllowedTools and DisallowedTools.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingToolPermissions = new(
        "CLAUDE003",
        "Tool in both AllowedTools and DisallowedTools",
        "Tool '{0}' is in both AllowedTools and DisallowedTools. DisallowedTools takes precedence.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "When a tool appears in both AllowedTools and DisallowedTools, it will be disallowed. This may not be the intended behavior.");

    /// <summary>
    ///     CLAUDE004: MaxTurns set to 0 or negative.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMaxTurns = new(
        "CLAUDE004",
        "Invalid MaxTurns value",
        "MaxTurns must be a positive integer. Value '{0}' will cause the agent to exit immediately.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Setting MaxTurns to 0 or a negative value will cause the agent to exit without processing any prompts.");

    /// <summary>
    ///     CLAUDE005: MaxBudgetUsd set to 0 or negative.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMaxBudget = new(
        "CLAUDE005",
        "Invalid MaxBudgetUsd value",
        "MaxBudgetUsd must be positive. Value '{0}' will prevent any API calls.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Setting MaxBudgetUsd to 0 or a negative value will prevent the agent from making any API calls.");

    /// <summary>
    ///     CLAUDE006: DangerouslySkipPermissions is enabled.
    /// </summary>
    public static readonly DiagnosticDescriptor DangerousPermissionSkip = new(
        "CLAUDE006",
        "DangerouslySkipPermissions is enabled",
        "DangerouslySkipPermissions bypasses all safety checks. Use with extreme caution in production.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Enabling DangerouslySkipPermissions allows the agent to execute any tool without user approval, including file writes and bash commands. This should only be used in controlled environments.");

    /// <summary>
    ///     CLAUDE007: MCP server name contains invalid characters.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMcpServerName = new(
        "CLAUDE007",
        "Invalid MCP server name",
        "MCP server name '{0}' contains invalid characters. Use alphanumeric characters, hyphens, and underscores only.",
        Category,
        DiagnosticSeverity.Error,
        true,
        "MCP server names are used in tool identifiers (mcp__server__tool) and must be valid identifiers.");

    /// <summary>
    ///     CLAUDE008: Empty system prompt.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptySystemPrompt = new(
        "CLAUDE008",
        "Empty system prompt",
        "System prompt is empty or whitespace. This may not be the intended behavior.",
        Category,
        DiagnosticSeverity.Info,
        true,
        "An empty system prompt means the agent will use default behavior without custom instructions.");

    /// <summary>
    ///     CLAUDE009: Using deprecated API.
    /// </summary>
    public static readonly DiagnosticDescriptor DeprecatedApi = new(
        "CLAUDE009",
        "Using deprecated API",
        "'{0}' is deprecated: {1}",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "This API is deprecated and may be removed in a future version.");

    /// <summary>
    ///     CLAUDE010: Result not checked.
    /// </summary>
    public static readonly DiagnosticDescriptor ResultNotChecked = new(
        "CLAUDE010",
        "Result not checked",
        "Return value of type Result<T> is not checked. This may hide errors.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "When using the functional Result type, you should check whether the operation succeeded or failed.");

    /// <summary>
    ///     CLAUDE011: Option value accessed without check.
    /// </summary>
    public static readonly DiagnosticDescriptor OptionValueUnchecked = new(
        "CLAUDE011",
        "Option.Value accessed without checking IsSome",
        "Option.Value accessed without checking IsSome. This may throw InvalidOperationException.",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Accessing Option.Value when IsSome is false throws InvalidOperationException. Use Match, GetValueOrDefault, or check IsSome first.");
}
