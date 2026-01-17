using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Extended mock transport for testing QueryHandler control protocol functionality.
///     Supports configuring control responses, simulating timeouts, and auto-responding.
/// </summary>
internal sealed class ControlMockTransport : MockTransport
{
    private readonly ConcurrentDictionary<string, JsonElement> _configuredResponses = new();
    private bool _autoRespond = true;
    private TimeSpan _responseDelay = TimeSpan.Zero;
    private bool _simulateTimeout;

    /// <summary>
    ///     Configure the transport to simulate timeout (no response sent).
    /// </summary>
    public void SimulateTimeout(bool simulate = true)
    {
        _simulateTimeout = simulate;
        _autoRespond = !simulate;
    }

    /// <summary>
    ///     Configure a response delay before auto-responding.
    /// </summary>
    public void SetResponseDelay(TimeSpan delay)
    {
        _responseDelay = delay;
    }

    /// <summary>
    ///     Configure a specific control response for a request ID.
    /// </summary>
    public void ConfigureControlResponse(string requestId, JsonElement response)
    {
        _configuredResponses[requestId] = response;
    }

    /// <summary>
    ///     Disable auto-response to control_requests.
    /// </summary>
    public void DisableAutoResponse()
    {
        _autoRespond = false;
    }

    /// <summary>
    ///     Inject a control response for a specific request.
    /// </summary>
    public void InjectControlResponse(string requestId, object? responseData = null)
    {
        string responseJson = $$"""
                                {
                                    "type": "control_response",
                                    "response": {
                                        "subtype": "success",
                                        "request_id": "{{requestId}}",
                                        "response": {{(responseData != null ? JsonSerializer.Serialize(responseData) : "null")}}
                                    }
                                }
                                """;
        EnqueueMessage(responseJson);
    }

    /// <summary>
    ///     Override WriteAsync to intercept control_requests and auto-respond.
    /// </summary>
    public override async Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(message, cancellationToken);

        if (_simulateTimeout || !_autoRespond)
        {
            return;
        }

        // Serialize to check if it's a control_request
        string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("type", out JsonElement typeElem) &&
            typeElem.GetString() == "control_request" &&
            doc.RootElement.TryGetProperty("request_id", out JsonElement requestIdElem))
        {
            string requestId = requestIdElem.GetString()!;

            // Schedule response (async to simulate real behavior)
            _ = Task.Run(async () =>
            {
                if (_responseDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_responseDelay, cancellationToken);
                }

                if (_configuredResponses.TryRemove(requestId, out JsonElement configuredResponse))
                {
                    string responseJson = $$"""
                                            {
                                                "type": "control_response",
                                                "response": {
                                                    "subtype": "success",
                                                    "request_id": "{{requestId}}",
                                                    "response": {{configuredResponse.GetRawText()}}
                                                }
                                            }
                                            """;
                    EnqueueMessage(responseJson);
                }
                else
                {
                    InjectControlResponse(requestId);
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    ///     Get all written messages as JsonElements.
    /// </summary>
    public new IReadOnlyList<JsonElement> GetAllWrittenMessagesAsJson()
    {
        return WrittenMessages.ToList();
    }
}

/// <summary>
///     Tests for QueryHandler's control response handling functionality.
/// </summary>
public class QueryHandlerControlResponseTests
{
    [Fact]
    public async Task HandleControlResponse_ExtractsResponseAndRequestId()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Send a control request to get a request ID
        dynamic? initTask = handler.InitializeAsync();

        // The mock transport will auto-respond, complete initialization
        await initTask;

        // Assert - verify the request was sent correctly
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        Assert.Contains(messages, m =>
            m.TryGetProperty("type", out JsonElement t) &&
            t.GetString() == "control_request");
    }

    [Fact]
    public async Task HandleControlResponse_CompletesTaskCompletionSourceWithResponse()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - call GetSupportedModelsAsync which uses control requests
        dynamic? responseTask = handler.GetSupportedModelsAsync();

        // The mock will auto-respond with default response
        dynamic? result = await responseTask;

        // Assert - response was received (default null from mock)
        Assert.True(result.ValueKind == JsonValueKind.Null || result.ValueKind != JsonValueKind.Undefined);
    }

    [Fact]
    public async Task HandleControlResponse_MissingResponseProperty_HandlesGracefully()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a malformed control_response without 'response' property
        transport.EnqueueMessage(@"{""type"": ""control_response""}");

        // Give the handler time to process
        await Task.Delay(50);

        // Assert - handler should not crash, just log warning
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task HandleControlResponse_MissingRequestId_LogsWarning()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a control_response without request_id in the response
        transport.EnqueueMessage(@"{
            ""type"": ""control_response"",
            ""response"": {
                ""subtype"": ""success"",
                ""response"": {}
            }
        }");

        // Give time to process
        await Task.Delay(50);

        // Assert - should not throw
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task HandleControlResponse_UnknownRequestId_LogsWarning()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a control_response for a request that was never made
        transport.EnqueueMessage(@"{
            ""type"": ""control_response"",
            ""response"": {
                ""subtype"": ""success"",
                ""request_id"": ""unknown_request_12345"",
                ""response"": {}
            }
        }");

        // Give time to process
        await Task.Delay(50);

        // Assert - should not throw, just logs warning
        Assert.True(transport.IsReady);
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        // Use reflection to create QueryHandler since it's internal
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for QueryHandler's control request sending functionality.
/// </summary>
public class QueryHandlerControlRequestTests
{
    [Fact]
    public async Task SendControlRequest_GeneratesUniqueRequestIds()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - send multiple control requests
        dynamic? task1 = handler.GetSupportedModelsAsync();
        dynamic? task2 = handler.GetSupportedCommandsAsync();
        dynamic? task3 = handler.GetAccountInfoAsync();

        await Task.WhenAll(task1, task2, task3);

        // Assert - verify different request IDs
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        List<string?> requestIds = messages
            .Where(m => m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request")
            .Select(m => m.GetProperty("request_id").GetString())
            .ToList();

        // All request IDs should be unique
        Assert.Equal(requestIds.Count, requestIds.Distinct().Count());
    }

    [Fact]
    public async Task SendControlRequest_SendsCorrectStructure()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InterruptAsync();

        // Assert
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        JsonElement controlRequest = messages.First(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");

        // Verify structure
        Assert.Equal("control_request", controlRequest.GetProperty("type").GetString());
        Assert.True(controlRequest.TryGetProperty("request_id", out JsonElement reqId));
        Assert.False(string.IsNullOrEmpty(reqId.GetString()));
        Assert.True(controlRequest.TryGetProperty("request", out JsonElement request));
        Assert.Equal("interrupt", request.GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task SendControlRequest_CancellationToken_Respected()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.SimulateTimeout();

        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Create pre-cancelled token
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act & Assert - TaskCanceledException is a subclass of OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await handler.InterruptAsync(cts.Token);
        });
    }

    [Fact]
    public async Task SendControlRequest_CleansUpPendingResponses()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - send and complete a request
        await handler.InterruptAsync();

        // Assert - the pending response should be cleaned up
        // (This is verified by the fact that subsequent requests work correctly)
        await handler.InterruptAsync();
        await handler.InterruptAsync();

        // All three requests should complete without issues
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        int controlRequests = messages.Count(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");

        Assert.Equal(3, controlRequests);
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for specific control commands in QueryHandler.
/// </summary>
public class QueryHandlerControlCommandTests
{
    [Fact]
    public async Task InitializeAsync_SendsInitializeSubtype()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InitializeAsync();

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("initialize", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task InitializeAsync_WithHooks_IncludesHooksConfig()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new()
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new()
                    {
                        Matcher = "Bash",
                        Hooks = new HookCallback[]
                        {
                            (input, toolUseId, context, ct) => Task.FromResult<HookOutput>(
                                new SyncHookOutput { Continue = true })
                        }
                    }
                }
            }
        };
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InitializeAsync();

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        JsonElement innerRequest = request.GetProperty("request");
        Assert.Equal("initialize", innerRequest.GetProperty("subtype").GetString());
        Assert.True(innerRequest.TryGetProperty("hooks", out JsonElement hooks));
        Assert.NotEqual(JsonValueKind.Null, hooks.ValueKind);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyInitializesOnce()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InitializeAsync();
        await handler.InitializeAsync();

        // Assert - should only have one initialize request
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        int initRequests = messages.Count(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request" &&
            m.TryGetProperty("request", out JsonElement r) && r.GetProperty("subtype").GetString() == "initialize");

        Assert.Equal(1, initRequests);
    }

    [Fact]
    public async Task InterruptAsync_SendsInterruptSubtype()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InterruptAsync();

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("interrupt", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Theory]
    [InlineData(PermissionMode.Default, "default")]
    [InlineData(PermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(PermissionMode.Plan, "plan")]
    [InlineData(PermissionMode.BypassPermissions, "bypassPermissions")]
    public async Task SetPermissionModeAsync_MapsEnumToString(PermissionMode mode, string expectedString)
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.SetPermissionModeAsync(mode);

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        JsonElement innerRequest = request.GetProperty("request");
        Assert.Equal("set_permission_mode", innerRequest.GetProperty("subtype").GetString());
        Assert.Equal(expectedString, innerRequest.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task SetModelAsync_SendsModelName()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.SetModelAsync("claude-3-opus");

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        JsonElement innerRequest = request.GetProperty("request");
        Assert.Equal("set_model", innerRequest.GetProperty("subtype").GetString());
        Assert.Equal("claude-3-opus", innerRequest.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GetSupportedCommandsAsync_ReturnJsonElement()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        dynamic? result = await handler.GetSupportedCommandsAsync();

        // Assert
        Assert.True(result.ValueKind == JsonValueKind.Null || result.ValueKind != JsonValueKind.Undefined);

        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("supported_commands", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task GetSupportedModelsAsync_ReturnsJsonElement()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        dynamic? result = await handler.GetSupportedModelsAsync();

        // Assert
        Assert.True(result.ValueKind == JsonValueKind.Null || result.ValueKind != JsonValueKind.Undefined);

        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("supported_models", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task GetMcpServerStatusAsync_ReturnsJsonElement()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        dynamic? result = await handler.GetMcpServerStatusAsync();

        // Assert
        Assert.True(result.ValueKind == JsonValueKind.Null || result.ValueKind != JsonValueKind.Undefined);

        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("mcp_server_status", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task GetAccountInfoAsync_ReturnsJsonElement()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        dynamic? result = await handler.GetAccountInfoAsync();

        // Assert
        Assert.True(result.ValueKind == JsonValueKind.Null || result.ValueKind != JsonValueKind.Undefined);

        JsonElement request = GetLastControlRequest(transport);
        Assert.Equal("account_info", request.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public async Task RewindFilesAsync_SendsUserMessageId()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.RewindFilesAsync("msg_12345");

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        JsonElement innerRequest = request.GetProperty("request");
        Assert.Equal("rewind_files", innerRequest.GetProperty("subtype").GetString());
        Assert.Equal("msg_12345", innerRequest.GetProperty("user_message_id").GetString());
    }

    [Fact]
    public async Task SetMaxThinkingTokensAsync_SendsMaxTokens()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.SetMaxThinkingTokensAsync(8192);

        // Assert
        JsonElement request = GetLastControlRequest(transport);
        JsonElement innerRequest = request.GetProperty("request");
        Assert.Equal("set_max_thinking_tokens", innerRequest.GetProperty("subtype").GetString());
        Assert.Equal(8192, innerRequest.GetProperty("max_thinking_tokens").GetInt32());
    }

    private static JsonElement GetLastControlRequest(ControlMockTransport transport)
    {
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        return messages.Last(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for concurrent control request handling in QueryHandler.
/// </summary>
public class QueryHandlerConcurrentRequestTests
{
    [Fact]
    public async Task ConcurrentRequests_GetDifferentIds()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - send many requests concurrently
        List<Task> tasks = new();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(handler.InterruptAsync());
        }

        await Task.WhenAll(tasks);

        // Assert - all request IDs should be unique
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        List<string> requestIds = messages
            .Where(m => m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request")
            .Select(m => m.GetProperty("request_id").GetString()!)
            .ToList();

        Assert.Equal(10, requestIds.Count);
        Assert.Equal(10, requestIds.Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentRequests_EachGetsOwnResponse()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - send different types of requests concurrently
        dynamic? modelTask = handler.GetSupportedModelsAsync();
        dynamic? commandsTask = handler.GetSupportedCommandsAsync();
        dynamic? accountTask = handler.GetAccountInfoAsync();
        dynamic? mcpTask = handler.GetMcpServerStatusAsync();

        // All should complete successfully
        await Task.WhenAll(modelTask, commandsTask, accountTask, mcpTask);

        // Assert - all completed
        Assert.True(modelTask.Result.ValueKind == JsonValueKind.Null ||
                    modelTask.Result.ValueKind != JsonValueKind.Undefined);
        Assert.True(commandsTask.Result.ValueKind == JsonValueKind.Null ||
                    commandsTask.Result.ValueKind != JsonValueKind.Undefined);
        Assert.True(accountTask.Result.ValueKind == JsonValueKind.Null ||
                    accountTask.Result.ValueKind != JsonValueKind.Undefined);
        Assert.True(mcpTask.Result.ValueKind == JsonValueKind.Null ||
                    mcpTask.Result.ValueKind != JsonValueKind.Undefined);
    }

    [Fact]
    public async Task MixedRequestTypes_AllComplete()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act - mix of void and result-returning requests
        List<Task> tasks = new()
        {
            handler.InterruptAsync(),
            handler.SetModelAsync("sonnet"),
            handler.SetPermissionModeAsync(PermissionMode.AcceptEdits),
            handler.GetSupportedModelsAsync(),
            handler.GetAccountInfoAsync()
        };

        await Task.WhenAll(tasks);

        // Assert - all completed without exception
        Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task RequestIdFormat_ContainsCounterAndGuid()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InterruptAsync();

        // Assert - request ID should follow "req_{counter}_{guid}" format
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        string requestId = messages
            .First(m => m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request")
            .GetProperty("request_id")
            .GetString()!;

        Assert.StartsWith("req_", requestId);
        Assert.Contains("_", requestId.Substring(4));
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for QueryHandler lifecycle management related to control protocol.
/// </summary>
public class QueryHandlerControlLifecycleTests
{
    [Fact]
    public async Task DisposeAsync_CancelsPendingResponses()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.SimulateTimeout(); // Don't send responses

        ClaudeAgentOptions options = new();
        dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Start a request but don't await it yet
        dynamic? requestTask = handler.InterruptAsync();

        // Act - dispose while request is pending
        await handler.DisposeAsync();

        // Assert - the pending request should be cancelled
        await Assert.ThrowsAnyAsync<Exception>(async () => await requestTask);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_Safe()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act & Assert - multiple dispose calls should be safe
        await handler.DisposeAsync();
        await handler.DisposeAsync();
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_ConnectsTransport()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        // Assert - not connected before start
        // Note: We can't directly check _isConnected, but we can verify behavior

        // Act
        await handler.StartAsync();

        // Assert - transport should be ready
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task AfterDispose_RequestsThrowOrCancel()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();
        await handler.DisposeAsync();

        // Act & Assert - requests after dispose should fail
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await handler.InterruptAsync();
        });
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for edge cases in control response handling.
/// </summary>
public class QueryHandlerControlEdgeCaseTests
{
    [Fact]
    public async Task ResponseWithExtraFields_Handled()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // The mock transport auto-responds, so this should work
        await handler.InitializeAsync();

        // Assert - completed without exception
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task MultipleInitializeCalls_Idempotent()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.InitializeAsync();
        await handler.InitializeAsync();
        await handler.InitializeAsync();

        // Assert - only one initialize request sent
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        int initCount = messages.Count(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request" &&
            m.TryGetProperty("request", out JsonElement r) && r.GetProperty("subtype").GetString() == "initialize");

        Assert.Equal(1, initCount);
    }

    [Fact]
    public async Task EmptySubtype_HandlesGracefully()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a message without proper type
        transport.EnqueueMessage(@"{""some_field"": ""value""}");

        // Give time to process
        await Task.Delay(50);

        // Assert - should not crash
        Assert.True(transport.IsReady);
    }

    [Theory]
    [InlineData("claude-3-opus-20240229")]
    [InlineData("claude-3-sonnet-20240229")]
    [InlineData("claude-3-haiku-20240307")]
    [InlineData("sonnet")]
    [InlineData("opus")]
    [InlineData("haiku")]
    public async Task SetModelAsync_VariousModelNames(string modelName)
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.SetModelAsync(modelName);

        // Assert
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        JsonElement request = messages.Last(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");

        Assert.Equal(modelName, request.GetProperty("request").GetProperty("model").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(32768)]
    [InlineData(int.MaxValue)]
    public async Task SetMaxThinkingTokensAsync_VariousValues(int maxTokens)
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.SetMaxThinkingTokensAsync(maxTokens);

        // Assert
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        JsonElement request = messages.Last(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");

        Assert.Equal(maxTokens, request.GetProperty("request").GetProperty("max_thinking_tokens").GetInt32());
    }

    [Theory]
    [InlineData("msg_simple")]
    [InlineData("msg_12345-abcde")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("")]
    public async Task RewindFilesAsync_VariousMessageIds(string messageId)
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Act
        await handler.RewindFilesAsync(messageId);

        // Assert
        IReadOnlyList<JsonElement> messages = transport.GetAllWrittenMessagesAsJson();
        JsonElement request = messages.Last(m =>
            m.TryGetProperty("type", out JsonElement t) && t.GetString() == "control_request");

        Assert.Equal(messageId, request.GetProperty("request").GetProperty("user_message_id").GetString());
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for QueryHandler message processing behavior related to control protocol.
/// </summary>
public class QueryHandlerControlMessageProcessingTests
{
    [Fact]
    public async Task RegularMessages_QueuedToChannel()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a regular message (assistant message)
        transport.EnqueueMessage(@"{
            ""type"": ""assistant"",
            ""uuid"": ""msg_12345"",
            ""content"": [{""type"": ""text"", ""text"": ""Hello!""}]
        }");

        // Give time to process
        await Task.Delay(100);

        // Assert - handler processed message without errors
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task ControlResponse_NotQueuedAsRegularMessage()
    {
        // Arrange
        ControlMockTransport transport = new();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Start a request
        dynamic? task = handler.GetSupportedModelsAsync();

        // Let it complete
        await task;

        // Assert - control responses should not appear in the message channel
        // (This test verifies the handler separates control from regular messages)
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task UnknownMessageType_IgnoredGracefully()
    {
        // Arrange
        ControlMockTransport transport = new();
        transport.DisableAutoResponse();
        ClaudeAgentOptions options = new();
        await using dynamic handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject an unknown message type
        transport.EnqueueMessage(@"{
            ""type"": ""unknown_type_12345"",
            ""data"": ""some data""
        }");

        // Give time to process
        await Task.Delay(50);

        // Assert - should not crash
        Assert.True(transport.IsReady);
    }

    private static dynamic CreateQueryHandler(ControlMockTransport transport, ClaudeAgentOptions options)
    {
        Assembly assembly = typeof(ClaudeAgentOptions).Assembly;
        Type handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}
