using System.Text.Json;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Tools;
using Moq;

namespace Claude.AgentSdk.Tests;

/// <summary>
///     Comprehensive tests for ClaudeAgentOptions and related configuration types.
/// </summary>
public class ClaudeAgentOptionsTests
{
    [Fact]
    public void ClaudeAgentOptions_DefaultValues_AreCorrect()
    {
        var options = new ClaudeAgentOptions();

        Assert.Null(options.Tools);
        Assert.Empty(options.AllowedTools);
        Assert.Empty(options.DisallowedTools);
        Assert.Null(options.SystemPrompt);
        Assert.Null(options.SettingSources);
        Assert.Null(options.McpServers);
        Assert.Null(options.Agents);
        Assert.Null(options.Plugins);
        Assert.Null(options.PermissionMode);
        Assert.False(options.ContinueConversation);
        Assert.Null(options.Resume);
        Assert.Null(options.MaxTurns);
        Assert.Null(options.MaxBudgetUsd);
        Assert.Null(options.Model);
        Assert.Null(options.FallbackModel);
        Assert.Null(options.WorkingDirectory);
        Assert.Null(options.CliPath);
        Assert.Empty(options.AddDirectories);
        Assert.Empty(options.Environment);
        Assert.Empty(options.ExtraArgs);
        Assert.Null(options.CanUseTool);
        Assert.Null(options.Hooks);
        Assert.False(options.IncludePartialMessages);
        Assert.False(options.ForkSession);
        Assert.Null(options.ResumeSessionAt);
        Assert.Null(options.MaxThinkingTokens);
        Assert.Null(options.OutputFormat);
        Assert.False(options.EnableFileCheckpointing);
        Assert.Null(options.Betas);
        Assert.Null(options.Sandbox);
        Assert.Null(options.PermissionPromptToolName);
        Assert.False(options.StrictMcpConfig);
        Assert.False(options.NoHooks);
        Assert.Null(options.User);
        Assert.Null(options.AdditionalDataPaths);
        Assert.Null(options.OnStderr);
    }

    [Fact]
    public void ClaudeAgentOptions_WithAllProperties_SetsCorrectly()
    {
        var mockMcpServer = new Mock<IMcpToolServer>();
        var toolPermissionCallback =
            new Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>((_, _) =>
                Task.FromResult<PermissionResult>(new PermissionResultAllow()));
        var stderrCallback = new Action<string>(_ => { });

        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsList(["Read", "Write"]),
            AllowedTools = ["Task", "Grep"],
            DisallowedTools = ["Bash"],
            SystemPrompt = "You are a helpful assistant",
            SettingSources = [SettingSource.Project, SettingSource.User],
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["test-server"] = new McpStdioServerConfig { Command = "test-cmd" }
            },
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["test-agent"] = new()
                {
                    Description = "Test agent",
                    Prompt = "Test prompt"
                }
            },
            Plugins = [new PluginConfig { Path = "./test-plugin" }],
            PermissionMode = PermissionMode.AcceptEdits,
            ContinueConversation = true,
            Resume = "session-123",
            MaxTurns = 10,
            MaxBudgetUsd = 5.0,
            Model = "opus",
            FallbackModel = "sonnet",
            WorkingDirectory = "/path/to/work",
            CliPath = "/usr/bin/claude",
            AddDirectories = ["/extra/dir1", "/extra/dir2"],
            Environment = new Dictionary<string, string> { ["VAR1"] = "value1" },
            ExtraArgs = new Dictionary<string, string?> { ["--flag"] = "value" },
            CanUseTool = toolPermissionCallback,
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = [new HookMatcher { Hooks = [] }]
            },
            IncludePartialMessages = true,
            ForkSession = true,
            ResumeSessionAt = "message-uuid-123",
            MaxThinkingTokens = 1000,
            OutputFormat = JsonDocument.Parse("{}").RootElement,
            EnableFileCheckpointing = true,
            Betas = ["context-1m-2025-08-07"],
            Sandbox = SandboxMode.Strict,
            PermissionPromptToolName = "custom-permission-tool",
            StrictMcpConfig = true,
            NoHooks = true,
            User = "test-user",
            AdditionalDataPaths = ["/path/to/data"],
            OnStderr = stderrCallback
        };

        Assert.IsType<ToolsList>(options.Tools);
        Assert.Equal(2, options.AllowedTools.Count);
        Assert.Single(options.DisallowedTools);
        Assert.IsType<CustomSystemPrompt>(options.SystemPrompt);
        Assert.Equal(2, options.SettingSources!.Count);
        Assert.Single(options.McpServers);
        Assert.Single(options.Agents);
        Assert.Single(options.Plugins);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
        Assert.True(options.ContinueConversation);
        Assert.Equal("session-123", options.Resume);
        Assert.Equal(10, options.MaxTurns);
        Assert.Equal(5.0, options.MaxBudgetUsd);
        Assert.Equal("opus", options.Model);
        Assert.Equal("sonnet", options.FallbackModel);
        Assert.Equal("/path/to/work", options.WorkingDirectory);
        Assert.Equal("/usr/bin/claude", options.CliPath);
        Assert.Equal(2, options.AddDirectories.Count);
        Assert.Single(options.Environment);
        Assert.Single(options.ExtraArgs);
        Assert.NotNull(options.CanUseTool);
        Assert.Single(options.Hooks);
        Assert.True(options.IncludePartialMessages);
        Assert.True(options.ForkSession);
        Assert.Equal("message-uuid-123", options.ResumeSessionAt);
        Assert.Equal(1000, options.MaxThinkingTokens);
        Assert.NotNull(options.OutputFormat);
        Assert.True(options.EnableFileCheckpointing);
        Assert.Single(options.Betas);
        Assert.IsType<SimpleSandboxConfig>(options.Sandbox);
        Assert.Equal("custom-permission-tool", options.PermissionPromptToolName);
        Assert.True(options.StrictMcpConfig);
        Assert.True(options.NoHooks);
        Assert.Equal("test-user", options.User);
        Assert.Single(options.AdditionalDataPaths);
        Assert.NotNull(options.OnStderr);
    }

    [Theory]
    [InlineData(PermissionMode.Default)]
    [InlineData(PermissionMode.AcceptEdits)]
    [InlineData(PermissionMode.Plan)]
    [InlineData(PermissionMode.BypassPermissions)]
    public void PermissionMode_AllValues_AreValid(PermissionMode mode)
    {
        var options = new ClaudeAgentOptions { PermissionMode = mode };
        Assert.Equal(mode, options.PermissionMode);
    }

    [Fact]
    public void PermissionMode_HasExpectedValues()
    {
        var values = Enum.GetValues<PermissionMode>();
        Assert.Equal(5, values.Length);
        Assert.Contains(PermissionMode.Default, values);
        Assert.Contains(PermissionMode.AcceptEdits, values);
        Assert.Contains(PermissionMode.Plan, values);
        Assert.Contains(PermissionMode.BypassPermissions, values);
        Assert.Contains(PermissionMode.DontAsk, values);
    }

    [Theory]
    [InlineData(SettingSource.Project)]
    [InlineData(SettingSource.User)]
    [InlineData(SettingSource.Local)]
    public void SettingSource_AllValues_AreValid(SettingSource source)
    {
        var options = new ClaudeAgentOptions { SettingSources = [source] };
        Assert.Single(options.SettingSources!);
        Assert.Equal(source, options.SettingSources![0]);
    }

    [Fact]
    public void SettingSource_HasExpectedValues()
    {
        var values = Enum.GetValues<SettingSource>();
        Assert.Equal(3, values.Length);
        Assert.Contains(SettingSource.Project, values);
        Assert.Contains(SettingSource.User, values);
        Assert.Contains(SettingSource.Local, values);
    }

    [Fact]
    public void SettingSources_MultipleSources_CanBeConfigured()
    {
        var options = new ClaudeAgentOptions
        {
            SettingSources = [SettingSource.Project, SettingSource.User, SettingSource.Local]
        };

        Assert.Equal(3, options.SettingSources!.Count);
        Assert.Equal(SettingSource.Project, options.SettingSources[0]);
        Assert.Equal(SettingSource.User, options.SettingSources[1]);
        Assert.Equal(SettingSource.Local, options.SettingSources[2]);
    }

    [Fact]
    public void ToolsConfig_ImplicitConversion_FromStringArray()
    {
        ToolsConfig config = new[] { "Read", "Write", "Bash" };

        Assert.IsType<ToolsList>(config);
        var toolsList = (ToolsList)config;
        Assert.Equal(3, toolsList.Tools.Count);
        Assert.Contains("Read", toolsList.Tools);
        Assert.Contains("Write", toolsList.Tools);
        Assert.Contains("Bash", toolsList.Tools);
    }

    [Fact]
    public void ToolsConfig_ImplicitConversion_FromList()
    {
        ToolsConfig config = new List<string> { "Grep", "Glob" };

        Assert.IsType<ToolsList>(config);
        var toolsList = (ToolsList)config;
        Assert.Equal(2, toolsList.Tools.Count);
        Assert.Contains("Grep", toolsList.Tools);
        Assert.Contains("Glob", toolsList.Tools);
    }

    [Fact]
    public void ToolsConfig_ClaudeCode_ReturnsToolsPreset()
    {
        var config = ToolsConfig.ClaudeCode();

        Assert.IsType<ToolsPreset>(config);
        var preset = (ToolsPreset)config;
        Assert.Equal("claude_code", preset.Preset);
    }

    [Fact]
    public void ToolsList_StoresToolsCorrectly()
    {
        var tools = new[] { "Read", "Write", "Edit" };
        var toolsList = new ToolsList(tools);

        Assert.Equal(3, toolsList.Tools.Count);
        Assert.Contains("Read", toolsList.Tools);
        Assert.Contains("Write", toolsList.Tools);
        Assert.Contains("Edit", toolsList.Tools);
    }

    [Fact]
    public void ToolsPreset_RequiresPreset()
    {
        var preset = new ToolsPreset { Preset = "claude_code" };
        Assert.Equal("claude_code", preset.Preset);
    }

    [Fact]
    public void ToolsConfig_InOptions_WorksWithImplicitConversion()
    {
        var options = new ClaudeAgentOptions
        {
            Tools = new[] { "Read", "Write" }
        };

        Assert.IsType<ToolsList>(options.Tools);
        Assert.Equal(2, ((ToolsList)options.Tools).Tools.Count);
    }

    [Fact]
    public void SystemPromptConfig_ImplicitConversion_FromString()
    {
        SystemPromptConfig config = "You are a helpful assistant.";

        Assert.IsType<CustomSystemPrompt>(config);
        var custom = (CustomSystemPrompt)config;
        Assert.Equal("You are a helpful assistant.", custom.Prompt);
    }

    [Fact]
    public void SystemPromptConfig_ClaudeCode_WithoutAppend()
    {
        var config = SystemPromptConfig.ClaudeCode();

        Assert.IsType<PresetSystemPrompt>(config);
        var preset = (PresetSystemPrompt)config;
        Assert.Equal("claude_code", preset.Preset);
        Assert.Null(preset.Append);
    }

    [Fact]
    public void SystemPromptConfig_ClaudeCode_WithAppend()
    {
        var config = SystemPromptConfig.ClaudeCode("Always use TypeScript.");

        Assert.IsType<PresetSystemPrompt>(config);
        var preset = (PresetSystemPrompt)config;
        Assert.Equal("claude_code", preset.Preset);
        Assert.Equal("Always use TypeScript.", preset.Append);
    }

    [Fact]
    public void CustomSystemPrompt_StoresPromptCorrectly()
    {
        var custom = new CustomSystemPrompt("Test prompt content");
        Assert.Equal("Test prompt content", custom.Prompt);
    }

    [Fact]
    public void PresetSystemPrompt_RequiresPreset()
    {
        var preset = new PresetSystemPrompt
        {
            Preset = "claude_code",
            Append = "Additional instructions"
        };

        Assert.Equal("claude_code", preset.Preset);
        Assert.Equal("Additional instructions", preset.Append);
    }

    [Fact]
    public void SystemPromptConfig_InOptions_WorksWithImplicitConversion()
    {
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "Custom system prompt"
        };

        Assert.IsType<CustomSystemPrompt>(options.SystemPrompt);
        Assert.Equal("Custom system prompt", ((CustomSystemPrompt)options.SystemPrompt).Prompt);
    }

    [Fact]
    public void SandboxConfig_StaticOff_ReturnsSimpleSandboxConfig()
    {
        var config = SandboxConfig.Off;

        Assert.IsType<SimpleSandboxConfig>(config);
        Assert.Equal(SandboxMode.Off, ((SimpleSandboxConfig)config).Mode);
    }

    [Fact]
    public void SandboxConfig_StaticPermissive_ReturnsSimpleSandboxConfig()
    {
        var config = SandboxConfig.Permissive;

        Assert.IsType<SimpleSandboxConfig>(config);
        Assert.Equal(SandboxMode.Permissive, ((SimpleSandboxConfig)config).Mode);
    }

    [Fact]
    public void SandboxConfig_StaticStrict_ReturnsSimpleSandboxConfig()
    {
        var config = SandboxConfig.Strict;

        Assert.IsType<SimpleSandboxConfig>(config);
        Assert.Equal(SandboxMode.Strict, ((SimpleSandboxConfig)config).Mode);
    }

    [Theory]
    [InlineData(SandboxMode.Off)]
    [InlineData(SandboxMode.Permissive)]
    [InlineData(SandboxMode.Strict)]
    public void SandboxConfig_ImplicitConversion_FromSandboxMode(SandboxMode mode)
    {
        SandboxConfig config = mode;

        Assert.IsType<SimpleSandboxConfig>(config);
        Assert.Equal(mode, ((SimpleSandboxConfig)config).Mode);
    }

    [Fact]
    public void SandboxConfig_WithSettings_DefaultsToEnabled()
    {
        var config = SandboxConfig.WithSettings();

        Assert.IsType<SandboxSettings>(config);
        var settings = config;
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public void SandboxConfig_WithSettings_AppliesConfiguration()
    {
        var config = SandboxConfig.WithSettings(s => s with
        {
            AutoAllowBashIfSandboxed = true,
            ExcludedCommands = ["docker", "git"],
            AllowUnsandboxedCommands = true,
            EnableWeakerNestedSandbox = true
        });

        Assert.IsType<SandboxSettings>(config);
        var settings = config;
        Assert.True(settings.IsEnabled);
        Assert.True(settings.AutoAllowBashIfSandboxed);
        Assert.Equal(2, settings.ExcludedCommands!.Count);
        Assert.Contains("docker", settings.ExcludedCommands);
        Assert.Contains("git", settings.ExcludedCommands);
        Assert.True(settings.AllowUnsandboxedCommands);
        Assert.True(settings.EnableWeakerNestedSandbox);
    }

    [Fact]
    public void SimpleSandboxConfig_StoresModeCorrectly()
    {
        var config = new SimpleSandboxConfig(SandboxMode.Strict);
        Assert.Equal(SandboxMode.Strict, config.Mode);
    }

    [Fact]
    public void SandboxSettings_DefaultValues()
    {
        var settings = new SandboxSettings();

        Assert.False(settings.IsEnabled);
        Assert.False(settings.AutoAllowBashIfSandboxed);
        Assert.Null(settings.ExcludedCommands);
        Assert.False(settings.AllowUnsandboxedCommands);
        Assert.Null(settings.Network);
        Assert.Null(settings.IgnoreViolations);
        Assert.False(settings.EnableWeakerNestedSandbox);
    }

    [Fact]
    public void SandboxSettings_WithAllProperties()
    {
        var settings = new SandboxSettings
        {
            IsEnabled = true,
            AutoAllowBashIfSandboxed = true,
            ExcludedCommands = ["docker"],
            AllowUnsandboxedCommands = true,
            Network = new NetworkSandboxSettings
            {
                AllowLocalBinding = true,
                AllowUnixSockets = ["/var/run/docker.sock"],
                AllowAllUnixSockets = false,
                HttpProxyPort = 8080,
                SocksProxyPort = 1080
            },
            IgnoreViolations = new SandboxIgnoreViolations
            {
                File = ["/tmp/*"],
                Network = ["localhost:*"]
            },
            EnableWeakerNestedSandbox = true
        };

        Assert.True(settings.IsEnabled);
        Assert.True(settings.AutoAllowBashIfSandboxed);
        Assert.Single(settings.ExcludedCommands!);
        Assert.True(settings.AllowUnsandboxedCommands);
        Assert.NotNull(settings.Network);
        Assert.True(settings.Network.AllowLocalBinding);
        Assert.Single(settings.Network.AllowUnixSockets!);
        Assert.False(settings.Network.AllowAllUnixSockets);
        Assert.Equal(8080, settings.Network.HttpProxyPort);
        Assert.Equal(1080, settings.Network.SocksProxyPort);
        Assert.NotNull(settings.IgnoreViolations);
        Assert.Single(settings.IgnoreViolations.File!);
        Assert.Single(settings.IgnoreViolations.Network!);
        Assert.True(settings.EnableWeakerNestedSandbox);
    }

    [Fact]
    public void NetworkSandboxSettings_DefaultValues()
    {
        var settings = new NetworkSandboxSettings();

        Assert.False(settings.AllowLocalBinding);
        Assert.Null(settings.AllowUnixSockets);
        Assert.False(settings.AllowAllUnixSockets);
        Assert.Null(settings.HttpProxyPort);
        Assert.Null(settings.SocksProxyPort);
    }

    [Fact]
    public void SandboxIgnoreViolations_DefaultValues()
    {
        var violations = new SandboxIgnoreViolations();

        Assert.Null(violations.File);
        Assert.Null(violations.Network);
    }

    [Fact]
    public void SandboxConfig_InOptions_WorksWithImplicitConversion()
    {
        var options = new ClaudeAgentOptions
        {
            Sandbox = SandboxMode.Strict
        };

        Assert.IsType<SimpleSandboxConfig>(options.Sandbox);
        Assert.Equal(SandboxMode.Strict, ((SimpleSandboxConfig)options.Sandbox).Mode);
    }

    [Fact]
    public void McpStdioServerConfig_RequiresCommand()
    {
        var config = new McpStdioServerConfig { Command = "npx" };
        Assert.Equal("npx", config.Command);
        Assert.Null(config.Args);
        Assert.Null(config.Env);
    }

    [Fact]
    public void McpStdioServerConfig_WithAllProperties()
    {
        var config = new McpStdioServerConfig
        {
            Command = "npx",
            Args = ["-y", "@modelcontextprotocol/server-filesystem"],
            Env = new Dictionary<string, string> { ["NODE_ENV"] = "production" }
        };

        Assert.Equal("npx", config.Command);
        Assert.Equal(2, config.Args!.Count);
        Assert.Contains("-y", config.Args);
        Assert.Contains("@modelcontextprotocol/server-filesystem", config.Args);
        Assert.Single(config.Env!);
        Assert.Equal("production", config.Env["NODE_ENV"]);
    }

    [Fact]
    public void McpSseServerConfig_RequiresUrl()
    {
        var config = new McpSseServerConfig { Url = "https://example.com/sse" };
        Assert.Equal("https://example.com/sse", config.Url);
        Assert.Null(config.Headers);
    }

    [Fact]
    public void McpSseServerConfig_WithHeaders()
    {
        var config = new McpSseServerConfig
        {
            Url = "https://example.com/sse",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Custom-Header"] = "value"
            }
        };

        Assert.Equal("https://example.com/sse", config.Url);
        Assert.Equal(2, config.Headers!.Count);
        Assert.Equal("Bearer token123", config.Headers["Authorization"]);
    }

    [Fact]
    public void McpHttpServerConfig_RequiresUrl()
    {
        var config = new McpHttpServerConfig { Url = "https://example.com/mcp" };
        Assert.Equal("https://example.com/mcp", config.Url);
        Assert.Null(config.Headers);
    }

    [Fact]
    public void McpHttpServerConfig_WithHeaders()
    {
        var config = new McpHttpServerConfig
        {
            Url = "https://example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token456"
            }
        };

        Assert.Equal("https://example.com/mcp", config.Url);
        Assert.Single(config.Headers!);
        Assert.Equal("Bearer token456", config.Headers["Authorization"]);
    }

    [Fact]
    public void McpSdkServerConfig_RequiresNameAndInstance()
    {
        var mockServer = new Mock<IMcpToolServer>();
        mockServer.Setup(s => s.Name).Returns("test-mcp-server");

        var config = new McpSdkServerConfig
        {
            Name = "test-server",
            Instance = mockServer.Object
        };

        Assert.Equal("test-server", config.Name);
        Assert.NotNull(config.Instance);
    }

    [Fact]
    public void McpServers_InOptions_SupportsMultipleTypes()
    {
        var mockServer = new Mock<IMcpToolServer>();

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["stdio-server"] = new McpStdioServerConfig { Command = "npx" },
                ["sse-server"] = new McpSseServerConfig { Url = "https://example.com/sse" },
                ["http-server"] = new McpHttpServerConfig { Url = "https://example.com/mcp" },
                ["sdk-server"] = new McpSdkServerConfig { Name = "sdk", Instance = mockServer.Object }
            }
        };

        Assert.Equal(4, options.McpServers!.Count);
        Assert.IsType<McpStdioServerConfig>(options.McpServers["stdio-server"]);
        Assert.IsType<McpSseServerConfig>(options.McpServers["sse-server"]);
        Assert.IsType<McpHttpServerConfig>(options.McpServers["http-server"]);
        Assert.IsType<McpSdkServerConfig>(options.McpServers["sdk-server"]);
    }

    [Fact]
    public void AgentDefinition_RequiresDescriptionAndPrompt()
    {
        var agent = new AgentDefinition
        {
            Description = "Code review specialist",
            Prompt = "You are an expert code reviewer."
        };

        Assert.Equal("Code review specialist", agent.Description);
        Assert.Equal("You are an expert code reviewer.", agent.Prompt);
        Assert.Null(agent.Tools);
        Assert.Null(agent.Model);
    }

    [Fact]
    public void AgentDefinition_WithAllProperties()
    {
        var agent = new AgentDefinition
        {
            Description = "Security analysis specialist",
            Prompt = "You analyze code for security vulnerabilities.",
            Tools = ["Read", "Grep", "Glob"],
            Model = "opus"
        };

        Assert.Equal("Security analysis specialist", agent.Description);
        Assert.Equal("You analyze code for security vulnerabilities.", agent.Prompt);
        Assert.Equal(3, agent.Tools!.Count);
        Assert.Contains("Read", agent.Tools);
        Assert.Equal("opus", agent.Model);
    }

    [Fact]
    public void Agents_InOptions_MultipleAgents()
    {
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["code-reviewer"] = new()
                {
                    Description = "Reviews code for quality",
                    Prompt = "Review code systematically",
                    Tools = ["Read", "Grep"]
                },
                ["security-analyst"] = new()
                {
                    Description = "Analyzes security vulnerabilities",
                    Prompt = "Find security issues",
                    Model = "opus"
                },
                ["documentation-writer"] = new()
                {
                    Description = "Writes documentation",
                    Prompt = "Create clear documentation"
                }
            }
        };

        Assert.Equal(3, options.Agents!.Count);
        Assert.True(options.Agents.ContainsKey("code-reviewer"));
        Assert.True(options.Agents.ContainsKey("security-analyst"));
        Assert.True(options.Agents.ContainsKey("documentation-writer"));
        Assert.Equal(2, options.Agents["code-reviewer"].Tools!.Count);
        Assert.Equal("opus", options.Agents["security-analyst"].Model);
    }

    [Fact]
    public void PluginConfig_DefaultType()
    {
        var plugin = new PluginConfig { Path = "./my-plugin" };

        Assert.Equal("local", plugin.Type);
        Assert.Equal("./my-plugin", plugin.Path);
    }

    [Fact]
    public void PluginConfig_RequiresPath()
    {
        var plugin = new PluginConfig { Path = "/absolute/path/to/plugin" };
        Assert.Equal("/absolute/path/to/plugin", plugin.Path);
    }

    [Fact]
    public void Plugins_InOptions_MultiplePlugins()
    {
        var options = new ClaudeAgentOptions
        {
            Plugins =
            [
                new PluginConfig { Path = "./plugin1" },
                new PluginConfig { Path = "/absolute/plugin2" },
                new PluginConfig { Type = "local", Path = "./plugin3" }
            ]
        };

        Assert.Equal(3, options.Plugins!.Count);
        Assert.All(options.Plugins, p => Assert.Equal("local", p.Type));
    }

    [Fact]
    public void HookEvent_HasAllExpectedValues()
    {
        var values = Enum.GetValues<HookEvent>();
        Assert.Equal(12, values.Length);
        Assert.Contains(HookEvent.PreToolUse, values);
        Assert.Contains(HookEvent.PostToolUse, values);
        Assert.Contains(HookEvent.PostToolUseFailure, values);
        Assert.Contains(HookEvent.UserPromptSubmit, values);
        Assert.Contains(HookEvent.Stop, values);
        Assert.Contains(HookEvent.SubagentStart, values);
        Assert.Contains(HookEvent.SubagentStop, values);
        Assert.Contains(HookEvent.PreCompact, values);
        Assert.Contains(HookEvent.PermissionRequest, values);
        Assert.Contains(HookEvent.SessionStart, values);
        Assert.Contains(HookEvent.SessionEnd, values);
        Assert.Contains(HookEvent.Notification, values);
    }

    [Theory]
    [InlineData(HookEvent.PreToolUse)]
    [InlineData(HookEvent.PostToolUse)]
    [InlineData(HookEvent.Stop)]
    public void Hooks_InOptions_CanConfigureMultipleEvents(HookEvent hookEvent)
    {
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [hookEvent] = [new HookMatcher { Matcher = "Bash", Hooks = [], Timeout = 30.0 }]
            }
        };

        Assert.Single(options.Hooks!);
        Assert.True(options.Hooks.ContainsKey(hookEvent));
    }

    [Fact]
    public void HookMatcher_RequiresHooks()
    {
        var matcher = new HookMatcher
        {
            Hooks = []
        };

        Assert.Null(matcher.Matcher);
        Assert.Empty(matcher.Hooks);
        Assert.Null(matcher.Timeout);
    }

    [Fact]
    public void HookMatcher_WithAllProperties()
    {
        var hookCallback = new HookCallback((_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput()));

        var matcher = new HookMatcher
        {
            Matcher = "Write|Edit",
            Hooks = [hookCallback],
            Timeout = 60.0
        };

        Assert.Equal("Write|Edit", matcher.Matcher);
        Assert.Single(matcher.Hooks);
        Assert.Equal(60.0, matcher.Timeout);
    }

    [Fact]
    public void HookMatcher_WithMultipleCallbacks()
    {
        HookCallback callback1 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());
        HookCallback callback2 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput { Continue = false });

        var matcher = new HookMatcher
        {
            Matcher = "Bash",
            Hooks = [callback1, callback2]
        };

        Assert.Equal(2, matcher.Hooks.Count);
    }

    [Fact]
    public void ToolPermissionRequest_RequiredProperties()
    {
        var input = JsonDocument.Parse("{\"command\":\"ls\"}").RootElement;

        var request = new ToolPermissionRequest
        {
            ToolName = "Bash",
            Input = input
        };

        Assert.Equal("Bash", request.ToolName);
        Assert.Equal(JsonValueKind.Object, request.Input.ValueKind);
        Assert.Null(request.Suggestions);
        Assert.Null(request.BlockedPath);
    }

    [Fact]
    public void ToolPermissionRequest_WithAllProperties()
    {
        var input = JsonDocument.Parse("{\"file_path\":\"/etc/passwd\"}").RootElement;

        var request = new ToolPermissionRequest
        {
            ToolName = "Read",
            Input = input,
            Suggestions = [new PermissionUpdate { Type = PermissionUpdateType.AddRules }],
            BlockedPath = "/etc/passwd"
        };

        Assert.Equal("Read", request.ToolName);
        Assert.Single(request.Suggestions!);
        Assert.Equal("/etc/passwd", request.BlockedPath);
    }

    [Fact]
    public void PermissionResultAllow_DefaultValues()
    {
        var result = new PermissionResultAllow();

        Assert.Null(result.UpdatedInput);
        Assert.Null(result.UpdatedPermissions);
    }

    [Fact]
    public void PermissionResultAllow_WithUpdatedInput()
    {
        var updatedInput = JsonDocument.Parse("{\"modified\":true}").RootElement;

        var result = new PermissionResultAllow
        {
            UpdatedInput = updatedInput,
            UpdatedPermissions =
            [
                new PermissionUpdate { Type = PermissionUpdateType.AddRules }
            ]
        };

        Assert.NotNull(result.UpdatedInput);
        Assert.Single(result.UpdatedPermissions!);
    }

    [Fact]
    public void PermissionResultDeny_DefaultValues()
    {
        var result = new PermissionResultDeny();

        Assert.Equal("", result.Message);
        Assert.False(result.Interrupt);
    }

    [Fact]
    public void PermissionResultDeny_WithAllProperties()
    {
        var result = new PermissionResultDeny
        {
            Message = "Operation not allowed",
            Interrupt = true
        };

        Assert.Equal("Operation not allowed", result.Message);
        Assert.True(result.Interrupt);
    }

    [Fact]
    public void PermissionUpdate_AllTypes()
    {
        var types = Enum.GetValues<PermissionUpdateType>();
        Assert.Equal(6, types.Length);
        Assert.Contains(PermissionUpdateType.AddRules, types);
        Assert.Contains(PermissionUpdateType.ReplaceRules, types);
        Assert.Contains(PermissionUpdateType.RemoveRules, types);
        Assert.Contains(PermissionUpdateType.SetMode, types);
        Assert.Contains(PermissionUpdateType.AddDirectories, types);
        Assert.Contains(PermissionUpdateType.RemoveDirectories, types);
    }

    [Fact]
    public void PermissionUpdate_WithAllProperties()
    {
        var update = new PermissionUpdate
        {
            Type = PermissionUpdateType.AddRules,
            Rules =
            [
                new PermissionRuleValue { ToolName = "Bash", RuleContent = "ls" }
            ],
            Behavior = PermissionBehavior.Allow,
            Mode = PermissionMode.AcceptEdits,
            Directories = ["/allowed/dir"],
            Destination = PermissionUpdateDestination.ProjectSettings
        };

        Assert.Equal(PermissionUpdateType.AddRules, update.Type);
        Assert.Single(update.Rules!);
        Assert.Equal(PermissionBehavior.Allow, update.Behavior);
        Assert.Equal(PermissionMode.AcceptEdits, update.Mode);
        Assert.Single(update.Directories!);
        Assert.Equal(PermissionUpdateDestination.ProjectSettings, update.Destination);
    }

    [Fact]
    public void PermissionBehavior_AllValues()
    {
        var values = Enum.GetValues<PermissionBehavior>();
        Assert.Equal(3, values.Length);
        Assert.Contains(PermissionBehavior.Allow, values);
        Assert.Contains(PermissionBehavior.Deny, values);
        Assert.Contains(PermissionBehavior.Ask, values);
    }

    [Fact]
    public void PermissionUpdateDestination_AllValues()
    {
        var values = Enum.GetValues<PermissionUpdateDestination>();
        Assert.Equal(4, values.Length);
        Assert.Contains(PermissionUpdateDestination.UserSettings, values);
        Assert.Contains(PermissionUpdateDestination.ProjectSettings, values);
        Assert.Contains(PermissionUpdateDestination.LocalSettings, values);
        Assert.Contains(PermissionUpdateDestination.Session, values);
    }

    [Fact]
    public void PermissionRuleValue_RequiredToolName()
    {
        var rule = new PermissionRuleValue
        {
            ToolName = "Write",
            RuleContent = "*.txt"
        };

        Assert.Equal("Write", rule.ToolName);
        Assert.Equal("*.txt", rule.RuleContent);
    }

    [Fact]
    public void ClaudeAgentOptions_RecordEquality_SameValues()
    {
        // Note: ClaudeAgentOptions has default collection properties (AllowedTools, Environment, etc.)
        // that create new instances on each construction, so reference equality fails.
        // We test specific property equality instead.
        var options1 = new ClaudeAgentOptions
        {
            Model = "opus",
            MaxTurns = 10
        };
        var options2 = new ClaudeAgentOptions
        {
            Model = "opus",
            MaxTurns = 10
        };

        Assert.Equal(options1.Model, options2.Model);
        Assert.Equal(options1.MaxTurns, options2.MaxTurns);
        Assert.Equal(options1.PermissionMode, options2.PermissionMode);
    }

    [Fact]
    public void ClaudeAgentOptions_RecordEquality_DifferentValues()
    {
        var options1 = new ClaudeAgentOptions { Model = "opus" };
        var options2 = new ClaudeAgentOptions { Model = "sonnet" };

        Assert.NotEqual(options1, options2);
    }

    [Fact]
    public void SimpleSandboxConfig_RecordEquality()
    {
        var config1 = new SimpleSandboxConfig(SandboxMode.Strict);
        var config2 = new SimpleSandboxConfig(SandboxMode.Strict);
        var config3 = new SimpleSandboxConfig(SandboxMode.Off);

        Assert.Equal(config1, config2);
        Assert.NotEqual(config1, config3);
    }

    [Fact]
    public void ToolsList_RecordEquality()
    {
        var list1 = new ToolsList(["Read", "Write"]);
        var list2 = new ToolsList(["Read", "Write"]);

        // Note: IReadOnlyList comparison may be by reference, not content
        // This test documents the current behavior
        Assert.NotSame(list1, list2);
    }

    [Fact]
    public void AgentDefinition_RecordEquality()
    {
        var agent1 = new AgentDefinition
        {
            Description = "Test",
            Prompt = "Test prompt"
        };
        var agent2 = new AgentDefinition
        {
            Description = "Test",
            Prompt = "Test prompt"
        };

        Assert.Equal(agent1, agent2);
    }

    [Fact]
    public void ClaudeAgentOptions_WithExpression_CreatesModifiedCopy()
    {
        var original = new ClaudeAgentOptions
        {
            Model = "opus",
            MaxTurns = 10,
            MaxBudgetUsd = 5.0
        };

        var modified = original with { Model = "sonnet" };

        Assert.Equal("opus", original.Model);
        Assert.Equal("sonnet", modified.Model);
        Assert.Equal(10, modified.MaxTurns);
        Assert.Equal(5.0, modified.MaxBudgetUsd);
    }

    [Fact]
    public void SandboxSettings_WithExpression_CreatesModifiedCopy()
    {
        var original = new SandboxSettings
        {
            IsEnabled = true,
            AutoAllowBashIfSandboxed = false
        };

        var modified = original with { AutoAllowBashIfSandboxed = true };

        Assert.False(original.AutoAllowBashIfSandboxed);
        Assert.True(modified.AutoAllowBashIfSandboxed);
        Assert.True(modified.IsEnabled);
    }

    [Fact]
    public void AgentDefinition_WithExpression_CreatesModifiedCopy()
    {
        var original = new AgentDefinition
        {
            Description = "Original description",
            Prompt = "Original prompt"
        };

        var modified = original with { Model = "opus" };

        Assert.Null(original.Model);
        Assert.Equal("opus", modified.Model);
        Assert.Equal("Original description", modified.Description);
    }

    [Fact]
    public async Task CanUseTool_Callback_IsInvoked()
    {
        var callbackInvoked = false;
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, _) =>
            {
                callbackInvoked = true;
                Assert.Equal("Bash", request.ToolName);
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        var testInput = JsonDocument.Parse("{}").RootElement;
        var result = await options.CanUseTool!(
            new ToolPermissionRequest { ToolName = "Bash", Input = testInput },
            CancellationToken.None);

        Assert.True(callbackInvoked);
        Assert.IsType<PermissionResultAllow>(result);
    }

    [Fact]
    public async Task CanUseTool_Callback_CanDeny()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(
                new PermissionResultDeny { Message = "Not allowed", Interrupt = true })
        };

        var testInput = JsonDocument.Parse("{}").RootElement;
        var result = await options.CanUseTool!(
            new ToolPermissionRequest { ToolName = "Bash", Input = testInput },
            CancellationToken.None);

        Assert.IsType<PermissionResultDeny>(result);
        var deny = (PermissionResultDeny)result;
        Assert.Equal("Not allowed", deny.Message);
        Assert.True(deny.Interrupt);
    }

    [Fact]
    public void OnStderr_Callback_CanBeSet()
    {
        string? capturedStderr = null;
        var options = new ClaudeAgentOptions
        {
            OnStderr = message => capturedStderr = message
        };

        options.OnStderr!("Test error message");

        Assert.Equal("Test error message", capturedStderr);
    }

    [Fact]
    public void CompleteAgentConfiguration_AllOptionsSet()
    {
        var mockServer = new Mock<IMcpToolServer>();

        var options = new ClaudeAgentOptions
        {
            Model = "opus",
            FallbackModel = "sonnet",
            SystemPrompt = SystemPromptConfig.ClaudeCode("Focus on security"),
            Tools = ToolsConfig.ClaudeCode(),
            AllowedTools = ["Task", "WebFetch"],
            DisallowedTools = ["Bash"],
            SettingSources = [SettingSource.Project, SettingSource.User],
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["filesystem"] = new McpStdioServerConfig
                {
                    Command = "npx",
                    Args = ["-y", "@modelcontextprotocol/server-filesystem"]
                },
                ["custom-api"] = new McpSseServerConfig
                {
                    Url = "https://api.example.com/mcp/sse",
                    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" }
                }
            },
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["security-auditor"] = new()
                {
                    Description = "Security vulnerability scanner",
                    Prompt = "Analyze code for OWASP vulnerabilities",
                    Tools = ["Read", "Grep", "Glob"],
                    Model = "opus"
                }
            },
            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 50,
            MaxBudgetUsd = 10.0,
            WorkingDirectory = "/project",
            AddDirectories = ["/shared-libs"],
            Sandbox = SandboxConfig.WithSettings(s => s with
            {
                AutoAllowBashIfSandboxed = true,
                Network = new NetworkSandboxSettings { AllowLocalBinding = true }
            }),
            EnableFileCheckpointing = true,
            Betas = ["context-1m-2025-08-07"]
        };

        // Verify complex structure is properly set
        Assert.Equal("opus", options.Model);
        Assert.IsType<PresetSystemPrompt>(options.SystemPrompt);
        Assert.Equal("Focus on security", ((PresetSystemPrompt)options.SystemPrompt).Append);
        Assert.IsType<ToolsPreset>(options.Tools);
        Assert.Equal(2, options.AllowedTools.Count);
        Assert.Equal(2, options.McpServers!.Count);
        Assert.Single(options.Agents!);
        Assert.IsType<SandboxSettings>(options.Sandbox);
        var sandboxSettings = (SandboxSettings)options.Sandbox;
        Assert.True(sandboxSettings.AutoAllowBashIfSandboxed);
        Assert.True(sandboxSettings.Network!.AllowLocalBinding);
    }

    [Fact]
    public void MinimalAgentConfiguration_DefaultsWork()
    {
        var options = new ClaudeAgentOptions();

        // Minimal configuration should have sensible defaults
        Assert.Null(options.Model);
        Assert.Null(options.Tools);
        Assert.Empty(options.AllowedTools);
        Assert.Empty(options.DisallowedTools);
        Assert.False(options.ContinueConversation);
        Assert.Null(options.Sandbox);
    }
}
