using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Types;

/// <summary>
///     Tests for the ToolName strongly-typed identifier.
/// </summary>
[UnitTest]
public class ToolNameTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidValue_SetsValue()
    {
        // Act
        var tool = new ToolName("Read");

        // Assert
        Assert.Equal("Read", tool.Value);
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolName(null!));
    }

    [Fact]
    public void Value_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        ToolName tool = default;

        // Act & Assert
        Assert.Equal(string.Empty, tool.Value);
    }

    #endregion

    #region Static Property Tests

    [Fact]
    public void Read_ReturnsCorrectToolName()
    {
        Assert.Equal("Read", ToolName.Read.Value);
    }

    [Fact]
    public void Write_ReturnsCorrectToolName()
    {
        Assert.Equal("Write", ToolName.Write.Value);
    }

    [Fact]
    public void Edit_ReturnsCorrectToolName()
    {
        Assert.Equal("Edit", ToolName.Edit.Value);
    }

    [Fact]
    public void MultiEdit_ReturnsCorrectToolName()
    {
        Assert.Equal("MultiEdit", ToolName.MultiEdit.Value);
    }

    [Fact]
    public void Bash_ReturnsCorrectToolName()
    {
        Assert.Equal("Bash", ToolName.Bash.Value);
    }

    [Fact]
    public void Grep_ReturnsCorrectToolName()
    {
        Assert.Equal("Grep", ToolName.Grep.Value);
    }

    [Fact]
    public void Glob_ReturnsCorrectToolName()
    {
        Assert.Equal("Glob", ToolName.Glob.Value);
    }

    [Fact]
    public void Task_ReturnsCorrectToolName()
    {
        Assert.Equal("Task", ToolName.Task.Value);
    }

    [Fact]
    public void WebFetch_ReturnsCorrectToolName()
    {
        Assert.Equal("WebFetch", ToolName.WebFetch.Value);
    }

    [Fact]
    public void WebSearch_ReturnsCorrectToolName()
    {
        Assert.Equal("WebSearch", ToolName.WebSearch.Value);
    }

    [Fact]
    public void TodoRead_ReturnsCorrectToolName()
    {
        Assert.Equal("TodoRead", ToolName.TodoRead.Value);
    }

    [Fact]
    public void TodoWrite_ReturnsCorrectToolName()
    {
        Assert.Equal("TodoWrite", ToolName.TodoWrite.Value);
    }

    [Fact]
    public void NotebookEdit_ReturnsCorrectToolName()
    {
        Assert.Equal("NotebookEdit", ToolName.NotebookEdit.Value);
    }

    [Fact]
    public void AskUserQuestion_ReturnsCorrectToolName()
    {
        Assert.Equal("AskUserQuestion", ToolName.AskUserQuestion.Value);
    }

    [Fact]
    public void Skill_ReturnsCorrectToolName()
    {
        Assert.Equal("Skill", ToolName.Skill.Value);
    }

    [Fact]
    public void TaskOutput_ReturnsCorrectToolName()
    {
        Assert.Equal("TaskOutput", ToolName.TaskOutput.Value);
    }

    [Fact]
    public void KillShell_ReturnsCorrectToolName()
    {
        Assert.Equal("KillShell", ToolName.KillShell.Value);
    }

    #endregion

    #region Mcp Factory Method Tests

    [Fact]
    public void Mcp_WithValidServerAndTool_ReturnsCorrectFormat()
    {
        // Act
        var tool = ToolName.Mcp("excel-tools", "create_spreadsheet");

        // Assert
        Assert.Equal("mcp__excel-tools__create_spreadsheet", tool.Value);
    }

    [Fact]
    public void Mcp_WithNullServerName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ToolName.Mcp((string)null!, "tool"));
    }

    [Fact]
    public void Mcp_WithEmptyServerName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ToolName.Mcp("", "tool"));
    }

    [Fact]
    public void Mcp_WithNullToolName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ToolName.Mcp("server", null!));
    }

    [Fact]
    public void Mcp_WithEmptyToolName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ToolName.Mcp("server", ""));
    }

    [Fact]
    public void Mcp_WithMcpServerName_ReturnsCorrectFormat()
    {
        // Arrange
        var serverName = new McpServerName("excel-tools");

        // Act
        var tool = ToolName.Mcp(serverName, "create_spreadsheet");

        // Assert
        Assert.Equal("mcp__excel-tools__create_spreadsheet", tool.Value);
    }

    #endregion

    #region Custom and FromNullable Tests

    [Fact]
    public void Custom_ReturnsNewToolName()
    {
        // Act
        var tool = ToolName.Custom("CustomTool");

        // Assert
        Assert.Equal("CustomTool", tool.Value);
    }

    [Fact]
    public void FromNullable_WithValue_ReturnsToolName()
    {
        // Act
        var tool = ToolName.FromNullable("Read");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("Read", tool.Value.Value);
    }

    [Fact]
    public void FromNullable_WithNull_ReturnsNull()
    {
        // Act
        var tool = ToolName.FromNullable(null);

        // Assert
        Assert.Null(tool);
    }

    #endregion

    #region MCP Tool Helper Tests

    [Fact]
    public void IsMcpTool_WithMcpTool_ReturnsTrue()
    {
        // Arrange
        var tool = ToolName.Mcp("server", "tool");

        // Act & Assert
        Assert.True(tool.IsMcpTool);
    }

    [Fact]
    public void IsMcpTool_WithBuiltInTool_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(ToolName.Read.IsMcpTool);
        Assert.False(ToolName.Bash.IsMcpTool);
    }

    [Fact]
    public void GetMcpServerName_WithMcpTool_ReturnsServerName()
    {
        // Arrange
        var tool = ToolName.Mcp("my-server", "my-tool");

        // Act
        var serverName = tool.GetMcpServerName();

        // Assert
        Assert.Equal("my-server", serverName);
    }

    [Fact]
    public void GetMcpServerName_WithBuiltInTool_ReturnsNull()
    {
        // Act
        var serverName = ToolName.Read.GetMcpServerName();

        // Assert
        Assert.Null(serverName);
    }

    [Fact]
    public void GetMcpToolName_WithMcpTool_ReturnsToolName()
    {
        // Arrange
        var tool = ToolName.Mcp("my-server", "my-tool");

        // Act
        var toolName = tool.GetMcpToolName();

        // Assert
        Assert.Equal("my-tool", toolName);
    }

    [Fact]
    public void GetMcpToolName_WithBuiltInTool_ReturnsNull()
    {
        // Act
        var toolName = ToolName.Read.GetMcpToolName();

        // Assert
        Assert.Null(toolName);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromString_CreatesToolName()
    {
        // Act
        ToolName tool = "Read";

        // Assert
        Assert.Equal("Read", tool.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var tool = ToolName.Read;

        // Act
        string value = tool;

        // Assert
        Assert.Equal("Read", value);
    }

    [Fact]
    public void ImplicitConversion_CanUseInStringArray()
    {
        // Arrange
        string[] tools = [ToolName.Read, ToolName.Write, ToolName.Bash];

        // Assert
        Assert.Equal(new[] { "Read", "Write", "Bash" }, tools);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var tool1 = new ToolName("Read");
        var tool2 = new ToolName("Read");

        // Act & Assert
        Assert.True(tool1.Equals(tool2));
        Assert.True(tool1 == tool2);
        Assert.False(tool1 != tool2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var tool1 = new ToolName("Read");
        var tool2 = new ToolName("Write");

        // Act & Assert
        Assert.False(tool1.Equals(tool2));
        Assert.False(tool1 == tool2);
        Assert.True(tool1 != tool2);
    }

    [Fact]
    public void Equals_WithStaticProperty_ReturnsTrue()
    {
        // Arrange
        var tool = new ToolName("Read");

        // Act & Assert
        Assert.True(tool.Equals(ToolName.Read));
        Assert.True(tool == ToolName.Read);
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new ToolName("Read");
        object other = new ToolName("Read");
        object different = new ToolName("Write");

        // Act & Assert
        Assert.True(tool.Equals(other));
        Assert.False(tool.Equals(different));
        Assert.True(tool.Equals("Read")); // Implicitly converted to ToolName
        Assert.False(tool.Equals((object?)null));
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var tool1 = new ToolName("Read");
        var tool2 = new ToolName("Read");

        // Act & Assert
        Assert.Equal(tool1.GetHashCode(), tool2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenDefault_ReturnsZero()
    {
        // Arrange
        ToolName tool = default;

        // Act & Assert
        Assert.Equal(0, tool.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var tool = new ToolName("Read");

        // Act & Assert
        Assert.Equal("Read", tool.ToString());
    }

    [Fact]
    public void ToString_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        ToolName tool = default;

        // Act & Assert
        Assert.Equal(string.Empty, tool.ToString());
    }

    #endregion
}
