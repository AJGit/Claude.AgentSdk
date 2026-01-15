using System.Diagnostics;
using System.Text.Json;
using Claude.AgentSdk;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Transport;

namespace Claude.AgentSdk.Tests.Transport;

#region CliArgumentsBuilder Tests

/// <summary>
/// Tests for CliArgumentsBuilder which constructs command-line arguments from ClaudeAgentOptions.
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
    public void Build_WithPrompt_AddsPrintArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions();
        var prompt = "Hello, Claude!";

        // Act
        var args = CliArgumentsBuilder.Create(options, prompt).Build();

        // Assert
        Assert.Contains("--print", args);
        Assert.Contains(prompt, args);
        Assert.DoesNotContain("--input-format", args);
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
    public void Build_WithToolsPreset_AddsJsonPreset()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsPreset { Preset = "claude_code" }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--tools", args);
        var toolsIndex = args.IndexOf("--tools");
        var toolsValue = args[toolsIndex + 1];
        Assert.Contains("type", toolsValue);
        Assert.Contains("preset", toolsValue);
        Assert.Contains("claude_code", toolsValue);
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

        // Assert
        Assert.Contains("--system-prompt", args);
        Assert.Contains("claude_code", args);
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
        Assert.Contains("--setting-sources", args);
        Assert.Contains(expected, args);
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
        Assert.Contains("--setting-sources", args);
        Assert.Contains("project,user", args);
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

        // Assert
        var pathArgCount = args.Count(a => a == "--additional-data-path");
        Assert.Equal(2, pathArgCount);
        Assert.Contains("/path/one", args);
        Assert.Contains("/path/two", args);
    }

    [Theory]
    [InlineData(PermissionMode.Default, "default")]
    [InlineData(PermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(PermissionMode.Plan, "plan")]
    [InlineData(PermissionMode.BypassPermissions, "bypassPermissions")]
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
    [InlineData(SandboxMode.Off, "off")]
    [InlineData(SandboxMode.Permissive, "permissive")]
    [InlineData(SandboxMode.Strict, "strict")]
    public void Build_WithSimpleSandbox_AddsSandboxArg(SandboxMode mode, string expected)
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Sandbox = new SimpleSandboxConfig(mode)
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--sandbox", args);
        Assert.Contains(expected, args);
    }

    [Fact]
    public void Build_WithDetailedSandbox_AddsSandboxConfigJson()
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

        // Assert
        Assert.Contains("--sandbox-config", args);
        var configIndex = args.IndexOf("--sandbox-config");
        var configJson = args[configIndex + 1];

        // Verify it's valid JSON with expected properties
        // Note: ConfigSerializerExtensions creates camelCase keys, then the dictionary
        // is serialized. Dictionary keys are preserved as-is (not affected by naming policy).
        var doc = JsonDocument.Parse(configJson);

        // Dictionary keys are preserved as camelCase from ConfigSerializerExtensions
        Assert.True(doc.RootElement.TryGetProperty("enabled", out var enabledProp), $"JSON missing 'enabled'. Actual JSON: {configJson}");
        Assert.True(enabledProp.GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("autoAllowBashIfSandboxed", out var autoAllowProp), $"JSON missing 'autoAllowBashIfSandboxed'. Actual JSON: {configJson}");
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

        // Assert
        Assert.Contains("--permission-prompt-tool", args);
        Assert.Contains("custom_perm_tool", args);
        Assert.Contains("--strict-mcp-config", args);
        Assert.Contains("--no-hooks", args);
        Assert.Contains("--user", args);
        Assert.Contains("test-user", args);
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

        // Assert
        Assert.Contains("--resume", args);
        Assert.Contains("session-123", args);
        Assert.Contains("--fork-session", args);
        Assert.Contains("--resume-session-at", args);
        Assert.Contains("message-456", args);
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
    public void Build_WithAgents_AddsAgentsJsonArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["code-reviewer"] = new AgentDefinition
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

        // Assert
        Assert.Contains("--agents", args);
        var agentsIndex = args.IndexOf("--agents");
        var agentsJson = args[agentsIndex + 1];

        // Verify valid JSON
        var doc = JsonDocument.Parse(agentsJson);
        Assert.True(doc.RootElement.TryGetProperty("code-reviewer", out var agent));
        Assert.Equal("Code review specialist", agent.GetProperty("description").GetString());
        Assert.Equal("You are a code reviewer", agent.GetProperty("prompt").GetString());
    }

    [Fact]
    public void Build_WithPlugins_AddsPluginsJsonArg()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Plugins = [
                new PluginConfig { Type = "local", Path = "./my-plugin" }
            ]
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--plugins", args);
        var pluginsIndex = args.IndexOf("--plugins");
        var pluginsJson = args[pluginsIndex + 1];

        // Verify valid JSON array
        var doc = JsonDocument.Parse(pluginsJson);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Single(doc.RootElement.EnumerateArray());
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

#endregion

#region ProcessStartInfoBuilder Tests

/// <summary>
/// Tests for ProcessStartInfoBuilder which creates ProcessStartInfo for the Claude CLI.
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
        Assert.Equal(System.Text.Encoding.UTF8.EncodingName, startInfo.StandardInputEncoding!.EncodingName);
        Assert.Equal(System.Text.Encoding.UTF8.EncodingName, startInfo.StandardOutputEncoding!.EncodingName);
        Assert.Equal(System.Text.Encoding.UTF8.EncodingName, startInfo.StandardErrorEncoding!.EncodingName);
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

#endregion

#region ConfigSerializerExtensions Tests

/// <summary>
/// Tests for ConfigSerializerExtensions which convert configuration objects to CLI-compatible dictionaries.
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

#endregion

#region CliPathResolver Tests

/// <summary>
/// Tests for CliPathResolver which finds the Claude CLI executable.
/// Note: These tests may need to be adjusted based on the actual file system state.
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
        var resolver = CliPathResolver.Create(null);

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

#endregion

#region ITransport Interface Contract Tests

/// <summary>
/// Tests that verify the expected contract of ITransport implementations.
/// These use Moq to create mock implementations.
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

#endregion

#region SubprocessTransport Tests (Initialization & State)

/// <summary>
/// Tests for SubprocessTransport focusing on initialization and state management.
/// Full integration tests require the actual Claude CLI to be installed.
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

#endregion

#region Additional Edge Case Tests

/// <summary>
/// Additional edge case tests for transport-related functionality.
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
                ["simple-agent"] = new AgentDefinition
                {
                    Description = "A simple agent",
                    Prompt = "You are a simple agent"
                    // No Tools or Model specified
                }
            }
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.Contains("--agents", args);
        var agentsIndex = args.IndexOf("--agents");
        var agentsJson = args[agentsIndex + 1];

        var doc = JsonDocument.Parse(agentsJson);
        var agent = doc.RootElement.GetProperty("simple-agent");

        // Should not have tools or model properties
        Assert.False(agent.TryGetProperty("tools", out _));
        Assert.False(agent.TryGetProperty("model", out _));
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
        Assert.Contains("--resume", args);
        Assert.DoesNotContain("--fork-session", args);
    }

    [Fact]
    public void CliArgumentsBuilder_ForkWithoutResume_DoesNotAddForkArg()
    {
        // Arrange - fork is only meaningful with resume
        var options = new ClaudeAgentOptions
        {
            ForkSession = true
            // No Resume specified
        };

        // Act
        var args = CliArgumentsBuilder.Create(options).Build();

        // Assert
        Assert.DoesNotContain("--fork-session", args);
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

        // Assert
        Assert.Contains("--system-prompt", args);
        Assert.DoesNotContain("--append-system-prompt", args);
    }
}

#endregion
