using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for ClaudeAgentConfiguration.
/// </summary>
public class ClaudeAgentConfigurationTests
{
    [Fact]
    public void SectionName_IsClaudeAgentOptions()
    {
        // Assert
        Assert.Equal("Claude", ClaudeAgentConfiguration.SectionName);
    }

    [Fact]
    public void DefaultMessageChannelCapacity_Is256()
    {
        // Arrange
        ClaudeAgentConfiguration config = new();

        // Assert
        Assert.Equal(256, config.MessageChannelCapacity);
    }

    [Fact]
    public void ToOptions_WithDefaults_ReturnsValidOptions()
    {
        // Arrange
        ClaudeAgentConfiguration config = new();

        // Act
        ClaudeAgentOptions options = config.ToOptions();

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
        ClaudeAgentConfiguration config = new()
        {
            Model = "sonnet"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.ModelId);
        Assert.Equal("sonnet", options.ModelId.Value.Value);
    }

    [Fact]
    public void ToOptions_WithFallbackModel_SetsFallbackModelId()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            Model = "sonnet",
            FallbackModel = "haiku"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.FallbackModelId);
        Assert.Equal("haiku", options.FallbackModelId.Value.Value);
    }

    [Fact]
    public void ToOptions_WithMaxTurns_SetsMaxTurns()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            MaxTurns = 10
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(10, options.MaxTurns);
    }

    [Fact]
    public void ToOptions_WithMaxBudgetUsd_SetsMaxBudgetUsd()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            MaxBudgetUsd = 5.50
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(5.50, options.MaxBudgetUsd);
    }

    [Fact]
    public void ToOptions_WithWorkingDirectory_SetsWorkingDirectory()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            WorkingDirectory = "C:/Projects/Test"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal("C:/Projects/Test", options.WorkingDirectory);
    }

    [Fact]
    public void ToOptions_WithCliPath_SetsCliPath()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            CliPath = "/usr/local/bin/claude"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal("/usr/local/bin/claude", options.CliPath);
    }

    [Fact]
    public void ToOptions_WithAllowedTools_SetsAllowedTools()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            AllowedTools = ["Read", "Write", "Bash"]
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

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
        ClaudeAgentConfiguration config = new()
        {
            DisallowedTools = ["WebSearch", "Bash"]
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.DisallowedTools.Count);
        Assert.Contains("WebSearch", options.DisallowedTools);
        Assert.Contains("Bash", options.DisallowedTools);
    }

    [Fact]
    public void ToOptions_WithNullAllowedTools_ReturnsEmptyList()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            AllowedTools = null
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.AllowedTools);
        Assert.Empty(options.AllowedTools);
    }

    [Fact]
    public void ToOptions_WithSystemPrompt_SetsCustomSystemPrompt()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            SystemPrompt = "You are a helpful coding assistant."
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_WithClaudeCodePreset_SetsClaudeCodePrompt()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            UseClaudeCodePreset = true
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_WithClaudeCodePresetAndAppend_SetsClaudeCodePromptWithAppend()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            UseClaudeCodePreset = true,
            ClaudeCodePresetAppend = "Additional instructions here."
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_CustomPromptTakesPrecedenceOverPreset()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            SystemPrompt = "Custom prompt",
            UseClaudeCodePreset = true
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.SystemPrompt);
    }

    [Fact]
    public void ToOptions_WithPermissionMode_Default_SetsPermissionMode()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            PermissionMode = "Default"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.Default, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithPermissionMode_AcceptEdits_SetsPermissionMode()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            PermissionMode = "AcceptEdits"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithPermissionMode_CaseInsensitive()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            PermissionMode = "acceptedits"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.PermissionMode);
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithInvalidPermissionMode_ReturnsNull()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            PermissionMode = "InvalidMode"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Null(options.PermissionMode);
    }

    [Fact]
    public void ToOptions_WithMaxThinkingTokens_SetsMaxThinkingTokens()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            MaxThinkingTokens = 8192
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(8192, options.MaxThinkingTokens);
    }

    [Fact]
    public void ToOptions_WithEnableFileCheckpointing_SetsEnableFileCheckpointing()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            EnableFileCheckpointing = true
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.True(options.EnableFileCheckpointing);
    }

    [Fact]
    public void ToOptions_WithBetas_SetsBetas()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            Betas = ["interleaved_thinking", "new_feature"]
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.NotNull(options.Betas);
        Assert.Equal(2, options.Betas.Count);
        Assert.Contains("interleaved_thinking", options.Betas);
    }

    [Fact]
    public void ToOptions_WithAddDirectories_SetsAddDirectories()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            AddDirectories = ["C:/Projects", "D:/Data"]
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.AddDirectories.Count);
        Assert.Contains("C:/Projects", options.AddDirectories);
        Assert.Contains("D:/Data", options.AddDirectories);
    }

    [Fact]
    public void ToOptions_WithEnvironment_SetsEnvironment()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            Environment = new Dictionary<string, string>
            {
                ["API_KEY"] = "secret",
                ["DEBUG"] = "true"
            }
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(2, options.Environment.Count);
        Assert.Equal("secret", options.Environment["API_KEY"]);
        Assert.Equal("true", options.Environment["DEBUG"]);
    }

    [Fact]
    public void ToOptions_WithUser_SetsUser()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            User = "test-user-123"
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal("test-user-123", options.User);
    }

    [Fact]
    public void ToOptions_WithMessageChannelCapacity_SetsMessageChannelCapacity()
    {
        // Arrange
        ClaudeAgentConfiguration config = new()
        {
            MessageChannelCapacity = 512
        };

        // Act
        ClaudeAgentOptions options = config.ToOptions();

        // Assert
        Assert.Equal(512, options.MessageChannelCapacity);
    }
}
