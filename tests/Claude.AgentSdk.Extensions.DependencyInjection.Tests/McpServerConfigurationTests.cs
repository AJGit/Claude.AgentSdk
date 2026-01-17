using Claude.AgentSdk;
using Claude.AgentSdk.Extensions.DependencyInjection;
using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for McpServerConfiguration.
/// </summary>
public class McpServerConfigurationTests
{
    #region Stdio Server Tests

    [Fact]
    public void ToConfig_StdioServer_ReturnsStdioConfig()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Stdio",
            Command = "python",
            Args = ["server.py", "--port", "8080"]
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpStdioServerConfig>(result);
        var stdioConfig = (McpStdioServerConfig)result;
        Assert.Equal("python", stdioConfig.Command);
        Assert.NotNull(stdioConfig.Args);
        Assert.Equal(3, stdioConfig.Args.Count);
        Assert.Equal("server.py", stdioConfig.Args[0]);
    }

    [Fact]
    public void ToConfig_StdioServer_WithEnvironment_ReturnsConfigWithEnv()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Stdio",
            Command = "node",
            Args = ["index.js"],
            Environment = new Dictionary<string, string>
            {
                ["NODE_ENV"] = "production",
                ["DEBUG"] = "false"
            }
        };

        // Act
        var result = config.ToConfig();

        // Assert
        var stdioConfig = (McpStdioServerConfig)result;
        Assert.NotNull(stdioConfig.Env);
        Assert.Equal(2, stdioConfig.Env.Count);
        Assert.Equal("production", stdioConfig.Env["NODE_ENV"]);
    }

    [Fact]
    public void ToConfig_StdioServer_WithoutCommand_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Stdio",
            Command = null
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Command", ex.Message);
    }

    [Fact]
    public void ToConfig_StdioServer_CaseInsensitive()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "STDIO",
            Command = "python"
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpStdioServerConfig>(result);
    }

    #endregion

    #region SSE Server Tests

    [Fact]
    public void ToConfig_SseServer_ReturnsSseConfig()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Sse",
            Url = "https://api.example.com/events"
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpSseServerConfig>(result);
        var sseConfig = (McpSseServerConfig)result;
        Assert.Equal("https://api.example.com/events", sseConfig.Url);
    }

    [Fact]
    public void ToConfig_SseServer_WithHeaders_ReturnsConfigWithHeaders()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Sse",
            Url = "https://api.example.com/events",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Custom-Header"] = "custom-value"
            }
        };

        // Act
        var result = config.ToConfig();

        // Assert
        var sseConfig = (McpSseServerConfig)result;
        Assert.NotNull(sseConfig.Headers);
        Assert.Equal(2, sseConfig.Headers.Count);
        Assert.Equal("Bearer token123", sseConfig.Headers["Authorization"]);
    }

    [Fact]
    public void ToConfig_SseServer_WithoutUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Sse",
            Url = null
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Url", ex.Message);
    }

    [Fact]
    public void ToConfig_SseServer_CaseInsensitive()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "SSE",
            Url = "https://api.example.com"
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpSseServerConfig>(result);
    }

    #endregion

    #region HTTP Server Tests

    [Fact]
    public void ToConfig_HttpServer_ReturnsHttpConfig()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Http",
            Url = "https://api.example.com/mcp"
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpHttpServerConfig>(result);
        var httpConfig = (McpHttpServerConfig)result;
        Assert.Equal("https://api.example.com/mcp", httpConfig.Url);
    }

    [Fact]
    public void ToConfig_HttpServer_WithHeaders_ReturnsConfigWithHeaders()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Http",
            Url = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token456",
                ["Content-Type"] = "application/json"
            }
        };

        // Act
        var result = config.ToConfig();

        // Assert
        var httpConfig = (McpHttpServerConfig)result;
        Assert.NotNull(httpConfig.Headers);
        Assert.Equal(2, httpConfig.Headers.Count);
    }

    [Fact]
    public void ToConfig_HttpServer_WithoutUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Http",
            Url = null
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Url", ex.Message);
    }

    [Fact]
    public void ToConfig_HttpServer_CaseInsensitive()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "HTTP",
            Url = "https://api.example.com"
        };

        // Act
        var result = config.ToConfig();

        // Assert
        Assert.IsType<McpHttpServerConfig>(result);
    }

    #endregion

    #region Unknown Type Tests

    [Fact]
    public void ToConfig_UnknownType_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = "Unknown"
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Unknown", ex.Message);
    }

    [Fact]
    public void ToConfig_EmptyType_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new McpServerConfiguration
        {
            Type = ""
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.ToConfig());
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void DefaultType_IsStdio()
    {
        // Arrange
        var config = new McpServerConfiguration();

        // Assert
        Assert.Equal("Stdio", config.Type);
    }

    #endregion
}
