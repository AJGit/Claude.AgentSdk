using System.Runtime.Serialization;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Metrics;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Sessions;
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

    /// <summary>Don't prompt for permissions; deny any tool call that is not pre-approved.</summary>
    [EnumMember(Value = "dontAsk")] DontAsk,

    /// <summary>Delegate permission decisions to a handler.</summary>
    [EnumMember(Value = "delegate")] Delegate,

    /// <summary>A model classifier approves or denies each tool call.</summary>
    [EnumMember(Value = "auto")] Auto
}

/// <summary>
///     How much effort Claude puts into its response. Works with adaptive thinking to guide thinking depth.
/// </summary>
[GenerateEnumStrings]
public enum EffortLevel
{
    /// <summary>Minimal thinking, fastest responses.</summary>
    [EnumMember(Value = "low")] Low,

    /// <summary>Moderate thinking.</summary>
    [EnumMember(Value = "medium")] Medium,

    /// <summary>Deep reasoning (default).</summary>
    [EnumMember(Value = "high")] High,

    /// <summary>Extended reasoning depth (falls back to <see cref="High" /> on models that do not support it).</summary>
    [EnumMember(Value = "xhigh")] XHigh,

    /// <summary>Maximum effort.</summary>
    [EnumMember(Value = "max")] Max
}

/// <summary>
///     Controls whether thinking text is returned summarized or omitted.
/// </summary>
[GenerateEnumStrings]
public enum ThinkingDisplay
{
    /// <summary>Return summarized thinking text.</summary>
    [EnumMember(Value = "summarized")] Summarized,

    /// <summary>Omit thinking text (signature only).</summary>
    [EnumMember(Value = "omitted")] Omitted
}

/// <summary>
///     Controls Claude's thinking/reasoning behavior.
/// </summary>
/// <example>
///     <code>
/// Thinking = ThinkingConfig.Adaptive()
/// Thinking = ThinkingConfig.Enabled(budgetTokens: 8000)
/// Thinking = ThinkingConfig.Disabled
/// </code>
/// </example>
public abstract record ThinkingConfig
{
    /// <summary>
    ///     No extended thinking.
    /// </summary>
    public static ThinkingConfig Disabled { get; } = new DisabledThinkingConfig();

    /// <summary>
    ///     Claude decides when and how much to think.
    /// </summary>
    public static ThinkingConfig Adaptive(ThinkingDisplay? display = null)
    {
        return new AdaptiveThinkingConfig { Display = display };
    }

    /// <summary>
    ///     Fixed thinking token budget.
    /// </summary>
    public static ThinkingConfig Enabled(int budgetTokens, ThinkingDisplay? display = null)
    {
        return new EnabledThinkingConfig(budgetTokens) { Display = display };
    }
}

/// <summary>
///     Adaptive thinking: Claude decides when and how much to think.
/// </summary>
public sealed record AdaptiveThinkingConfig : ThinkingConfig
{
    /// <summary>
    ///     Whether thinking text is summarized or omitted. Null uses the model default.
    /// </summary>
    public ThinkingDisplay? Display { get; init; }
}

/// <summary>
///     Thinking with a fixed token budget.
/// </summary>
public sealed record EnabledThinkingConfig(int BudgetTokens) : ThinkingConfig
{
    /// <summary>
    ///     Whether thinking text is summarized or omitted. Null uses the model default.
    /// </summary>
    public ThinkingDisplay? Display { get; init; }
}

/// <summary>
///     Extended thinking disabled.
/// </summary>
public sealed record DisabledThinkingConfig : ThinkingConfig;

/// <summary>
///     Skills to enable for the main session.
/// </summary>
/// <remarks>
///     This is a context filter, not a sandbox: unlisted skills are hidden from the model's listing and
///     rejected by the Skill tool, but their files remain on disk. Do not store secrets in skill files.
/// </remarks>
/// <example>
///     <code>
/// Skills = SkillsConfig.All
/// Skills = SkillsConfig.Only("pdf", "my-plugin:review")
/// Skills = new[] { "pdf" }        // implicit conversion
/// Skills = SkillsConfig.None      // suppress every skill
/// </code>
/// </example>
public abstract record SkillsConfig
{
    /// <summary>
    ///     Enable every discovered skill.
    /// </summary>
    public static SkillsConfig All { get; } = new AllSkillsConfig();

    /// <summary>
    ///     Suppress every skill from the listing.
    /// </summary>
    public static SkillsConfig None => new SkillListConfig([]);

    /// <summary>
    ///     Enable only the listed skills (SKILL.md name / directory name, or "plugin:skill").
    /// </summary>
    public static SkillsConfig Only(params string[] names)
    {
        return new SkillListConfig(names);
    }

    /// <summary>
    ///     Creates a skill list from an array of skill names.
    /// </summary>
    public static implicit operator SkillsConfig(string[] names)
    {
        return new SkillListConfig(names);
    }

    /// <summary>
    ///     Creates a skill list from a list of skill names.
    /// </summary>
    public static implicit operator SkillsConfig(List<string> names)
    {
        return new SkillListConfig(names);
    }
}

/// <summary>
///     Enable every discovered skill.
/// </summary>
public sealed record AllSkillsConfig : SkillsConfig;

/// <summary>
///     Enable only the listed skills. An empty list suppresses all skills.
/// </summary>
public sealed record SkillListConfig(IReadOnlyList<string> Names) : SkillsConfig;

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
    ///     Domain names that sandboxed processes can access.
    /// </summary>
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>
    ///     Domains that are always blocked, even if matched by <see cref="AllowedDomains" />.
    /// </summary>
    public IReadOnlyList<string>? DeniedDomains { get; init; }

    /// <summary>
    ///     When true in managed settings, only managed-settings allowed domains are respected.
    /// </summary>
    public bool AllowManagedDomainsOnly { get; init; }

    /// <summary>
    ///     macOS only: XPC/Mach service names to allow (supports trailing wildcard).
    /// </summary>
    public IReadOnlyList<string>? AllowMachLookup { get; init; }
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
[JsonDerivedType(typeof(FileSystemPrompt), "file")]
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

    /// <summary>
    ///     Creates a system prompt config that loads the prompt from a file (<c>--system-prompt-file</c>).
    /// </summary>
    public static SystemPromptConfig FromFile(string path)
    {
        return new FileSystemPrompt(path);
    }
}

/// <summary>
///     A custom string system prompt that replaces the default entirely.
/// </summary>
public sealed record CustomSystemPrompt(string Prompt) : SystemPromptConfig
{
    /// <summary>
    ///     Same as <see cref="PresetSystemPrompt.Snapshot" />, applied to <see cref="Prompt" />.
    /// </summary>
    public bool? Snapshot { get; init; }
}

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

    /// <summary>
    ///     Strip per-user dynamic sections (working directory, auto-memory, git status) from the system
    ///     prompt so it stays static and cacheable across users. The stripped content is re-injected into
    ///     the first user message. Older CLIs silently ignore this option.
    /// </summary>
    public bool? ExcludeDynamicSections { get; init; }

    /// <summary>
    ///     Whether the session keeps the system prompt it recorded on its first request. When true, every
    ///     later request (including after resume) sends the recorded prompt, so changing <see cref="Append" />
    ///     has no effect until the session is compacted or a new session starts. When false, the prompt is
    ///     rebuilt on every request. Requires Claude Code CLI 2.1.257 or later.
    /// </summary>
    public bool? Snapshot { get; init; }
}

/// <summary>
///     A system prompt loaded from a file via <c>--system-prompt-file</c>.
/// </summary>
public sealed record FileSystemPrompt(string Path) : SystemPromptConfig;

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
    ///     Custom permission handler for tool calls that would otherwise prompt the user.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Invoked only when the CLI's permission rules evaluate to "ask". It is not invoked for tool calls
    ///         already permitted by <see cref="AllowedTools" />, <see cref="PermissionMode" /> (e.g. acceptEdits /
    ///         bypassPermissions), or allow rules in settings. A warning is logged when the callback is visibly
    ///         shadowed by such options. To observe every tool call, use a PreToolUse hook instead.
    ///     </para>
    ///     <para>
    ///         Cannot be combined with <see cref="PermissionPromptToolName" />; the SDK routes permission
    ///         prompts over the control protocol automatically.
    ///     </para>
    /// </remarks>
    public Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>? CanUseTool { get; init; }

    /// <summary>
    ///     Hook configurations by event type.
    /// </summary>
    /// <remarks>
    ///     Multiple matchers registered on the same event are dispatched concurrently by the CLI; do not rely
    ///     on one hook completing before another starts.
    /// </remarks>
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
    ///     With <see cref="ResumeSessionAt" />: the UUID of the user prompt whose turn this truncating resume
    ///     intends to discard.
    /// </summary>
    /// <remarks>
    ///     When set, the CLI validates at load time that every transcript entry after the
    ///     <see cref="ResumeSessionAt" /> point is attributable to that turn, and refuses the resume otherwise
    ///     (the error text contains <c>Resume rejected by --resume-drops-turn:</c>). Treat a refusal as
    ///     deterministic: resume plainly rather than retrying the same request.
    /// </remarks>
    public string? ResumeDropsTurn { get; init; }

    /// <summary>
    ///     Maximum tokens for thinking blocks.
    /// </summary>
    /// <remarks>
    ///     Deprecated: use <see cref="Thinking" /> instead. When <see cref="Thinking" /> is set it takes precedence.
    /// </remarks>
    public int? MaxThinkingTokens { get; init; }

    /// <summary>
    ///     Controls Claude's thinking/reasoning behavior. Takes precedence over <see cref="MaxThinkingTokens" />.
    /// </summary>
    public ThinkingConfig? Thinking { get; init; }

    /// <summary>
    ///     Controls how much effort Claude puts into its response.
    /// </summary>
    public EffortLevel? Effort { get; init; }

    /// <summary>
    ///     API-side task budget in tokens. The model is made aware of its remaining budget so it can pace
    ///     tool use and wrap up before the limit.
    /// </summary>
    public int? TaskBudget { get; init; }

    /// <summary>
    ///     Skills to enable for the main session. When set, the SDK adds the matching <c>Skill</c> rules to
    ///     <see cref="AllowedTools" /> and defaults <see cref="SettingSources" /> to user + project.
    /// </summary>
    /// <remarks>
    ///     Null (default) applies no SDK configuration; the CLI's own defaults still apply. Use
    ///     <see cref="SkillsConfig.None" /> to suppress every skill. Skill names are validated when the
    ///     session starts: delimiters, wildcards, surrounding whitespace, or a leading '/' throw
    ///     <see cref="ArgumentException" />.
    /// </remarks>
    public SkillsConfig? Skills { get; init; }

    /// <summary>
    ///     Path to an additional settings JSON file, or an inline JSON string (<c>--settings</c>).
    ///     When <see cref="Sandbox" /> is also set, both are merged into one JSON object.
    /// </summary>
    public string? Settings { get; init; }

    /// <summary>
    ///     Include hook lifecycle events (hook_started / hook_response system messages) in the message stream.
    /// </summary>
    public bool IncludeHookEvents { get; init; }

    /// <summary>
    ///     Forward subagent text and thinking blocks as messages in the stream (not just tool_use/tool_result),
    ///     so consumers can render the full nested transcript.
    /// </summary>
    public bool ForwardSubagentText { get; init; }

    /// <summary>
    ///     Deliver every prompt to Claude exactly as written: no <c>@path</c> file-mention expansion and no
    ///     slash-command dispatch.
    /// </summary>
    /// <remarks>
    ///     Use this when prompt text is assembled from content the end user did not type (prior turns, tool
    ///     results, third-party text), so an <c>@/absolute/path</c> inside it cannot make Claude Code read a
    ///     local file. Requires Claude Code 2.1.248 or later; older versions ignore the flag.
    /// </remarks>
    public bool VerbatimPrompts { get; init; }

    /// <summary>
    ///     Mirror session transcripts to an external store.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every transcript line the CLI writes locally is also passed to <see cref="ISessionStore.AppendAsync" />
    ///         (flushed per turn, or per frame with <see cref="SessionStoreFlushMode.Eager" />). Failures never stop the
    ///         session; a dropped batch surfaces as a system message with subtype <c>mirror_error</c>.
    ///     </para>
    ///     <para>
    ///         With <see cref="Resume" /> or <see cref="ContinueConversation" />, the session is loaded from the store
    ///         into a temporary <c>CLAUDE_CONFIG_DIR</c> (with the caller's credentials, refresh token redacted) so
    ///         the CLI can resume it even when the local file is absent. The temp dir is removed when the query or
    ///         session ends. <see cref="ContinueConversation" /> requires <see cref="ISessionStoreListing" />, and a
    ///         store cannot be combined with <see cref="EnableFileCheckpointing" />.
    ///     </para>
    /// </remarks>
    public ISessionStore? SessionStore { get; init; }

    /// <summary>
    ///     When to flush mirrored transcript entries to <see cref="SessionStore" />. Defaults to batched.
    /// </summary>
    public SessionStoreFlushMode SessionStoreFlush { get; init; } = SessionStoreFlushMode.Batched;

    /// <summary>
    ///     Timeout for each store load / list call while materializing a store-backed resume. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan SessionStoreLoadTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Timeout for the initialize control request. MCP servers may take a while to start, so this is
    ///     longer than other control requests. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan InitializeTimeout { get; init; } = TimeSpan.FromSeconds(60);

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
    ///     Explicitly disable all hooks: SDK hook callbacks are not registered, and settings-file hooks are
    ///     disabled via the <c>disableAllHooks</c> setting.
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
    /// <remarks>
    ///     Not forwarded to the CLI, which has no equivalent flag (passing one made the CLI exit with
    ///     "unknown option"). Retained for source compatibility.
    /// </remarks>
    public string? User { get; init; }

    /// <summary>
    ///     Additional paths to load CLAUDE.md-like files from. Passed to the CLI as <c>--add-dir</c>.
    /// </summary>
    public IReadOnlyList<string>? AdditionalDataPaths { get; init; }

    /// <summary>
    ///     Callback for stderr output from the CLI.
    /// </summary>
    public Action<string>? OnStderr { get; init; }

    /// <summary>
    ///     Timeout for control protocol requests. Defaults to 60 seconds.
    /// </summary>
    /// <remarks>
    ///     This is primarily used for testing to avoid long waits.
    /// </remarks>
    internal TimeSpan ControlRequestTimeout { get; init; } = TimeSpan.FromSeconds(60);

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
    ///     Inline MCP server configurations for this subagent.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }

    /// <summary>
    ///     Names of MCP servers (already configured on the parent session) that this subagent may use.
    /// </summary>
    public IReadOnlyList<string>? McpServerNames { get; init; }

    /// <summary>
    ///     Experimental critical system reminder appended to the system prompt.
    /// </summary>
    public string? CriticalSystemReminderExperimental { get; init; }

    /// <summary>
    ///     Prompt sent to the subagent as its first user message.
    /// </summary>
    public string? InitialPrompt { get; init; }

    /// <summary>
    ///     Memory scope for this subagent (user, project, or local).
    /// </summary>
    public SettingSource? Memory { get; init; }

    /// <summary>
    ///     Whether this subagent runs in the background by default.
    /// </summary>
    public bool? Background { get; init; }

    /// <summary>
    ///     Effort level override for this subagent.
    /// </summary>
    public EffortLevel? Effort { get; init; }

    /// <summary>
    ///     Permission mode override for this subagent.
    /// </summary>
    public PermissionMode? PermissionMode { get; init; }
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
