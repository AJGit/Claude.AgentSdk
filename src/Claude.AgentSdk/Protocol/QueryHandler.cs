using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Logging;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Tools;
using Claude.AgentSdk.Transport;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Protocol;

/// <summary>
///     Handles the bidirectional control protocol with the Claude CLI.
///     Routes messages between regular message stream and control requests.
/// </summary>
/// <remarks>
///     As the central protocol handler, this class necessarily coordinates between transport, messages,
///     hooks, MCP servers, and control requests. The size and coupling are inherent to its role as
///     the main protocol orchestration point. Splitting this class would fragment the protocol logic
///     and make the control flow harder to follow.
/// </remarks>
internal sealed class QueryHandler : IAsyncDisposable
{
    private readonly Dictionary<string, Func<JsonElement, CancellationToken, Task<object?>>> _controlRequestHandlers;
    private readonly Dictionary<string, HookCallback> _hookCallbacks = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<QueryHandler>? _logger;
    private readonly Dictionary<string, IMcpToolServer> _mcpServers = new();

    private readonly Channel<Message> _messageChannel;
    private readonly ClaudeAgentOptions _options;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingResponses = new();
    private readonly ITransport _transport;
    private bool _disposed;
    private int _hookCallbackCounter;
    private bool _initialized;

    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private int _requestCounter;

    public QueryHandler(
        ITransport transport,
        ClaudeAgentOptions options,
        ILogger<QueryHandler>? logger = null)
    {
        _transport = transport;
        _options = options;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(options.MessageChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Register control request handlers
        _controlRequestHandlers = new Dictionary<string, Func<JsonElement, CancellationToken, Task<object?>>>
        {
            [ControlSubtype.CanUseTool] = HandleToolPermissionAsync,
            [ControlSubtype.HookCallback] = HandleHookCallbackAsync,
            [ControlSubtype.McpMessage] = HandleMcpMessageAsync
        };

        // Register SDK MCP servers
        if (options.McpServers is not null)
        {
            foreach (var (name, config) in options.McpServers)
            {
                if (config is McpSdkServerConfig sdkConfig)
                {
                    _mcpServers[name] = sdkConfig.Instance;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the exception that caused the message stream to terminate, if any.
    /// </summary>
    /// <remarks>
    ///     This is null if the stream ended normally (cancellation or EOF).
    ///     Check this property after the message stream completes to distinguish
    ///     between normal termination and failure.
    /// </remarks>
    public Exception? TerminalException { get; private set; }

    /// <summary>
    ///     When true, completes the message channel after receiving a final ResultMessage.
    ///     Use for one-shot queries. For interactive multi-turn sessions, keep false.
    /// </summary>
    public bool CompleteOnResult { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Proactively complete the channel to unblock consumers immediately,
        // even if the reader loop is stuck on IO
        _messageChannel.Writer.TryComplete();

        if (_readerCts is not null)
        {
            await _readerCts.CancelAsync().ConfigureAwait(false);
        }

        if (_readerTask is not null)
        {
            try
            {
                await _readerTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        _readerCts?.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);

        // Cancel any pending responses
        foreach (var tcs in _pendingResponses.Values)
        {
            tcs.TrySetCanceled();
        }

        _pendingResponses.Clear();
    }

    /// <summary>
    ///     Start the message reader loop.
    /// </summary>
    /// <remarks>
    ///     The cancellation token only governs the transport connection phase.
    ///     The reader loop runs until the handler is disposed - it is not linked
    ///     to the caller's token to avoid premature cancellation when the startup
    ///     token scope ends.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);

        // Use a standalone CTS for the reader loop - not linked to the caller's token.
        // The reader should only stop when DisposeAsync is called, not when the
        // startup cancellation token is cancelled.
        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReadMessagesLoopAsync(_readerCts.Token), cancellationToken);
    }

    /// <summary>
    ///     Initialize the control protocol (for streaming mode).
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var hooks = BuildHooksConfig();

        var request = new InitializeRequestBody { Hooks = hooks };

        await SendControlRequestAsync(request, cancellationToken).ConfigureAwait(false);
        _initialized = true;

        if (_logger is not null)
        {
            Log.ControlProtocolInitialized(_logger);
        }
    }

    /// <summary>
    ///     Send a message to the CLI.
    /// </summary>
    public Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        return _transport.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    ///     Send a user message (prompt) to the CLI.
    /// </summary>
    public Task SendUserMessageAsync(string content, string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var message = new UserMessageEnvelope
        {
            Message = new UserMessagePayload { Content = content },
            SessionId = sessionId
        };

        return _transport.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    ///     Receive messages from the CLI.
    /// </summary>
    public async IAsyncEnumerable<Message> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return message;
        }
    }

    /// <summary>
    ///     Send an interrupt signal.
    /// </summary>
    public Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new InterruptRequestBody(), cancellationToken);
    }

    /// <summary>
    ///     Set the permission mode.
    /// </summary>
    public Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        var modeString = mode switch
        {
            PermissionMode.Default => "default",
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            _ => "default"
        };
        return SendControlRequestAsync(new SetPermissionModeRequestBody { Mode = modeString }, cancellationToken);
    }

    /// <summary>
    ///     Set the model.
    /// </summary>
    public Task SetModelAsync(string model, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetModelRequestBody { Model = model }, cancellationToken);
    }

    /// <summary>
    ///     Rewind files to a specific user message.
    /// </summary>
    public Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new RewindFilesRequestBody { UserMessageId = userMessageId }, cancellationToken);
    }

    /// <summary>
    ///     Set max thinking tokens.
    /// </summary>
    public Task SetMaxThinkingTokensAsync(int maxTokens, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetMaxThinkingTokensRequestBody { MaxThinkingTokens = maxTokens },
            cancellationToken);
    }

    /// <summary>
    ///     Get the list of supported slash commands.
    /// </summary>
    public Task<JsonElement> GetSupportedCommandsAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SupportedCommandsRequestBody(), cancellationToken);
    }

    /// <summary>
    ///     Get the list of supported models.
    /// </summary>
    public Task<JsonElement> GetSupportedModelsAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SupportedModelsRequestBody(), cancellationToken);
    }

    /// <summary>
    ///     Get MCP server status.
    /// </summary>
    public Task<JsonElement> GetMcpServerStatusAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new McpServerStatusRequestBody(), cancellationToken);
    }

    /// <summary>
    ///     Get account information.
    /// </summary>
    public Task<JsonElement> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new AccountInfoRequestBody(), cancellationToken);
    }

    private async Task ReadMessagesLoopAsync(CancellationToken cancellationToken)
    {
        Exception? completionException = null;

        try
        {
            await foreach (var doc in _transport.ReadMessagesAsync(cancellationToken).ConfigureAwait(false))
            {
                await ProcessMessageAsync(doc, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.MessageReaderCancelled(_logger);
            }
            // Normal cancellation - no exception to propagate
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.MessageReaderError(_logger, ex);
            }

            // Store terminal exception for consumers to inspect
            TerminalException = ex;
            completionException = ex;
        }
        finally
        {
            // Complete the channel, optionally with the exception so consumers can observe the failure
            // TryComplete returns false if already completed (e.g., by ResultMessage handling)
            _messageChannel.Writer.TryComplete(completionException);
        }
    }

    private async Task ProcessMessageAsync(JsonDocument doc, CancellationToken cancellationToken)
    {
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
        {
            if (_logger is not null)
            {
                Log.MessageMissingType(_logger);
            }

            return;
        }

        var type = typeElement.GetString();

        switch (type)
        {
            case "control_response":
                HandleControlResponse(root);
                break;

            case "control_request":
                // Handle control requests - await to ensure response is sent promptly
                await HandleControlRequestAsync(root, cancellationToken).ConfigureAwait(false);
                break;

            case "control_cancel_request":
                // CLI is requesting cancellation of a pending operation
                // Currently we acknowledge but don't have pending operations to cancel
                if (_logger is not null)
                {
                    Log.ControlCancelRequestReceived(_logger);
                }

                break;

            default:
                // Regular message - parse and queue
                var message = ParseMessage(root, type);
                if (message is not null)
                {
                    await _messageChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);

                    // In one-shot query mode, final ResultMessage indicates completion
                    // Only complete on terminal subtypes (success, error_*, etc.), not on "partial"
                    // In interactive mode (ConnectAsync), keep channel open for multi-turn conversations
                    if (CompleteOnResult && message is ResultMessage result && result.Subtype != "partial")
                    {
                        if (_logger is not null)
                        {
                            Log.ResultMessageReceived(_logger, result.Subtype);
                        }

                        _messageChannel.Writer.Complete();
                    }
                }

                break;
        }
    }

    private void HandleControlResponse(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var response))
        {
            if (_logger is not null)
            {
                Log.ControlResponseMissingResponse(_logger);
            }

            return;
        }

        if (!response.TryGetProperty("request_id", out var requestIdElement))
        {
            if (_logger is not null)
            {
                Log.ControlResponseMissingRequestId(_logger);
            }

            return;
        }

        var requestId = requestIdElement.GetString()!;

        if (_pendingResponses.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
        else if (_logger is not null)
        {
            Log.UnknownRequestResponse(_logger, requestId);
        }
    }

    private async Task HandleControlRequestAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var requestId = root.GetProperty("request_id").GetString()!;
        var request = root.GetProperty("request");
        var subtype = request.GetProperty("subtype").GetString()!;

        try
        {
            object? responseData = null;

            if (_controlRequestHandlers.TryGetValue(subtype, out var handler))
            {
                responseData = await handler(request, cancellationToken).ConfigureAwait(false);
            }

            await SendControlResponseAsync(requestId, responseData, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.ControlRequestError(_logger, ex, subtype);
            }

            await SendControlErrorResponseAsync(requestId, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<object?> HandleToolPermissionAsync(JsonElement request, CancellationToken cancellationToken)
    {
        if (_options.CanUseTool is null)
            // No callback - allow by default
        {
            return new PermissionAllowResponse();
        }

        var toolName = request.GetProperty("tool_name").GetString()!;
        var input = request.GetProperty("input");

        var permissionRequest = new ToolPermissionRequest
        {
            ToolName = toolName,
            Input = input,
            BlockedPath = request.TryGetProperty("blocked_path", out var bp) ? bp.GetString() : null
        };

        var result = await _options.CanUseTool(permissionRequest, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            PermissionResultAllow allow => new PermissionAllowResponse
            {
                UpdatedInput = allow.UpdatedInput,
                UpdatedPermissions = allow.UpdatedPermissions
            },
            PermissionResultDeny deny => new PermissionDenyResponse
            {
                Message = deny.Message,
                Interrupt = deny.Interrupt
            },
            _ => new PermissionAllowResponse()
        };
    }

    private async Task<object?> HandleHookCallbackAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var callbackId = request.GetProperty("callback_id").GetString()!;
        var input = request.GetProperty("input");
        var toolUseId = request.TryGetProperty("tool_use_id", out var tui) ? tui.GetString() : null;

        // Look up the callback by ID
        if (!_hookCallbacks.TryGetValue(callbackId, out var callback))
        {
            if (_logger is not null)
            {
                Log.HookCallbackNotFound(_logger, callbackId);
            }

            return new HookContinueResponse();
        }

        // Parse hook event from input
        if (!input.TryGetProperty("hook_event_name", out var eventNameElement))
        {
            return new HookContinueResponse();
        }

        var eventName = eventNameElement.GetString()!;
        var hookInput = ParseHookInput(input, eventName);

        if (hookInput is null)
        {
            return new HookContinueResponse();
        }

        var context = new HookContext();

        try
        {
            var output = await callback(hookInput, toolUseId, context, cancellationToken).ConfigureAwait(false);
            return ConvertHookOutput(output);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.HookCallbackError(_logger, ex, callbackId);
            }

            return new HookContinueResponse { Reason = $"Hook error: {ex.Message}" };
        }
    }

    private async Task<object?> HandleMcpMessageAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var serverName = request.GetProperty("server_name").GetString()!;
        var message = request.GetProperty("message");
        var messageId = ExtractJsonRpcId(message);

        if (!_mcpServers.TryGetValue(serverName, out var server))
        {
            return new McpResponseWrapper
                { McpResponse = BuildJsonRpcError(messageId, -32601, $"Server '{serverName}' not found") };
        }

        try
        {
            var response = await server.HandleRequestAsync(message, cancellationToken).ConfigureAwait(false);
            return new McpResponseWrapper { McpResponse = response };
        }
        catch (Exception ex)
        {
            return new McpResponseWrapper { McpResponse = BuildJsonRpcError(messageId, -32603, ex.Message) };
        }
    }

    private static object? ExtractJsonRpcId(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElem))
        {
            return null;
        }

        return idElem.ValueKind switch
        {
            JsonValueKind.Number => idElem.GetInt64(),
            JsonValueKind.String => idElem.GetString(),
            _ => null
        };
    }

    private static Dictionary<string, object?> BuildJsonRpcError(object? id, int code, string message)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object> { ["code"] = code, ["message"] = message }
        };
    }

    /// <summary>
    ///     Deserializes hook input based on event type.
    /// </summary>
    /// <remarks>
    ///     Complexity is inherent to the number of hook event types in the protocol (12 types).
    ///     Each case is a simple deserialization call - no nested logic.
    /// </remarks>
    private HookInput? ParseHookInput(JsonElement input, string eventName)
    {
        try
        {
            return eventName switch
            {
                "PreToolUse" => JsonSerializer.Deserialize<PreToolUseHookInput>(input.GetRawText(), _jsonOptions),
                "PostToolUse" => JsonSerializer.Deserialize<PostToolUseHookInput>(input.GetRawText(), _jsonOptions),
                "PostToolUseFailure" => JsonSerializer.Deserialize<PostToolUseFailureHookInput>(input.GetRawText(),
                    _jsonOptions),
                "UserPromptSubmit" => JsonSerializer.Deserialize<UserPromptSubmitHookInput>(input.GetRawText(),
                    _jsonOptions),
                "Stop" => JsonSerializer.Deserialize<StopHookInput>(input.GetRawText(), _jsonOptions),
                "SubagentStart" => JsonSerializer.Deserialize<SubagentStartHookInput>(input.GetRawText(), _jsonOptions),
                "SubagentStop" => JsonSerializer.Deserialize<SubagentStopHookInput>(input.GetRawText(), _jsonOptions),
                "PreCompact" => JsonSerializer.Deserialize<PreCompactHookInput>(input.GetRawText(), _jsonOptions),
                "PermissionRequest" => JsonSerializer.Deserialize<PermissionRequestHookInput>(input.GetRawText(),
                    _jsonOptions),
                "SessionStart" => JsonSerializer.Deserialize<SessionStartHookInput>(input.GetRawText(), _jsonOptions),
                "SessionEnd" => JsonSerializer.Deserialize<SessionEndHookInput>(input.GetRawText(), _jsonOptions),
                "Notification" => JsonSerializer.Deserialize<NotificationHookInput>(input.GetRawText(), _jsonOptions),
                _ => null
            };
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.HookInputParseError(_logger, ex, eventName);
            }

            return null;
        }
    }

    private static object ConvertHookOutput(HookOutput output)
    {
        return output switch
        {
            SyncHookOutput sync => BuildSyncHookResponse(sync),
            AsyncHookOutput hookOutput => new Dictionary<string, object?>
            {
                ["async"] = true,
                ["asyncTimeout"] = hookOutput.AsyncTimeout
            }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value),
            _ => new HookContinueResponse()
        };
    }

    /// <summary>
    ///     Builds a dictionary response from SyncHookOutput, only including non-null fields.
    /// </summary>
    /// <remarks>
    ///     This method intentionally accesses SyncHookOutput properties to build a wire-format response.
    ///     Feature envy is expected for DTO-to-dictionary conversion methods.
    /// </remarks>
    private static Dictionary<string, object?> BuildSyncHookResponse(SyncHookOutput sync)
    {
        // Only include non-null fields to avoid Zod validation errors
        var response = new Dictionary<string, object?>
        {
            ["continue"] = sync.Continue ?? true
        };

        if (sync.SuppressOutput.HasValue)
        {
            response["suppressOutput"] = sync.SuppressOutput.Value;
        }

        if (!string.IsNullOrEmpty(sync.StopReason))
        {
            response["stopReason"] = sync.StopReason;
        }

        if (!string.IsNullOrEmpty(sync.Decision))
        {
            response["decision"] = sync.Decision;
        }

        if (!string.IsNullOrEmpty(sync.SystemMessage))
        {
            response["systemMessage"] = sync.SystemMessage;
        }

        if (!string.IsNullOrEmpty(sync.Reason))
        {
            response["reason"] = sync.Reason;
        }

        if (sync.HookSpecificOutput is not null)
        {
            response["hookSpecificOutput"] = sync.HookSpecificOutput;
        }

        return response;
    }

    private Message? ParseMessage(JsonElement root, string? type)
    {
        try
        {
            return type switch
            {
                "user" => JsonSerializer.Deserialize<UserMessage>(root.GetRawText(), _jsonOptions),
                "assistant" => JsonSerializer.Deserialize<AssistantMessage>(root.GetRawText(), _jsonOptions),
                "system" => JsonSerializer.Deserialize<SystemMessage>(root.GetRawText(), _jsonOptions),
                "result" => JsonSerializer.Deserialize<ResultMessage>(root.GetRawText(), _jsonOptions),
                "stream_event" => JsonSerializer.Deserialize<StreamEvent>(root.GetRawText(), _jsonOptions),
                _ => null
            };
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.MessageParseError(_logger, ex, type);
            }

            return null;
        }
    }

    private async Task<JsonElement> SendControlRequestAsync(object request, CancellationToken cancellationToken)
    {
        var requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pendingResponses[requestId] = tcs;

        // Create logging scope with RequestId for correlation
        using var logScope = _logger?.BeginRequestScope(requestId);

        try
        {
            var controlRequest = new ControlRequestEnvelope
            {
                RequestId = requestId,
                Request = request
            };

            await _transport.WriteAsync(controlRequest, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.ControlRequestTimeout);

            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ControlTimeoutException(requestId, "Control request timed out");
        }
        finally
        {
            _pendingResponses.TryRemove(requestId, out _);
        }
    }

    private Task SendControlResponseAsync(string requestId, object? responseData,
        CancellationToken cancellationToken)
    {
        var response = new ControlResponse
        {
            Response = new ControlSuccessResponse
            {
                RequestId = requestId,
                ResponseData = responseData
            }
        };

        return _transport.WriteAsync(response, cancellationToken);
    }

    private Task SendControlErrorResponseAsync(string requestId, string error,
        CancellationToken cancellationToken)
    {
        var response = new ControlResponse
        {
            Response = new ControlErrorResponse
            {
                RequestId = requestId,
                Error = error
            }
        };

        return _transport.WriteAsync(response, cancellationToken);
    }

    private string GenerateRequestId()
    {
        var count = Interlocked.Increment(ref _requestCounter);
        return $"req_{count}_{Guid.NewGuid():N}";
    }

    private Dictionary<string, object>? BuildHooksConfig()
    {
        if (_options.Hooks is null || _options.Hooks.Count == 0)
        {
            return null;
        }

        var config = new Dictionary<string, object>();

        foreach (var (hookEvent, matchers) in _options.Hooks)
        {
            var eventName = hookEvent.ToString();
            var matcherConfigs = new List<object>();

            foreach (var matcher in matchers)
            {
                // Generate callback IDs for each hook callback
                var callbackIds = new List<string>();
                foreach (var callback in matcher.Hooks)
                {
                    var callbackId = $"hook_{_hookCallbackCounter++}";
                    _hookCallbacks[callbackId] = callback;
                    callbackIds.Add(callbackId);
                }

                var matcherConfig = new Dictionary<string, object?>
                {
                    ["matcher"] = matcher.Matcher,
                    ["hookCallbackIds"] = callbackIds
                };

                if (matcher.Timeout.HasValue)
                {
                    matcherConfig["timeout"] = matcher.Timeout.Value;
                }

                matcherConfigs.Add(matcherConfig);
            }

            config[eventName] = matcherConfigs;
        }

        return config;
    }
}
