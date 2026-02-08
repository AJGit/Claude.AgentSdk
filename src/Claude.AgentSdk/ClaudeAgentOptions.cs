using System.Runtime.Serialization;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Metrics;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Tools;
using Claude.AgentSdk.Types;
using HookEvent = Claude.AgentSdk.Protocol.HookEvent;

namespace Claude.AgentSdk;

/// <summary>
///     Permission modes for tool usage.
/// </summary>
[GenerateEnumStrings]
public enum PermissionMode
{
    /// <summary>Default permission mode.</summary>
    [EnumMember(Value = "default")] Default,

    /// <summary>Auto-accept file edits.</summary>
    [EnumMember(Value = "acceptEdits")] AcceptEdits,

    /// <summary>Plan mode for planning-only sessions.</summary>
    [EnumMember(Value = "plan")] Plan,

    /// <summary>Bypass all permission checks.</summary>
    [EnumMember(Value = "bypassPermissions")]
    BypassPermissions,

    /// <summary>Don't ask for any permissions.</summary>
    [EnumMember(Value = "dontAsk")] DontAsk,

    /// <summary>Delegate permission decisions to a handler.</summary>
    [EnumMember(Value = "delegate")] Delegate
}

/// <summary>
///     Sources for loading settings (e.g., CLAUDE.md files).
/// </summary>
[GenerateEnumStrings]
public enum SettingSource
{
    /// <summary>
    ///     Load project-level settings (CLAUDE.md or .claude/CLAUDE.md in working directory).
    ///     Traverses up parent directories looking for project root.
    /// </summary>
    [EnumMember(Value = "project")] Project,

    /// <summary>
    ///     Load user-level settings (~/.claude/CLAUDE.md).
    /// </summary>
    [EnumMember(Value = "user")] User,

    /// <summary>
    ///     Load settings from the current working directory only (no parent traversal).
    ///     Useful for workspace-specific settings.
    /// </summary>
    [EnumMember(Value = "local")] Local
}

/// <summary>
///     Sandboxing mode for command execution.
/// </summary>
[GenerateEnumStrings]
public enum SandboxMode
{
    /// <summary>
    ///     No sandboxing - commands run with full permissions.
    /// </summary>
    [EnumMember(Value = "off")] Off,

    /// <summary>
    ///     Permissive sandboxing - allows most operations with some restrictions.
    /// </summary>
    [EnumMember(Value = "permissive")] Permissive,

    /// <summary>
    ///     Strict sandboxing - limits file system and network access.
    /// </summary>
    [EnumMember(Value = "strict")] Strict
}

/// <summary>
///     Base class for sandbox configuration.
///     Can be a simple mode or detailed settings.
/// </summary>
/// <remarks>
///     The circular dependency (SandboxConfig -> SandboxSettings -> SandboxConfig) is intentional.
///     This pattern is required for polymorphic JSON serialization with JsonDerivedType attributes.
/// </remarks>
[JsonDerivedType(typeof(SimpleSandboxConfig), "simple")]
[JsonDerivedType(typeof(SandboxSettings), "detailed")]
public abstract record SandboxConfig
{
    /// <summary>
    ///     Creates sandbox config with sandbox disabled.
    /// </summary>
    public static SandboxConfig Off => new SimpleSandboxConfig(SandboxMode.Off);

    /// <summary>
    ///     Creates sandbox config with permissive mode.
    /// </summary>
    public static SandboxConfig Permissive => new SimpleSandboxConfig(SandboxMode.Permissive);

    /// <summary>
    ///     Creates sandbox config with strict mode.
    /// </summary>
    public static SandboxConfig Strict => new SimpleSandboxConfig(SandboxMode.Strict);

    /// <summary>
    ///     Creates a sandbox config from a simple mode.
    /// </summary>
    public static implicit operator SandboxConfig(SandboxMode mode)
    {
        return new SimpleSandboxConfig(mode);
    }

    /// <summary>
    ///     Creates detailed sandbox settings with sandbox enabled.
    /// </summary>
    /// <param name="configure">Function to configure sandbox settings. Returns the configured settings.</param>
    public static SandboxSettings WithSettings(Func<SandboxSettings, SandboxSettings>? configure = null)
    {
        var settings = new SandboxSettings { IsEnabled = true };
        return configure?.Invoke(settings) ?? settings;
    }
}

/// <summary>
///     Simple sandbox configuration using just a mode.
/// </summary>
public sealed record SimpleSandboxConfig(SandboxMode Mode) : SandboxConfig;

/// <summary>
///     Detailed sandbox configuration with network and violation settings.
/// </summary>
/// <remarks>
///     Filesystem and network access restrictions are derived from permission rules,
///     not sandbox settings. Use permission rules for:
///     - Filesystem read restrictions: Read deny rules
///     - Filesystem write restrictions: Edit allow/deny rules
///     - Network restrictions: WebFetch allow/deny rules
/// </remarks>
public sealed record SandboxSettings : SandboxConfig
{
    /// <summary>
    ///     Enable sandbox mode for command execution.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    ///     Auto-approve bash commands when sandbox is enabled.
    ///     When true, bash commands run without permission prompts if sandboxed.
    /// </summary>
    public bool AutoAllowBashIfSandboxed { get; init; }

    /// <summary>
    ///     Commands that always bypass sandbox restrictions (e.g., ["docker"]).
    ///     These run unsandboxed automatically without model involvement.
    /// </summary>
    public IReadOnlyList<string>? ExcludedCommands { get; init; }

    /// <summary>
    ///     Allow the model to request running commands outside the sandbox.
    ///     When true, the model can set dangerouslyDisableSandbox in tool input,
    ///     which falls back to the permissions system (canUseTool callback).
    /// </summary>
    public bool AllowUnsandboxedCommands { get; init; }

    /// <summary>
    ///     Network-specific sandbox configuration.
    /// </summary>
    public NetworkSandboxSettings? Network { get; init; }

    /// <summary>
    ///     Configure which sandbox violations to ignore.
    /// </summary>
    public SandboxIgnoreViolations? IgnoreViolations { get; init; }

    /// <summary>
    ///     Enable a weaker nested sandbox for compatibility with certain environments.
    /// </summary>
    public bool EnableWeakerNestedSandbox { get; init; }

    /// <summary>
    ///     Configuration for ripgrep (rg) command.
    /// </summary>
    public RipgrepConfig? Ripgrep { get; init; }
}

/// <summary>
///     Network-specific configuration for sandbox mode.
/// </summary>
public sealed record NetworkSandboxSettings
{
    /// <summary>
    ///     Allow processes to bind to local ports (e.g., for dev servers).
    /// </summary>
    public bool AllowLocalBinding { get; init; }

    /// <summary>
    ///     Unix socket paths that processes can access (e.g., Docker socket).
    /// </summary>
    /// <example>["/var/run/docker.sock"]</example>
    public IReadOnlyList<string>? AllowUnixSockets { get; init; }

    /// <summary>
    ///     Allow access to all Unix sockets.
    /// </summary>
    public bool AllowAllUnixSockets { get; init; }

    /// <summary>
    ///     HTTP proxy port for network requests.
    /// </summary>
    public int? HttpProxyPort { get; init; }

    /// <summary>
    ///     SOCKS proxy port for network requests.
    /// </summary>
    public int? SocksProxyPort { get; init; }

    /// <summary>
    ///     Allowed network domains.
    /// </summary>
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>
    ///     Only allow managed domains for network access.
    /// </summary>
    public bool AllowManagedDomainsOnly { get; init; }
}

/// <summary>
///     Configuration for ignoring specific sandbox violations.
/// </summary>
public sealed record SandboxIgnoreViolations
{
    /// <summary>
    ///     File path patterns to ignore violations for.
    /// </summary>
    public IReadOnlyList<string>? File { get; init; }

    /// <summary>
    ///     Network patterns to ignore violations for.
    /// </summary>
    public IReadOnlyList<string>? Network { get; init; }
}

/// <summary>
///     Base class for system prompt configuration.
///     Can be a custom string or a preset configuration.
/// </summary>
[JsonDerivedType(typeof(CustomSystemPrompt), "custom")]
[JsonDerivedType(typeof(PresetSystemPrompt), "preset")]
public abstract record SystemPromptConfig
{
    /// <summary>
    ///     Creates a custom system prompt from a string.
    /// </summary>
    public static implicit operator SystemPromptConfig(string prompt)
    {
        return new CustomSystemPrompt(prompt);
    }

    /// <summary>
    ///     Creates a system prompt config from the claude_code preset.
    /// </summary>
    public static SystemPromptConfig ClaudeCode(string? append = null)
    {
        return new PresetSystemPrompt { Preset = "claude_code", Append = append };
    }
}

/// <summary>
///     A custom string system prompt that replaces the default entirely.
/// </summary>
public sealed record CustomSystemPrompt(string Prompt) : SystemPromptConfig;

/// <summary>
///     A preset system prompt configuration (e.g., claude_code).
/// </summary>
public sealed record PresetSystemPrompt : SystemPromptConfig
{
    /// <summary>
    ///     The preset name. Currently only "claude_code" is supported.
    /// </summary>
    public required string Preset { get; init; }

    /// <summary>
    ///     Optional text to append to the preset system prompt.
    /// </summary>
    public string? Append { get; init; }
}

/// <summary>
///     Base class for tools configuration.
///     Can be a list of tool names or a preset configuration.
/// </summary>
[JsonDerivedType(typeof(ToolsList), "list")]
[JsonDerivedType(typeof(ToolsPreset), "preset")]
public abstract record ToolsConfig
{
    /// <summary>
    ///     Creates a tools config from a list of tool names.
    /// </summary>
    public static implicit operator ToolsConfig(string[] tools)
    {
        return new ToolsList(tools);
    }

    /// <summary>
    ///     Creates a tools config from a list of tool names.
    /// </summary>
    public static implicit operator ToolsConfig(List<string> tools)
    {
        return new ToolsList(tools);
    }

    /// <summary>
    ///     Creates a tools config from the claude_code preset.
    /// </summary>
    public static ToolsConfig ClaudeCode()
    {
        return new ToolsPreset { Preset = "claude_code" };
    }
}

/// <summary>
///     A list of specific tool names to enable.
/// </summary>
public sealed record ToolsList(IReadOnlyList<string> Tools) : ToolsConfig;

/// <summary>
///     A preset tools configuration (e.g., claude_code).
/// </summary>
public sealed record ToolsPreset : ToolsConfig
{
    /// <summary>
    ///     The preset name. Currently only "claude_code" is supported.
    /// </summary>
    public required string Preset { get; init; }
}

/// <summary>
///     Options for configuring the Claude Agent.
/// </summary>
public sealed record ClaudeAgentOptions
{
    /// <summary>
    ///     Tools configuration. Can be a list of tool names or a preset.
    /// </summary>
    /// <remarks>
    ///     Use <see cref="ToolsList" /> for specific tools, or <see cref="ToolsPreset" /> for presets.
    ///     A string array is implicitly converted to <see cref="ToolsList" />.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // List of specific tools
    /// Tools = new ToolsList(["Read", "Write", "Bash"])
    /// 
    /// // Using claude_code preset
    /// Tools = ToolsConfig.ClaudeCode()
    /// 
    /// // Implicit array conversion
    /// Tools = new[] { "Read", "Write" }
    /// </code>
    /// </example>
    public ToolsConfig? Tools { get; init; }

    /// <summary>
    ///     Additional tools to allow beyond the default set.
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    ///     Tools to explicitly disallow.
    /// </summary>
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];

    /// <summary>
    ///     System prompt configuration. Can be a custom string, a preset (e.g., "claude_code"),
    ///     or null for default (empty system prompt).
    /// </summary>
    /// <remarks>
    ///     Use <see cref="CustomSystemPrompt" /> for a completely custom prompt, or
    ///     <see cref="PresetSystemPrompt" /> to use the claude_code preset with optional appended text.
    ///     A string value is implicitly converted to <see cref="CustomSystemPrompt" />.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Custom string (implicitly converted)
    /// SystemPrompt = "You are a helpful assistant."
    /// 
    /// // Using claude_code preset
    /// SystemPrompt = SystemPromptConfig.ClaudeCode()
    /// 
    /// // Using claude_code preset with appended instructions
    /// SystemPrompt = SystemPromptConfig.ClaudeCode(append: "Always use TypeScript.")
    /// 
    /// // Explicit preset configuration
    /// SystemPrompt = new PresetSystemPrompt { Preset = "claude_code", Append = "Custom instructions" }
    /// </code>
    /// </example>
    public SystemPromptConfig? SystemPrompt { get; init; }

    /// <summary>
    ///     Sources for loading settings (e.g., CLAUDE.md files).
    ///     Include <see cref="SettingSource.Project" /> to load CLAUDE.md from the project directory.
    ///     Include <see cref="SettingSource.User" /> to load ~/.claude/CLAUDE.md.
    /// </summary>
    /// <remarks>
    ///     IMPORTANT: The claude_code system prompt preset does NOT automatically load CLAUDE.md files.
    ///     You must explicitly specify setting sources to load them.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Load project-level CLAUDE.md
    /// SettingSources = [SettingSource.Project]
    /// 
    /// // Load both project and user-level CLAUDE.md
    /// SettingSources = [SettingSource.Project, SettingSource.User]
    /// </code>
    /// </example>
    public IReadOnlyList<SettingSource>? SettingSources { get; init; }

    /// <summary>
    ///     MCP server configurations keyed by server name.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }

    /// <summary>
    ///     Custom subagent definitions keyed by agent name.
    ///     Subagents are invoked via the Task tool - ensure "Task" is in <see cref="AllowedTools" />.
    /// </summary>
    /// <remarks>
    ///     Claude automatically decides when to invoke subagents based on each agent's Description.
    ///     You can also explicitly request a subagent by name in your prompt.
    /// </remarks>
    /// <example>
    ///     <code>
    /// Agents = new Dictionary&lt;string, AgentDefinition&gt;
    /// {
    ///     ["code-reviewer"] = new AgentDefinition
    ///     {
    ///         Description = "Expert code reviewer for security and quality",
    ///         Prompt = "You are a code review specialist...",
    ///         Tools = ["Read", "Grep", "Glob"],
    ///         Model = "sonnet"
    ///     }
    /// }
    /// </code>
    /// </example>
    public IReadOnlyDictionary<string, AgentDefinition>? Agents { get; init; }

    /// <summary>
    ///     Plugins to load from local filesystem paths.
    ///     Plugins can provide commands, agents, skills, hooks, and MCP servers.
    /// </summary>
    /// <remarks>
    ///     Plugin commands are namespaced as "plugin-name:command-name".
    /// </remarks>
    /// <example>
    ///     <code>
    /// Plugins = [
    ///     new PluginConfig { Path = "./my-plugin" },
    ///     new PluginConfig { Path = "/absolute/path/to/another-plugin" }
    /// ]
    /// </code>
    /// </example>
    public IReadOnlyList<PluginConfig>? Plugins { get; init; }

    /// <summary>
    ///     Permission mode for tool usage.
    /// </summary>
    public PermissionMode? PermissionMode { get; init; }

    /// <summary>
    ///     Whether to continue an existing conversation.
    /// </summary>
    public bool ContinueConversation { get; init; }

    /// <summary>
    ///     Session ID to resume.
    /// </summary>
    public string? Resume { get; init; }

    /// <summary>
    ///     Maximum number of turns in the conversation.
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    ///     Maximum budget in USD.
    /// </summary>
    public double? MaxBudgetUsd { get; init; }

    /// <summary>
    ///     Model to use (e.g., "sonnet", "opus", "haiku").
    /// </summary>
    /// <remarks>
    ///     Consider using <see cref="ModelId" /> for strongly-typed model selection.
    /// </remarks>
    public string? Model { get; init; }

    /// <summary>
    ///     Display name for metrics tracking. Shows in metrics output instead of session ID.
    /// </summary>
    public string? SessionDisplayName { get; init; }

    /// <summary>
    ///     Strongly-typed model identifier. Use this instead of <see cref="Model" /> for type safety.
    /// </summary>
    /// <remarks>
    ///     If both <see cref="Model" /> and <see cref="ModelId" /> are set, <see cref="ModelId" /> takes precedence.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     // Using predefined models
    ///     ModelId = ModelIdentifier.Sonnet
    ///     ModelId = ModelIdentifier.ClaudeOpus45
    /// 
    ///     // Custom model (e.g., fine-tuned)
    ///     ModelId = ModelIdentifier.Custom("my-custom-model")
    ///     </code>
    /// </example>
    public ModelIdentifier? ModelId { get; init; }

    /// <summary>
    ///     Fallback model if primary is unavailable.
    /// </summary>
    public string? FallbackModel { get; init; }

    /// <summary>
    ///     Strongly-typed fallback model identifier.
    /// </summary>
    /// <remarks>
    ///     If both <see cref="FallbackModel" /> and <see cref="FallbackModelId" /> are set,
    ///     <see cref="FallbackModelId" /> takes precedence.
    /// </remarks>
    public ModelIdentifier? FallbackModelId { get; init; }

    /// <summary>
    ///     Working directory for the agent.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    ///     Path to the Claude CLI executable. If null, searches PATH.
    /// </summary>
    public string? CliPath { get; init; }

    /// <summary>
    ///     Additional directories to add to the agent's scope.
    /// </summary>
    public IReadOnlyList<string> AddDirectories { get; init; } = [];

    /// <summary>
    ///     Environment variables to pass to the CLI.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    ///     Extra CLI arguments to pass.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ExtraArgs { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>
    ///     Callback for tool permission requests.
    /// </summary>
    public Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>? CanUseTool { get; init; }

    /// <summary>
    ///     Hook configurations by event type.
    /// </summary>
    public IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>? Hooks { get; init; }

    /// <summary>
    ///     Whether to include partial message streaming events.
    /// </summary>
    public bool IncludePartialMessages { get; init; }

    /// <summary>
    ///     Whether to fork to a new session when resuming.
    /// </summary>
    public bool ForkSession { get; init; }

    /// <summary>
    ///     Resume session at a specific message UUID within the session.
    ///     Use with <see cref="Resume" /> to specify both session ID and message position.
    /// </summary>
    /// <remarks>
    ///     This is useful for rewinding a conversation to a specific point and continuing from there.
    ///     The message UUID can be obtained from previous <see cref="Messages.UserMessage" /> or
    ///     <see cref="Messages.AssistantMessage" /> objects.
    /// </remarks>
    public string? ResumeSessionAt { get; init; }

    /// <summary>
    ///     Maximum tokens for thinking blocks.
    /// </summary>
    public int? MaxThinkingTokens { get; init; }

    /// <summary>
    ///     Output format for structured outputs.
    /// </summary>
    public JsonElement? OutputFormat { get; init; }

    /// <summary>
    ///     Enable file checkpointing for rewind support.
    /// </summary>
    public bool EnableFileCheckpointing { get; init; }

    /// <summary>
    ///     Beta features to enable.
    /// </summary>
    /// <example>
    ///     <code>
    /// // Enable extended context window
    /// Betas = ["context-1m-2025-08-07"]
    /// </code>
    /// </example>
    public IReadOnlyList<string>? Betas { get; init; }

    /// <summary>
    ///     Sandbox configuration for command execution.
    ///     Can be a simple mode (<see cref="SandboxMode" />) or detailed settings (<see cref="SandboxSettings" />).
    /// </summary>
    /// <example>
    ///     <code>
    /// // Simple mode (implicit conversion)
    /// Sandbox = SandboxMode.Strict
    /// 
    /// // Using static helpers
    /// Sandbox = SandboxConfig.Strict
    /// 
    /// // Detailed settings
    /// Sandbox = new SandboxSettings
    /// {
    ///     IsEnabled = true,
    ///     AutoAllowBashIfSandboxed = true,
    ///     ExcludedCommands = ["docker"],
    ///     Network = new NetworkSandboxSettings
    ///     {
    ///         AllowLocalBinding = true,
    ///         AllowUnixSockets = ["/var/run/docker.sock"]
    ///     }
    /// }
    /// 
    /// // Fluent configuration (returns configured settings)
    /// Sandbox = SandboxConfig.WithSettings(s => s with
    /// {
    ///     AutoAllowBashIfSandboxed = true,
    ///     ExcludedCommands = ["docker"]
    /// })
    /// </code>
    /// </example>
    public SandboxConfig? Sandbox { get; init; }

    /// <summary>
    ///     MCP tool name for handling permission prompts programmatically.
    ///     When set, permission prompts are sent to this MCP tool instead of the default handler.
    /// </summary>
    public string? PermissionPromptToolName { get; init; }

    /// <summary>
    ///     Enforce strict MCP configuration validation.
    /// </summary>
    public bool StrictMcpConfig { get; init; }

    /// <summary>
    ///     Skip all permission prompts. Use with extreme caution.
    /// </summary>
    /// <remarks>
    ///     When enabled, Claude has full system access without any permission checks.
    ///     Only use in controlled environments where you trust all possible operations.
    ///     Hooks still execute and can block operations if needed.
    /// </remarks>
    public bool DangerouslySkipPermissions { get; init; }

    /// <summary>
    ///     Explicitly disable all hooks.
    /// </summary>
    public bool NoHooks { get; init; }

    /// <summary>
    ///     Callback for JSON messages sent to the CLI.
    ///     Useful for debugging the actual conversation context.
    /// </summary>
    public Action<string>? OnMessageSent { get; init; }

    /// <summary>
    ///     Callback for JSON messages received from the CLI.
    ///     Useful for debugging the actual conversation context.
    /// </summary>
    public Action<string>? OnMessageReceived { get; init; }

    /// <summary>
    ///     Callback invoked when metrics are available (token usage, cost, timing).
    ///     Called for each turn/result in both one-shot queries and interactive sessions.
    /// </summary>
    /// <remarks>
    ///     For interactive sessions created via <see cref="ClaudeAgentClient.CreateSessionAsync" />,
    ///     you can also set <see cref="ClaudeAgentSession.OnMetrics" /> directly on the session.
    ///     This option provides a way to capture metrics for one-shot queries via
    ///     <see cref="ClaudeAgentClient.QueryAsync" /> and <see cref="ClaudeAgentClient.QueryToCompletionAsync" />.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var options = new ClaudeAgentOptions
    /// {
    ///     OnMetrics = evt =>
    ///     {
    ///         Console.WriteLine($"Tokens: {evt.InputTokens} in, {evt.OutputTokens} out");
    ///         Console.WriteLine($"Cost: ${evt.CostUsd:F4}");
    ///         return ValueTask.CompletedTask;
    ///     }
    /// };
    /// </code>
    /// </example>
    public Func<MetricsEvent, ValueTask>? OnMetrics { get; init; }

    /// <summary>
    ///     Custom session ID (UUID) for conversations.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    ///     Enable debug logging.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    ///     Path for debug output file.
    /// </summary>
    public string? DebugFile { get; init; }

    /// <summary>
    ///     Persist session state across restarts.
    /// </summary>
    public bool PersistSession { get; init; }

    /// <summary>
    ///     User identifier for analytics and attribution.
    /// </summary>
    public string? User { get; init; }

    /// <summary>
    ///     Additional paths to load CLAUDE.md-like files from.
    /// </summary>
    public IReadOnlyList<string>? AdditionalDataPaths { get; init; }

    /// <summary>
    ///     Callback for stderr output from the CLI.
    /// </summary>
    public Action<string>? OnStderr { get; init; }

    /// <summary>
    ///     Timeout for control protocol requests. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    ///     This is primarily used for testing to avoid long waits.
    /// </remarks>
    internal TimeSpan ControlRequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Capacity of the internal message channel. Defaults to 256.
    /// </summary>
    /// <remarks>
    ///     This controls backpressure when the consumer reads messages slower than they arrive.
    ///     A bounded channel prevents unbounded memory growth in long-running sessions.
    ///     When the channel is full, message production will wait until space is available.
    /// </remarks>
    public int MessageChannelCapacity { get; init; } = 256;
}

/// <summary>
///     Configuration for loading a plugin.
/// </summary>
public sealed record PluginConfig
{
    /// <summary>
    ///     Plugin type. Currently only "local" is supported.
    /// </summary>
    public string Type { get; init; } = "local";

    /// <summary>
    ///     Path to the plugin directory (containing .claude-plugin/plugin.json).
    ///     Can be relative (to working directory) or absolute.
    /// </summary>
    public required string Path { get; init; }
}

/// <summary>
///     Definition for a custom subagent that can be invoked via the Task tool.
/// </summary>
public sealed record AgentDefinition
{
    /// <summary>
    ///     Natural language description of when to use this agent.
    ///     Claude uses this to decide when to delegate tasks to the subagent.
    /// </summary>
    /// <example>"Expert code review specialist. Use for quality, security, and maintainability reviews."</example>
    public required string Description { get; init; }

    /// <summary>
    ///     The agent's system prompt defining its role and behavior.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    ///     Array of allowed tool names for this subagent.
    ///     If null/empty, the subagent inherits all tools from the parent.
    /// </summary>
    /// <remarks>
    ///     Do NOT include "Task" in a subagent's tools - subagents cannot spawn their own subagents.
    /// </remarks>
    /// <example>["Read", "Grep", "Glob"] for read-only analysis</example>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    ///     Model override for this subagent. If null, inherits from the main agent.
    /// </summary>
    /// <example>"sonnet", "opus", "haiku"</example>
    public string? Model { get; init; }

    /// <summary>
    ///     Skills available to this subagent.
    /// </summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>
    ///     Maximum number of turns for this subagent.
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    ///     Tools explicitly disallowed for this subagent.
    /// </summary>
    public IReadOnlyList<string>? DisallowedTools { get; init; }

    /// <summary>
    ///     MCP server configurations for this subagent.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }

    /// <summary>
    ///     Experimental critical system reminder appended to the system prompt.
    /// </summary>
    public string? CriticalSystemReminderExperimental { get; init; }
}

/// <summary>
///     Configuration for ripgrep (rg) command.
/// </summary>
public sealed record RipgrepConfig
{
    /// <summary>
    ///     The ripgrep command path.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    ///     Additional arguments for ripgrep.
    /// </summary>
    public IReadOnlyList<string>? Args { get; init; }
}

/// <summary>
///     MCP server configuration.
/// </summary>
public abstract record McpServerConfig;

/// <summary>
///     MCP server using stdio transport.
/// </summary>
public sealed record McpStdioServerConfig : McpServerConfig
{
    public required string Command { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}

/// <summary>
///     MCP server using SSE transport.
/// </summary>
public sealed record McpSseServerConfig : McpServerConfig
{
    public required string Url { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>
///     MCP server using HTTP transport.
/// </summary>
public sealed record McpHttpServerConfig : McpServerConfig
{
    public required string Url { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>
///     In-process SDK MCP server.
/// </summary>
public sealed record McpSdkServerConfig : McpServerConfig
{
    public required string Name { get; init; }
    public required IMcpToolServer Instance { get; init; }
}
