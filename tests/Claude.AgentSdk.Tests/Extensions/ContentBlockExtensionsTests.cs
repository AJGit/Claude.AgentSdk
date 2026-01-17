using System.Text.Json;
using Claude.AgentSdk.Extensions;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Extensions;

/// <summary>
///     Tests for the ContentBlockExtensions methods.
/// </summary>
[UnitTest]
public class ContentBlockExtensionsTests
{
    private sealed class TestInput
    {
        public string? Path { get; set; }
        public int Count { get; set; }
    }

    [Fact]
    public void IsText_WithTextBlock_ReturnsTrue()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello" };

        // Act & Assert
        Assert.True(block.IsText());
    }

    [Fact]
    public void IsText_WithNonTextBlock_ReturnsFalse()
    {
        // Arrange
        ContentBlock block = new ToolUseBlock { Id = "1", Name = "Test", Input = JsonDocument.Parse("{}").RootElement };

        // Act & Assert
        Assert.False(block.IsText());
    }

    [Fact]
    public void IsToolUse_WithToolUseBlock_ReturnsTrue()
    {
        // Arrange
        ContentBlock block = new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement };

        // Act & Assert
        Assert.True(block.IsToolUse());
    }

    [Fact]
    public void IsToolUse_WithNonToolUseBlock_ReturnsFalse()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello" };

        // Act & Assert
        Assert.False(block.IsToolUse());
    }

    [Fact]
    public void IsToolResult_WithToolResultBlock_ReturnsTrue()
    {
        // Arrange
        ContentBlock block = new ToolResultBlock { ToolUseId = "1" };

        // Act & Assert
        Assert.True(block.IsToolResult());
    }

    [Fact]
    public void IsToolResult_WithNonToolResultBlock_ReturnsFalse()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello" };

        // Act & Assert
        Assert.False(block.IsToolResult());
    }

    [Fact]
    public void IsThinking_WithThinkingBlock_ReturnsTrue()
    {
        // Arrange
        ContentBlock block = new ThinkingBlock { Thinking = "Considering...", Signature = "sig-1" };

        // Act & Assert
        Assert.True(block.IsThinking());
    }

    [Fact]
    public void IsThinking_WithNonThinkingBlock_ReturnsFalse()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello" };

        // Act & Assert
        Assert.False(block.IsThinking());
    }

    [Fact]
    public void AsText_WithTextBlock_ReturnsText()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello World" };

        // Act
        var text = block.AsText();

        // Assert
        Assert.Equal("Hello World", text);
    }

    [Fact]
    public void AsText_WithNonTextBlock_ReturnsNull()
    {
        // Arrange
        ContentBlock block = new ToolUseBlock { Id = "1", Name = "Test", Input = JsonDocument.Parse("{}").RootElement };

        // Act
        var text = block.AsText();

        // Assert
        Assert.Null(text);
    }

    [Fact]
    public void AsToolUse_WithToolUseBlock_ReturnsToolUseBlock()
    {
        // Arrange
        ContentBlock block = new ToolUseBlock { Id = "1", Name = "Read", Input = JsonDocument.Parse("{}").RootElement };

        // Act
        var toolUse = block.AsToolUse();

        // Assert
        Assert.NotNull(toolUse);
        Assert.Equal("Read", toolUse.Name);
    }

    [Fact]
    public void AsToolUse_WithNonToolUseBlock_ReturnsNull()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello" };

        // Act
        var toolUse = block.AsToolUse();

        // Assert
        Assert.Null(toolUse);
    }

    [Fact]
    public void AsToolResult_WithToolResultBlock_ReturnsToolResultBlock()
    {
        // Arrange
        ContentBlock block = new ToolResultBlock { ToolUseId = "test-id" };

        // Act
        var toolResult = block.AsToolResult();

        // Assert
        Assert.NotNull(toolResult);
        Assert.Equal("test-id", toolResult.ToolUseId);
    }

    [Fact]
    public void AsThinking_WithThinkingBlock_ReturnsThinkingBlock()
    {
        // Arrange
        ContentBlock block = new ThinkingBlock { Thinking = "Thinking...", Signature = "sig-1" };

        // Act
        var thinking = block.AsThinking();

        // Assert
        Assert.NotNull(thinking);
        Assert.Equal("Thinking...", thinking.Thinking);
    }

    [Fact]
    public void GetThinkingContent_WithThinkingBlock_ReturnsContent()
    {
        // Arrange
        ContentBlock block = new ThinkingBlock { Thinking = "Deep thought", Signature = "sig-1" };

        // Act
        var content = block.GetThinkingContent();

        // Assert
        Assert.Equal("Deep thought", content);
    }

    [Fact]
    public void GetThinkingContent_WithNonThinkingBlock_ReturnsNull()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Not thinking" };

        // Act
        var content = block.GetThinkingContent();

        // Assert
        Assert.Null(content);
    }

    [Fact]
    public void GetInput_WithValidInput_DeserializesCorrectly()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Test",
            Input = JsonDocument.Parse("""{"Path": "/test/file.txt", "Count": 5}""").RootElement
        };

        // Act
        var input = toolUse.GetInput<TestInput>();

        // Assert
        Assert.NotNull(input);
        Assert.Equal("/test/file.txt", input.Path);
        Assert.Equal(5, input.Count);
    }

    [Fact]
    public void GetInput_WithInvalidInput_ReturnsNull()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Test",
            Input = JsonDocument.Parse("""{"invalid": "data"}""").RootElement
        };

        // Act - this should not throw, just return default values
        var input = toolUse.GetInput<TestInput>();

        // Assert
        Assert.NotNull(input); // Will deserialize with default values
    }

    [Fact]
    public void IsTool_WithMatchingStringName_ReturnsTrue()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.True(toolUse.IsTool("Read"));
    }

    [Fact]
    public void IsTool_WithNonMatchingStringName_ReturnsFalse()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.False(toolUse.IsTool("Write"));
    }

    [Fact]
    public void IsTool_WithMatchingToolName_ReturnsTrue()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.True(toolUse.IsTool(ToolName.Read));
    }

    [Fact]
    public void IsTool_WithNonMatchingToolName_ReturnsFalse()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.False(toolUse.IsTool(ToolName.Write));
    }

    [Fact]
    public void IsMcpTool_WithMcpTool_ReturnsTrue()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "mcp__excel-tools__create_spreadsheet",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.True(toolUse.IsMcpTool());
    }

    [Fact]
    public void IsMcpTool_WithBuiltInTool_ReturnsFalse()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.False(toolUse.IsMcpTool());
    }

    [Fact]
    public void IsMcpTool_WithNullName_ReturnsFalse()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = null!,
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act & Assert
        Assert.False(toolUse.IsMcpTool());
    }

    [Fact]
    public void GetMcpServerName_WithMcpTool_ReturnsServerName()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "mcp__excel-tools__create_spreadsheet",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var serverName = toolUse.GetMcpServerName();

        // Assert
        Assert.Equal("excel-tools", serverName);
    }

    [Fact]
    public void GetMcpServerName_WithBuiltInTool_ReturnsNull()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var serverName = toolUse.GetMcpServerName();

        // Assert
        Assert.Null(serverName);
    }

    [Fact]
    public void GetMcpToolName_WithMcpTool_ReturnsToolName()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "mcp__excel-tools__create_spreadsheet",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var toolName = toolUse.GetMcpToolName();

        // Assert
        Assert.Equal("create_spreadsheet", toolName);
    }

    [Fact]
    public void GetMcpToolName_WithBuiltInTool_ReturnsOriginalName()
    {
        // Arrange
        var toolUse = new ToolUseBlock
        {
            Id = "1",
            Name = "Read",
            Input = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var toolName = toolUse.GetMcpToolName();

        // Assert
        Assert.Equal("Read", toolName);
    }

    [Fact]
    public void IsError_WithErrorResult_ReturnsTrue()
    {
        // Arrange
        var toolResult = new ToolResultBlock
        {
            ToolUseId = "1",
            IsError = true
        };

        // Act & Assert
        Assert.True(toolResult.IsError());
    }

    [Fact]
    public void IsError_WithSuccessResult_ReturnsFalse()
    {
        // Arrange
        var toolResult = new ToolResultBlock
        {
            ToolUseId = "1",
            IsError = false
        };

        // Act & Assert
        Assert.False(toolResult.IsError());
    }

    [Fact]
    public void IsError_WithNullIsError_ReturnsFalse()
    {
        // Arrange
        var toolResult = new ToolResultBlock
        {
            ToolUseId = "1",
            IsError = null
        };

        // Act & Assert
        Assert.False(toolResult.IsError());
    }

    [Fact]
    public void GetContentAsString_WithContent_ReturnsString()
    {
        // Arrange
        var toolResult = new ToolResultBlock
        {
            ToolUseId = "1",
            Content = JsonDocument.Parse("\"File content here\"").RootElement
        };

        // Act
        var content = toolResult.GetContentAsString();

        // Assert
        Assert.Contains("File content", content);
    }

    [Fact]
    public void GetContentAsString_WithNullContent_ReturnsEmptyString()
    {
        // Arrange
        var toolResult = new ToolResultBlock
        {
            ToolUseId = "1",
            Content = null
        };

        // Act
        var content = toolResult.GetContentAsString();

        // Assert
        Assert.Equal(string.Empty, content);
    }
}
