using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Logging;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Sessions;
using Claude.AgentSdk.Tools;
using Claude.AgentSdk.Transport;
using Claude.AgentSdk.Types;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Protocol;

/// <summary>
///     Handles the bidirectional control protocol with the Claude CLI.
///     Routes messages between regular message stream and control requests.
/// </summary>
/// <remarks>
///     <para>
///         As the central protocol handler, this class necessarily coordinates between transport, messages,
///         hooks, MCP servers, and control requests. The size and coupling are inherent to its role as
///         the main protocol orchestration point.
///     </para>
///     <para>
///         Incoming control requests (permissions, hooks, SDK MCP calls) are handled concurrently so a slow
///         callback never blocks the read loop, and each can be cancelled by a <c>control_cancel_request</c>.
///     </para>
///     <para>
///         A result message ends one turn, not necessarily the run: a background agent that finished just before
///         it may still wake the session for a follow-up turn whose hook, permission and SDK MCP requests need
///         stdin. In one-shot mode (<see cref="CompleteOnResult" />) the stream therefore stays open until the
///         CLI reports <c>idle</c> (or, for CLIs without session state events, until a result arrives with no
///         tracked agent tasks in flight), bounded by <c>CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS</c>.
///     </para>
/// </remarks>
internal sealed class QueryHandler : IAsyncDisposable
{
    internal const string RunEndCeilingEnv = "CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS";
    internal const int DefaultRunEndCeilingMs = 600_000;

    // Task types whose completion runs a follow-up turn and which reliably reach a terminal status.
    // Background shells, monitors and teammates are deliberately excluded: they may run indefinitely.
    private static readonly HashSet<string> _deferringTaskTypes = ["local_agent", "local_workflow"];

    private static readonly JsonElement _emptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly Dictionary<string, HookCallback> _hookCallbacks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inflightRequests = new();
    private readonly HashSet<string> _inflightTasks = [];
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<QueryHandler>? _logger;
    private readonly ConcurrentDictionary<string, IMcpToolServer> _mcpServers = new();
    private readonly Channel<Message> _messageChannel;
    private readonly TranscriptMirrorBatcher? _mirrorBatcher;
    private readonly ClaudeAgentOptions _options;
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingResponses = new();
    private readonly ConcurrentDictionary<Task, byte> _requestTasks = new();
    private readonly int _runEndCeilingMs;
#if NET9_0_OR_GREATER
    private readonly Lock _runLock = new();
#else
    private readonly object _runLock = new();
#endif
    private readonly ITransport _transport;

    private CancellationTokenSource? _ceilingCts;
    private int _ceilingGeneration;
    private bool _disposed;
    private int _hookCallbackCounter;
    private bool _initialized;
    private JsonElement? _lastErrorResult;
    private bool _oneShotCompleted;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private int _requestCounter;
    private bool _resultReceived;
    private TaskCompletionSource _runEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _runFinal;
    private string? _sessionState;
    private bool _turnInProgress;

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
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _runEndCeilingMs = ResolveRunEndCeilingMs(options.Environment);

        if (options.SessionStore is { } store)
        {
            // Resolve transcript paths against the same config dir the CLI writes to (the materialized temp dir
            // for a store-backed resume, since that is set through CLAUDE_CONFIG_DIR).
            _mirrorBatcher = new TranscriptMirrorBatcher(
                store,
                SessionResumeMaterializer.ResolveProjectsDir(options),
                ReportMirrorErrorAsync,
                options.SessionStoreFlush,
                logger);
        }

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
    ///     One-shot mode: completes the message channel (and closes stdin) once the run has ended after a result.
    ///     For interactive multi-turn sessions, keep false.
    /// </summary>
    public bool CompleteOnResult { get; set; }

    /// <summary>
    ///     The response to the initialize request (available commands, models, account, output styles, ...),
    ///     or null before initialization.
    /// </summary>
    public JsonElement? InitializationResult { get; private set; }

    /// <summary>
    ///     Callback invoked when a ResultMessage is received.
    ///     Used by ClaudeAgentSession to fire metrics events.
    /// </summary>
    internal Action<ResultMessage>? OnResultMessage { get; set; }

    /// <summary>
    ///     The most recently observed model from system init or assistant messages.
    /// </summary>
    internal string? CurrentModel { get; private set; }

    /// <summary>
    ///     The session-store mirror batcher, when <see cref="ClaudeAgentOptions.SessionStore" /> is set.
    /// </summary>
    internal TranscriptMirrorBatcher? MirrorBatcher => _mirrorBatcher;

    /// <summary>
    ///     Whether the CLI may still send control requests that need a reply (SDK MCP servers, hooks, or a
    ///     CanUseTool callback). Closing stdin while any of these are configured makes later requests fail.
    /// </summary>
    internal bool HasBidirectionalNeeds =>
        _mcpServers.Count > 0 || _options.Hooks is { Count: > 0 } && !_options.NoHooks ||
        _options.CanUseTool is not null;

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

        lock (_runLock)
        {
            _runFinal = true;
            ClearRunEndCeiling();
        }

        foreach (var cts in _inflightRequests.Values)
        {
            TryCancel(cts);
        }

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

        try
        {
            await Task.WhenAll(_requestTasks.Keys).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Handlers observe cancellation; anything still running is abandoned.
        }

        // Final flush so the current turn is not lost when the consumer stops early. Never throws.
        if (_mirrorBatcher is not null)
        {
            await _mirrorBatcher.FlushAsync().ConfigureAwait(false);
        }

        _readerCts?.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);

        // Cancel any pending responses
        foreach (var pending in _pendingResponses.Values)
        {
            pending.Completion.TrySetCanceled();
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
        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReadMessagesLoopAsync(_readerCts.Token), cancellationToken);
    }

    /// <summary>
    ///     Initialize the control protocol: registers hooks and sends agents and session-level flags.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var request = new InitializeRequestBody
        {
            Hooks = BuildHooksConfig(),
            Agents = _options.Agents is { Count: > 0 } agents
                ? agents.ToDictionary(a => a.Key, a => (object)a.Value.ToCliDictionary())
                : null,
            ExcludeDynamicSections = (_options.SystemPrompt as PresetSystemPrompt)?.ExcludeDynamicSections,
            SystemPromptSnapshot = _options.SystemPrompt switch
            {
                PresetSystemPrompt preset => preset.Snapshot,
                CustomSystemPrompt custom => custom.Snapshot,
                _ => null
            },
            Skills = (_options.Skills as SkillListConfig)?.Names,
            ForwardSubagentText = _options.ForwardSubagentText ? true : null
        };

        // Longer timeout for initialize since MCP servers may take time to start
        InitializationResult = await SendControlRequestAsync(
                request, ControlSubtype.Initialize, _options.InitializeTimeout, cancellationToken)
            .ConfigureAwait(false);
        _initialized = true;

        if (_logger is not null)
        {
            Log.ControlProtocolInitialized(_logger);
        }
    }

    /// <summary>
    ///     Send a raw message to the CLI.
    /// </summary>
    public Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (message is UserMessageEnvelope envelope)
        {
            return SendUserEnvelopeAsync(envelope, cancellationToken);
        }

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

        return SendUserEnvelopeAsync(message, cancellationToken);
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
        return SendControlRequestAsync(new InterruptRequestBody(), ControlSubtype.Interrupt, null, cancellationToken);
    }

    /// <summary>
    ///     Set the permission mode.
    /// </summary>
    public Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetPermissionModeRequestBody { Mode = mode.ToJsonString() },
            ControlSubtype.SetPermissionMode, null, cancellationToken);
    }

    /// <summary>
    ///     Set the model, or reset to the default when <paramref name="model" /> is null.
    /// </summary>
    public Task SetModelAsync(string? model, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetModelRequestBody { Model = model }, ControlSubtype.SetModel, null,
            cancellationToken);
    }

    /// <summary>
    ///     Rewind files to a specific user message.
    /// </summary>
    public Task<JsonElement> RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new RewindFilesRequestBody { UserMessageId = userMessageId },
            ControlSubtype.RewindFiles, null, cancellationToken);
    }

    /// <summary>
    ///     Set max thinking tokens.
    /// </summary>
    public Task SetMaxThinkingTokensAsync(int maxTokens, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetMaxThinkingTokensRequestBody { MaxThinkingTokens = maxTokens },
            ControlSubtype.SetMaxThinkingTokens, null, cancellationToken);
    }

    /// <summary>
    ///     Reconnect a disconnected or failed MCP server.
    /// </summary>
    public Task ReconnectMcpServerAsync(string serverName, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new ReconnectMcpServerRequestBody { ServerName = serverName },
            ControlSubtype.ReconnectMcpServer, null, cancellationToken);
    }

    /// <summary>
    ///     Enable or disable an MCP server.
    /// </summary>
    public Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(
            new ToggleMcpServerRequestBody { ServerName = serverName, Enabled = enabled },
            ControlSubtype.ToggleMcpServer, null, cancellationToken);
    }

    /// <summary>
    ///     Configure MCP servers.
    /// </summary>
    public Task<JsonElement> SetMcpServersAsync(object servers, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new SetMcpServersRequestBody { Servers = servers },
            ControlSubtype.SetMcpServers, null, cancellationToken);
    }

    /// <summary>
    ///     Registers an in-process SDK MCP server so mcp_message requests for it are routed here.
    /// </summary>
    internal void RegisterSdkMcpServer(string name, IMcpToolServer server)
    {
        _mcpServers[name] = server;
    }

    /// <summary>
    ///     Get a breakdown of current context window usage by category.
    /// </summary>
    public Task<JsonElement> GetContextUsageAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new GetContextUsageRequestBody(), ControlSubtype.GetContextUsage, null,
            cancellationToken);
    }

    /// <summary>
    ///     Stop a running background task.
    /// </summary>
    public Task StopTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new StopTaskRequestBody { TaskId = taskId }, ControlSubtype.StopTask, null,
            cancellationToken);
    }

    /// <summary>
    ///     Get the list of supported slash commands (from the initialize response when available).
    /// </summary>
    public Task<JsonElement> GetSupportedCommandsAsync(CancellationToken cancellationToken = default)
    {
        return TryGetInitializationField("commands", out var commands)
            ? Task.FromResult(commands)
            : SendControlRequestAsync(new SupportedCommandsRequestBody(), ControlSubtype.SupportedCommands, null,
                cancellationToken);
    }

    /// <summary>
    ///     Get the list of supported models (from the initialize response when available).
    /// </summary>
    public Task<JsonElement> GetSupportedModelsAsync(CancellationToken cancellationToken = default)
    {
        return TryGetInitializationField("models", out var models)
            ? Task.FromResult(models)
            : SendControlRequestAsync(new SupportedModelsRequestBody(), ControlSubtype.SupportedModels, null,
                cancellationToken);
    }

    /// <summary>
    ///     Get current MCP server connection status.
    /// </summary>
    public Task<JsonElement> GetMcpServerStatusAsync(CancellationToken cancellationToken = default)
    {
        return SendControlRequestAsync(new McpServerStatusRequestBody(), ControlSubtype.McpServerStatus, null,
            cancellationToken);
    }

    /// <summary>
    ///     Get account information (from the initialize response when available).
    /// </summary>
    public Task<JsonElement> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        return TryGetInitializationField("account", out var account)
            ? Task.FromResult(account)
            : SendControlRequestAsync(new AccountInfoRequestBody(), ControlSubtype.AccountInfo, null,
                cancellationToken);
    }

    internal static int ResolveRunEndCeilingMs(IReadOnlyDictionary<string, string> optionsEnvironment)
    {
        var raw = optionsEnvironment.TryGetValue(RunEndCeilingEnv, out var fromOptions)
            ? fromOptions
            : Environment.GetEnvironmentVariable(RunEndCeilingEnv);

        if (raw is null || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return DefaultRunEndCeilingMs;
        }

        return value;
    }

    private bool TryGetInitializationField(string name, out JsonElement value)
    {
        if (InitializationResult is { ValueKind: JsonValueKind.Object } init &&
            init.TryGetProperty(name, out value) &&
            value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            return true;
        }

        value = default;
        return false;
    }

    private Task SendUserEnvelopeAsync(UserMessageEnvelope envelope, CancellationToken cancellationToken)
    {
        // Each prompt owes a run of its own, result included: an earlier one having ended does not end it.
        lock (_runLock)
        {
            ReopenRun();
            _resultReceived = false;
            ClearRunEndCeiling();
        }

        if (_options.VerbatimPrompts)
        {
            envelope = envelope with { ClientComposed = true };
        }

        return _transport.WriteAsync(envelope, cancellationToken);
    }

    private async Task ReadMessagesLoopAsync(CancellationToken cancellationToken)
    {
        Exception? completionException = null;
        var cancelled = false;

        try
        {
            await foreach (var doc in _transport.ReadMessagesAsync(cancellationToken).ConfigureAwait(false))
            {
                await ProcessMessageAsync(doc.RootElement, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            if (_logger is not null)
            {
                Log.MessageReaderCancelled(_logger);
            }
        }
        catch (Exception ex)
        {
            // When the CLI emits an error result it then exits non-zero on purpose. The trailing process
            // error carries nothing beyond the exit code, so replace it with what the CLI already reported.
            var error = ex;
            if (ex is CliProcessException processError and not ResultException && _lastErrorResult is { } last)
            {
                error = new ResultException(
                    $"Claude Code returned an error result: {ResultException.DescribeErrorResult(last)}",
                    last,
                    processError.ExitCode);
            }

            if (_logger is not null)
            {
                Log.MessageReaderError(_logger, error);
            }

            TerminalException = error;
            completionException = error;
            FailPendingRequests(error);
        }
        finally
        {
            // Flush entries batched this turn so an early EOF or transport error does not drop them.
            if (_mirrorBatcher is not null)
            {
                await _mirrorBatcher.FlushAsync().ConfigureAwait(false);
            }

            lock (_runLock)
            {
                _runFinal = true;
                EndRun();
            }

            // On EOF, pending requests can no longer be answered; fail them fast instead of waiting for the
            // timeout. (On shutdown, DisposeAsync cancels them instead.)
            if (!cancelled && !_disposed)
            {
                FailPendingRequests(new TransportException(
                    "The CLI closed its output before responding to the control request"));
            }

            _messageChannel.Writer.TryComplete(completionException);
        }
    }

    private async Task ProcessMessageAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
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
                return;

            case "control_request":
                SpawnControlRequestHandler(root.Clone(), cancellationToken);
                return;

            case "control_cancel_request":
                HandleControlCancelRequest(root);
                return;

            case "transcript_mirror":
                // Session-store write path: hand to the batcher; never yielded to consumers.
                EnqueueTranscriptMirror(root);
                return;
        }

        if (type == "result" && _mirrorBatcher is not null)
        {
            // Consumers observing the result can rely on the store being up to date for this turn.
            await _mirrorBatcher.FlushAsync().ConfigureAwait(false);
        }

        var skip = TrackRunState(root, type);

        if (!skip)
        {
            await EmitMessageAsync(root, type, cancellationToken).ConfigureAwait(false);
        }

        TryCompleteOneShot();
    }

    /// <summary>
    ///     Updates turn/run bookkeeping for a regular message.
    /// </summary>
    /// <returns>True when the message should not be yielded to consumers.</returns>
    private bool TrackRunState(JsonElement root, string? type)
    {
        var subtype = root.TryGetProperty("subtype", out var subtypeElement) &&
                      subtypeElement.ValueKind == JsonValueKind.String
            ? subtypeElement.GetString()
            : null;
        var isSessionState = type == "system" && subtype == "session_state_changed";

        lock (_runLock)
        {
            if (type == "system")
            {
                var hadTasksInFlight = _inflightTasks.Count > 0;
                TrackTaskLifecycle(root, subtype);
                if (hadTasksInFlight && _inflightTasks.Count == 0)
                {
                    // The last tracked agent settled; the wait between turns starts over.
                    RearmRunEndCeilingBetweenTurns();
                }

                if (isSessionState)
                {
                    var state = root.TryGetProperty("state", out var stateElement) &&
                                stateElement.ValueKind == JsonValueKind.String
                        ? stateElement.GetString()
                        : null;
                    OnSessionState(state);

                    // Frames the CLI sent only because the SDK asked for them; the caller did not opt in.
                    if (root.TryGetProperty("sdk_host_only", out var hostOnly) &&
                        hostOnly.ValueKind == JsonValueKind.True)
                    {
                        return true;
                    }
                }
            }

            if (type == "result")
            {
                _resultReceived = true;
                _turnInProgress = false;

                if (_sessionState is null or "idle" || !HasBidirectionalNeeds)
                {
                    MaybeEndRun();
                }
                else if (_sessionState != "requires_action")
                {
                    ArmRunEndCeiling();
                }

                _lastErrorResult = root.TryGetProperty("is_error", out var isError) &&
                                   isError.ValueKind == JsonValueKind.True
                    ? root.Clone()
                    : null;
            }
            else if (!isSessionState)
            {
                // The conversation moved on: a process error now is a fresh crash, not the expected exit
                // from a prior error result.
                _lastErrorResult = null;

                var isMainThread = !root.TryGetProperty("parent_tool_use_id", out var parent) ||
                                   parent.ValueKind == JsonValueKind.Null;
                if (type is "assistant" or "stream_event" && isMainThread)
                {
                    _turnInProgress = true;
                    ReopenRun();
                    ClearRunEndCeiling();
                }
            }
        }

        return false;
    }

    private async Task EmitMessageAsync(JsonElement root, string? type, CancellationToken cancellationToken)
    {
        var message = ParseMessage(root, type);
        if (message is null)
        {
            return;
        }

        // Track model from system init and assistant messages for metrics
        if (message is SystemMessage { IsInit: true, Model: not null } sys)
        {
            CurrentModel = sys.Model;
        }
        else if (message is AssistantMessage { MessageContent.Model: not null } asst)
        {
            CurrentModel = asst.MessageContent.Model;
        }

        await _messageChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);

        if (message is ResultMessage resultMsg)
        {
            OnResultMessage?.Invoke(resultMsg);

            if (CompleteOnResult && _logger is not null)
            {
                Log.ResultMessageReceived(_logger, resultMsg.Subtype);
            }
        }
    }

    private void EnqueueTranscriptMirror(JsonElement root)
    {
        if (_mirrorBatcher is null)
        {
            return;
        }

        if (!root.TryGetProperty("filePath", out var filePath) || filePath.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            _logger?.LogDebug("Ignoring malformed transcript_mirror frame");
            return;
        }

        _mirrorBatcher.Enqueue(filePath.GetString()!, entries.EnumerateArray().ToList());
    }

    /// <summary>
    ///     Surfaces a dropped <see cref="ISessionStore.AppendAsync" /> batch as a <c>mirror_error</c> system message.
    ///     Non-blocking: if the message buffer is full the error is logged and dropped.
    /// </summary>
    internal Task ReportMirrorErrorAsync(SessionKey? key, string error)
    {
        var frame = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["type"] = "system",
            ["subtype"] = "mirror_error",
            ["error"] = error,
            ["key"] = key,
            ["uuid"] = Guid.NewGuid().ToString(),
            ["session_id"] = key?.SessionId ?? ""
        });

        if (ParseMessage(frame, "system") is { } message && !_messageChannel.Writer.TryWrite(message))
        {
            _logger?.LogWarning("Dropping mirror_error message (buffer full or closed): {Error}", error);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     In one-shot mode, completes the stream and closes stdin once the run has ended after a result.
    /// </summary>
    private void TryCompleteOneShot()
    {
        if (!CompleteOnResult)
        {
            return;
        }

        lock (_runLock)
        {
            if (_oneShotCompleted || !_resultReceived || !_runEnded.Task.IsCompleted)
            {
                return;
            }

            _oneShotCompleted = true;
            _runFinal = true;
            ClearRunEndCeiling();
        }

        _messageChannel.Writer.TryComplete();
        _ = EndInputQuietlyAsync();
    }

    private async Task EndInputQuietlyAsync()
    {
        try
        {
            await _transport.EndInputAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error closing CLI input");
        }
    }

    // ------------------------------------------------------------------
    // Run-end bookkeeping (callers hold _runLock)
    // ------------------------------------------------------------------

    private void TrackTaskLifecycle(JsonElement root, string? subtype)
    {
        if (!root.TryGetProperty("task_id", out var taskIdElement) ||
            taskIdElement.ValueKind != JsonValueKind.String ||
            taskIdElement.GetString() is not { Length: > 0 } taskId)
        {
            return;
        }

        switch (subtype)
        {
            case "task_started":
                if (root.TryGetProperty("task_type", out var taskType) &&
                    taskType.ValueKind == JsonValueKind.String &&
                    _deferringTaskTypes.Contains(taskType.GetString()!))
                {
                    _inflightTasks.Add(taskId);
                }

                break;

            case "task_notification":
                _inflightTasks.Remove(taskId);
                break;

            case "task_updated":
                if (root.TryGetProperty("patch", out var patch) &&
                    patch.ValueKind == JsonValueKind.Object &&
                    patch.TryGetProperty("status", out var status) &&
                    status.ValueKind == JsonValueKind.String &&
                    TaskStatuses.IsTerminal(status.GetString()))
                {
                    _inflightTasks.Remove(taskId);
                }

                break;
        }
    }

    private void OnSessionState(string? state)
    {
        _sessionState = state;
        if (state == "idle")
        {
            if (_resultReceived)
            {
                MaybeEndRun();
            }

            return;
        }

        // Work the CLI took up after the run ended (a finished background task woke it) reopens the run.
        ReopenRun();
        if (state == "requires_action")
        {
            // The host is answering a request; stdin must outlast it.
            ClearRunEndCeiling();
        }
        else
        {
            RearmRunEndCeilingBetweenTurns();
        }
    }

    private void MaybeEndRun()
    {
        if (_inflightTasks.Count > 0)
        {
            _logger?.LogDebug("Turn ended with {Count} task(s) in flight; keeping the run open",
                _inflightTasks.Count);
            return;
        }

        EndRun();
    }

    private void EndRun()
    {
        ClearRunEndCeiling();
        _runEnded.TrySetResult();
    }

    private void ReopenRun()
    {
        if (_runEnded.Task.IsCompleted && !_runFinal)
        {
            _runEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private void ArmRunEndCeiling()
    {
        ClearRunEndCeiling();
        if (_runEndCeilingMs <= 0 || _runEnded.Task.IsCompleted || _runFinal || _disposed || _turnInProgress ||
            !HasBidirectionalNeeds)
        {
            return;
        }

        var generation = _ceilingGeneration;
        var cts = new CancellationTokenSource();
        _ceilingCts = cts;
        _ = RunEndCeilingAsync(generation, cts.Token);
    }

    private void RearmRunEndCeilingBetweenTurns()
    {
        if (_resultReceived && _sessionState is not (null or "idle" or "requires_action"))
        {
            ArmRunEndCeiling();
        }
    }

    private async Task RunEndCeilingAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_runEndCeilingMs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_runLock)
        {
            if (generation != _ceilingGeneration)
            {
                return;
            }

            if (_inflightTasks.Count > 0)
            {
                // A tracked background agent may still need stdin; the ceiling restarts once it settles.
                return;
            }

            _logger?.LogDebug("No 'idle' {Ceiling}ms after the last result; ending the run", _runEndCeilingMs);
            EndRun();
        }

        TryCompleteOneShot();
    }

    private void ClearRunEndCeiling()
    {
        _ceilingGeneration++;
        var cts = _ceilingCts;
        _ceilingCts = null;
        if (cts is not null)
        {
            TryCancel(cts);
            cts.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Control responses (CLI -> SDK, answering our requests)
    // ------------------------------------------------------------------

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

        if (!response.TryGetProperty("request_id", out var requestIdElement) ||
            requestIdElement.ValueKind != JsonValueKind.String)
        {
            if (_logger is not null)
            {
                Log.ControlResponseMissingRequestId(_logger);
            }

            return;
        }

        var requestId = requestIdElement.GetString()!;

        if (!_pendingResponses.TryRemove(requestId, out var pending))
        {
            if (_logger is not null)
            {
                Log.UnknownRequestResponse(_logger, requestId);
            }

            return;
        }

        if (response.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "error")
        {
            var error = response.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()!
                : "Unknown error";
            pending.Completion.TrySetException(new ControlRequestException(pending.Subtype, error));
            return;
        }

        var data = response.TryGetProperty("response", out var responseData) &&
                   responseData.ValueKind == JsonValueKind.Object
            ? responseData.Clone()
            : _emptyObject;
        pending.Completion.TrySetResult(data);
    }

    private void FailPendingRequests(Exception error)
    {
        foreach (var requestId in _pendingResponses.Keys)
        {
            if (_pendingResponses.TryRemove(requestId, out var pending))
            {
                pending.Completion.TrySetException(error);
            }
        }
    }

    // ------------------------------------------------------------------
    // Control requests (CLI -> SDK)
    // ------------------------------------------------------------------

    private void SpawnControlRequestHandler(JsonElement root, CancellationToken readerToken)
    {
        if (!root.TryGetProperty("request_id", out var requestIdElement) ||
            requestIdElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("request", out var request) ||
            !request.TryGetProperty("subtype", out var subtypeElement))
        {
            _logger?.LogDebug("Ignoring malformed control request");
            return;
        }

        var requestId = requestIdElement.GetString()!;
        var subtype = subtypeElement.GetString() ?? "";
        var cts = CancellationTokenSource.CreateLinkedTokenSource(readerToken);
        _inflightRequests[requestId] = cts;

        var task = Task.Run(async () =>
        {
            try
            {
                await HandleControlRequestAsync(requestId, request, subtype, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                _inflightRequests.TryRemove(requestId, out _);
                cts.Dispose();
            }
        }, CancellationToken.None);

        _requestTasks.TryAdd(task, 0);
        _ = task.ContinueWith(t => _requestTasks.TryRemove(t, out _), TaskScheduler.Default);
    }

    private void HandleControlCancelRequest(JsonElement root)
    {
        if (_logger is not null)
        {
            Log.ControlCancelRequestReceived(_logger);
        }

        if (root.TryGetProperty("request_id", out var requestIdElement) &&
            requestIdElement.ValueKind == JsonValueKind.String &&
            _inflightRequests.TryRemove(requestIdElement.GetString()!, out var cts))
        {
            TryCancel(cts);
        }
    }

    private async Task HandleControlRequestAsync(string requestId, JsonElement request, string subtype,
        CancellationToken cancellationToken)
    {
        try
        {
            object? responseData = subtype switch
            {
                ControlSubtype.CanUseTool => await HandleToolPermissionAsync(request, cancellationToken)
                    .ConfigureAwait(false),
                ControlSubtype.HookCallback => await HandleHookCallbackAsync(request, cancellationToken)
                    .ConfigureAwait(false),
                ControlSubtype.McpMessage => await HandleMcpMessageAsync(request, cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new NotSupportedException($"Unsupported control request subtype: {subtype}")
            };

            await SendControlResponseAsync(requestId, responseData).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancelled via control_cancel_request (or shutdown): the CLI abandoned the request, so no response.
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.ControlRequestError(_logger, ex, subtype);
            }

            await SendControlErrorResponseAsync(requestId, ex.Message).ConfigureAwait(false);
        }
    }

    private async Task<object?> HandleToolPermissionAsync(JsonElement request, CancellationToken cancellationToken)
    {
        if (_options.CanUseTool is null)
        {
            throw new InvalidOperationException("canUseTool callback is not provided");
        }

        var toolName = request.GetProperty("tool_name").GetString()!;
        var input = request.TryGetProperty("input", out var inputElement) ? inputElement.Clone() : _emptyObject;

        var permissionRequest = new ToolPermissionRequest
        {
            ToolName = toolName,
            Input = input,
            Suggestions = ParsePermissionSuggestions(request),
            BlockedPath = GetOptionalString(request, "blocked_path"),
            ToolUseId = GetOptionalString(request, "tool_use_id"),
            AgentId = GetOptionalString(request, "agent_id"),
            DecisionReason = GetOptionalString(request, "decision_reason"),
            Title = GetOptionalString(request, "title"),
            DisplayName = GetOptionalString(request, "display_name"),
            Description = GetOptionalString(request, "description")
        };

        var result = await _options.CanUseTool(permissionRequest, cancellationToken).ConfigureAwait(false);

        switch (result)
        {
            case PermissionResultAllow allow:
            {
                var response = new Dictionary<string, object?>
                {
                    ["behavior"] = "allow",
                    ["updatedInput"] = allow.UpdatedInput ?? input
                };
                if (allow.UpdatedPermissions is not null)
                {
                    response["updatedPermissions"] = allow.UpdatedPermissions.Select(p => p.ToWireFormat()).ToList();
                }

                return response;
            }

            case PermissionResultDeny deny:
            {
                var response = new Dictionary<string, object?>
                {
                    ["behavior"] = "deny",
                    ["message"] = deny.Message
                };
                if (deny.Interrupt)
                {
                    response["interrupt"] = true;
                }

                return response;
            }

            default:
                throw new InvalidOperationException(
                    "Tool permission callback must return PermissionResultAllow or PermissionResultDeny, got " +
                    (result?.GetType().Name ?? "null"));
        }
    }

    private static List<PermissionUpdate> ParsePermissionSuggestions(JsonElement request)
    {
        if (!request.TryGetProperty("permission_suggestions", out var suggestions) ||
            suggestions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return suggestions.EnumerateArray()
            .Select(PermissionUpdate.FromWireFormat)
            .Where(update => update is not null)
            .Select(update => update!)
            .ToList();
    }

    private static string? GetOptionalString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private async Task<object?> HandleHookCallbackAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var callbackId = request.GetProperty("callback_id").GetString()!;
        var input = request.GetProperty("input");
        var toolUseId = GetOptionalString(request, "tool_use_id");

        if (!_hookCallbacks.TryGetValue(callbackId, out var callback))
        {
            if (_logger is not null)
            {
                Log.HookCallbackNotFound(_logger, callbackId);
            }

            throw new InvalidOperationException($"No hook callback found for ID: {callbackId}");
        }

        // Hook events this SDK version does not model are let through unchanged.
        if (!input.TryGetProperty("hook_event_name", out var eventNameElement) ||
            ParseHookInput(input, eventNameElement.GetString() ?? "") is not { } hookInput)
        {
            return new HookContinueResponse();
        }

        try
        {
            var output = await callback(hookInput, toolUseId, new HookContext(), cancellationToken)
                .ConfigureAwait(false);
            return ConvertHookOutput(output);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.HookCallbackError(_logger, ex, callbackId);
            }

            throw;
        }
    }

    private async Task<object?> HandleMcpMessageAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var serverName = GetOptionalString(request, "server_name");
        if (serverName is null || !request.TryGetProperty("message", out var message))
        {
            throw new InvalidOperationException("Missing server_name or message for MCP request");
        }

        var messageId = ExtractJsonRpcId(message);

        if (!_mcpServers.TryGetValue(serverName, out var server))
        {
            return new McpResponseWrapper
                { McpResponse = BuildJsonRpcError(messageId, -32601, $"Server '{serverName}' not found") };
        }

        try
        {
            // JSON-RPC notifications get no reply, but the control request that carried one still expects an ack.
            var response = await server.HandleRequestAsync(message, cancellationToken).ConfigureAwait(false)
                           ?? new Dictionary<string, object?>
                               { ["jsonrpc"] = "2.0", ["result"] = new Dictionary<string, object>() };
            return new McpResponseWrapper { McpResponse = response };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new McpResponseWrapper
            {
                McpResponse = BuildJsonRpcError(messageId, -32603,
                    string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message)
            };
        }
    }

    private static object? ExtractJsonRpcId(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("id", out var idElem))
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
    ///     Complexity is inherent to the number of hook event types in the protocol.
    ///     Each case is a simple deserialization call - no nested logic.
    /// </remarks>
    private HookInput? ParseHookInput(JsonElement input, string eventName)
    {
        try
        {
            return eventName switch
            {
                "PreToolUse" => input.Deserialize<PreToolUseHookInput>(_jsonOptions),
                "PostToolUse" => input.Deserialize<PostToolUseHookInput>(_jsonOptions),
                "PostToolUseFailure" => input.Deserialize<PostToolUseFailureHookInput>(_jsonOptions),
                "UserPromptSubmit" => input.Deserialize<UserPromptSubmitHookInput>(_jsonOptions),
                "Stop" => input.Deserialize<StopHookInput>(_jsonOptions),
                "SubagentStart" => input.Deserialize<SubagentStartHookInput>(_jsonOptions),
                "SubagentStop" => input.Deserialize<SubagentStopHookInput>(_jsonOptions),
                "PreCompact" => input.Deserialize<PreCompactHookInput>(_jsonOptions),
                "PermissionRequest" => input.Deserialize<PermissionRequestHookInput>(_jsonOptions),
                "SessionStart" => input.Deserialize<SessionStartHookInput>(_jsonOptions),
                "SessionEnd" => input.Deserialize<SessionEndHookInput>(_jsonOptions),
                "Notification" => input.Deserialize<NotificationHookInput>(_jsonOptions),
                "Setup" => input.Deserialize<SetupHookInput>(_jsonOptions),
                "TeammateIdle" => input.Deserialize<TeammateIdleHookInput>(_jsonOptions),
                "TaskCompleted" => input.Deserialize<TaskCompletedHookInput>(_jsonOptions),
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
            Message? message = type switch
            {
                "user" => root.Deserialize<UserMessage>(_jsonOptions),
                "assistant" => root.Deserialize<AssistantMessage>(_jsonOptions),
                "system" => root.Deserialize<SystemMessage>(_jsonOptions),
                "result" => root.Deserialize<ResultMessage>(_jsonOptions),
                "stream_event" => root.Deserialize<StreamEvent>(_jsonOptions),
                "tool_progress" => root.Deserialize<ToolProgressMessage>(_jsonOptions),
                "tool_use_summary" => root.Deserialize<ToolUseSummaryMessage>(_jsonOptions),
                "auth_status" => root.Deserialize<AuthStatusMessage>(_jsonOptions),
                "rate_limit_event" => root.Deserialize<RateLimitEvent>(_jsonOptions),
                "conversation_reset" => root.Deserialize<ConversationResetMessage>(_jsonOptions),
                // Forward-compatible: skip unrecognized message types from newer CLI versions.
                _ => null
            };

            return message is null ? null : message with { RawJson = root.Clone() };
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

    private async Task<JsonElement> SendControlRequestAsync(object request, string subtype, TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var requestId = GenerateRequestId();
        var pending = new PendingRequest(
            new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously),
            subtype);

        _pendingResponses[requestId] = pending;

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
            timeoutCts.CancelAfter(timeout ?? _options.ControlRequestTimeout);

            return await pending.Completion.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                                                 !pending.Completion.Task.IsCanceled)
        {
            throw new ControlTimeoutException(requestId, $"Control request timed out: {subtype}");
        }
        finally
        {
            _pendingResponses.TryRemove(requestId, out _);
        }
    }

    private async Task SendControlResponseAsync(string requestId, object? responseData)
    {
        var response = new ControlResponse
        {
            Response = new ControlSuccessResponse
            {
                RequestId = requestId,
                ResponseData = responseData ?? new Dictionary<string, object>()
            }
        };

        await WriteResponseQuietlyAsync(response).ConfigureAwait(false);
    }

    private async Task SendControlErrorResponseAsync(string requestId, string error)
    {
        var response = new ControlResponse
        {
            Response = new ControlErrorResponse
            {
                RequestId = requestId,
                Error = error
            }
        };

        await WriteResponseQuietlyAsync(response).ConfigureAwait(false);
    }

    private async Task WriteResponseQuietlyAsync(ControlResponse response)
    {
        try
        {
            await _transport.WriteAsync(response).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TransportException or InvalidOperationException ||
                                   _disposed)
        {
            // The transport went away while the handler ran; nobody is left to receive the response.
            _logger?.LogDebug(ex, "Could not write control response");
        }
    }

    private string GenerateRequestId()
    {
        var count = Interlocked.Increment(ref _requestCounter);
        return $"req_{count}_{Guid.NewGuid():N}";
    }

    private static void TryCancel(CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already completed and disposed
        }
    }

    private Dictionary<string, object>? BuildHooksConfig()
    {
        if (_options.Hooks is null || _options.Hooks.Count == 0 || _options.NoHooks)
        {
            return null;
        }

        var config = new Dictionary<string, object>();

        foreach (var (hookEvent, matchers) in _options.Hooks)
        {
            if (matchers.Count == 0)
            {
                continue;
            }

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

        return config.Count > 0 ? config : null;
    }

    private sealed record PendingRequest(TaskCompletionSource<JsonElement> Completion, string Subtype);
}
