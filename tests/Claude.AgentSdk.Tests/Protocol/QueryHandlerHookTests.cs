using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Transport;
using Moq;
using Xunit;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Comprehensive unit tests for QueryHandler's hook callback functionality.
///     Tests hook registration, callback handling, input parsing, and output conversion.
/// </summary>
public class QueryHandlerHookTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    #region Helper Classes

    /// <summary>
    ///     Mock transport implementation for testing QueryHandler hook functionality.
    /// </summary>
    private sealed class HookTestMockTransport : ITransport
    {
        private readonly Channel<JsonDocument> _incomingMessages = Channel.CreateUnbounded<JsonDocument>();
        private readonly List<object> _writtenMessages = new();
        private readonly object _writeLock = new();

        public bool IsReady { get; private set; }

        public IReadOnlyList<object> WrittenMessages
        {
            get
            {
                lock (_writeLock)
                {
                    return _writtenMessages.ToList();
                }
            }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(JsonDocument message, CancellationToken cancellationToken = default)
        {
            lock (_writeLock)
            {
                _writtenMessages.Add(message);
            }
            return Task.CompletedTask;
        }

        public Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            lock (_writeLock)
            {
                // Serialize to JSON and parse back to capture the actual structure
                var json = JsonSerializer.Serialize(message, JsonOptions);
                _writtenMessages.Add(JsonDocument.Parse(json));
            }
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<JsonDocument> ReadMessagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var msg in _incomingMessages.Reader.ReadAllAsync(cancellationToken))
            {
                yield return msg;
            }
        }

        public Task EndInputAsync(CancellationToken cancellationToken = default)
        {
            _incomingMessages.Writer.Complete();
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            _incomingMessages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _incomingMessages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        ///     Enqueue a message to be received by the QueryHandler.
        /// </summary>
        public void EnqueueIncomingMessage(string json)
        {
            _incomingMessages.Writer.TryWrite(JsonDocument.Parse(json));
        }

        /// <summary>
        ///     Enqueue a control request that triggers a hook callback.
        /// </summary>
        public void EnqueueHookCallbackRequest(string requestId, string callbackId, string hookEventName,
            string? toolUseId = null, string additionalInput = "")
        {
            var toolUseIdJson = toolUseId is not null ? $"\"tool_use_id\": \"{toolUseId}\"," : "";
            var json = $$"""
                {
                    "type": "control_request",
                    "request_id": "{{requestId}}",
                    "request": {
                        "subtype": "hook_callback",
                        "callback_id": "{{callbackId}}",
                        {{toolUseIdJson}}
                        "input": {
                            "hook_event_name": "{{hookEventName}}",
                            "session_id": "test-session",
                            "transcript_path": "/test/transcript.json",
                            "cwd": "/test/cwd"
                            {{additionalInput}}
                        }
                    }
                }
                """;
            EnqueueIncomingMessage(json);
        }

        /// <summary>
        ///     Gets the last written message as a JsonElement.
        /// </summary>
        public JsonElement? GetLastWrittenMessage()
        {
            lock (_writeLock)
            {
                if (_writtenMessages.Count == 0)
                    return null;
                return _writtenMessages.Last() switch
                {
                    JsonDocument doc => doc.RootElement,
                    _ => null
                };
            }
        }

        /// <summary>
        ///     Gets a written message at a specific index.
        /// </summary>
        public JsonElement? GetWrittenMessageAt(int index)
        {
            lock (_writeLock)
            {
                if (index < 0 || index >= _writtenMessages.Count)
                    return null;
                return _writtenMessages[index] switch
                {
                    JsonDocument doc => doc.RootElement,
                    _ => null
                };
            }
        }
    }

    /// <summary>
    ///     Helper to create QueryHandler with options and track hook invocations.
    /// </summary>
    private sealed class HookTestContext : IAsyncDisposable
    {
        private readonly HookTestMockTransport _transport;
        private readonly object _queryHandler;
        private readonly List<(HookInput Input, string? ToolUseId, HookContext Context)> _invocations = new();
        private HookOutput _returnOutput = new SyncHookOutput { Continue = true };
        private Exception? _throwException;
        private bool _shouldCancel;

        public IReadOnlyList<(HookInput Input, string? ToolUseId, HookContext Context)> Invocations => _invocations;
        public HookTestMockTransport Transport => _transport;

        public HookTestContext(Dictionary<HookEvent, IReadOnlyList<HookMatcher>>? hooks = null)
        {
            _transport = new HookTestMockTransport();

            var options = new ClaudeAgentOptions
            {
                Hooks = hooks
            };

            // Use reflection to create QueryHandler since it's internal
            var queryHandlerType = typeof(ClaudeAgentOptions).Assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
            _queryHandler = Activator.CreateInstance(queryHandlerType, _transport, options, null)!;
        }

        public HookCallback CreateTrackingCallback()
        {
            return async (input, toolUseId, context, ct) =>
            {
                _invocations.Add((input, toolUseId, context));

                if (_shouldCancel)
                {
                    ct.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(ct);
                }

                if (_throwException is not null)
                {
                    throw _throwException;
                }

                return _returnOutput;
            };
        }

        public void SetReturnOutput(HookOutput output)
        {
            _returnOutput = output;
        }

        public void SetThrowException(Exception ex)
        {
            _throwException = ex;
        }

        public void SetShouldCancel(bool shouldCancel)
        {
            _shouldCancel = shouldCancel;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var startMethod = _queryHandler.GetType().GetMethod("StartAsync")!;
            await (Task)startMethod.Invoke(_queryHandler, new object[] { cancellationToken })!;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var initMethod = _queryHandler.GetType().GetMethod("InitializeAsync")!;
            await (Task)initMethod.Invoke(_queryHandler, new object[] { cancellationToken })!;
        }

        public ValueTask DisposeAsync()
        {
            var disposeMethod = _queryHandler.GetType().GetMethod("DisposeAsync")!;
            return (ValueTask)disposeMethod.Invoke(_queryHandler, Array.Empty<object>())!;
        }
    }

    #endregion

    #region Hook Registration (BuildHooksConfig) Tests

    [Fact]
    public async Task BuildHooksConfig_WithHooks_RegistersCallbackIds()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[]
            {
                new HookMatcher
                {
                    Matcher = "Bash",
                    Hooks = new[] { callback }
                }
            }
        };

        await using var context = new HookTestContext(hooks);
        var transport = context.Transport;

        // Simulate a control response for the initialize request
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            var initRequestId = GetInitializeRequestId(transport);
            if (initRequestId is not null)
            {
                transport.EnqueueIncomingMessage($$"""
                    {
                        "type": "control_response",
                        "response": {
                            "request_id": "{{initRequestId}}",
                            "subtype": "success"
                        }
                    }
                    """);
            }
        });

        await context.StartAsync();
        await context.InitializeAsync();

        // Assert - Check that the initialize request was sent with hooks config
        var messages = transport.WrittenMessages;
        Assert.NotEmpty(messages);

        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var request = initRequest.Value.GetProperty("request");
        Assert.True(request.TryGetProperty("hooks", out var hooks_elem));
        Assert.True(hooks_elem.TryGetProperty("PreToolUse", out var preToolUse));

        var matchers = preToolUse.EnumerateArray().ToArray();
        Assert.Single(matchers);
        Assert.Equal("Bash", matchers[0].GetProperty("matcher").GetString());
        Assert.True(matchers[0].TryGetProperty("hookCallbackIds", out var callbackIds));
        Assert.Single(callbackIds.EnumerateArray());
    }

    [Fact]
    public async Task BuildHooksConfig_WithMultipleMatchers_RegistersAllCallbacks()
    {
        // Arrange
        HookCallback callback1 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());
        HookCallback callback2 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[]
            {
                new HookMatcher { Matcher = "Bash", Hooks = new[] { callback1 } },
                new HookMatcher { Matcher = "Write|Edit", Hooks = new[] { callback2 } }
            }
        };

        await using var context = new HookTestContext(hooks);
        var transport = context.Transport;

        SetupInitializeResponse(transport);
        await context.StartAsync();
        await context.InitializeAsync();

        // Assert
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var preToolUse = initRequest.Value.GetProperty("request").GetProperty("hooks").GetProperty("PreToolUse");
        var matchers = preToolUse.EnumerateArray().ToArray();
        Assert.Equal(2, matchers.Length);
        Assert.Equal("Bash", matchers[0].GetProperty("matcher").GetString());
        Assert.Equal("Write|Edit", matchers[1].GetProperty("matcher").GetString());
    }

    [Fact]
    public async Task BuildHooksConfig_WithTimeout_IncludesTimeoutProperty()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PostToolUse] = new[]
            {
                new HookMatcher
                {
                    Matcher = "Read",
                    Hooks = new[] { callback },
                    Timeout = 30.5
                }
            }
        };

        await using var context = new HookTestContext(hooks);
        var transport = context.Transport;

        SetupInitializeResponse(transport);
        await context.StartAsync();
        await context.InitializeAsync();

        // Assert
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var postToolUse = initRequest.Value.GetProperty("request").GetProperty("hooks").GetProperty("PostToolUse");
        var matcher = postToolUse.EnumerateArray().First();
        Assert.Equal(30.5, matcher.GetProperty("timeout").GetDouble());
    }

    [Fact]
    public async Task BuildHooksConfig_CallbackIdFormat_IsHook_N()
    {
        // Arrange
        HookCallback callback1 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());
        HookCallback callback2 = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.Stop] = new[]
            {
                new HookMatcher { Hooks = new[] { callback1, callback2 } }
            }
        };

        await using var context = new HookTestContext(hooks);
        var transport = context.Transport;

        SetupInitializeResponse(transport);
        await context.StartAsync();
        await context.InitializeAsync();

        // Assert
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var stopHooks = initRequest.Value.GetProperty("request").GetProperty("hooks").GetProperty("Stop");
        var matcher = stopHooks.EnumerateArray().First();
        var callbackIds = matcher.GetProperty("hookCallbackIds").EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Equal(2, callbackIds.Length);
        Assert.StartsWith("hook_", callbackIds[0]);
        Assert.StartsWith("hook_", callbackIds[1]);
    }

    [Fact]
    public async Task BuildHooksConfig_WithMultipleHookEvents_RegistersAll()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Matcher = "Bash", Hooks = new[] { callback } } },
            [HookEvent.PostToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } },
            [HookEvent.Stop] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var context = new HookTestContext(hooks);
        var transport = context.Transport;

        SetupInitializeResponse(transport);
        await context.StartAsync();
        await context.InitializeAsync();

        // Assert
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var hooksConfig = initRequest.Value.GetProperty("request").GetProperty("hooks");
        Assert.True(hooksConfig.TryGetProperty("PreToolUse", out _));
        Assert.True(hooksConfig.TryGetProperty("PostToolUse", out _));
        Assert.True(hooksConfig.TryGetProperty("Stop", out _));
    }

    [Fact]
    public async Task BuildHooksConfig_WithNoHooks_SendsNullHooksConfig()
    {
        // Arrange
        await using var context = new HookTestContext(null);
        var transport = context.Transport;

        SetupInitializeResponse(transport);
        await context.StartAsync();
        await context.InitializeAsync();

        // Assert
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);

        var request = initRequest.Value.GetProperty("request");
        // hooks should be null or not present
        if (request.TryGetProperty("hooks", out var hooks))
        {
            Assert.Equal(JsonValueKind.Null, hooks.ValueKind);
        }
    }

    #endregion

    #region Hook Callback Handling Tests

    [Fact]
    public async Task HandleHookCallbackAsync_UnknownCallbackId_ReturnsContinueTrue()
    {
        // Arrange
        await using var context = new HookTestContext(null);
        var transport = context.Transport;

        await context.StartAsync();

        // Enqueue a hook callback request with unknown callback ID
        transport.EnqueueHookCallbackRequest(
            requestId: "req-unknown",
            callbackId: "hook_unknown",
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        // Wait for processing
        await Task.Delay(200);

        // Complete the transport to stop the reader
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-unknown");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
    }

    [Fact]
    public async Task HandleHookCallbackAsync_WithToolUseId_PassesToCallback()
    {
        // Arrange
        string? receivedToolUseId = null;
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedToolUseId = toolUseId;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[]
            {
                new HookMatcher { Hooks = new[] { callback } }
            }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        // Get the callback ID from the initialize request
        var initRequest = FindInitializeRequest(transport);
        var callbackId = initRequest!.Value.GetProperty("request")
            .GetProperty("hooks")
            .GetProperty("PreToolUse")
            .EnumerateArray()
            .First()
            .GetProperty("hookCallbackIds")
            .EnumerateArray()
            .First()
            .GetString()!;

        // Enqueue hook callback with toolUseId
        transport.EnqueueHookCallbackRequest(
            requestId: "req-with-tool-use",
            callbackId: callbackId,
            hookEventName: "PreToolUse",
            toolUseId: "toolu_abc123",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.Equal("toolu_abc123", receivedToolUseId);
    }

    [Fact]
    public async Task HandleHookCallbackAsync_WithNullToolUseId_PassesNull()
    {
        // Arrange
        string? receivedToolUseId = "not-null";
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedToolUseId = toolUseId;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.Stop] = new[]
            {
                new HookMatcher { Hooks = new[] { callback } }
            }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "Stop");

        // Enqueue hook callback without toolUseId
        transport.EnqueueHookCallbackRequest(
            requestId: "req-no-tool-use",
            callbackId: callbackId!,
            hookEventName: "Stop",
            additionalInput: ", \"stop_hook_active\": true");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.Null(receivedToolUseId);
    }

    [Fact]
    public async Task HandleHookCallbackAsync_PassesHookContext()
    {
        // Arrange
        HookContext? receivedContext = null;
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedContext = context;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.UserPromptSubmit] = new[]
            {
                new HookMatcher { Hooks = new[] { callback } }
            }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "UserPromptSubmit");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-context",
            callbackId: callbackId!,
            hookEventName: "UserPromptSubmit",
            additionalInput: ", \"prompt\": \"Hello\"");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.NotNull(receivedContext);
    }

    #endregion

    #region Hook Input Parsing Tests

    [Theory]
    [InlineData("PreToolUse", typeof(PreToolUseHookInput), ", \"tool_name\": \"Bash\", \"tool_input\": {\"command\": \"ls\"}")]
    [InlineData("PostToolUse", typeof(PostToolUseHookInput), ", \"tool_name\": \"Bash\", \"tool_input\": {}, \"tool_response\": {\"output\": \"result\"}")]
    [InlineData("PostToolUseFailure", typeof(PostToolUseFailureHookInput), ", \"tool_name\": \"Bash\", \"tool_input\": {}, \"error\": \"Command failed\"")]
    [InlineData("UserPromptSubmit", typeof(UserPromptSubmitHookInput), ", \"prompt\": \"Hello Claude\"")]
    [InlineData("Stop", typeof(StopHookInput), ", \"stop_hook_active\": true")]
    [InlineData("SubagentStart", typeof(SubagentStartHookInput), ", \"agent_id\": \"agent-1\", \"agent_type\": \"task\"")]
    [InlineData("SubagentStop", typeof(SubagentStopHookInput), ", \"stop_hook_active\": false")]
    [InlineData("PreCompact", typeof(PreCompactHookInput), ", \"trigger\": \"token_limit\"")]
    [InlineData("PermissionRequest", typeof(PermissionRequestHookInput), ", \"tool_name\": \"Write\", \"tool_input\": {}")]
    [InlineData("SessionStart", typeof(SessionStartHookInput), ", \"source\": \"startup\"")]
    [InlineData("SessionEnd", typeof(SessionEndHookInput), ", \"reason\": \"logout\"")]
    [InlineData("Notification", typeof(NotificationHookInput), ", \"message\": \"Test\", \"notification_type\": \"idle_prompt\"")]
    public async Task ParseHookInput_AllEventTypes_ParsesCorrectType(string hookEventName, Type expectedInputType, string additionalInput)
    {
        // Arrange
        HookInput? receivedInput = null;
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedInput = input;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        var hookEvent = Enum.Parse<HookEvent>(hookEventName);
        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [hookEvent] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, hookEventName);

        transport.EnqueueHookCallbackRequest(
            requestId: $"req-{hookEventName}",
            callbackId: callbackId!,
            hookEventName: hookEventName,
            additionalInput: additionalInput);

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.NotNull(receivedInput);
        Assert.IsType(expectedInputType, receivedInput);
        Assert.Equal(hookEventName, receivedInput.HookEventName);
    }

    [Fact]
    public async Task ParseHookInput_PreToolUseHookInput_ParsesAllProperties()
    {
        // Arrange
        PreToolUseHookInput? receivedInput = null;
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedInput = input as PreToolUseHookInput;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-pre-tool",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Write\", \"tool_input\": {\"file_path\": \"/test.txt\", \"content\": \"hello\"}, \"permission_mode\": \"strict\"");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.NotNull(receivedInput);
        Assert.Equal("Write", receivedInput.ToolName);
        Assert.Equal("/test.txt", receivedInput.ToolInput.GetProperty("file_path").GetString());
        Assert.Equal("hello", receivedInput.ToolInput.GetProperty("content").GetString());
        Assert.Equal("strict", receivedInput.PermissionMode);
        Assert.Equal("test-session", receivedInput.SessionId);
        Assert.Equal("/test/transcript.json", receivedInput.TranscriptPath);
        Assert.Equal("/test/cwd", receivedInput.Cwd);
    }

    [Fact]
    public async Task ParseHookInput_UnknownHookEvent_ReturnsNull()
    {
        // Arrange
        await using var ctx = new HookTestContext(null);
        var transport = ctx.Transport;

        await ctx.StartAsync();

        // Send a hook callback with unknown event name
        transport.EnqueueIncomingMessage("""
            {
                "type": "control_request",
                "request_id": "req-unknown-event",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {
                        "hook_event_name": "UnknownEvent",
                        "session_id": "test",
                        "transcript_path": "/test",
                        "cwd": "/cwd"
                    }
                }
            }
            """);

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert - should return continue: true for unknown events
        var response = FindControlResponseFor(transport, "req-unknown-event");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
    }

    #endregion

    #region Hook Output Conversion Tests

    [Fact]
    public async Task ConvertHookOutput_SyncHookOutput_WithContinueTrue()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.Stop] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "Stop");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-continue-true",
            callbackId: callbackId!,
            hookEventName: "Stop",
            additionalInput: ", \"stop_hook_active\": true");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-continue-true");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
    }

    [Fact]
    public async Task ConvertHookOutput_SyncHookOutput_WithContinueFalse()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new SyncHookOutput { Continue = false });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-continue-false",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-continue-false");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.False(responseData.GetProperty("continue").GetBoolean());
    }

    [Fact]
    public async Task ConvertHookOutput_SyncHookOutput_WithAllProperties()
    {
        // Arrange
        var hookSpecificOutput = JsonDocument.Parse("{\"modified_input\": {\"command\": \"safe-cmd\"}}").RootElement;

        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new SyncHookOutput
            {
                Continue = false,
                SuppressOutput = true,
                StopReason = "Blocked by policy",
                Decision = "block",
                SystemMessage = "Warning: dangerous operation",
                Reason = "Security policy violation",
                HookSpecificOutput = hookSpecificOutput
            });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-all-props",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-all-props");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.False(responseData.GetProperty("continue").GetBoolean());
        Assert.True(responseData.GetProperty("suppressOutput").GetBoolean());
        Assert.Equal("Blocked by policy", responseData.GetProperty("stopReason").GetString());
        Assert.Equal("block", responseData.GetProperty("decision").GetString());
        Assert.Equal("Warning: dangerous operation", responseData.GetProperty("systemMessage").GetString());
        Assert.Equal("Security policy violation", responseData.GetProperty("reason").GetString());
        Assert.True(responseData.TryGetProperty("hookSpecificOutput", out var hso));
        Assert.Equal("safe-cmd", hso.GetProperty("modified_input").GetProperty("command").GetString());
    }

    [Fact]
    public async Task ConvertHookOutput_SyncHookOutput_NullFieldsOmitted()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PostToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PostToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-null-fields",
            callbackId: callbackId!,
            hookEventName: "PostToolUse",
            additionalInput: ", \"tool_name\": \"Read\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-null-fields");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());

        // Null fields should not be present
        Assert.False(responseData.TryGetProperty("suppressOutput", out _));
        Assert.False(responseData.TryGetProperty("stopReason", out _));
        Assert.False(responseData.TryGetProperty("decision", out _));
        Assert.False(responseData.TryGetProperty("systemMessage", out _));
        Assert.False(responseData.TryGetProperty("reason", out _));
        Assert.False(responseData.TryGetProperty("hookSpecificOutput", out _));
    }

    [Fact]
    public async Task ConvertHookOutput_AsyncHookOutput_WithTimeout()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new AsyncHookOutput { AsyncTimeout = 5000 });

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-async",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-async");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("async").GetBoolean());
        Assert.Equal(5000, responseData.GetProperty("asyncTimeout").GetInt32());
    }

    [Fact]
    public async Task ConvertHookOutput_DefaultContinueTrue_WhenContinueNull()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            Task.FromResult<HookOutput>(new SyncHookOutput()); // Continue is null

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.Notification] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "Notification");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-default-continue",
            callbackId: callbackId!,
            hookEventName: "Notification",
            additionalInput: ", \"message\": \"test\", \"notification_type\": \"auth_success\"");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-default-continue");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
    }

    #endregion

    #region Hook Error Handling Tests

    [Fact]
    public async Task HandleHookCallbackAsync_CallbackThrowsException_ReturnsErrorResponse()
    {
        // Arrange
        HookCallback callback = (_, _, _, _) =>
            throw new InvalidOperationException("Test exception message");

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { callback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");

        transport.EnqueueHookCallbackRequest(
            requestId: "req-exception",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-exception");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
        Assert.Contains("Hook error", responseData.GetProperty("reason").GetString());
        Assert.Contains("Test exception message", responseData.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task HandleHookCallbackAsync_MissingHookEventName_ReturnsContinueTrue()
    {
        // Arrange
        await using var ctx = new HookTestContext(null);
        var transport = ctx.Transport;

        await ctx.StartAsync();

        // Send a hook callback without hook_event_name
        transport.EnqueueIncomingMessage("""
            {
                "type": "control_request",
                "request_id": "req-no-event-name",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {
                        "session_id": "test",
                        "transcript_path": "/test",
                        "cwd": "/cwd"
                    }
                }
            }
            """);

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        var response = FindControlResponseFor(transport, "req-no-event-name");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullHookLifecycle_RegisterInvokeRespond()
    {
        // Arrange
        var invocationCount = 0;
        HookInput? lastInput = null;

        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            invocationCount++;
            lastInput = input;
            return Task.FromResult<HookOutput>(new SyncHookOutput
            {
                Continue = true,
                Reason = "Hook processed successfully"
            });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[]
            {
                new HookMatcher
                {
                    Matcher = "Bash",
                    Hooks = new[] { callback },
                    Timeout = 10.0
                }
            }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        // Step 1: Initialize
        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        // Step 2: Verify hook registration
        var initRequest = FindInitializeRequest(transport);
        Assert.NotNull(initRequest);
        var callbackId = GetCallbackIdForEvent(transport, "PreToolUse");
        Assert.NotNull(callbackId);

        // Step 3: Trigger hook callback
        transport.EnqueueHookCallbackRequest(
            requestId: "req-full-lifecycle",
            callbackId: callbackId!,
            hookEventName: "PreToolUse",
            toolUseId: "toolu_lifecycle_123",
            additionalInput: ", \"tool_name\": \"Bash\", \"tool_input\": {\"command\": \"echo hello\"}");

        await Task.Delay(200);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Step 4: Verify callback was invoked
        Assert.Equal(1, invocationCount);
        Assert.NotNull(lastInput);
        Assert.IsType<PreToolUseHookInput>(lastInput);
        Assert.Equal("Bash", ((PreToolUseHookInput)lastInput).ToolName);

        // Step 5: Verify response was sent
        var response = FindControlResponseFor(transport, "req-full-lifecycle");
        Assert.NotNull(response);

        var responseData = response.Value.GetProperty("response").GetProperty("response");
        Assert.True(responseData.GetProperty("continue").GetBoolean());
        Assert.Equal("Hook processed successfully", responseData.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task MultipleHookCallbacks_ProcessedIndependently()
    {
        // Arrange
        var preToolUseInvocations = 0;
        var postToolUseInvocations = 0;

        HookCallback preToolUseCallback = (_, _, _, _) =>
        {
            preToolUseInvocations++;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true, Reason = "pre" });
        };

        HookCallback postToolUseCallback = (_, _, _, _) =>
        {
            postToolUseInvocations++;
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true, Reason = "post" });
        };

        var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new[] { new HookMatcher { Hooks = new[] { preToolUseCallback } } },
            [HookEvent.PostToolUse] = new[] { new HookMatcher { Hooks = new[] { postToolUseCallback } } }
        };

        await using var ctx = new HookTestContext(hooks);
        var transport = ctx.Transport;

        SetupInitializeResponse(transport);
        await ctx.StartAsync();
        await ctx.InitializeAsync();

        var preCallbackId = GetCallbackIdForEvent(transport, "PreToolUse");
        var postCallbackId = GetCallbackIdForEvent(transport, "PostToolUse");

        // Trigger pre-tool-use
        transport.EnqueueHookCallbackRequest(
            requestId: "req-pre",
            callbackId: preCallbackId!,
            hookEventName: "PreToolUse",
            additionalInput: ", \"tool_name\": \"Read\", \"tool_input\": {}");

        // Trigger post-tool-use
        transport.EnqueueHookCallbackRequest(
            requestId: "req-post",
            callbackId: postCallbackId!,
            hookEventName: "PostToolUse",
            additionalInput: ", \"tool_name\": \"Read\", \"tool_input\": {}, \"tool_response\": {}");

        await Task.Delay(300);
        await transport.EndInputAsync();
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, preToolUseInvocations);
        Assert.Equal(1, postToolUseInvocations);

        var preResponse = FindControlResponseFor(transport, "req-pre");
        var postResponse = FindControlResponseFor(transport, "req-post");

        Assert.NotNull(preResponse);
        Assert.NotNull(postResponse);

        Assert.Equal("pre", preResponse.Value.GetProperty("response").GetProperty("response").GetProperty("reason").GetString());
        Assert.Equal("post", postResponse.Value.GetProperty("response").GetProperty("response").GetProperty("reason").GetString());
    }

    #endregion

    #region Helper Methods

    private static void SetupInitializeResponse(HookTestMockTransport transport)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            for (var i = 0; i < 100; i++)
            {
                var initRequestId = GetInitializeRequestId(transport);
                if (initRequestId is not null)
                {
                    transport.EnqueueIncomingMessage($$"""
                        {
                            "type": "control_response",
                            "response": {
                                "request_id": "{{initRequestId}}",
                                "subtype": "success"
                            }
                        }
                        """);
                    return;
                }
                await Task.Delay(10);
            }
        });
    }

    private static string? GetInitializeRequestId(HookTestMockTransport transport)
    {
        var initRequest = FindInitializeRequest(transport);
        return initRequest?.GetProperty("request_id").GetString();
    }

    private static JsonElement? FindInitializeRequest(HookTestMockTransport transport)
    {
        foreach (var msg in transport.WrittenMessages)
        {
            if (msg is JsonDocument doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeElem) &&
                    typeElem.GetString() == "control_request" &&
                    root.TryGetProperty("request", out var reqElem) &&
                    reqElem.TryGetProperty("subtype", out var subtypeElem) &&
                    subtypeElem.GetString() == "initialize")
                {
                    return root;
                }
            }
        }
        return null;
    }

    private static string? GetCallbackIdForEvent(HookTestMockTransport transport, string eventName)
    {
        var initRequest = FindInitializeRequest(transport);
        if (initRequest is null) return null;

        var hooks = initRequest.Value.GetProperty("request").GetProperty("hooks");
        if (!hooks.TryGetProperty(eventName, out var eventHooks))
            return null;

        var matcher = eventHooks.EnumerateArray().FirstOrDefault();
        if (matcher.ValueKind == JsonValueKind.Undefined)
            return null;

        var callbackIds = matcher.GetProperty("hookCallbackIds").EnumerateArray();
        return callbackIds.FirstOrDefault().GetString();
    }

    private static JsonElement? FindControlResponseFor(HookTestMockTransport transport, string requestId)
    {
        foreach (var msg in transport.WrittenMessages)
        {
            if (msg is JsonDocument doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeElem) &&
                    typeElem.GetString() == "control_response" &&
                    root.TryGetProperty("response", out var respElem) &&
                    respElem.TryGetProperty("request_id", out var reqIdElem) &&
                    reqIdElem.GetString() == requestId)
                {
                    return root;
                }
            }
        }
        return null;
    }

    #endregion
}
