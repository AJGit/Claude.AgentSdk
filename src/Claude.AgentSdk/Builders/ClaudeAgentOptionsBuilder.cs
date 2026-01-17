using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Tools;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Builders;

/// <summary>
///     Fluent builder for configuring <see cref="ClaudeAgentOptions" />.
/// </summary>
/// <remarks>
///     <para>
///         This builder provides a more ergonomic way to configure agent options
///         compared to using object initializers with many properties.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     var options = new ClaudeAgentOptionsBuilder()
///         .WithModel(ModelIdentifier.Sonnet)
///         .WithSystemPrompt("You are a helpful assistant.")
///         .UseClaudeCodePreset(append: "Focus on C# development.")
///         .AllowTools(ToolName.Read, ToolName.Write, ToolName.Bash)
///         .AddMcpServer("tools", config)
///         .WithPermissionMode(PermissionMode.AcceptEdits)
///         .Build();
///     </code>
///     </para>
/// </remarks>
public sealed class ClaudeAgentOptionsBuilder
{
    private readonly List<string> _addDirectories = [];
    private readonly List<string> _additionalDataPaths = [];
    private readonly Dictionary<string, AgentDefinition> _agents = [];
    private readonly List<string> _allowedTools = [];
    private readonly List<string> _betas = [];
    private readonly List<string> _disallowedTools = [];
    private readonly Dictionary<string, string> _environment = [];
    private readonly Dictionary<string, string?> _extraArgs = [];
    private readonly Dictionary<HookEvent, List<HookMatcher>> _hooks = [];
    private readonly Dictionary<string, McpServerConfig> _mcpServers = [];
    private readonly List<PluginConfig> _plugins = [];
    private readonly List<SettingSource> _settingSources = [];
    private Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>? _canUseTool;
    private string? _cliPath;
    private bool _continueConversation;
    private bool _dangerouslySkipPermissions;
    private bool _enableFileCheckpointing;
    private ModelIdentifier? _fallbackModelId;
    private bool _forkSession;
    private bool _includePartialMessages;
    private double? _maxBudgetUsd;
    private int? _maxThinkingTokens;
    private int? _maxTurns;
    private int _messageChannelCapacity = 256;
    private ModelIdentifier? _modelId;
    private bool _noHooks;
    private Action<string>? _onStderr;
    private JsonElement? _outputFormat;
    private PermissionMode? _permissionMode;
    private string? _permissionPromptToolName;
    private string? _resume;
    private string? _resumeSessionAt;
    private SandboxConfig? _sandbox;
    private bool _strictMcpConfig;
    private SystemPromptConfig? _systemPrompt;
    private ToolsConfig? _tools;
    private string? _user;
    private string? _workingDirectory;

    /// <summary>
    ///     Builds the <see cref="ClaudeAgentOptions" /> instance.
    /// </summary>
    /// <returns>The configured options.</returns>
    public ClaudeAgentOptions Build()
    {
        return new ClaudeAgentOptions
        {
            Tools = _tools,
            AllowedTools = _allowedTools.Count > 0 ? _allowedTools : [],
            DisallowedTools = _disallowedTools.Count > 0 ? _disallowedTools : [],
            SystemPrompt = _systemPrompt,
            SettingSources = _settingSources.Count > 0 ? _settingSources : null,
            McpServers = _mcpServers.Count > 0 ? _mcpServers : null,
            Agents = _agents.Count > 0 ? _agents : null,
            Plugins = _plugins.Count > 0 ? _plugins : null,
            PermissionMode = _permissionMode,
            ContinueConversation = _continueConversation,
            Resume = _resume,
            MaxTurns = _maxTurns,
            MaxBudgetUsd = _maxBudgetUsd,
            ModelId = _modelId,
            FallbackModelId = _fallbackModelId,
            WorkingDirectory = _workingDirectory,
            CliPath = _cliPath,
            AddDirectories = _addDirectories.Count > 0 ? _addDirectories : [],
            Environment = _environment.Count > 0 ? _environment : new Dictionary<string, string>(),
            ExtraArgs = _extraArgs.Count > 0 ? _extraArgs : new Dictionary<string, string?>(),
            CanUseTool = _canUseTool,
            Hooks = _hooks.Count > 0
                ? _hooks.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<HookMatcher>)kvp.Value)
                : null,
            IncludePartialMessages = _includePartialMessages,
            ForkSession = _forkSession,
            ResumeSessionAt = _resumeSessionAt,
            MaxThinkingTokens = _maxThinkingTokens,
            OutputFormat = _outputFormat,
            EnableFileCheckpointing = _enableFileCheckpointing,
            Betas = _betas.Count > 0 ? _betas : null,
            Sandbox = _sandbox,
            PermissionPromptToolName = _permissionPromptToolName,
            StrictMcpConfig = _strictMcpConfig,
            DangerouslySkipPermissions = _dangerouslySkipPermissions,
            NoHooks = _noHooks,
            User = _user,
            AdditionalDataPaths = _additionalDataPaths.Count > 0 ? _additionalDataPaths : null,
            OnStderr = _onStderr,
            MessageChannelCapacity = _messageChannelCapacity
        };
    }

    /// <summary>
    ///     Sets the model to use.
    /// </summary>
    /// <param name="model">The model identifier.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithModel(ModelIdentifier model)
    {
        _modelId = model;
        return this;
    }

    /// <summary>
    ///     Sets the fallback model to use if the primary is unavailable.
    /// </summary>
    /// <param name="model">The fallback model identifier.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithFallbackModel(ModelIdentifier model)
    {
        _fallbackModelId = model;
        return this;
    }

    /// <summary>
    ///     Sets a custom system prompt.
    /// </summary>
    /// <param name="prompt">The system prompt text.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = new CustomSystemPrompt(prompt);
        return this;
    }

    /// <summary>
    ///     Uses the claude_code system prompt preset.
    /// </summary>
    /// <param name="append">Optional text to append to the preset prompt.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder UseClaudeCodePreset(string? append = null)
    {
        _systemPrompt = SystemPromptConfig.ClaudeCode(append);
        return this;
    }

    /// <summary>
    ///     Sets the tools configuration to use a preset.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder UseClaudeCodeTools()
    {
        _tools = ToolsConfig.ClaudeCode();
        return this;
    }

    /// <summary>
    ///     Sets a specific list of tools to enable.
    /// </summary>
    /// <param name="tools">The tool names to enable.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithTools(params string[] tools)
    {
        _tools = new ToolsList(tools);
        return this;
    }

    /// <summary>
    ///     Sets a specific list of tools to enable using strongly-typed names.
    /// </summary>
    /// <param name="tools">The tool names to enable.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithTools(params ToolName[] tools)
    {
        _tools = new ToolsList(tools.Select(t => t.Value).ToList());
        return this;
    }

    /// <summary>
    ///     Adds tools to the allowed tools list.
    /// </summary>
    /// <param name="tools">The tool names to allow.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AllowTools(params string[] tools)
    {
        _allowedTools.AddRange(tools);
        return this;
    }

    /// <summary>
    ///     Adds tools to the allowed tools list using strongly-typed names.
    /// </summary>
    /// <param name="tools">The tool names to allow.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AllowTools(params ToolName[] tools)
    {
        _allowedTools.AddRange(tools.Select(t => t.Value));
        return this;
    }

    /// <summary>
    ///     Adds tools to the disallowed tools list.
    /// </summary>
    /// <param name="tools">The tool names to disallow.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder DisallowTools(params string[] tools)
    {
        _disallowedTools.AddRange(tools);
        return this;
    }

    /// <summary>
    ///     Adds tools to the disallowed tools list using strongly-typed names.
    /// </summary>
    /// <param name="tools">The tool names to disallow.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder DisallowTools(params ToolName[] tools)
    {
        _disallowedTools.AddRange(tools.Select(t => t.Value));
        return this;
    }

    /// <summary>
    ///     Adds an MCP server configuration.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="config">The server configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddMcpServer(string name, McpServerConfig config)
    {
        _mcpServers[name] = config;
        return this;
    }

    /// <summary>
    ///     Adds an MCP server configuration using a strongly-typed name.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="config">The server configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddMcpServer(McpServerName name, McpServerConfig config)
    {
        _mcpServers[name.Value] = config;
        return this;
    }

    /// <summary>
    ///     Adds an in-process SDK MCP server.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="server">The MCP tool server instance.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddSdkServer(string name, IMcpToolServer server)
    {
        _mcpServers[name] = new McpSdkServerConfig { Name = name, Instance = server };
        return this;
    }

    /// <summary>
    ///     Adds an in-process SDK MCP server using a strongly-typed name.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="server">The MCP tool server instance.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddSdkServer(McpServerName name, IMcpToolServer server)
    {
        _mcpServers[name.Value] = new McpSdkServerConfig { Name = name.Value, Instance = server };
        return this;
    }

    /// <summary>
    ///     Adds MCP servers from a builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddMcpServers(McpServerBuilder builder)
    {
        foreach (var (name, config) in builder.Build())
        {
            _mcpServers[name] = config;
        }

        return this;
    }

    /// <summary>
    ///     Adds a subagent definition.
    /// </summary>
    /// <param name="name">The agent name.</param>
    /// <param name="definition">The agent definition.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddAgent(string name, AgentDefinition definition)
    {
        _agents[name] = definition;
        return this;
    }

    /// <summary>
    ///     Adds a subagent definition using a builder.
    /// </summary>
    /// <param name="name">The agent name.</param>
    /// <param name="configure">A function to configure the agent builder.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddAgent(string name,
        Func<AgentDefinitionBuilder, AgentDefinitionBuilder> configure)
    {
        var builder = new AgentDefinitionBuilder();
        _agents[name] = configure(builder).Build();
        return this;
    }

    /// <summary>
    ///     Adds a hook matcher for an event.
    /// </summary>
    /// <param name="hookEvent">The hook event type.</param>
    /// <param name="matcher">The hook matcher.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddHook(HookEvent hookEvent, HookMatcher matcher)
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
    ///     Adds hooks from a builder.
    /// </summary>
    /// <param name="builder">The hook configuration builder.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddHooks(HookConfigurationBuilder builder)
    {
        foreach (var (hookEvent, matchers) in builder.Build())
        {
            if (!_hooks.TryGetValue(hookEvent, out var existingMatchers))
            {
                existingMatchers = [];
                _hooks[hookEvent] = existingMatchers;
            }

            existingMatchers.AddRange(matchers);
        }

        return this;
    }

    /// <summary>
    ///     Adds a PreToolUse hook.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder OnPreToolUse(HookCallback handler, string? matcher = null, double? timeout = null)
    {
        return AddHook(HookEvent.PreToolUse, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a PostToolUse hook.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="matcher">Optional tool name pattern to match.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder OnPostToolUse(HookCallback handler, string? matcher = null, double? timeout = null)
    {
        return AddHook(HookEvent.PostToolUse, new HookMatcher
        {
            Matcher = matcher,
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Adds a Stop hook.
    /// </summary>
    /// <param name="handler">The hook callback handler.</param>
    /// <param name="timeout">Optional timeout in seconds.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder OnStop(HookCallback handler, double? timeout = null)
    {
        return AddHook(HookEvent.Stop, new HookMatcher
        {
            Hooks = [handler],
            Timeout = timeout
        });
    }

    /// <summary>
    ///     Sets the permission mode.
    /// </summary>
    /// <param name="mode">The permission mode.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithPermissionMode(PermissionMode mode)
    {
        _permissionMode = mode;
        return this;
    }

    /// <summary>
    ///     Sets the tool permission callback.
    /// </summary>
    /// <param name="callback">The permission callback function.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithCanUseTool(
        Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>> callback)
    {
        _canUseTool = callback;
        return this;
    }

    /// <summary>
    ///     Enables bypassing all permissions. Use with extreme caution.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder DangerouslySkipAllPermissions()
    {
        _dangerouslySkipPermissions = true;
        return this;
    }

    /// <summary>
    ///     Sets the sandbox mode.
    /// </summary>
    /// <param name="mode">The sandbox mode.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithSandbox(SandboxMode mode)
    {
        _sandbox = mode;
        return this;
    }

    /// <summary>
    ///     Sets detailed sandbox settings.
    /// </summary>
    /// <param name="settings">The sandbox settings.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithSandbox(SandboxSettings settings)
    {
        _sandbox = settings;
        return this;
    }

    /// <summary>
    ///     Configures sandbox settings using a fluent builder.
    /// </summary>
    /// <param name="configure">A function to configure sandbox settings.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithSandbox(Func<SandboxSettings, SandboxSettings> configure)
    {
        _sandbox = SandboxConfig.WithSettings(configure);
        return this;
    }

    /// <summary>
    ///     Enables continuing an existing conversation.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder ContinueConversation()
    {
        _continueConversation = true;
        return this;
    }

    /// <summary>
    ///     Sets the session ID to resume.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder Resume(string sessionId)
    {
        _resume = sessionId;
        return this;
    }

    /// <summary>
    ///     Resumes at a specific message within a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="messageUuid">The message UUID.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder ResumeAt(string sessionId, string messageUuid)
    {
        _resume = sessionId;
        _resumeSessionAt = messageUuid;
        return this;
    }

    /// <summary>
    ///     Enables forking to a new session when resuming.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder ForkSession()
    {
        _forkSession = true;
        return this;
    }

    /// <summary>
    ///     Sets the maximum number of turns.
    /// </summary>
    /// <param name="maxTurns">The maximum turn count.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithMaxTurns(int maxTurns)
    {
        _maxTurns = maxTurns;
        return this;
    }

    /// <summary>
    ///     Sets the maximum budget in USD.
    /// </summary>
    /// <param name="maxBudget">The maximum budget.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithMaxBudget(double maxBudget)
    {
        _maxBudgetUsd = maxBudget;
        return this;
    }

    /// <summary>
    ///     Sets the maximum thinking tokens.
    /// </summary>
    /// <param name="maxTokens">The maximum thinking token count.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithMaxThinkingTokens(int maxTokens)
    {
        _maxThinkingTokens = maxTokens;
        return this;
    }

    /// <summary>
    ///     Sets the working directory.
    /// </summary>
    /// <param name="directory">The working directory path.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithWorkingDirectory(string directory)
    {
        _workingDirectory = directory;
        return this;
    }

    /// <summary>
    ///     Sets the path to the Claude CLI executable.
    /// </summary>
    /// <param name="path">The CLI executable path.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithCliPath(string path)
    {
        _cliPath = path;
        return this;
    }

    /// <summary>
    ///     Adds additional directories to the agent's scope.
    /// </summary>
    /// <param name="directories">The directory paths to add.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddDirectories(params string[] directories)
    {
        _addDirectories.AddRange(directories);
        return this;
    }

    /// <summary>
    ///     Adds setting sources for loading CLAUDE.md files.
    /// </summary>
    /// <param name="sources">The setting sources to add.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddSettingSources(params SettingSource[] sources)
    {
        _settingSources.AddRange(sources);
        return this;
    }

    /// <summary>
    ///     Loads project-level CLAUDE.md settings.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder LoadProjectSettings()
    {
        if (!_settingSources.Contains(SettingSource.Project))
        {
            _settingSources.Add(SettingSource.Project);
        }

        return this;
    }

    /// <summary>
    ///     Loads user-level CLAUDE.md settings.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder LoadUserSettings()
    {
        if (!_settingSources.Contains(SettingSource.User))
        {
            _settingSources.Add(SettingSource.User);
        }

        return this;
    }

    /// <summary>
    ///     Adds additional paths to load CLAUDE.md-like files from.
    /// </summary>
    /// <param name="paths">The file paths to add.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddDataPaths(params string[] paths)
    {
        _additionalDataPaths.AddRange(paths);
        return this;
    }

    /// <summary>
    ///     Adds an environment variable.
    /// </summary>
    /// <param name="key">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithEnvironment(string key, string value)
    {
        _environment[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple environment variables.
    /// </summary>
    /// <param name="variables">The environment variables.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithEnvironment(IReadOnlyDictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            _environment[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Adds an extra CLI argument.
    /// </summary>
    /// <param name="key">The argument name.</param>
    /// <param name="value">The argument value (optional).</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithExtraArg(string key, string? value = null)
    {
        _extraArgs[key] = value;
        return this;
    }

    /// <summary>
    ///     Enables partial message streaming events.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder IncludePartialMessages()
    {
        _includePartialMessages = true;
        return this;
    }

    /// <summary>
    ///     Sets the output format for structured outputs.
    /// </summary>
    /// <param name="format">The output format JSON schema.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithOutputFormat(JsonElement format)
    {
        _outputFormat = format;
        return this;
    }

    /// <summary>
    ///     Sets the stderr callback.
    /// </summary>
    /// <param name="callback">The stderr callback.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder OnStderr(Action<string> callback)
    {
        _onStderr = callback;
        return this;
    }

    /// <summary>
    ///     Sets the message channel capacity.
    /// </summary>
    /// <param name="capacity">The channel capacity.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithMessageChannelCapacity(int capacity)
    {
        _messageChannelCapacity = capacity;
        return this;
    }

    /// <summary>
    ///     Enables file checkpointing for rewind support.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder EnableFileCheckpointing()
    {
        _enableFileCheckpointing = true;
        return this;
    }

    /// <summary>
    ///     Adds beta features to enable.
    /// </summary>
    /// <param name="betas">The beta feature names.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder EnableBetas(params string[] betas)
    {
        _betas.AddRange(betas);
        return this;
    }

    /// <summary>
    ///     Disables all hooks.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder DisableHooks()
    {
        _noHooks = true;
        return this;
    }

    /// <summary>
    ///     Enables strict MCP configuration validation.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder UseStrictMcpConfig()
    {
        _strictMcpConfig = true;
        return this;
    }

    /// <summary>
    ///     Sets the user identifier for analytics.
    /// </summary>
    /// <param name="user">The user identifier.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithUser(string user)
    {
        _user = user;
        return this;
    }

    /// <summary>
    ///     Adds a plugin to load.
    /// </summary>
    /// <param name="path">The plugin directory path.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder AddPlugin(string path)
    {
        _plugins.Add(new PluginConfig { Path = path });
        return this;
    }

    /// <summary>
    ///     Sets the MCP tool name for handling permission prompts.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>This builder for chaining.</returns>
    public ClaudeAgentOptionsBuilder WithPermissionPromptTool(string toolName)
    {
        _permissionPromptToolName = toolName;
        return this;
    }
}
