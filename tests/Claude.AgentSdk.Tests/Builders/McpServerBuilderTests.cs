using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Tests.Builders;

/// <summary>
///     Tests for the McpServerBuilder fluent builder.
/// </summary>
[UnitTest]
public class McpServerBuilderTests
{
    #region AddStdio Tests

    [Fact]
    public void AddStdio_WithCommand_CreatesStdioConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("python-tools", "python", "tools.py")
            .Build();

        // Assert
        Assert.Single(servers);
        Assert.True(servers.ContainsKey("python-tools"));
        var config = servers["python-tools"] as McpStdioServerConfig;
        Assert.NotNull(config);
        Assert.Equal("python", config.Command);
        Assert.Equal(["tools.py"], config.Args);
    }

    [Fact]
    public void AddStdio_WithNoArgs_CreatesConfigWithNullArgs()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("tool", "command")
            .Build();

        // Assert
        var config = servers["tool"] as McpStdioServerConfig;
        Assert.NotNull(config);
        Assert.Null(config.Args);
    }

    [Fact]
    public void AddStdio_WithMultipleArgs_CreatesConfigWithAllArgs()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("node-tools", "node", "tools.js", "--port", "3000")
            .Build();

        // Assert
        var config = servers["node-tools"] as McpStdioServerConfig;
        Assert.NotNull(config);
        Assert.Equal(["tools.js", "--port", "3000"], config.Args);
    }

    #endregion

    #region AddSse Tests

    [Fact]
    public void AddSse_CreatesSSEConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddSse("remote-api", "https://api.example.com/mcp")
            .Build();

        // Assert
        Assert.Single(servers);
        var config = servers["remote-api"] as McpSseServerConfig;
        Assert.NotNull(config);
        Assert.Equal("https://api.example.com/mcp", config.Url);
    }

    #endregion

    #region AddHttp Tests

    [Fact]
    public void AddHttp_CreatesHTTPConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddHttp("http-api", "https://api.example.com/rpc")
            .Build();

        // Assert
        Assert.Single(servers);
        var config = servers["http-api"] as McpHttpServerConfig;
        Assert.NotNull(config);
        Assert.Equal("https://api.example.com/rpc", config.Url);
    }

    #endregion

    #region AddSdk Tests

    [Fact]
    public void AddSdk_CreatesSdkConfig()
    {
        // Arrange
        var mockServer = new McpToolServer("test", "1.0.0");

        // Act
        var servers = new McpServerBuilder()
            .AddSdk("sdk-tools", mockServer)
            .Build();

        // Assert
        Assert.Single(servers);
        var config = servers["sdk-tools"] as McpSdkServerConfig;
        Assert.NotNull(config);
        Assert.Equal("sdk-tools", config.Name);
        Assert.Same(mockServer, config.Instance);
    }

    #endregion

    #region WithEnvironment Tests

    [Fact]
    public void WithEnvironment_SingleVar_AddsToStdioConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("python-tools", "python", "tools.py")
            .WithEnvironment("DEBUG", "true")
            .Build();

        // Assert
        var config = servers["python-tools"] as McpStdioServerConfig;
        Assert.NotNull(config);
        Assert.NotNull(config.Env);
        Assert.Contains(new KeyValuePair<string, string>("DEBUG", "true"), config.Env);
    }

    [Fact]
    public void WithEnvironment_MultipleVars_AddsAllToConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("python-tools", "python", "tools.py")
            .WithEnvironment("DEBUG", "true")
            .WithEnvironment("LOG_LEVEL", "verbose")
            .Build();

        // Assert
        var config = servers["python-tools"] as McpStdioServerConfig;
        Assert.NotNull(config?.Env);
        Assert.Equal(2, config.Env.Count);
    }

    [Fact]
    public void WithEnvironment_Dictionary_AddsAllVars()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["VAR1"] = "value1",
            ["VAR2"] = "value2"
        };

        // Act
        var servers = new McpServerBuilder()
            .AddStdio("tool", "cmd")
            .WithEnvironment(vars)
            .Build();

        // Assert
        var config = servers["tool"] as McpStdioServerConfig;
        Assert.NotNull(config?.Env);
        Assert.Contains(new KeyValuePair<string, string>("VAR1", "value1"), config.Env);
        Assert.Contains(new KeyValuePair<string, string>("VAR2", "value2"), config.Env);
    }

    [Fact]
    public void WithEnvironment_BeforeAddServer_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new McpServerBuilder().WithEnvironment("KEY", "value"));

        Assert.Contains("No server has been added", ex.Message);
    }

    #endregion

    #region WithHeaders Tests

    [Fact]
    public void WithHeaders_SingleHeader_AddsToSseConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddSse("api", "https://api.example.com/mcp")
            .WithHeaders("Authorization", "Bearer token123")
            .Build();

        // Assert
        var config = servers["api"] as McpSseServerConfig;
        Assert.NotNull(config);
        Assert.NotNull(config.Headers);
        Assert.Contains(new KeyValuePair<string, string>("Authorization", "Bearer token123"), config.Headers);
    }

    [Fact]
    public void WithHeaders_AddToHttpConfig()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddHttp("api", "https://api.example.com/rpc")
            .WithHeaders("X-API-Key", "key123")
            .Build();

        // Assert
        var config = servers["api"] as McpHttpServerConfig;
        Assert.NotNull(config?.Headers);
        Assert.Contains(new KeyValuePair<string, string>("X-API-Key", "key123"), config.Headers);
    }

    [Fact]
    public void WithHeaders_Dictionary_AddsAllHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token",
            ["X-Custom"] = "value"
        };

        // Act
        var servers = new McpServerBuilder()
            .AddSse("api", "https://api.example.com")
            .WithHeaders(headers)
            .Build();

        // Assert
        var config = servers["api"] as McpSseServerConfig;
        Assert.NotNull(config?.Headers);
        Assert.Equal(2, config.Headers.Count);
    }

    [Fact]
    public void WithHeaders_BeforeAddServer_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new McpServerBuilder().WithHeaders("Key", "value"));

        Assert.Contains("No server has been added", ex.Message);
    }

    #endregion

    #region Multiple Servers Tests

    [Fact]
    public void MultipleServers_BuildsAllConfigs()
    {
        // Arrange
        var mockServer = new McpToolServer("test", "1.0.0");

        // Act
        var servers = new McpServerBuilder()
            .AddStdio("python-tools", "python", "tools.py")
            .WithEnvironment("DEBUG", "true")
            .AddSse("remote-api", "https://api.example.com/mcp")
            .WithHeaders("Authorization", "Bearer token")
            .AddSdk("sdk-tools", mockServer)
            .Build();

        // Assert
        Assert.Equal(3, servers.Count);
        Assert.True(servers.ContainsKey("python-tools"));
        Assert.True(servers.ContainsKey("remote-api"));
        Assert.True(servers.ContainsKey("sdk-tools"));
    }

    [Fact]
    public void ModifiersApplyToCurrentServer_NotPrevious()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("server1", "cmd1")
            .AddStdio("server2", "cmd2")
            .WithEnvironment("KEY", "value") // Should only apply to server2
            .Build();

        // Assert
        var config1 = servers["server1"] as McpStdioServerConfig;
        var config2 = servers["server2"] as McpStdioServerConfig;

        Assert.Null(config1?.Env);
        Assert.NotNull(config2?.Env);
        Assert.Contains(new KeyValuePair<string, string>("KEY", "value"), config2.Env);
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithNoServers_ReturnsEmptyDictionary()
    {
        // Act
        var servers = new McpServerBuilder().Build();

        // Assert
        Assert.Empty(servers);
    }

    [Fact]
    public void Build_ReturnsReadOnlyDictionary()
    {
        // Act
        var servers = new McpServerBuilder()
            .AddStdio("tool", "cmd")
            .Build();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, McpServerConfig>>(servers);
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void AllMethods_ReturnBuilder_ForChaining()
    {
        // Arrange
        var mockServer = new McpToolServer("test", "1.0.0");

        // Act & Assert - each method should return the builder
        var builder = new McpServerBuilder();
        Assert.Same(builder, builder.AddStdio("s1", "cmd"));
        Assert.Same(builder, builder.WithEnvironment("K", "V"));
        Assert.Same(builder, builder.AddSse("s2", "url"));
        Assert.Same(builder, builder.WithHeaders("H", "V"));
        Assert.Same(builder, builder.AddHttp("s3", "url"));
        Assert.Same(builder, builder.AddSdk("s4", mockServer));
    }

    #endregion
}
