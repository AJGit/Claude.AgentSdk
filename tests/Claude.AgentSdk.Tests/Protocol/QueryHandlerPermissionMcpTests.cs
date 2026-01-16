using System.Text.Json;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Tools;
using Moq;
using Xunit;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Comprehensive unit tests for QueryHandler's permission handling and MCP routing functionality.
/// </summary>
public class QueryHandlerPermissionMcpTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    #region Test Helpers

    /// <summary>
    ///     Creates a control request JSON string for testing.
    /// </summary>
    private static string CreateControlRequestJson(string requestId, object request)
    {
        var obj = new
        {
            type = "control_request",
            request_id = requestId,
            request
        };
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    /// <summary>
    ///     Creates a can_use_tool request JSON string.
    /// </summary>
    private static string CreateCanUseToolRequestJson(
        string requestId,
        string toolName,
        object input,
        string? blockedPath = null,
        object? permissionSuggestions = null)
    {
        return CreateControlRequestJson(requestId, new
        {
            subtype = "can_use_tool",
            tool_name = toolName,
            input,
            blocked_path = blockedPath,
            permission_suggestions = permissionSuggestions
        });
    }

    /// <summary>
    ///     Creates an mcp_message request JSON string.
    /// </summary>
    private static string CreateMcpMessageRequestJson(string requestId, string serverName, object message)
    {
        return CreateControlRequestJson(requestId, new
        {
            subtype = "mcp_message",
            server_name = serverName,
            message
        });
    }

    /// <summary>
    ///     Creates a hook_callback request JSON string.
    /// </summary>
    private static string CreateHookCallbackRequestJson(string requestId, string callbackId, object input, string? toolUseId = null)
    {
        return CreateControlRequestJson(requestId, new
        {
            subtype = "hook_callback",
            callback_id = callbackId,
            input,
            tool_use_id = toolUseId
        });
    }

    /// <summary>
    ///     Gets the latest written response from the transport.
    /// </summary>
    private static JsonElement? GetLatestWrittenResponse(MockTransport transport)
    {
        return transport.GetLastWrittenMessage();
    }

    /// <summary>
    ///     Waits for a response to be written and returns it.
    /// </summary>
    private static async Task<JsonElement> WaitForWrittenResponseAsync(MockTransport transport, int expectedCount, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            var messages = transport.WrittenMessages;
            if (messages.Count >= expectedCount)
            {
                return (JsonElement)messages[expectedCount - 1];
            }
            await Task.Delay(10);
        }

        throw new TimeoutException($"Expected {expectedCount} response(s) but got {transport.WrittenMessages.Count}");
    }

    /// <summary>
    ///     Extracts the inner response data from a control_response.
    /// </summary>
    private static JsonElement GetResponseData(JsonElement controlResponse)
    {
        return controlResponse.GetProperty("response").GetProperty("response");
    }

    #endregion

    #region Tool Permission Handling (HandleToolPermissionAsync) Tests

    [Fact]
    public async Task HandleToolPermissionAsync_NoCanUseToolCallback_ReturnsAllow()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions(); // No CanUseTool callback
        await using var handler = new QueryHandler(transport, options);

        await handler.StartAsync();

        // Send a can_use_tool request
        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-1",
            "Read",
            new { file_path = "/test/file.txt" }));

        // Wait briefly for processing
        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("control_response", response.GetProperty("type").GetString());
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_CallbackInvokedWithCorrectRequest()
    {
        // Arrange
        var transport = new MockTransport();
        ToolPermissionRequest? capturedRequest = null;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                capturedRequest = request;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        var inputObj = new { file_path = "/test/file.txt", content = "Hello" };
        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-2",
            "Write",
            inputObj,
            blockedPath: "/test/blocked"));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("Write", capturedRequest.ToolName);
        Assert.Equal("/test/blocked", capturedRequest.BlockedPath);
        Assert.Equal("/test/file.txt", capturedRequest.Input.GetProperty("file_path").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_ParsesToolNameAndInput()
    {
        // Arrange
        var transport = new MockTransport();
        string? capturedToolName = null;
        JsonElement capturedInput = default;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                capturedToolName = request.ToolName;
                capturedInput = request.Input;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-3",
            "Bash",
            new { command = "ls -la", timeout = 30000 }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("Bash", capturedToolName);
        Assert.Equal("ls -la", capturedInput.GetProperty("command").GetString());
        Assert.Equal(30000, capturedInput.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_ParsesBlockedPath()
    {
        // Arrange
        var transport = new MockTransport();
        string? capturedBlockedPath = null;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                capturedBlockedPath = request.BlockedPath;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-4",
            "Edit",
            new { file_path = "/etc/passwd" },
            blockedPath: "/etc/passwd"));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("/etc/passwd", capturedBlockedPath);
    }

    [Fact]
    public async Task HandleToolPermissionAsync_PermissionResultAllow_ReturnsBehaviorAllow()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(new PermissionResultAllow())
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-5",
            "Read",
            new { file_path = "/allowed/file.txt" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_PermissionResultAllow_WithUpdatedInput_ReturnsUpdatedInputField()
    {
        // Arrange
        var transport = new MockTransport();
        var updatedInput = JsonDocument.Parse(@"{""file_path"":""/modified/path.txt""}").RootElement;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(
                new PermissionResultAllow { UpdatedInput = updatedInput })
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-6",
            "Read",
            new { file_path = "/original/path.txt" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
        Assert.True(responseData.TryGetProperty("updated_input", out var updatedInputProp));
        Assert.Equal("/modified/path.txt", updatedInputProp.GetProperty("file_path").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_PermissionResultAllow_WithUpdatedPermissions_ReturnsPermissionUpdateField()
    {
        // Arrange
        var transport = new MockTransport();
        var permissions = new List<PermissionUpdate>
        {
            new PermissionUpdate
            {
                Type = PermissionUpdateType.AddRules,
                Rules = new List<PermissionRuleValue>
                {
                    new PermissionRuleValue { ToolName = "Read", RuleContent = "/*" }
                },
                Behavior = PermissionBehavior.Allow,
                Destination = PermissionUpdateDestination.Session
            }
        };

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(
                new PermissionResultAllow { UpdatedPermissions = permissions })
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-7",
            "Read",
            new { file_path = "/test.txt" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
        Assert.True(responseData.TryGetProperty("updated_permissions", out _));
    }

    [Fact]
    public async Task HandleToolPermissionAsync_PermissionResultDeny_ReturnsBehaviorDenyWithMessage()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(
                new PermissionResultDeny { Message = "Access to system files is not allowed" })
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-8",
            "Read",
            new { file_path = "/etc/shadow" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("deny", responseData.GetProperty("behavior").GetString());
        Assert.Equal("Access to system files is not allowed", responseData.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_PermissionResultDeny_WithInterrupt_ReturnsInterruptTrue()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(
                new PermissionResultDeny { Message = "Critical security violation", Interrupt = true })
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-9",
            "Bash",
            new { command = "rm -rf /" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("deny", responseData.GetProperty("behavior").GetString());
        Assert.True(responseData.GetProperty("interrupt").GetBoolean());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_UnknownResultType_DefaultsToAllow()
    {
        // Arrange
        var transport = new MockTransport();

        // Using a custom permission result that's not PermissionResultAllow or PermissionResultDeny
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(new UnknownPermissionResult())
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-10",
            "Read",
            new { file_path = "/test.txt" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task HandleToolPermissionAsync_CallbackThrowsException_ReturnsErrorResponse()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => throw new InvalidOperationException("Permission check failed")
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "req-11",
            "Read",
            new { file_path = "/test.txt" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseObj = response.GetProperty("response");
        Assert.Equal("error", responseObj.GetProperty("subtype").GetString());
        Assert.Contains("Permission check failed", responseObj.GetProperty("error").GetString());
    }

    // Helper class for testing unknown permission result types
    private sealed record UnknownPermissionResult : PermissionResult;

    #endregion

    #region MCP Message Routing (HandleMcpMessageAsync) Tests

    [Fact]
    public async Task HandleMcpMessageAsync_LooksUpServerByName()
    {
        // Arrange
        var transport = new MockTransport();
        var mockMcpServer = new Mock<IMcpToolServer>();
        mockMcpServer.SetupGet(s => s.Name).Returns("test-server");
        mockMcpServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["test-server"] = new McpSdkServerConfig { Name = "test-server", Instance = mockMcpServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-mcp-1",
            "test-server",
            new { jsonrpc = "2.0", id = 1, method = "tools/list" }));

        await Task.Delay(100);

        // Act
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        mockMcpServer.Verify(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMcpMessageAsync_ServerNotFound_ReturnsJsonRpcError32601()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions(); // No MCP servers registered

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-mcp-2",
            "nonexistent-server",
            new { jsonrpc = "2.0", id = 1, method = "tools/list" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        var mcpResponse = responseData.GetProperty("mcp_response");
        Assert.Equal(-32601, mcpResponse.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Contains("nonexistent-server", mcpResponse.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task HandleMcpMessageAsync_CallsServerHandleRequestAsync()
    {
        // Arrange
        var transport = new MockTransport();
        JsonElement? capturedRequest = null;

        var mockMcpServer = new Mock<IMcpToolServer>();
        mockMcpServer.SetupGet(s => s.Name).Returns("capture-server");
        mockMcpServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback<JsonElement, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["capture-server"] = new McpSdkServerConfig { Name = "capture-server", Instance = mockMcpServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-mcp-3",
            "capture-server",
            new { jsonrpc = "2.0", id = 42, method = "tools/call", @params = new { name = "test-tool" } }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("tools/call", capturedRequest.Value.GetProperty("method").GetString());
        Assert.Equal(42, capturedRequest.Value.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task HandleMcpMessageAsync_ResponseWrappedInMcpResponseFormat()
    {
        // Arrange
        var transport = new MockTransport();
        var mockMcpServer = new Mock<IMcpToolServer>();
        mockMcpServer.SetupGet(s => s.Name).Returns("wrapper-server");
        mockMcpServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["result"] = new { tools = new[] { new { name = "tool1" } } }
            });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["wrapper-server"] = new McpSdkServerConfig { Name = "wrapper-server", Instance = mockMcpServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-mcp-4",
            "wrapper-server",
            new { jsonrpc = "2.0", id = 1, method = "tools/list" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.True(responseData.TryGetProperty("mcp_response", out var mcpResponse));
        Assert.Equal("2.0", mcpResponse.GetProperty("jsonrpc").GetString());
    }

    [Fact]
    public async Task HandleMcpMessageAsync_ServerException_ReturnsJsonRpcError32603()
    {
        // Arrange
        var transport = new MockTransport();
        var mockMcpServer = new Mock<IMcpToolServer>();
        mockMcpServer.SetupGet(s => s.Name).Returns("error-server");
        mockMcpServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Server internal error"));

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["error-server"] = new McpSdkServerConfig { Name = "error-server", Instance = mockMcpServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-mcp-5",
            "error-server",
            new { jsonrpc = "2.0", id = 1, method = "tools/call" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        var mcpResponse = responseData.GetProperty("mcp_response");
        Assert.Equal(-32603, mcpResponse.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Contains("Server internal error", mcpResponse.GetProperty("error").GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(999999)]
    public async Task HandleMcpMessageAsync_JsonRpcNumericId_ExtractedCorrectly(int id)
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions(); // No servers - will return error with extracted ID

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            $"req-id-{id}",
            "nonexistent",
            new { jsonrpc = "2.0", id, method = "test" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        var mcpResponse = responseData.GetProperty("mcp_response");
        Assert.Equal(id, mcpResponse.GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData("abc-123")]
    [InlineData("request-uuid")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    public async Task HandleMcpMessageAsync_JsonRpcStringId_ExtractedCorrectly(string id)
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            $"req-string-id",
            "nonexistent",
            new { jsonrpc = "2.0", id, method = "test" }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        var mcpResponse = responseData.GetProperty("mcp_response");
        Assert.Equal(id, mcpResponse.GetProperty("id").GetString());
    }

    [Fact]
    public async Task HandleMcpMessageAsync_JsonRpcNullId_HandledCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        // Enqueue raw JSON to ensure null id
        transport.EnqueueMessage(@"{
            ""type"": ""control_request"",
            ""request_id"": ""req-null-id"",
            ""request"": {
                ""subtype"": ""mcp_message"",
                ""server_name"": ""nonexistent"",
                ""message"": {
                    ""jsonrpc"": ""2.0"",
                    ""method"": ""notification/test""
                }
            }
        }");

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        var mcpResponse = responseData.GetProperty("mcp_response");
        // Should still have error but null id
        Assert.True(mcpResponse.TryGetProperty("id", out var idProp));
        Assert.Equal(JsonValueKind.Null, idProp.ValueKind);
    }

    #endregion

    #region MCP Server Registration Tests

    [Fact]
    public async Task McpServerRegistration_SdkMcpServersFromOptions_Registered()
    {
        // Arrange
        var transport = new MockTransport();
        var mockServer1 = new Mock<IMcpToolServer>();
        mockServer1.SetupGet(s => s.Name).Returns("server1");
        mockServer1
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = "server1-response" });

        var mockServer2 = new Mock<IMcpToolServer>();
        mockServer2.SetupGet(s => s.Name).Returns("server2");
        mockServer2
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = "server2-response" });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["server1"] = new McpSdkServerConfig { Name = "server1", Instance = mockServer1.Object },
                ["server2"] = new McpSdkServerConfig { Name = "server2", Instance = mockServer2.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        // Test server1
        transport.EnqueueMessage(CreateMcpMessageRequestJson("req-s1", "server1", new { jsonrpc = "2.0", id = 1, method = "test" }));
        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Test server2
        transport.EnqueueMessage(CreateMcpMessageRequestJson("req-s2", "server2", new { jsonrpc = "2.0", id = 1, method = "test" }));
        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 2);

        // Assert both servers were called
        mockServer1.Verify(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Once);
        mockServer2.Verify(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task McpServerRegistration_ServerLookupByName()
    {
        // Arrange
        var transport = new MockTransport();
        var correctServerCalled = false;
        var wrongServerCalled = false;

        var mockCorrectServer = new Mock<IMcpToolServer>();
        mockCorrectServer.SetupGet(s => s.Name).Returns("correct-server");
        mockCorrectServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => correctServerCalled = true)
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var mockOtherServer = new Mock<IMcpToolServer>();
        mockOtherServer.SetupGet(s => s.Name).Returns("other-server");
        mockOtherServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => wrongServerCalled = true)
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["correct-server"] = new McpSdkServerConfig { Name = "correct-server", Instance = mockCorrectServer.Object },
                ["other-server"] = new McpSdkServerConfig { Name = "other-server", Instance = mockOtherServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "req-lookup",
            "correct-server",
            new { jsonrpc = "2.0", id = 1, method = "test" }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.True(correctServerCalled, "Correct server should have been called");
        Assert.False(wrongServerCalled, "Other server should not have been called");
    }

    [Fact]
    public async Task McpServerRegistration_IgnoresNonSdkServers()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["stdio-server"] = new McpStdioServerConfig { Command = "node", Args = ["server.js"] },
                ["sse-server"] = new McpSseServerConfig { Url = "http://localhost:8080" },
                ["http-server"] = new McpHttpServerConfig { Url = "http://localhost:9090" }
            }
        };

        // Act - should not throw
        await using var handler = new QueryHandler(transport, options);

        // Assert - handler created successfully, no SDK servers registered
        // (verified by internal _mcpServers dictionary being empty for SDK servers)
    }

    #endregion

    #region Control Request Routing Tests

    [Fact]
    public async Task ControlRequestRouting_CanUseTool_RoutedToHandleToolPermissionAsync()
    {
        // Arrange
        var transport = new MockTransport();
        var callbackInvoked = false;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                callbackInvoked = true;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "route-1",
            "Test",
            new { test = "data" }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.True(callbackInvoked, "CanUseTool should route to HandleToolPermissionAsync");
    }

    [Fact]
    public async Task ControlRequestRouting_McpMessage_RoutedToHandleMcpMessageAsync()
    {
        // Arrange
        var transport = new MockTransport();
        var mockServer = new Mock<IMcpToolServer>();
        mockServer.SetupGet(s => s.Name).Returns("route-test");
        mockServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["route-test"] = new McpSdkServerConfig { Name = "route-test", Instance = mockServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateMcpMessageRequestJson(
            "route-2",
            "route-test",
            new { jsonrpc = "2.0", id = 1, method = "test" }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        mockServer.Verify(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ControlRequestRouting_ExceptionInHandler_SendsControlErrorResponse()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => throw new InvalidOperationException("Handler explosion")
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "route-error",
            "Test",
            new { }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("control_response", response.GetProperty("type").GetString());
        var responseObj = response.GetProperty("response");
        Assert.Equal("error", responseObj.GetProperty("subtype").GetString());
        Assert.Equal("route-error", responseObj.GetProperty("request_id").GetString());
        Assert.Contains("Handler explosion", responseObj.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ControlRequestRouting_UnknownSubtype_ReturnsNullResponseData()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateControlRequestJson("unknown-subtype", new
        {
            subtype = "unknown_request_type",
            data = "some data"
        }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("control_response", response.GetProperty("type").GetString());
        var responseObj = response.GetProperty("response");
        Assert.Equal("success", responseObj.GetProperty("subtype").GetString());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_MultipleControlRequestTypes_ProcessedCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        var permissionCallCount = 0;
        var mcpCallCount = 0;

        var mockServer = new Mock<IMcpToolServer>();
        mockServer.SetupGet(s => s.Name).Returns("integration-server");
        mockServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => mcpCallCount++)
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = new { } });

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                permissionCallCount++;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            },
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["integration-server"] = new McpSdkServerConfig { Name = "integration-server", Instance = mockServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        // Send mixed requests
        transport.EnqueueMessage(CreateCanUseToolRequestJson("int-1", "Read", new { }));
        transport.EnqueueMessage(CreateMcpMessageRequestJson("int-2", "integration-server", new { jsonrpc = "2.0", id = 1, method = "test" }));
        transport.EnqueueMessage(CreateCanUseToolRequestJson("int-3", "Write", new { }));
        transport.EnqueueMessage(CreateMcpMessageRequestJson("int-4", "integration-server", new { jsonrpc = "2.0", id = 2, method = "test2" }));

        await Task.Delay(300);

        // Wait for all 4 responses
        await WaitForWrittenResponseAsync(transport, 4);

        // Assert
        Assert.Equal(2, permissionCallCount);
        Assert.Equal(2, mcpCallCount);
    }

    [Fact]
    public async Task Integration_PermissionDenyWithMcpCall_BothProcessedIndependently()
    {
        // Arrange
        var transport = new MockTransport();
        var mockServer = new Mock<IMcpToolServer>();
        mockServer.SetupGet(s => s.Name).Returns("deny-test-server");
        mockServer
            .Setup(s => s.HandleRequestAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = 1, ["result"] = "success" });

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                if (request.ToolName == "DangerousTool")
                    return Task.FromResult<PermissionResult>(new PermissionResultDeny { Message = "Denied!" });
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            },
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["deny-test-server"] = new McpSdkServerConfig { Name = "deny-test-server", Instance = mockServer.Object }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        // Send a denied permission request and an MCP call
        transport.EnqueueMessage(CreateCanUseToolRequestJson("deny-1", "DangerousTool", new { }));
        transport.EnqueueMessage(CreateMcpMessageRequestJson("deny-2", "deny-test-server", new { jsonrpc = "2.0", id = 1, method = "test" }));

        await Task.Delay(200);

        // Get responses
        var response1 = await WaitForWrittenResponseAsync(transport, 1);
        var response2 = await WaitForWrittenResponseAsync(transport, 2);

        // Assert - permission denied
        var data1 = GetResponseData(response1);
        Assert.Equal("deny", data1.GetProperty("behavior").GetString());

        // Assert - MCP call succeeded
        var data2 = GetResponseData(response2);
        Assert.True(data2.TryGetProperty("mcp_response", out var mcpResp));
        Assert.Equal("success", mcpResp.GetProperty("result").GetString());
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task EdgeCase_EmptyInput_HandledGracefully()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(new PermissionResultAllow())
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(@"{
            ""type"": ""control_request"",
            ""request_id"": ""empty-input"",
            ""request"": {
                ""subtype"": ""can_use_tool"",
                ""tool_name"": ""EmptyTest"",
                ""input"": {}
            }
        }");

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task EdgeCase_ComplexNestedInput_ParsedCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        JsonElement? capturedInput = null;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                capturedInput = request.Input;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(@"{
            ""type"": ""control_request"",
            ""request_id"": ""complex-input"",
            ""request"": {
                ""subtype"": ""can_use_tool"",
                ""tool_name"": ""ComplexTool"",
                ""input"": {
                    ""nested"": {
                        ""array"": [1, 2, 3],
                        ""object"": { ""key"": ""value"" }
                    },
                    ""numbers"": [1.5, 2.7, 3.9]
                }
            }
        }");

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.NotNull(capturedInput);
        var nested = capturedInput.Value.GetProperty("nested");
        Assert.Equal(3, nested.GetProperty("array").GetArrayLength());
        Assert.Equal("value", nested.GetProperty("object").GetProperty("key").GetString());
    }

    [Fact]
    public async Task EdgeCase_UnicodeInToolName_HandledCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        string? capturedName = null;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                capturedName = request.ToolName;
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "unicode-test",
            "Tool_\u4e2d\u6587_\ud83d\ude00",
            new { }));

        await Task.Delay(100);
        await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        Assert.Equal("Tool_\u4e2d\u6587_\ud83d\ude00", capturedName);
    }

    [Fact]
    public async Task EdgeCase_VeryLongToolInput_ProcessedCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        var longContent = new string('x', 100000); // 100KB of content

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) => Task.FromResult<PermissionResult>(new PermissionResultAllow())
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage(CreateCanUseToolRequestJson(
            "long-input",
            "Write",
            new { file_path = "/test.txt", content = longContent }));

        await Task.Delay(100);

        // Act
        var response = await WaitForWrittenResponseAsync(transport, 1);

        // Assert
        var responseData = GetResponseData(response);
        Assert.Equal("allow", responseData.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task EdgeCase_ConcurrentRequests_AllProcessed()
    {
        // Arrange
        var transport = new MockTransport();
        var processedCount = 0;

        var options = new ClaudeAgentOptions
        {
            CanUseTool = (request, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                return Task.FromResult<PermissionResult>(new PermissionResultAllow());
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        // Enqueue many requests rapidly
        for (int i = 0; i < 20; i++)
        {
            transport.EnqueueMessage(CreateCanUseToolRequestJson(
                $"concurrent-{i}",
                "Tool",
                new { index = i }));
        }

        await Task.Delay(500);

        // Wait for all responses
        await WaitForWrittenResponseAsync(transport, 20);

        // Assert
        Assert.Equal(20, processedCount);
        Assert.Equal(20, transport.WrittenMessages.Count);
    }

    #endregion
}
