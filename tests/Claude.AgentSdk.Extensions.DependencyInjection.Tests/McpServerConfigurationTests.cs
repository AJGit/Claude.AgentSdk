using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for McpServerConfiguration.
/// </summary>
public class McpServerConfigurationTests
{
    [Fact]
    public void DefaultType_IsStdio()
    {
        // Arrange
        McpServerConfiguration config = new();

        // Assert
        Assert.Equal("Stdio", config.Type);
    }

    [Fact]
    public void ToConfig_StdioServer_ReturnsStdioConfig()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Stdio",
            Command = "python",
            Args = ["server.py", "--port", "8080"]
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpStdioServerConfig>(result);
        McpStdioServerConfig stdioConfig = (McpStdioServerConfig)result;
        Assert.Equal("python", stdioConfig.Command);
        Assert.NotNull(stdioConfig.Args);
        Assert.Equal(3, stdioConfig.Args.Count);
        Assert.Equal("server.py", stdioConfig.Args[0]);
    }

    [Fact]
    public void ToConfig_StdioServer_WithEnvironment_ReturnsConfigWithEnv()
    {
        // Arrange
        McpServerConfiguration config = new()
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
        McpServerConfig result = config.ToConfig();

        // Assert
        McpStdioServerConfig stdioConfig = (McpStdioServerConfig)result;
        Assert.NotNull(stdioConfig.Env);
        Assert.Equal(2, stdioConfig.Env.Count);
        Assert.Equal("production", stdioConfig.Env["NODE_ENV"]);
    }

    [Fact]
    public void ToConfig_StdioServer_WithoutCommand_ThrowsInvalidOperationException()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Stdio",
            Command = null
        };

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Command", ex.Message);
    }

    [Fact]
    public void ToConfig_StdioServer_CaseInsensitive()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "STDIO",
            Command = "python"
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpStdioServerConfig>(result);
    }

    [Fact]
    public void ToConfig_SseServer_ReturnsSseConfig()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Sse",
            Url = "https://api.example.com/events"
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpSseServerConfig>(result);
        McpSseServerConfig sseConfig = (McpSseServerConfig)result;
        Assert.Equal("https://api.example.com/events", sseConfig.Url);
    }

    [Fact]
    public void ToConfig_SseServer_WithHeaders_ReturnsConfigWithHeaders()
    {
        // Arrange
        McpServerConfiguration config = new()
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
        McpServerConfig result = config.ToConfig();

        // Assert
        McpSseServerConfig sseConfig = (McpSseServerConfig)result;
        Assert.NotNull(sseConfig.Headers);
        Assert.Equal(2, sseConfig.Headers.Count);
        Assert.Equal("Bearer token123", sseConfig.Headers["Authorization"]);
    }

    [Fact]
    public void ToConfig_SseServer_WithoutUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Sse",
            Url = null
        };

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Url", ex.Message);
    }

    [Fact]
    public void ToConfig_SseServer_CaseInsensitive()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "SSE",
            Url = "https://api.example.com"
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpSseServerConfig>(result);
    }

    [Fact]
    public void ToConfig_HttpServer_ReturnsHttpConfig()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Http",
            Url = "https://api.example.com/mcp"
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpHttpServerConfig>(result);
        McpHttpServerConfig httpConfig = (McpHttpServerConfig)result;
        Assert.Equal("https://api.example.com/mcp", httpConfig.Url);
    }

    [Fact]
    public void ToConfig_HttpServer_WithHeaders_ReturnsConfigWithHeaders()
    {
        // Arrange
        McpServerConfiguration config = new()
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
        McpServerConfig result = config.ToConfig();

        // Assert
        McpHttpServerConfig httpConfig = (McpHttpServerConfig)result;
        Assert.NotNull(httpConfig.Headers);
        Assert.Equal(2, httpConfig.Headers.Count);
    }

    [Fact]
    public void ToConfig_HttpServer_WithoutUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Http",
            Url = null
        };

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Url", ex.Message);
    }

    [Fact]
    public void ToConfig_HttpServer_CaseInsensitive()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "HTTP",
            Url = "https://api.example.com"
        };

        // Act
        McpServerConfig result = config.ToConfig();

        // Assert
        Assert.IsType<McpHttpServerConfig>(result);
    }

    [Fact]
    public void ToConfig_UnknownType_ThrowsInvalidOperationException()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = "Unknown"
        };

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => config.ToConfig());
        Assert.Contains("Unknown", ex.Message);
    }

    [Fact]
    public void ToConfig_EmptyType_ThrowsInvalidOperationException()
    {
        // Arrange
        McpServerConfiguration config = new()
        {
            Type = ""
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.ToConfig());
    }
}
