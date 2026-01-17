using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Types;

/// <summary>
///     Tests for the McpServerName strongly-typed identifier.
/// </summary>
[UnitTest]
public class McpServerNameTests
{
    [Fact]
    public void Constructor_WithValidValue_SetsValue()
    {
        // Act
        var server = new McpServerName("excel-tools");

        // Assert
        Assert.Equal("excel-tools", server.Value);
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new McpServerName(null!));
    }

    [Fact]
    public void Value_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        McpServerName server = default;

        // Act & Assert
        Assert.Equal(string.Empty, server.Value);
    }

    [Fact]
    public void Sdk_WithValidName_ReturnsServerName()
    {
        // Act
        var server = McpServerName.Sdk("my-sdk-server");

        // Assert
        Assert.Equal("my-sdk-server", server.Value);
    }

    [Fact]
    public void Sdk_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => McpServerName.Sdk(null!));
    }

    [Fact]
    public void Sdk_WithEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => McpServerName.Sdk(""));
    }

    [Fact]
    public void Custom_ReturnsNewServerName()
    {
        // Act
        var server = McpServerName.Custom("custom-server");

        // Assert
        Assert.Equal("custom-server", server.Value);
    }

    [Fact]
    public void FromNullable_WithValue_ReturnsServerName()
    {
        // Act
        var server = McpServerName.FromNullable("my-server");

        // Assert
        Assert.NotNull(server);
        Assert.Equal("my-server", server.Value.Value);
    }

    [Fact]
    public void FromNullable_WithNull_ReturnsNull()
    {
        // Act
        var server = McpServerName.FromNullable(null);

        // Assert
        Assert.Null(server);
    }

    [Fact]
    public void Tool_WithValidName_ReturnsCorrectToolName()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act
        var tool = server.Tool("create_spreadsheet");

        // Assert
        Assert.Equal("mcp__excel-tools__create_spreadsheet", tool.Value);
    }

    [Fact]
    public void Tool_WithNull_ThrowsArgumentException()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => server.Tool(null!));
    }

    [Fact]
    public void Tool_WithEmpty_ThrowsArgumentException()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => server.Tool(""));
    }

    [Fact]
    public void Tools_WithValidNames_ReturnsCorrectToolNames()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act
        var tools = server.Tools("create", "read", "update");

        // Assert
        Assert.Equal(3, tools.Length);
        Assert.Equal("mcp__excel-tools__create", tools[0].Value);
        Assert.Equal("mcp__excel-tools__read", tools[1].Value);
        Assert.Equal("mcp__excel-tools__update", tools[2].Value);
    }

    [Fact]
    public void Tools_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => server.Tools(null!));
    }

    [Fact]
    public void Tools_WithEmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var server = new McpServerName("excel-tools");

        // Act
        var tools = server.Tools();

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesServerName()
    {
        // Act
        McpServerName server = "my-server";

        // Assert
        Assert.Equal("my-server", server.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var server = new McpServerName("my-server");

        // Act
        string value = server;

        // Assert
        Assert.Equal("my-server", value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var server1 = new McpServerName("my-server");
        var server2 = new McpServerName("my-server");

        // Act & Assert
        Assert.True(server1.Equals(server2));
        Assert.True(server1 == server2);
        Assert.False(server1 != server2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var server1 = new McpServerName("server1");
        var server2 = new McpServerName("server2");

        // Act & Assert
        Assert.False(server1.Equals(server2));
        Assert.False(server1 == server2);
        Assert.True(server1 != server2);
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var server = new McpServerName("my-server");
        object other = new McpServerName("my-server");
        object different = new McpServerName("other-server");

        // Act & Assert
        Assert.True(server.Equals(other));
        Assert.False(server.Equals(different));
        Assert.True(server.Equals("my-server")); // Implicitly converted to McpServerName
        Assert.False(server.Equals((object?)null));
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var server1 = new McpServerName("my-server");
        var server2 = new McpServerName("my-server");

        // Act & Assert
        Assert.Equal(server1.GetHashCode(), server2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenDefault_ReturnsZero()
    {
        // Arrange
        McpServerName server = default;

        // Act & Assert
        Assert.Equal(0, server.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var server = new McpServerName("my-server");

        // Act & Assert
        Assert.Equal("my-server", server.ToString());
    }

    [Fact]
    public void ToString_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        McpServerName server = default;

        // Act & Assert
        Assert.Equal(string.Empty, server.ToString());
    }
}
