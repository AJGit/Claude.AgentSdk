using System.Text;
using System.Text.Json;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Transport;

namespace Claude.AgentSdk.Tests.Transport;

/// <summary>
///     Tests for CliArgumentsBuilder which constructs command-line arguments from ClaudeAgentOptions.
/// </summary>
public class CliArgumentsBuilderTests
{
    [Fact]
    public void Build_WithDefaultOptions_ReturnsBaseArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions();

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--output-format", args);
        Assert.Contains("stream-json", args);
        Assert.Contains("--verbose", args);
        Assert.Contains("--input-format", args);
    }

    [Fact]
    public void Build_WithPrompt_StillUsesStreamingInput()
    {
        // Arrange - prompts always travel over stdin, never argv
        var options = new ClaudeAgentOptions();
        var prompt = "Hello, Claude!";

        // Act
        var args = CliArgumentsBuilder.Create(options, prompt).Build();

        // Assert
        Assert.DoesNotContain("--print", args);
        Assert.DoesNotContain(prompt, args);
        Assert.Equal("--input-format", args[^2]);
        Assert.Equal("stream-json", args[^1]);
    }

    [Fact]
    public void Build_WithNullSystemPrompt_PassesEmptySystemPrompt()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions()).Build();

        var index = args.IndexOf("--system-prompt");
        Assert.True(index >= 0);
        Assert.Equal("", args[index + 1]);
    }

    [Fact]
    public void Build_WithFileSystemPrompt_AddsSystemPromptFileArg()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = SystemPromptConfig.FromFile("/tmp/prompt.md") };

        var args = CliArgumentsBuilder.Create(options).Build();

        var index = args.IndexOf("--system-prompt-file");
        Assert.Equal("/tmp/prompt.md", args[index + 1]);
        Assert.DoesNotContain("--system-prompt", args);
    }

    [Theory]
    [InlineData(EffortLevel.Low, "low")]
    [InlineData(EffortLevel.XHigh, "xhigh")]
    [InlineData(EffortLevel.Max, "max")]
    public void Build_WithEffort_AddsEffortArg(EffortLevel effort, string expected)
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { Effort = effort }).Build();

        var index = args.IndexOf("--effort");
        Assert.Equal(expected, args[index + 1]);
    }

    [Fact]
    public void Build_WithAdaptiveThinking_AddsThinkingAndDisplayArgs()
    {
        var options = new ClaudeAgentOptions
        {
            Thinking = ThinkingConfig.Adaptive(ThinkingDisplay.Summarized),
            MaxThinkingTokens = 999 // ignored when Thinking is set
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        Assert.Equal("adaptive", args[args.IndexOf("--thinking") + 1]);
        Assert.Equal("summarized", args[args.IndexOf("--thinking-display") + 1]);
        Assert.DoesNotContain("--max-thinking-tokens", args);
    }

    [Fact]
    public void Build_WithEnabledThinking_UsesMaxThinkingTokens()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { Thinking = ThinkingConfig.Enabled(4096) })
            .Build();

        Assert.Equal("4096", args[args.IndexOf("--max-thinking-tokens") + 1]);
        Assert.DoesNotContain("--thinking", args);
    }

    [Fact]
    public void Build_WithDisabledThinking_AddsThinkingDisabled()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { Thinking = ThinkingConfig.Disabled }).Build();

        Assert.Equal("disabled", args[args.IndexOf("--thinking") + 1]);
    }

    [Fact]
    public void Build_WithTaskBudget_AddsTaskBudgetArg()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { TaskBudget = 50000 }).Build();

        Assert.Equal("50000", args[args.IndexOf("--task-budget") + 1]);
    }

    [Fact]
    public void Build_WithNewStreamingFlags_AddsFlags()
    {
        var options = new ClaudeAgentOptions
        {
            IncludeHookEvents = true,
            ResumeDropsTurn = "prompt-uuid"
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        Assert.Contains("--include-hook-events", args);
        Assert.Contains("--resume-drops-turn=prompt-uuid", args);
    }

    [Fact]
    public void Build_WithAllSkills_AddsSkillToolAndDefaultSettingSources()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { Skills = SkillsConfig.All }).Build();

        Assert.Equal("Skill", args[args.IndexOf("--allowedTools") + 1]);
        Assert.Contains("--setting-sources=user,project", args);
    }

    [Fact]
    public void Build_WithSkillList_AddsSkillRulesAndKeepsExplicitSettingSources()
    {
        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Read"],
            Skills = SkillsConfig.Only("pdf", "plugin:review"),
            SettingSources = [SettingSource.Project]
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        Assert.Equal("Read,Skill(pdf),Skill(plugin:review)", args[args.IndexOf("--allowedTools") + 1]);
        Assert.Contains("--setting-sources=project", args);
    }

    [Theory]
    [InlineData("a,b")]
    [InlineData("a(b)")]
    [InlineData(" pdf")]
    [InlineData("*")]
    [InlineData("plugin:*")]
    [InlineData("/pdf")]
    [InlineData("")]
    public void Build_WithInvalidSkillName_Throws(string name)
    {
        var options = new ClaudeAgentOptions { Skills = SkillsConfig.Only(name) };

        Assert.Throws<ArgumentException>(() => CliArgumentsBuilder.Create(options).Build());
    }

    [Fact]
    public void Build_WithEmptySettingSources_DisablesAllSources()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { SettingSources = [] }).Build();

        Assert.Contains("--setting-sources=", args);
    }

    [Fact]
    public void Build_WithCanUseTool_RoutesPermissionPromptsOverStdio()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<Claude.AgentSdk.Protocol.PermissionResult>(new Claude.AgentSdk.Protocol.PermissionResultAllow())
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        Assert.Equal("stdio", args[args.IndexOf("--permission-prompt-tool") + 1]);
    }

    [Fact]
    public void Build_WithDashLeadingExtraArgValue_UsesEqualsForm()
    {
        var options = new ClaudeAgentOptions
        {
            ExtraArgs = new Dictionary<string, string?> { ["some-flag"] = "-value", ["other"] = "plain" }
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        Assert.Contains("--some-flag=-value", args);
        Assert.Equal("plain", args[args.IndexOf("--other") + 1]);
    }

    [Fact]
    public void Build_WithSettingsAndSandbox_MergesIntoSettingsJson()
    {
        var options = new ClaudeAgentOptions
        {
            Settings = """{"model":"opus"}""",
            Sandbox = new SandboxSettings { IsEnabled = true }
        };

        var args = CliArgumentsBuilder.Create(options).Build();

        var settings = JsonDocument.Parse(args[args.IndexOf("--settings") + 1]).RootElement;
        Assert.Equal("opus", settings.GetProperty("model").GetString());
        Assert.True(settings.GetProperty("sandbox").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Build_WithSettingsPathOnly_PassesThrough()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { Settings = "/etc/claude.json" }).Build();

        Assert.Equal("/etc/claude.json", args[args.IndexOf("--settings") + 1]);
    }

    [Fact]
    public void Build_WithMessagesApiOutputFormat_ExtractsSchema()
    {
        var format = JsonDocument.Parse("""{"type":"json_schema","schema":{"type":"object"}}""").RootElement;

        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { OutputFormat = format }).Build();

        var schema = JsonDocument.Parse(args[args.IndexOf("--json-schema") + 1]).RootElement;
        Assert.Equal("object", schema.GetProperty("type").GetString());
    }

    [Fact]
    public void Build_WithModel_AddsModelArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions { Model = "opus" };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--model", args);
        Assert.Contains("opus", args);
    }

    [Fact]
    public void Build_WithToolsList_AddsToolsArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsList(["Read", "Write", "Bash"])
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--tools", args);
        Assert.Contains("Read,Write,Bash", args);
    }

    [Fact]
    public void Build_WithEmptyToolsList_AddsEmptyToolsArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsList([])
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        var toolsIndex = args.IndexOf("--tools");
        Assert.NotEqual(-1, toolsIndex);
        Assert.True(toolsIndex + 1 < args.Count);
        Assert.Equal("", args[toolsIndex + 1]);
    }

    [Fact]
    public void Build_WithToolsPreset_AddsDefaultTools()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsPreset { Preset = "claude_code" }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - the claude_code preset maps to the CLI's default tool set
        Assert.Contains("--tools", args);
        var toolsIndex = args.IndexOf("--tools");
        Assert.Equal("default", args[toolsIndex + 1]);
    }

    [Fact]
    public void Build_WithAllowedTools_AddsAllowedToolsArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["mcp__custom_tool"]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--allowedTools", args);
        Assert.Contains("mcp__custom_tool", args);
    }

    [Fact]
    public void Build_WithDisallowedTools_AddsDisallowedToolsArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            DisallowedTools = ["Bash"]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--disallowedTools", args);
        Assert.Contains("Bash", args);
    }

    [Fact]
    public void Build_WithCustomSystemPrompt_AddsSystemPromptArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new CustomSystemPrompt("You are a helpful assistant.")
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--system-prompt", args);
        Assert.Contains("You are a helpful assistant.", args);
    }

    [Fact]
    public void Build_WithPresetSystemPrompt_AddsPresetAndAppendArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new PresetSystemPrompt
            {
                Preset = "claude_code",
                Append = "Always use TypeScript."
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - the preset is the CLI default prompt; only the appended text travels on argv
        Assert.DoesNotContain("--system-prompt", args);
        Assert.DoesNotContain("claude_code", args);
        Assert.Contains("--append-system-prompt", args);
        Assert.Contains("Always use TypeScript.", args);
    }

    [Theory]
    [InlineData(SettingSource.Project, "project")]
    [InlineData(SettingSource.User, "user")]
    [InlineData(SettingSource.Local, "local")]
    public void Build_WithSettingSources_AddsCorrectSourceNames(SettingSource source, string expected)
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SettingSources = [source]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains($"--setting-sources={expected}", args);
    }

    [Fact]
    public void Build_WithMultipleSettingSources_JoinsWithComma()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SettingSources = [SettingSource.Project, SettingSource.User]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--setting-sources=project,user", args);
    }

    [Fact]
    public void Build_WithAdditionalDataPaths_AddsMultipleArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            AdditionalDataPaths = ["/path/one", "/path/two"]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - the CLI has no --additional-data-path flag; --add-dir is where it loads CLAUDE.md from
        Assert.DoesNotContain("--additional-data-path", args);
        var pathArgCount = args.Count(a => a == "--add-dir");
        Assert.Equal(2, pathArgCount);
        Assert.Contains("/path/one", args);
        Assert.Contains("/path/two", args);
    }

    [Theory]
    [InlineData(PermissionMode.Default, "default")]
    [InlineData(PermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(PermissionMode.Plan, "plan")]
    [InlineData(PermissionMode.BypassPermissions, "bypassPermissions")]
    [InlineData(PermissionMode.DontAsk, "dontAsk")]
    [InlineData(PermissionMode.Auto, "auto")]
    public void Build_WithPermissionMode_AddsCorrectModeValue(PermissionMode mode, string expected)
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            PermissionMode = mode
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--permission-mode", args);
        Assert.Contains(expected, args);
    }

    [Fact]
    public void Build_WithBetas_AddsBetasArgWithCommaSeparatedValues()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Betas = ["context-1m-2025-08-07", "another-beta"]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--betas", args);
        var betasIndex = args.IndexOf("--betas");
        Assert.True(betasIndex >= 0 && betasIndex < args.Count - 1);
        Assert.Equal("context-1m-2025-08-07,another-beta", args[betasIndex + 1]);
    }

    [Theory]
    [InlineData(SandboxMode.Off, false)]
    [InlineData(SandboxMode.Permissive, true)]
    [InlineData(SandboxMode.Strict, true)]
    public void Build_WithSimpleSandbox_AddsSandboxSettings(SandboxMode mode, bool expectedEnabled)
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Sandbox = new SimpleSandboxConfig(mode)
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - sandbox settings travel through --settings
        Assert.DoesNotContain("--sandbox", args);
        var settings = JsonDocument.Parse(args[args.IndexOf("--settings") + 1]).RootElement;
        Assert.Equal(expectedEnabled, settings.GetProperty("sandbox").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Build_WithDetailedSandbox_AddsSandboxSettingsJson()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Sandbox = new SandboxSettings
            {
                IsEnabled = true,
                AutoAllowBashIfSandboxed = true,
                ExcludedCommands = ["docker", "podman"]
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - sandbox settings travel as the "sandbox" object of --settings
        Assert.Contains("--settings", args);
        var configIndex = args.IndexOf("--settings");
        var configJson = args[configIndex + 1];

        // Dictionary keys are preserved as camelCase from ConfigSerializerExtensions
        var doc = JsonDocument.Parse(configJson);
        var sandbox = doc.RootElement.GetProperty("sandbox");

        Assert.True(sandbox.TryGetProperty("enabled", out var enabledProp),
            $"JSON missing 'enabled'. Actual JSON: {configJson}");
        Assert.True(enabledProp.GetBoolean());
        Assert.True(sandbox.TryGetProperty("autoAllowBashIfSandboxed", out var autoAllowProp),
            $"JSON missing 'autoAllowBashIfSandboxed'. Actual JSON: {configJson}");
        Assert.True(autoAllowProp.GetBoolean());
    }

    [Fact]
    public void Build_WithMiscOptions_AddsCorrectArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            PermissionPromptToolName = "custom_perm_tool",
            StrictMcpConfig = true,
            NoHooks = true,
            User = "test-user",
            MaxTurns = 5
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - NoHooks and User have no CLI flags (the CLI rejects unknown options)
        Assert.Contains("--permission-prompt-tool", args);
        Assert.Contains("custom_perm_tool", args);
        Assert.Contains("--strict-mcp-config", args);
        Assert.DoesNotContain("--no-hooks", args);
        Assert.DoesNotContain("--user", args);
        Assert.DoesNotContain("test-user", args);
        var settings = JsonDocument.Parse(args[args.IndexOf("--settings") + 1]).RootElement;
        Assert.True(settings.GetProperty("disableAllHooks").GetBoolean());
        Assert.Contains("--max-turns", args);
        Assert.Contains("5", args);
    }

    [Fact]
    public void Build_WithSessionResume_AddsResumeArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Resume = "session-123",
            ForkSession = true,
            ResumeSessionAt = "message-456"
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - values are bound with '=' so a dash-leading value can't be parsed as a flag
        Assert.Contains("--resume=session-123", args);
        Assert.Contains("--fork-session", args);
        Assert.Contains("--resume-session-at=message-456", args);
    }

    [Fact]
    public void Build_WithSessionId_UsesEqualsForm()
    {
        var args = CliArgumentsBuilder.Create(new ClaudeAgentOptions { SessionId = "-injected" }).Build();

        Assert.Contains("--session-id=-injected", args);
    }

    [Fact]
    public void Build_WithWindowsMetacharactersInResume_ThrowsOnWindows()
    {
        var options = new ClaudeAgentOptions { Resume = "abc & calc.exe" };

        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<ArgumentException>(() => CliArgumentsBuilder.Create(options).Build());
        }
        else
        {
            Assert.Contains("--resume=abc & calc.exe", CliArgumentsBuilder.Create(options).Build());
        }
    }

    [Fact]
    public void Build_WithContinueConversation_AddsContinueArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            ContinueConversation = true
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--continue", args);
    }

    [Fact]
    public void Build_WithAddDirectories_AddsMultipleAddDirArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            AddDirectories = ["/dir/one", "/dir/two"]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        var addDirCount = args.Count(a => a == "--add-dir");
        Assert.Equal(2, addDirCount);
        Assert.Contains("/dir/one", args);
        Assert.Contains("/dir/two", args);
    }

    [Fact]
    public void Build_WithOutputOptions_AddsCorrectArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true,
            MaxThinkingTokens = 1000
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--include-partial-messages", args);
        Assert.Contains("--max-thinking-tokens", args);
        Assert.Contains("1000", args);
    }

    [Fact]
    public void Build_WithAgents_DoesNotPassAgentsOnArgv()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["code-reviewer"] = new()
                {
                    Description = "Code review specialist",
                    Prompt = "You are a code reviewer",
                    Tools = ["Read", "Grep"],
                    Model = "sonnet"
                }
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - agents are sent via the initialize control request, not argv
        Assert.DoesNotContain("--agents", args);
        var agent = options.Agents!["code-reviewer"].ToCliDictionary();
        Assert.Equal("Code review specialist", agent["description"]);
        Assert.Equal("You are a code reviewer", agent["prompt"]);
        Assert.Equal("sonnet", agent["model"]);
    }

    [Fact]
    public void Build_WithPlugins_AddsPluginDirArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Plugins =
            [
                new PluginConfig { Type = "local", Path = "./my-plugin" }
            ]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Equal(1, args.Count(a => a == "--plugin-dir"));
        Assert.Equal("./my-plugin", args[args.IndexOf("--plugin-dir") + 1]);
    }

    [Fact]
    public void Build_WithMcpServers_AddsMcpConfigJsonArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["my-server"] = new McpStdioServerConfig
                {
                    Command = "node",
                    Args = ["server.js"],
                    Env = new Dictionary<string, string> { ["NODE_ENV"] = "production" }
                }
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--mcp-config", args);
        var mcpIndex = args.IndexOf("--mcp-config");
        var mcpJson = args[mcpIndex + 1];

        var doc = JsonDocument.Parse(mcpJson);
        Assert.True(doc.RootElement.TryGetProperty("mcpServers", out var servers));
        Assert.True(servers.TryGetProperty("my-server", out var server));
        Assert.Equal("node", server.GetProperty("command").GetString());
    }

    [Fact]
    public void Build_WithExtraArgs_AppendsExtraArgs()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            ExtraArgs = new Dictionary<string, string?>
            {
                ["custom-flag"] = "value",
                ["boolean-flag"] = null
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--custom-flag", args);
        Assert.Contains("value", args);
        Assert.Contains("--boolean-flag", args);
    }

    [Fact]
    public void GetArgs_ReturnsNewListInstance()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var builder = CliArgumentsBuilder.Create(options);
        builder.Build();

        // Act
        var args1 = builder.GetArgs();
        var args2 = builder.GetArgs();

        // Assert
        Assert.NotSame(args1, args2);
        Assert.Equal(args1, args2);
    }
}

/// <summary>
///     Tests for ProcessStartInfoBuilder which creates ProcessStartInfo for the Claude CLI.
/// </summary>
public class ProcessStartInfoBuilderTests
{
    [Fact]
    public void Build_WithCliPath_SetsFileName()
    {
        // Arrange
        var cliPath = "/usr/local/bin/claude";
        var args = new List<string> { "--verbose" };

        // Act
        var startInfo = ProcessStartInfoBuilder.Create(cliPath, args).Build();

        // Assert
        Assert.Equal(cliPath, startInfo.FileName);
    }

    [Fact]
    public void Build_ConfiguresStdioRedirection()
    {
        // Arrange
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", []).Build();

        // Assert
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public void Build_SetsUTF8Encoding()
    {
        // Arrange & Act
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", []).Build();

        // Assert
        Assert.NotNull(startInfo.StandardInputEncoding);
        Assert.NotNull(startInfo.StandardOutputEncoding);
        Assert.NotNull(startInfo.StandardErrorEncoding);
        Assert.Equal(Encoding.UTF8.EncodingName, startInfo.StandardInputEncoding!.EncodingName);
        Assert.Equal(Encoding.UTF8.EncodingName, startInfo.StandardOutputEncoding!.EncodingName);
        Assert.Equal(Encoding.UTF8.EncodingName, startInfo.StandardErrorEncoding!.EncodingName);
    }

    [Fact]
    public void WithWorkingDirectory_SetsWorkingDirectory()
    {
        // Arrange
        var workingDir = "/home/user/project";

        // Act
        var startInfo = ProcessStartInfoBuilder
            .Create("/bin/claude", [])
            .WithWorkingDirectory(workingDir)
            .Build();

        // Assert
        Assert.Equal(workingDir, startInfo.WorkingDirectory);
    }

    [Fact]
    public void WithWorkingDirectory_NullOrEmpty_UsesDefaultDirectory()
    {
        // Arrange & Act
        var startInfo = ProcessStartInfoBuilder
            .Create("/bin/claude", [])
            .WithWorkingDirectory(null)
            .Build();

        // Assert
        Assert.Equal(Environment.CurrentDirectory, startInfo.WorkingDirectory);
    }

    [Fact]
    public void WithEnvironment_AddsEnvironmentVariables()
    {
        // Arrange
        var env = new Dictionary<string, string>
        {
            ["MY_VAR"] = "my_value",
            ["ANOTHER_VAR"] = "another_value"
        };

        // Act
        var startInfo = ProcessStartInfoBuilder
            .Create("/bin/claude", [])
            .WithEnvironment(env)
            .Build();

        // Assert
        Assert.Equal("my_value", startInfo.Environment["MY_VAR"]);
        Assert.Equal("another_value", startInfo.Environment["ANOTHER_VAR"]);
    }

    [Fact]
    public void WithEnvironmentVariable_AddsSingleVariable()
    {
        // Arrange & Act
        var startInfo = ProcessStartInfoBuilder
            .Create("/bin/claude", [])
            .WithEnvironmentVariable("CUSTOM_KEY", "custom_value")
            .Build();

        // Assert
        Assert.Equal("custom_value", startInfo.Environment["CUSTOM_KEY"]);
    }

    [Fact]
    public void Build_AlwaysSetsClaudeCodeEntrypoint()
    {
        // Arrange & Act
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", []).Build();

        // Assert
        Assert.Equal("sdk-csharp", startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"]);
    }

    [Fact]
    public void Build_AddsArgsToArgumentList()
    {
        // Arrange
        var args = new List<string> { "--verbose", "--model", "opus" };

        // Act
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", args).Build();

        // Assert
        Assert.Contains("--verbose", startInfo.ArgumentList);
        Assert.Contains("--model", startInfo.ArgumentList);
        Assert.Contains("opus", startInfo.ArgumentList);
    }

    [Fact]
    public void Build_FluentChaining_ReturnsCorrectBuilder()
    {
        // Arrange & Act
        var builder = ProcessStartInfoBuilder.Create("/bin/claude", []);
        var result = builder
            .WithWorkingDirectory("/some/dir")
            .WithEnvironment(new Dictionary<string, string>())
            .WithEnvironmentVariable("KEY", "VALUE");

        // Assert - fluent chaining returns the same builder
        Assert.Same(builder, result);
    }
}

/// <summary>
///     Tests for ConfigSerializerExtensions which convert configuration objects to CLI-compatible dictionaries.
/// </summary>
public class ConfigSerializerExtensionsTests
{
    [Fact]
    public void SandboxSettings_ToCliDictionary_EmptySettings_ReturnsEmptyDictionary()
    {
        // Arrange
        var settings = new SandboxSettings();

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.Empty(dict);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithEnabled_AddsEnabled()
    {
        // Arrange
        var settings = new SandboxSettings { IsEnabled = true };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True((bool)dict["enabled"]);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithAutoAllowBash_AddsAutoAllow()
    {
        // Arrange
        var settings = new SandboxSettings { AutoAllowBashIfSandboxed = true };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True((bool)dict["autoAllowBashIfSandboxed"]);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithExcludedCommands_AddsCommands()
    {
        // Arrange
        var settings = new SandboxSettings
        {
            ExcludedCommands = ["docker", "podman"]
        };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        var commands = (IReadOnlyList<string>)dict["excludedCommands"];
        Assert.Equal(2, commands.Count);
        Assert.Contains("docker", commands);
        Assert.Contains("podman", commands);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithAllowUnsandboxed_AddsFlag()
    {
        // Arrange
        var settings = new SandboxSettings { AllowUnsandboxedCommands = true };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True((bool)dict["allowUnsandboxedCommands"]);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithWeakerNested_AddsFlag()
    {
        // Arrange
        var settings = new SandboxSettings { EnableWeakerNestedSandbox = true };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True((bool)dict["enableWeakerNestedSandbox"]);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithNetworkSettings_AddsNetwork()
    {
        // Arrange
        var settings = new SandboxSettings
        {
            Network = new NetworkSandboxSettings { AllowLocalBinding = true }
        };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True(dict.ContainsKey("network"));
        var network = (Dictionary<string, object>)dict["network"];
        Assert.True((bool)network["allowLocalBinding"]);
    }

    [Fact]
    public void SandboxSettings_ToCliDictionary_WithIgnoreViolations_AddsViolations()
    {
        // Arrange
        var settings = new SandboxSettings
        {
            IgnoreViolations = new SandboxIgnoreViolations
            {
                File = ["/tmp/*"],
                Network = ["localhost"]
            }
        };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True(dict.ContainsKey("ignoreViolations"));
        var violations = (Dictionary<string, object>)dict["ignoreViolations"];
        Assert.Contains("/tmp/*", (IReadOnlyList<string>)violations["file"]);
        Assert.Contains("localhost", (IReadOnlyList<string>)violations["network"]);
    }

    [Fact]
    public void NetworkSandboxSettings_ToCliDictionary_EmptySettings_ReturnsEmptyDictionary()
    {
        // Arrange
        var settings = new NetworkSandboxSettings();

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.Empty(dict);
    }

    [Fact]
    public void NetworkSandboxSettings_ToCliDictionary_WithAllSettings_AddsAllProperties()
    {
        // Arrange
        var settings = new NetworkSandboxSettings
        {
            AllowLocalBinding = true,
            AllowUnixSockets = ["/var/run/docker.sock"],
            AllowAllUnixSockets = true,
            HttpProxyPort = 8080,
            SocksProxyPort = 1080
        };

        // Act
        var dict = settings.ToCliDictionary();

        // Assert
        Assert.True((bool)dict["allowLocalBinding"]);
        Assert.Contains("/var/run/docker.sock", (IReadOnlyList<string>)dict["allowUnixSockets"]);
        Assert.True((bool)dict["allowAllUnixSockets"]);
        Assert.Equal(8080, dict["httpProxyPort"]);
        Assert.Equal(1080, dict["socksProxyPort"]);
    }

    [Fact]
    public void SandboxIgnoreViolations_ToCliDictionary_EmptySettings_ReturnsEmptyDictionary()
    {
        // Arrange
        var violations = new SandboxIgnoreViolations();

        // Act
        var dict = violations.ToCliDictionary();

        // Assert
        Assert.Empty(dict);
    }

    [Fact]
    public void SandboxIgnoreViolations_ToCliDictionary_WithBothTypes_AddsBoth()
    {
        // Arrange
        var violations = new SandboxIgnoreViolations
        {
            File = ["/etc/passwd"],
            Network = ["10.0.0.0/8"]
        };

        // Act
        var dict = violations.ToCliDictionary();

        // Assert
        Assert.Equal(2, dict.Count);
        Assert.Contains("/etc/passwd", (IReadOnlyList<string>)dict["file"]);
        Assert.Contains("10.0.0.0/8", (IReadOnlyList<string>)dict["network"]);
    }

    [Fact]
    public void McpStdioServerConfig_ToCliDictionary_MinimalConfig_HasCommand()
    {
        // Arrange
        var config = new McpStdioServerConfig { Command = "node" };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Equal("node", dict["command"]);
        Assert.False(dict.ContainsKey("args"));
        Assert.False(dict.ContainsKey("env"));
    }

    [Fact]
    public void McpStdioServerConfig_ToCliDictionary_FullConfig_HasAllProperties()
    {
        // Arrange
        var config = new McpStdioServerConfig
        {
            Command = "python",
            Args = ["-m", "my_server"],
            Env = new Dictionary<string, string> { ["PYTHONPATH"] = "/lib" }
        };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Equal("python", dict["command"]);
        var args = (IReadOnlyList<string>)dict["args"];
        Assert.Equal(2, args.Count);
        var env = (IReadOnlyDictionary<string, string>)dict["env"];
        Assert.Equal("/lib", env["PYTHONPATH"]);
    }

    [Fact]
    public void McpSseServerConfig_ToCliDictionary_MinimalConfig_HasTypeAndUrl()
    {
        // Arrange
        var config = new McpSseServerConfig { Url = "https://api.example.com/sse" };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Equal("sse", dict["type"]);
        Assert.Equal("https://api.example.com/sse", dict["url"]);
    }

    [Fact]
    public void McpSseServerConfig_ToCliDictionary_WithHeaders_HasHeaders()
    {
        // Arrange
        var config = new McpSseServerConfig
        {
            Url = "https://api.example.com/sse",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" }
        };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        var headers = (IReadOnlyDictionary<string, string>)dict["headers"];
        Assert.Equal("Bearer token", headers["Authorization"]);
    }

    [Fact]
    public void McpHttpServerConfig_ToCliDictionary_MinimalConfig_HasTypeAndUrl()
    {
        // Arrange
        var config = new McpHttpServerConfig { Url = "https://api.example.com/mcp" };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Equal("http", dict["type"]);
        Assert.Equal("https://api.example.com/mcp", dict["url"]);
    }

    [Fact]
    public void McpHttpServerConfig_ToCliDictionary_WithHeaders_HasHeaders()
    {
        // Arrange
        var config = new McpHttpServerConfig
        {
            Url = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string> { ["X-API-Key"] = "secret" }
        };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        var headers = (IReadOnlyDictionary<string, string>)dict["headers"];
        Assert.Equal("secret", headers["X-API-Key"]);
    }

    [Fact]
    public void McpSdkServerConfig_ToCliDictionary_HasTypeAndName()
    {
        // Arrange
        var config = new McpSdkServerConfig
        {
            Name = "my-sdk-server",
            Instance = null! // Instance is not serialized to CLI
        };

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Equal("sdk", dict["type"]);
        Assert.Equal("my-sdk-server", dict["name"]);
    }

    [Fact]
    public void McpServerConfig_ToCliDictionary_BaseClass_ReturnsNull()
    {
        // Arrange - create a mock derived type not handled by the extension
        McpServerConfig config = new UnknownMcpServerConfig();

        // Act
        var dict = config.ToCliDictionary();

        // Assert
        Assert.Null(dict);
    }

    // Test helper - unknown MCP server type
    private sealed record UnknownMcpServerConfig : McpServerConfig;
}

/// <summary>
///     Tests for CliPathResolver which finds the Claude CLI executable.
///     Note: These tests may need to be adjusted based on the actual file system state.
/// </summary>
public class CliPathResolverTests
{
    [Fact]
    public void Resolve_WithExplicitValidPath_ReturnsPath()
    {
        // This test requires a real file to exist
        // We use a common system file that exists on most platforms
        string testPath;
        if (OperatingSystem.IsWindows())
        {
            testPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        }
        else
        {
            testPath = "/bin/sh";
        }

        if (!File.Exists(testPath))
        {
            // Skip if test file doesn't exist
            return;
        }

        // Act
        var result = CliPathResolver.Create(testPath).Resolve();

        // Assert
        Assert.Equal(testPath, result);
    }

    [Fact]
    public void Resolve_WithExplicitInvalidPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent_claude_" + Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<CliNotFoundException>(() =>
            CliPathResolver.Create(invalidPath).Resolve());

        Assert.Contains("not found at specified path", ex.Message);
    }

    [Fact]
    public void Resolve_WithNullPath_SearchesPATH()
    {
        // This test verifies behavior when no explicit path is provided
        // It may throw CliNotFoundException if Claude is not installed
        // or return a path if Claude is installed

        // Arrange
        var resolver = CliPathResolver.Create();

        // Act & Assert
        try
        {
            var result = resolver.Resolve();
            // If it doesn't throw, verify it returned a valid path
            Assert.NotNull(result);
            Assert.True(File.Exists(result), $"Resolved path should exist: {result}");
        }
        catch (CliNotFoundException ex)
        {
            // Expected if Claude is not installed
            Assert.Contains("Claude CLI not found", ex.Message);
            Assert.Contains("npm install", ex.Message);
        }
    }

    [Fact]
    public void Create_ReturnsNewInstance()
    {
        // Arrange & Act
        var resolver1 = CliPathResolver.Create("/path/one");
        var resolver2 = CliPathResolver.Create("/path/two");

        // Assert
        Assert.NotSame(resolver1, resolver2);
    }
}

/// <summary>
///     Tests that verify the expected contract of ITransport implementations.
///     These use Moq to create mock implementations.
/// </summary>
public class ITransportContractTests
{
    [Fact]
    public void ITransport_HasExpectedMembers()
    {
        // Verify the interface has all expected members through reflection
        var interfaceType = typeof(ITransport);

        // Properties
        Assert.NotNull(interfaceType.GetProperty("IsReady"));

        // Methods
        Assert.NotNull(interfaceType.GetMethod("ConnectAsync"));
        Assert.NotNull(interfaceType.GetMethod("WriteAsync", [typeof(JsonDocument), typeof(CancellationToken)]));
        Assert.NotNull(interfaceType.GetMethod("ReadMessagesAsync"));
        Assert.NotNull(interfaceType.GetMethod("EndInputAsync"));
        Assert.NotNull(interfaceType.GetMethod("CloseAsync"));

        // Verify IAsyncDisposable inheritance
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(interfaceType));
    }

    [Fact]
    public void ITransport_WriteAsyncGeneric_HasCorrectSignature()
    {
        // Verify the generic WriteAsync exists
        var interfaceType = typeof(ITransport);
        var methods = interfaceType.GetMethods().Where(m => m.Name == "WriteAsync");

        Assert.Contains(methods, m => m.IsGenericMethod);
    }
}

/// <summary>
///     Tests for SubprocessTransport focusing on initialization and state management.
///     Full integration tests require the actual Claude CLI to be installed.
/// </summary>
public class SubprocessTransportTests
{
    [Fact]
    public void Constructor_WithDefaultOptions_CreatesInstance()
    {
        // Arrange
        var options = new ClaudeAgentOptions();

        // Act
        var transport = new SubprocessTransport(options);

        // Assert
        Assert.NotNull(transport);
        Assert.False(transport.IsReady);
    }

    [Fact]
    public void Constructor_WithPrompt_CreatesInstance()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var prompt = "Hello, Claude!";

        // Act
        var transport = new SubprocessTransport(options, prompt);

        // Assert
        Assert.NotNull(transport);
        Assert.False(transport.IsReady);
    }

    [Fact]
    public async Task WriteAsync_BeforeConnect_ThrowsTransportException()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);
        var message = JsonDocument.Parse("{}");

        // Act & Assert
        await Assert.ThrowsAsync<TransportException>(async () =>
            await transport.WriteAsync(message));
    }

    [Fact]
    public async Task WriteAsync_Generic_BeforeConnect_ThrowsTransportException()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert
        await Assert.ThrowsAsync<TransportException>(async () =>
            await transport.WriteAsync(new { type = "test" }));
    }

    [Fact]
    public async Task ReadMessagesAsync_BeforeConnect_ThrowsTransportException()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert
        await Assert.ThrowsAsync<TransportException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task EndInputAsync_BeforeConnect_DoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert (should not throw)
        await transport.EndInputAsync();
    }

    [Fact]
    public async Task CloseAsync_BeforeConnect_DoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert (should not throw)
        await transport.CloseAsync();
    }

    [Fact]
    public async Task DisposeAsync_BeforeConnect_DoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert (should not throw)
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);

        // Act & Assert (should not throw)
        await transport.DisposeAsync();
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);
        await transport.DisposeAsync();
        var message = JsonDocument.Parse("{}");

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await transport.WriteAsync(message));
    }

    [Fact]
    public async Task ReadMessagesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var transport = new SubprocessTransport(options);
        await transport.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidCliPath_ThrowsTransportException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid())
        };
        var transport = new SubprocessTransport(options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await transport.ConnectAsync());

        Assert.Contains("not found", ex.Message);
    }
}

/// <summary>
///     Additional edge case tests for transport-related functionality.
/// </summary>
public class TransportEdgeCaseTests
{
    [Fact]
    public void CliArgumentsBuilder_WithOutputFormat_ExtractsInnerSchema()
    {
        // Arrange - create a wrapped json_schema format
        var wrappedSchema = JsonDocument.Parse(@"{
            ""type"": ""json_schema"",
            ""json_schema"": {
                ""name"": ""response"",
                ""schema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""result"": { ""type"": ""string"" }
                    }
                }
            }
        }");

        var options = new ClaudeAgentOptions
        {
            OutputFormat = wrappedSchema.RootElement
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--json-schema", args);
        var schemaIndex = args.IndexOf("--json-schema");
        var schemaJson = args[schemaIndex + 1];

        // Verify it extracted the inner schema
        var parsedSchema = JsonDocument.Parse(schemaJson);
        Assert.True(parsedSchema.RootElement.TryGetProperty("type", out var typeValue));
        Assert.Equal("object", typeValue.GetString());
    }

    [Fact]
    public void CliArgumentsBuilder_WithRawOutputFormat_PassesThrough()
    {
        // Arrange - create a raw schema (not wrapped)
        var rawSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""result"": { ""type"": ""string"" }
            }
        }");

        var options = new ClaudeAgentOptions
        {
            OutputFormat = rawSchema.RootElement
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--json-schema", args);
        var schemaIndex = args.IndexOf("--json-schema");
        var schemaJson = args[schemaIndex + 1];

        // Verify it passed through as-is
        var parsedSchema = JsonDocument.Parse(schemaJson);
        Assert.True(parsedSchema.RootElement.TryGetProperty("type", out var typeValue));
        Assert.Equal("object", typeValue.GetString());
    }

    [Fact]
    public void ProcessStartInfoBuilder_QuoteArgument_HandlesSpecialCharacters()
    {
        // This test verifies the behavior indirectly through the Build method
        // when on Windows with a .cmd path

        if (!OperatingSystem.IsWindows())
        {
            // Skip on non-Windows platforms
            return;
        }

        // Arrange - args with special characters that need quoting
        var args = new List<string>
        {
            "--prompt",
            "Hello & World | Test < > ^"
        };

        // This test would require a .cmd file to trigger the cmd.exe fallback path
        // For now, we just verify that Build() doesn't throw with complex args
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", args).Build();

        // Assert
        Assert.Contains("Hello & World | Test < > ^", startInfo.ArgumentList);
    }

    [Fact]
    public void CliArgumentsBuilder_AgentWithoutOptionalFields_WorksCorrectly()
    {
        // Arrange - agent without tools and model
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["simple-agent"] = new()
                {
                    Description = "A simple agent",
                    Prompt = "You are a simple agent"
                    // No Tools or Model specified
                }
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - agents go through initialize; the wire dictionary omits unset optional fields
        Assert.DoesNotContain("--agents", args);
        var agent = options.Agents!["simple-agent"].ToCliDictionary();

        Assert.False(agent.ContainsKey("tools"));
        Assert.False(agent.ContainsKey("model"));
        Assert.Equal(2, agent.Count);
    }

    [Fact]
    public void AgentDefinition_ToCliDictionary_IncludesNewFields()
    {
        var agent = new AgentDefinition
        {
            Description = "d",
            Prompt = "p",
            DisallowedTools = ["Bash"],
            InitialPrompt = "start",
            MaxTurns = 3,
            Background = true,
            Effort = EffortLevel.High,
            PermissionMode = PermissionMode.AcceptEdits,
            Memory = SettingSource.Project,
            McpServerNames = ["shared"],
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["inline"] = new McpStdioServerConfig { Command = "node" }
            }
        };

        var json = JsonDocument.Parse(JsonSerializer.Serialize(agent.ToCliDictionary())).RootElement;

        Assert.Equal("Bash", json.GetProperty("disallowedTools")[0].GetString());
        Assert.Equal("start", json.GetProperty("initialPrompt").GetString());
        Assert.Equal(3, json.GetProperty("maxTurns").GetInt32());
        Assert.True(json.GetProperty("background").GetBoolean());
        Assert.Equal("high", json.GetProperty("effort").GetString());
        Assert.Equal("acceptEdits", json.GetProperty("permissionMode").GetString());
        Assert.Equal("project", json.GetProperty("memory").GetString());
        var servers = json.GetProperty("mcpServers");
        Assert.Equal("shared", servers[0].GetString());
        Assert.Equal("node", servers[1].GetProperty("inline").GetProperty("command").GetString());
    }

    [Fact]
    public void CliArgumentsBuilder_EmptyMcpServers_DoesNotAddArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>()
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.DoesNotContain("--mcp-config", args);
    }

    [Fact]
    public void CliArgumentsBuilder_ResumeWithoutFork_DoesNotAddForkArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Resume = "session-123",
            ForkSession = false
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--resume=session-123", args);
        Assert.DoesNotContain("--fork-session", args);
    }

    [Fact]
    public void CliArgumentsBuilder_ForkWithContinue_AddsForkArg()
    {
        // Arrange - fork also applies to --continue, so it is forwarded whenever set
        var options = new ClaudeAgentOptions
        {
            ForkSession = true,
            ContinueConversation = true
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--continue", args);
        Assert.Contains("--fork-session", args);
    }

    [Fact]
    public void CliArgumentsBuilder_PresetSystemPromptWithoutAppend_DoesNotAddAppendArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new PresetSystemPrompt
            {
                Preset = "claude_code"
                // No Append specified
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert - the preset is the CLI default: no system prompt flags at all
        Assert.DoesNotContain("--system-prompt", args);
        Assert.DoesNotContain("--append-system-prompt", args);
    }
}