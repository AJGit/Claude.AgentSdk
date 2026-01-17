using System.Text.Json;
using Claude.AgentSdk.Extensions;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Extensions;

/// <summary>
///     Tests for the MessageExtensions methods.
/// </summary>
[UnitTest]
public class MessageExtensionsTests
{
    private static AssistantMessage CreateAssistantMessage(params ContentBlock[] blocks)
    {
        return new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Content = blocks.ToList(),
                Model = "test-model"
            }
        };
    }

    [Fact]
    public void GetText_WithSingleTextBlock_ReturnsText()
    {
        // Arrange
        var message = CreateAssistantMessage(new TextBlock { Text = "Hello World" });

        // Act
        var text = message.GetText();

        // Assert
        Assert.Equal("Hello World", text);
    }

    [Fact]
    public void GetText_WithMultipleTextBlocks_ReturnsConcatenatedText()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new TextBlock { Text = "Line 1" },
            new TextBlock { Text = "Line 2" });

        // Act
        var text = message.GetText();

        // Assert
        Assert.Equal("Line 1\nLine 2", text);
    }

    [Fact]
    public void GetText_WithNoTextBlocks_ReturnsEmptyString()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var text = message.GetText();

        // Assert
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void GetText_WithNullContent_ReturnsEmptyString()
    {
        // Arrange
        var message = new AssistantMessage { MessageContent = null! };

        // Act
        var text = message.GetText();

        // Assert
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void GetText_WithMixedBlocks_ReturnsOnlyText()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new TextBlock { Text = "Before tool" },
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement },
            new TextBlock { Text = "After tool" });

        // Act
        var text = message.GetText();

        // Assert
        Assert.Equal("Before tool\nAfter tool", text);
    }

    [Fact]
    public void GetToolUses_WithToolUseBlocks_ReturnsToolUses()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement },
            new ToolUseBlock { Id = "2", Name = "Write", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var toolUses = message.GetToolUses().ToList();

        // Assert
        Assert.Equal(2, toolUses.Count);
        Assert.Equal("Read", toolUses[0].Name);
        Assert.Equal("Write", toolUses[1].Name);
    }

    [Fact]
    public void GetToolUses_WithNoToolUseBlocks_ReturnsEmpty()
    {
        // Arrange
        var message = CreateAssistantMessage(new TextBlock { Text = "Hello" });

        // Act
        var toolUses = message.GetToolUses();

        // Assert
        Assert.Empty(toolUses);
    }

    [Fact]
    public void GetToolUses_WithNullContent_ReturnsEmpty()
    {
        // Arrange
        var message = new AssistantMessage { MessageContent = null! };

        // Act
        var toolUses = message.GetToolUses();

        // Assert
        Assert.Empty(toolUses);
    }

    [Fact]
    public void HasToolUse_WithMatchingTool_ReturnsTrue()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act & Assert
        Assert.True(message.HasToolUse("Read"));
    }

    [Fact]
    public void HasToolUse_WithNonMatchingTool_ReturnsFalse()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act & Assert
        Assert.False(message.HasToolUse("Write"));
    }

    [Fact]
    public void HasToolUse_WithToolName_ReturnsCorrectResult()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act & Assert
        Assert.True(message.HasToolUse(ToolName.Read));
        Assert.False(message.HasToolUse(ToolName.Write));
    }

    [Fact]
    public void GetToolUse_WithMatchingTool_ReturnsToolUse()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement },
            new ToolUseBlock { Id = "2", Name = "Write", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var toolUse = message.GetToolUse("Read");

        // Assert
        Assert.NotNull(toolUse);
        Assert.Equal("1", toolUse.Id);
    }

    [Fact]
    public void GetToolUse_WithNonMatchingTool_ReturnsNull()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var toolUse = message.GetToolUse("Bash");

        // Assert
        Assert.Null(toolUse);
    }

    [Fact]
    public void GetToolUse_WithToolName_ReturnsCorrectResult()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var toolUse = message.GetToolUse(ToolName.Read);

        // Assert
        Assert.NotNull(toolUse);
    }

    [Fact]
    public void GetThinking_WithThinkingBlocks_ReturnsThinkingBlocks()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ThinkingBlock { Thinking = "Thought 1", Signature = "sig-1" },
            new ThinkingBlock { Thinking = "Thought 2", Signature = "sig-2" });

        // Act
        var thinking = message.GetThinking().ToList();

        // Assert
        Assert.Equal(2, thinking.Count);
        Assert.Equal("Thought 1", thinking[0].Thinking);
    }

    [Fact]
    public void GetThinkingText_WithThinkingBlocks_ReturnsConcatenatedText()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ThinkingBlock { Thinking = "Thought 1", Signature = "sig-1" },
            new ThinkingBlock { Thinking = "Thought 2", Signature = "sig-2" });

        // Act
        var text = message.GetThinkingText();

        // Assert
        Assert.Equal("Thought 1\nThought 2", text);
    }

    [Fact]
    public void HasThinking_WithThinkingBlocks_ReturnsTrue()
    {
        // Arrange
        var message = CreateAssistantMessage(new ThinkingBlock { Thinking = "Thinking...", Signature = "sig-1" });

        // Act & Assert
        Assert.True(message.HasThinking());
    }

    [Fact]
    public void HasThinking_WithoutThinkingBlocks_ReturnsFalse()
    {
        // Arrange
        var message = CreateAssistantMessage(new TextBlock { Text = "Hello" });

        // Act & Assert
        Assert.False(message.HasThinking());
    }

    [Fact]
    public void GetTextBlocks_WithTextBlocks_ReturnsTextBlocks()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new TextBlock { Text = "Text 1" },
            new TextBlock { Text = "Text 2" });

        // Act
        var textBlocks = message.GetTextBlocks().ToList();

        // Assert
        Assert.Equal(2, textBlocks.Count);
    }

    [Fact]
    public void GetTextBlocks_WithNoTextBlocks_ReturnsEmpty()
    {
        // Arrange
        var message = CreateAssistantMessage(
            new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement });

        // Act
        var textBlocks = message.GetTextBlocks();

        // Assert
        Assert.Empty(textBlocks);
    }

    [Fact]
    public void HasError_WithError_ReturnsTrue()
    {
        // Arrange
        var message = new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Content = Array.Empty<ContentBlock>(),
                Model = "test-model",
                Error = "Something went wrong"
            }
        };

        // Act & Assert
        Assert.True(message.HasError());
    }

    [Fact]
    public void HasError_WithNoError_ReturnsFalse()
    {
        // Arrange
        var message = CreateAssistantMessage(new TextBlock { Text = "Hello" });

        // Act & Assert
        Assert.False(message.HasError());
    }

    [Fact]
    public void GetError_WithError_ReturnsErrorMessage()
    {
        // Arrange
        var message = new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Content = Array.Empty<ContentBlock>(),
                Model = "test-model",
                Error = "Error message"
            }
        };

        // Act
        var error = message.GetError();

        // Assert
        Assert.Equal("Error message", error);
    }

    [Fact]
    public void GetError_WithNoError_ReturnsNull()
    {
        // Arrange
        var message = CreateAssistantMessage(new TextBlock { Text = "Hello" });

        // Act
        var error = message.GetError();

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void GetModel_WithModel_ReturnsModelName()
    {
        // Arrange
        var message = new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Content = Array.Empty<ContentBlock>(),
                Model = "claude-sonnet-4"
            }
        };

        // Act
        var model = message.GetModel();

        // Assert
        Assert.Equal("claude-sonnet-4", model);
    }

    [Fact]
    public void GetModel_WithNoModel_ReturnsEmptyString()
    {
        // Arrange
        var message = new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Content = Array.Empty<ContentBlock>(),
                Model = string.Empty
            }
        };

        // Act
        var model = message.GetModel();

        // Assert
        Assert.Equal(string.Empty, model);
    }

    [Fact]
    public void IsSuccess_WithSuccessResult_ReturnsTrue()
    {
        // Arrange
        var message = new ResultMessage
        {
            Subtype = "success",
            IsError = false,
            DurationMs = 1000,
            DurationApiMs = 800,
            NumTurns = 1,
            SessionId = "test-session"
        };

        // Act & Assert
        Assert.True(message.IsSuccess());
    }

    [Fact]
    public void IsSuccess_WithErrorResult_ReturnsFalse()
    {
        // Arrange
        var message = new ResultMessage
        {
            Subtype = "error",
            IsError = true,
            DurationMs = 1000,
            DurationApiMs = 800,
            NumTurns = 1,
            SessionId = "test-session"
        };

        // Act & Assert
        Assert.False(message.IsSuccess());
    }

    [Fact]
    public void IsSuccess_WithSuccessSubtypeButIsError_ReturnsFalse()
    {
        // Arrange
        var message = new ResultMessage
        {
            Subtype = "success",
            IsError = true,
            DurationMs = 1000,
            DurationApiMs = 800,
            NumTurns = 1,
            SessionId = "test-session"
        };

        // Act & Assert
        Assert.False(message.IsSuccess());
    }

    [Fact]
    public void IsInitMessage_WithInitSubtype_ReturnsTrue()
    {
        // Arrange
        var message = new SystemMessage { Subtype = "init" };

        // Act & Assert
        Assert.True(message.IsInitMessage());
    }

    [Fact]
    public void IsInitMessage_WithOtherSubtype_ReturnsFalse()
    {
        // Arrange
        var message = new SystemMessage { Subtype = "other" };

        // Act & Assert
        Assert.False(message.IsInitMessage());
    }

    [Fact]
    public void GetTools_WithInitMessage_ReturnsTools()
    {
        // Arrange
        var message = new SystemMessage
        {
            Subtype = "init",
            Tools = new List<string> { "Read", "Write", "Bash" }
        };

        // Act
        var tools = message.GetTools();

        // Assert
        Assert.Equal(3, tools.Count);
        Assert.Contains("Read", tools);
    }

    [Fact]
    public void GetTools_WithNonInitMessage_ReturnsEmpty()
    {
        // Arrange
        var message = new SystemMessage
        {
            Subtype = "other",
            Tools = new List<string> { "Read" }
        };

        // Act
        var tools = message.GetTools();

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public void GetMcpServers_WithInitMessage_ReturnsMcpServers()
    {
        // Arrange
        var message = new SystemMessage
        {
            Subtype = "init",
            McpServers = new List<McpServerStatus>
            {
                new() { Name = "server1", Status = "connected" }
            }
        };

        // Act
        var servers = message.GetMcpServers();

        // Assert
        Assert.Single(servers);
        Assert.Equal("server1", servers[0].Name);
    }

    [Fact]
    public void GetMcpServers_WithNonInitMessage_ReturnsEmpty()
    {
        // Arrange
        var message = new SystemMessage { Subtype = "other" };

        // Act
        var servers = message.GetMcpServers();

        // Assert
        Assert.Empty(servers);
    }
}
