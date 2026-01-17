using Claude.AgentSdk.Extensions.DependencyInjection;
using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for ClaudeAgentConfiguration.
/// </summary>
public class ClaudeAgentConfigurationTests
{
    #region ToOptions Basic Tests

    [Fact]
    public void ToOptions_WithDefaults_ReturnsValidOptions()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration();

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options);
        Assert.Null(options.MaxTurns);
        Assert.Null(options.MaxBudgetUsd);
        Assert.Null(options.WorkingDirectory);
    }

    [Fact]
    public void ToOptions_WithModel_SetsModelId()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            Model = "sonnet"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.ModelId);
        Assert.Equal("sonnet", options.ModelId.Value.Value);
    }

    [Fact]
    public void ToOptions_WithFallbackModel_SetsFallbackModelId()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            Model = "sonnet",
            FallbackModel = "haiku"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.FallbackModelId);
        Assert.Equal("haiku", options.FallbackModelId.Value.Value);
    }

    [Fact]
    public void ToOptions_WithMaxTurns_SetsMaxTurns()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            MaxTurns = 10
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(10, options.MaxTurns);
    }

    [Fact]
    public void ToOptions_WithMaxBudgetUsd_SetsMaxBudgetUsd()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            MaxBudgetUsd = 5.50
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(5.50, options.MaxBudgetUsd);
    }

    [Fact]
    public void ToOptions_WithWorkingDirectory_SetsWorkingDirectory()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            WorkingDirectory = "C:/Projects/Test"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal("C:/Projects/Test", options.WorkingDirectory);
    }

    [Fact]
    public void ToOptions_WithCliPath_SetsCliPath()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            CliPath = "/usr/local/bin/claude"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal("/usr/local/bin/claude", options.CliPath);
    }

    #endregion

    #region Tools Configuration Tests

    [Fact]
    public void ToOptions_WithAllowedTools_SetsAllowedTools()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            AllowedTools = ["Read", "Write", "Bash"]
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(3, options.AllowedTools.Count);
        Assert.Contains("Read", options.AllowedTools);
        Assert.Contains("Write", options.AllowedTools);
        Assert.Contains("Bash", options.AllowedTools);
    }

    [Fact]
    public void ToOptions_WithDisallowedTools_SetsDisallowedTools()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            DisallowedTools = ["WebSearch", "Bash"]
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.DisallowedTools.Count);
        Assert.Contains("WebSearch", options.DisallowedTools);
        Assert.Contains("Bash", options.DisallowedTools);
    }

    [Fact]
    public void ToOptions_WithNullAllowedTools_ReturnsEmptyList()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            AllowedTools = null
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.AllowedTools);
        Assert.Empty(options.AllowedTools);
    }

    #endregion

    #region System Prompt Tests

    [Fact]
    public void ToOptions_WithSystemPrompt_SetsCustomSystemPrompt()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            SystemPrompt = "You are a helpful coding assistant."
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_WithClaudeCodePreset_SetsClaudeCodePrompt()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            UseClaudeCodePreset = true
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_WithClaudeCodePresetAndAppend_SetsClaudeCodePromptWithAppend()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            UseClaudeCodePreset = true,
            ClaudeCodePresetAppend = "Additional instructions here."
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_CustomPromptTakesPrecedenceOverPreset()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            SystemPrompt = "Custom prompt",
            UseClaudeCodePreset = true
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    #endregion

    #region Permission Mode Tests

    [Fact]
    public void ToOptions_WithPermissionMode_Default_SetsPermissionMode()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            PermissionMode = "Default"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.Default, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithPermissionMode_AcceptEdits_SetsPermissionMode()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            PermissionMode = "AcceptEdits"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithPermissionMode_CaseInsensitive()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            PermissionMode = "acceptedits"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithInvalidPermissionMode_ReturnsNull()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            PermissionMode = "InvalidMode"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Null(options.PermissionMode);
    }

    #endregion

    #region Advanced Configuration Tests

    [Fact]
    public void ToOptions_WithMaxThinkingTokens_SetsMaxThinkingTokens()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            MaxThinkingTokens = 8192
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(8192, options.MaxThinkingTokens);
    }

    [Fact]
    public void ToOptions_WithEnableFileCheckpointing_SetsEnableFileCheckpointing()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            EnableFileCheckpointing = true
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.True(options.EnableFileCheckpointing);
    }

    [Fact]
    public void ToOptions_WithBetas_SetsBetas()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            Betas = ["interleaved_thinking", "new_feature"]
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.NotNull(options.Betas);
        Assert.Equal(2, options.Betas.Count);
        Assert.Contains("interleaved_thinking", options.Betas);
    }

    [Fact]
    public void ToOptions_WithAddDirectories_SetsAddDirectories()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            AddDirectories = ["C:/Projects", "D:/Data"]
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.AddDirectories.Count);
        Assert.Contains("C:/Projects", options.AddDirectories);
        Assert.Contains("D:/Data", options.AddDirectories);
    }

    [Fact]
    public void ToOptions_WithEnvironment_SetsEnvironment()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            Environment = new Dictionary<string, string>
            {
                ["API_KEY"] = "secret",
                ["DEBUG"] = "true"
            }
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.Environment.Count);
        Assert.Equal("secret", options.Environment["API_KEY"]);
        Assert.Equal("true", options.Environment["DEBUG"]);
    }

    [Fact]
    public void ToOptions_WithUser_SetsUser()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            User = "test-user-123"
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal("test-user-123", options.User);
    }

    [Fact]
    public void ToOptions_WithMessageChannelCapacity_SetsMessageChannelCapacity()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration
        {
            MessageChannelCapacity = 512
        };

        // Act
        var options = config.ToOptions();

        // Assert
        Assert.Equal(512, options.MessageChannelCapacity);
    }

    #endregion

    #region SectionName Tests

    [Fact]
    public void SectionName_IsClaudeAgentOptions()
    {
        // Assert
        Assert.Equal("Claude", ClaudeAgentConfiguration.SectionName);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DefaultMessageChannelCapacity_Is256()
    {
        // Arrange
        var config = new ClaudeAgentConfiguration();

        // Assert
        Assert.Equal(256, config.MessageChannelCapacity);
    }

    #endregion
}
