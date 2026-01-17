using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Builders;

/// <summary>
///     Tests for the ClaudeAgentOptionsBuilder fluent builder.
/// </summary>
[UnitTest]
public class ClaudeAgentOptionsBuilderTests
{
    #region Model Configuration Tests

    [Fact]
    public void WithModel_SetsModelId()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithModel(ModelIdentifier.Sonnet)
            .Build();

        // Assert
        Assert.Equal("sonnet", options.ModelId?.Value);
    }

    [Fact]
    public void WithFallbackModel_SetsFallbackModelId()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithFallbackModel(ModelIdentifier.Haiku)
            .Build();

        // Assert
        Assert.Equal("haiku", options.FallbackModelId?.Value);
    }

    #endregion

    #region System Prompt Tests

    [Fact]
    public void WithSystemPrompt_SetsCustomSystemPrompt()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithSystemPrompt("You are a helpful assistant.")
            .Build();

        // Assert
        Assert.NotNull(options.SystemPrompt);
        Assert.IsType<CustomSystemPrompt>(options.SystemPrompt);
        Assert.Equal("You are a helpful assistant.", ((CustomSystemPrompt)options.SystemPrompt).Prompt);
    }

    [Fact]
    public void UseClaudeCodePreset_SetsClaudeCodePreset()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .UseClaudeCodePreset()
            .Build();

        // Assert
        Assert.NotNull(options.SystemPrompt);
        Assert.IsType<PresetSystemPrompt>(options.SystemPrompt);
    }

    [Fact]
    public void UseClaudeCodePreset_WithAppend_SetsClaudeCodePresetWithAppend()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .UseClaudeCodePreset(append: "Focus on C# development.")
            .Build();

        // Assert
        Assert.NotNull(options.SystemPrompt);
        Assert.IsType<PresetSystemPrompt>(options.SystemPrompt);
        var preset = (PresetSystemPrompt)options.SystemPrompt;
        Assert.Equal("Focus on C# development.", preset.Append);
    }

    #endregion

    #region Tools Configuration Tests

    [Fact]
    public void UseClaudeCodeTools_SetsClaudeCodeToolsPreset()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .UseClaudeCodeTools()
            .Build();

        // Assert
        Assert.NotNull(options.Tools);
        Assert.IsType<ToolsPreset>(options.Tools);
    }

    [Fact]
    public void WithTools_StringArray_SetsToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithTools("Read", "Write", "Bash")
            .Build();

        // Assert
        Assert.NotNull(options.Tools);
        Assert.IsType<ToolsList>(options.Tools);
        var toolsList = (ToolsList)options.Tools;
        Assert.Equal(["Read", "Write", "Bash"], toolsList.Tools);
    }

    [Fact]
    public void WithTools_ToolNameArray_SetsToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithTools(ToolName.Read, ToolName.Write, ToolName.Bash)
            .Build();

        // Assert
        Assert.NotNull(options.Tools);
        Assert.IsType<ToolsList>(options.Tools);
        var toolsList = (ToolsList)options.Tools;
        Assert.Equal(["Read", "Write", "Bash"], toolsList.Tools);
    }

    [Fact]
    public void AllowTools_AddsToAllowedToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .AllowTools("Read", "Write")
            .AllowTools("Bash")
            .Build();

        // Assert
        Assert.Equal(["Read", "Write", "Bash"], options.AllowedTools);
    }

    [Fact]
    public void AllowTools_WithToolNames_AddsToAllowedToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .AllowTools(ToolName.Read, ToolName.Write)
            .Build();

        // Assert
        Assert.Equal(["Read", "Write"], options.AllowedTools);
    }

    [Fact]
    public void DisallowTools_AddsToDisallowedToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .DisallowTools("Bash", "Write")
            .Build();

        // Assert
        Assert.Equal(["Bash", "Write"], options.DisallowedTools);
    }

    [Fact]
    public void DisallowTools_WithToolNames_AddsToDisallowedToolsList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .DisallowTools(ToolName.Bash, ToolName.Write)
            .Build();

        // Assert
        Assert.Equal(["Bash", "Write"], options.DisallowedTools);
    }

    #endregion

    #region Permission Configuration Tests

    [Fact]
    public void WithPermissionMode_SetsPermissionMode()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithPermissionMode(PermissionMode.AcceptEdits)
            .Build();

        // Assert
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void DangerouslySkipAllPermissions_SetsDangerouslySkipPermissions()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .DangerouslySkipAllPermissions()
            .Build();

        // Assert
        Assert.True(options.DangerouslySkipPermissions);
    }

    [Fact]
    public void WithCanUseTool_SetsCallback()
    {
        // Arrange
        Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>> callback =
            (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow());

        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithCanUseTool(callback)
            .Build();

        // Assert
        Assert.NotNull(options.CanUseTool);
    }

    #endregion

    #region Session Configuration Tests

    [Fact]
    public void ContinueConversation_SetsContinueConversation()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .ContinueConversation()
            .Build();

        // Assert
        Assert.True(options.ContinueConversation);
    }

    [Fact]
    public void Resume_SetsResumeSessionId()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .Resume("session-123")
            .Build();

        // Assert
        Assert.Equal("session-123", options.Resume);
    }

    [Fact]
    public void ResumeAt_SetsResumeSessionIdAndMessageUuid()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .ResumeAt("session-123", "message-456")
            .Build();

        // Assert
        Assert.Equal("session-123", options.Resume);
        Assert.Equal("message-456", options.ResumeSessionAt);
    }

    [Fact]
    public void ForkSession_SetsForkSession()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .ForkSession()
            .Build();

        // Assert
        Assert.True(options.ForkSession);
    }

    #endregion

    #region Limits and Budget Tests

    [Fact]
    public void WithMaxTurns_SetsMaxTurns()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithMaxTurns(10)
            .Build();

        // Assert
        Assert.Equal(10, options.MaxTurns);
    }

    [Fact]
    public void WithMaxBudget_SetsMaxBudgetUsd()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithMaxBudget(5.50)
            .Build();

        // Assert
        Assert.Equal(5.50, options.MaxBudgetUsd);
    }

    [Fact]
    public void WithMaxThinkingTokens_SetsMaxThinkingTokens()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithMaxThinkingTokens(1000)
            .Build();

        // Assert
        Assert.Equal(1000, options.MaxThinkingTokens);
    }

    #endregion

    #region Path Configuration Tests

    [Fact]
    public void WithWorkingDirectory_SetsWorkingDirectory()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithWorkingDirectory("/home/user/project")
            .Build();

        // Assert
        Assert.Equal("/home/user/project", options.WorkingDirectory);
    }

    [Fact]
    public void WithCliPath_SetsCliPath()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithCliPath("/usr/local/bin/claude")
            .Build();

        // Assert
        Assert.Equal("/usr/local/bin/claude", options.CliPath);
    }

    [Fact]
    public void AddDirectories_AddsToAddDirectoriesList()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .AddDirectories("/home/user/lib", "/home/user/includes")
            .Build();

        // Assert
        Assert.Equal(["/home/user/lib", "/home/user/includes"], options.AddDirectories);
    }

    #endregion

    #region Environment and Extra Args Tests

    [Fact]
    public void WithEnvironment_SingleVar_AddsEnvironmentVariable()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithEnvironment("DEBUG", "true")
            .Build();

        // Assert
        Assert.Contains(new KeyValuePair<string, string>("DEBUG", "true"), options.Environment);
    }

    [Fact]
    public void WithEnvironment_Dictionary_AddsMultipleEnvironmentVariables()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["DEBUG"] = "true",
            ["LOG_LEVEL"] = "verbose"
        };

        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithEnvironment(vars)
            .Build();

        // Assert
        Assert.Contains(new KeyValuePair<string, string>("DEBUG", "true"), options.Environment);
        Assert.Contains(new KeyValuePair<string, string>("LOG_LEVEL", "verbose"), options.Environment);
    }

    [Fact]
    public void WithExtraArg_AddsExtraArgument()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithExtraArg("--verbose")
            .WithExtraArg("--config", "/path/to/config")
            .Build();

        // Assert
        Assert.Contains(new KeyValuePair<string, string?>("--verbose", null), options.ExtraArgs);
        Assert.Contains(new KeyValuePair<string, string?>("--config", "/path/to/config"), options.ExtraArgs);
    }

    #endregion

    #region Features Tests

    [Fact]
    public void IncludePartialMessages_SetsIncludePartialMessages()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .IncludePartialMessages()
            .Build();

        // Assert
        Assert.True(options.IncludePartialMessages);
    }

    [Fact]
    public void EnableFileCheckpointing_SetsEnableFileCheckpointing()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .EnableFileCheckpointing()
            .Build();

        // Assert
        Assert.True(options.EnableFileCheckpointing);
    }

    [Fact]
    public void EnableBetas_AddsBetaFeatures()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .EnableBetas("feature1", "feature2")
            .Build();

        // Assert
        Assert.Equal(["feature1", "feature2"], options.Betas);
    }

    [Fact]
    public void DisableHooks_SetsNoHooks()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .DisableHooks()
            .Build();

        // Assert
        Assert.True(options.NoHooks);
    }

    [Fact]
    public void UseStrictMcpConfig_SetsStrictMcpConfig()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .UseStrictMcpConfig()
            .Build();

        // Assert
        Assert.True(options.StrictMcpConfig);
    }

    [Fact]
    public void WithUser_SetsUser()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithUser("user-123")
            .Build();

        // Assert
        Assert.Equal("user-123", options.User);
    }

    [Fact]
    public void WithMessageChannelCapacity_SetsCapacity()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithMessageChannelCapacity(512)
            .Build();

        // Assert
        Assert.Equal(512, options.MessageChannelCapacity);
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void Build_WithMultipleConfigurations_CreatesCorrectOptions()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder()
            .WithModel(ModelIdentifier.Sonnet)
            .WithSystemPrompt("You are a code reviewer.")
            .AllowTools(ToolName.Read, ToolName.Grep, ToolName.Glob)
            .WithMaxTurns(5)
            .WithMaxBudget(1.00)
            .WithPermissionMode(PermissionMode.AcceptEdits)
            .WithWorkingDirectory("/project")
            .Build();

        // Assert
        Assert.Equal("sonnet", options.ModelId?.Value);
        Assert.IsType<CustomSystemPrompt>(options.SystemPrompt);
        Assert.Equal(["Read", "Grep", "Glob"], options.AllowedTools);
        Assert.Equal(5, options.MaxTurns);
        Assert.Equal(1.00, options.MaxBudgetUsd);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
        Assert.Equal("/project", options.WorkingDirectory);
    }

    [Fact]
    public void Build_WithDefaults_CreatesValidOptions()
    {
        // Act
        var options = new ClaudeAgentOptionsBuilder().Build();

        // Assert
        Assert.NotNull(options);
        Assert.Null(options.ModelId);
        Assert.Null(options.SystemPrompt);
        Assert.Empty(options.AllowedTools);
        Assert.Empty(options.DisallowedTools);
    }

    #endregion
}
