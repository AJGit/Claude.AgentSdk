using System.Runtime.CompilerServices;
using Claude.AgentSdk.Logging;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Metrics;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Sessions;
using Claude.AgentSdk.Transport;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk;

/// <summary>
///     Client for interacting with the Claude Agent via the Claude CLI.
/// </summary>
/// <remarks>
///     As the main SDK entry point, this class necessarily coordinates between transport,
///     protocol, and message types. The type dependencies are inherent to its orchestration role.
/// </remarks>
public sealed class ClaudeAgentClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<ClaudeAgentClient>? _logger;
    private readonly ILoggerFactory? _loggerFactory;

    private readonly ClaudeAgentOptions _options;
    private readonly Func<ClaudeAgentOptions, string?, ITransport>? _transportFactory;
    private bool _disposed;

    /// <summary>
    ///     Create a new Claude Agent client with the specified options.
    /// </summary>
    public ClaudeAgentClient(ClaudeAgentOptions? options = null, ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? new ClaudeAgentOptions();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ClaudeAgentClient>();
    }

    /// <summary>
    ///     Internal constructor for testing with a custom transport factory.
    /// </summary>
    internal ClaudeAgentClient(
        ClaudeAgentOptions options,
        Func<ClaudeAgentOptions, string?, ITransport> transportFactory,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _transportFactory = transportFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ClaudeAgentClient>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // ClaudeAgentClient is stateless - sessions own their own lifecycle
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    ///     Create a client configured for testing with a mock transport.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="transport">The mock transport to use. If null, creates a new <see cref="MockTransport" />.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <returns>A client that uses the provided mock transport.</returns>
    /// <remarks>
    ///     <para>
    ///         Use this factory method in unit tests to avoid requiring the Claude CLI.
    ///         The mock transport allows you to control the messages returned and verify
    ///         what was sent.
    ///     </para>
    ///     <para>
    ///         Note that the same transport instance is used for all queries and sessions.
    ///         For tests that require fresh transport state, create a new client.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// var transport = new MockTransport();
    /// transport.EnqueueMessage("""{"type":"system","subtype":"init"}""");
    /// transport.EnqueueMessage("""{"type":"result","subtype":"success","is_error":false,"session_id":"test"}""");
    /// 
    /// var client = ClaudeAgentClient.CreateForTesting(transport: transport);
    /// await foreach (var message in client.QueryAsync("Test"))
    /// {
    ///     // Process messages
    /// }
    /// 
    /// // Verify what was sent
    /// var sent = transport.WrittenMessages;
    /// </code>
    /// </example>
    internal static ClaudeAgentClient CreateForTesting(
        ClaudeAgentOptions? options = null,
        MockTransport? transport = null,
        ILoggerFactory? loggerFactory = null)
    {
        var opts = options ?? new ClaudeAgentOptions();
        var mock = transport ?? new MockTransport();
        return new ClaudeAgentClient(opts, (_, _) => mock, loggerFactory);
    }

    /// <summary>
    ///     Execute a one-shot query and stream the responses.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional overrides for this query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of messages from Claude.</returns>
    public async IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var effectiveOptions = MergeOptions(options);
        ClaudeAgentOptionsValidation.Validate(effectiveOptions, _logger);

        // Store-backed resume: load the session into a temp CLAUDE_CONFIG_DIR the CLI can resume from.
        var materialized = await MaterializeResumeAsync(effectiveOptions, cancellationToken).ConfigureAwait(false);
        if (materialized is not null)
        {
            effectiveOptions = SessionResumeMaterializer.Apply(effectiveOptions, materialized);
        }

        QueryHandler handler;
        try
        {
            var transport = _transportFactory?.Invoke(effectiveOptions, prompt)
                            ?? new SubprocessTransport(
                                effectiveOptions,
                                prompt,
                                _loggerFactory?.CreateLogger<SubprocessTransport>());

            handler = new QueryHandler(
                transport,
                effectiveOptions,
                _loggerFactory?.CreateLogger<QueryHandler>())
            {
                CompleteOnResult = true // One-shot query completes once the run ends after a ResultMessage
            };
        }
        catch
        {
            if (materialized is not null)
            {
                await materialized.CleanupAsync().ConfigureAwait(false);
            }

            throw;
        }

        // Wire up metrics callback if configured
        if (effectiveOptions.OnMetrics is { } metricsCallback)
        {
            handler.OnResultMessage = result => HandleResultMessageForMetrics(
                result,
                metricsCallback,
                effectiveOptions.SessionDisplayName,
                handler.CurrentModel);
        }

        try
        {
            await handler.StartAsync(cancellationToken).ConfigureAwait(false);

            // Always streaming mode (matches the Python/TypeScript SDKs): initialize the control protocol so
            // hooks, CanUseTool, agents and SDK MCP servers work, then send the prompt over stdin.
            await handler.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await handler.SendUserMessageAsync(prompt, null, cancellationToken).ConfigureAwait(false);

            await foreach (var message in handler.ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return message;
            }
        }
        finally
        {
            // Terminate the CLI before removing the temp config dir it reads and writes.
            await handler.DisposeAsync().ConfigureAwait(false);
            if (materialized is not null)
            {
                await materialized.CleanupAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Execute a one-shot query and wait for the final result.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional overrides for this query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result message, or null if the query failed.</returns>
    public async Task<ResultMessage?> QueryToCompletionAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ResultMessage? result = null;

        await foreach (var message in QueryAsync(prompt, options, cancellationToken).ConfigureAwait(false))
        {
            if (message is ResultMessage resultMessage)
            {
                result = resultMessage;
            }
        }

        return result;
    }

    /// <summary>
    ///     Create a new interactive session for bidirectional communication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the connection operation.</param>
    /// <returns>A new session that can be used for bidirectional communication.</returns>
    /// <remarks>
    ///     <para>
    ///         The returned session owns its lifecycle independently. The cancellation token
    ///         is only used for the connection/initialization phase, not for the session lifetime.
    ///     </para>
    ///     <para>
    ///         Multiple sessions can be created from the same client. Each session should be
    ///         disposed when no longer needed.
    ///     </para>
    /// </remarks>
    public async Task<ClaudeAgentSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClaudeAgentOptionsValidation.Validate(_options, _logger);

        var options = _options;
        var materialized = await MaterializeResumeAsync(options, cancellationToken).ConfigureAwait(false);
        if (materialized is not null)
        {
            options = SessionResumeMaterializer.Apply(options, materialized);
        }

        QueryHandler? handler = null;

        // Use the caller token only for connection/initialization
        try
        {
            var transport = _transportFactory?.Invoke(options, null)
                            ?? new SubprocessTransport(
                                options,
                                null, // No prompt in bidirectional mode
                                _loggerFactory?.CreateLogger<SubprocessTransport>());

            handler = new QueryHandler(
                transport,
                options,
                _loggerFactory?.CreateLogger<QueryHandler>());

            await handler.StartAsync(cancellationToken).ConfigureAwait(false);
            await handler.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Don't leak the CLI process (or the temp config dir) when the handshake fails, e.g. a rejected resume.
            if (handler is not null)
            {
                await handler.DisposeAsync().ConfigureAwait(false);
            }

            if (materialized is not null)
            {
                await materialized.CleanupAsync().ConfigureAwait(false);
            }

            throw;
        }

        if (_logger is not null)
        {
            Log.ConnectedBidirectional(_logger);
        }

        return new ClaudeAgentSession(
            handler,
            options.SessionDisplayName,
            _loggerFactory?.CreateLogger<ClaudeAgentSession>(),
            materialized is null ? null : materialized.CleanupAsync);
    }

    /// <summary>
    ///     Materializes a store-backed resume, or returns null when none is needed. Skipped for custom (test)
    ///     transports, which never see the materialized options.
    /// </summary>
    private async Task<MaterializedResume?> MaterializeResumeAsync(ClaudeAgentOptions options,
        CancellationToken cancellationToken)
    {
        if (_transportFactory is not null)
        {
            return null;
        }

        return await SessionResumeMaterializer.MaterializeAsync(options, _logger, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Merges override options with base options, with overrides taking precedence.
    /// </summary>
    /// <remarks>
    ///     Complexity suppressed: This method has high cyclomatic complexity due to the number of properties
    ///     in ClaudeAgentOptions, but each assignment is a simple null-coalescing operation with no nested logic.
    /// </remarks>
    private ClaudeAgentOptions MergeOptions(ClaudeAgentOptions? overrides)
    {
        if (overrides is null)
        {
            return _options;
        }

        // Merge with overrides taking precedence
        // Use null-conditional operators for defensive null safety on collection properties
        return _options with
        {
            Tools = overrides.Tools ?? _options.Tools,
            AllowedTools = overrides.AllowedTools.Count > 0 ? overrides.AllowedTools : _options.AllowedTools,
            DisallowedTools =
            overrides.DisallowedTools.Count > 0 ? overrides.DisallowedTools : _options.DisallowedTools,
            SystemPrompt = overrides.SystemPrompt ?? _options.SystemPrompt,
            SettingSources = overrides.SettingSources ?? _options.SettingSources,
            McpServers = overrides.McpServers ?? _options.McpServers,
            Agents = overrides.Agents ?? _options.Agents,
            Plugins = overrides.Plugins ?? _options.Plugins,
            PermissionMode = overrides.PermissionMode ?? _options.PermissionMode,
            ContinueConversation = overrides.ContinueConversation || _options.ContinueConversation,
            Resume = overrides.Resume ?? _options.Resume,
            MaxTurns = overrides.MaxTurns ?? _options.MaxTurns,
            MaxBudgetUsd = overrides.MaxBudgetUsd ?? _options.MaxBudgetUsd,
            Model = overrides.Model ?? _options.Model,
            FallbackModel = overrides.FallbackModel ?? _options.FallbackModel,
            WorkingDirectory = overrides.WorkingDirectory ?? _options.WorkingDirectory,
            CliPath = overrides.CliPath ?? _options.CliPath,
            AddDirectories = overrides.AddDirectories.Count > 0 ? overrides.AddDirectories : _options.AddDirectories,
            Environment = overrides.Environment.Count > 0 ? overrides.Environment : _options.Environment,
            ExtraArgs = overrides.ExtraArgs.Count > 0 ? overrides.ExtraArgs : _options.ExtraArgs,
            CanUseTool = overrides.CanUseTool ?? _options.CanUseTool,
            Hooks = overrides.Hooks ?? _options.Hooks,
            IncludePartialMessages = overrides.IncludePartialMessages || _options.IncludePartialMessages,
            ForkSession = overrides.ForkSession || _options.ForkSession,
            ResumeSessionAt = overrides.ResumeSessionAt ?? _options.ResumeSessionAt,
            ResumeDropsTurn = overrides.ResumeDropsTurn ?? _options.ResumeDropsTurn,
            SessionId = overrides.SessionId ?? _options.SessionId,
            MaxThinkingTokens = overrides.MaxThinkingTokens ?? _options.MaxThinkingTokens,
            Thinking = overrides.Thinking ?? _options.Thinking,
            Effort = overrides.Effort ?? _options.Effort,
            TaskBudget = overrides.TaskBudget ?? _options.TaskBudget,
            Skills = overrides.Skills ?? _options.Skills,
            Settings = overrides.Settings ?? _options.Settings,
            IncludeHookEvents = overrides.IncludeHookEvents || _options.IncludeHookEvents,
            ForwardSubagentText = overrides.ForwardSubagentText || _options.ForwardSubagentText,
            VerbatimPrompts = overrides.VerbatimPrompts || _options.VerbatimPrompts,
            ModelId = overrides.ModelId ?? _options.ModelId,
            FallbackModelId = overrides.FallbackModelId ?? _options.FallbackModelId,
            DangerouslySkipPermissions = overrides.DangerouslySkipPermissions || _options.DangerouslySkipPermissions,
            OnMessageSent = overrides.OnMessageSent ?? _options.OnMessageSent,
            OnMessageReceived = overrides.OnMessageReceived ?? _options.OnMessageReceived,
            SessionStore = overrides.SessionStore ?? _options.SessionStore,
            SessionStoreFlush = overrides.SessionStore is not null ? overrides.SessionStoreFlush : _options.SessionStoreFlush,
            SessionStoreLoadTimeout = overrides.SessionStore is not null
                ? overrides.SessionStoreLoadTimeout
                : _options.SessionStoreLoadTimeout,
            OutputFormat = overrides.OutputFormat ?? _options.OutputFormat,
            EnableFileCheckpointing = overrides.EnableFileCheckpointing || _options.EnableFileCheckpointing,
            Betas = overrides.Betas ?? _options.Betas,
            Sandbox = overrides.Sandbox ?? _options.Sandbox,
            PermissionPromptToolName = overrides.PermissionPromptToolName ?? _options.PermissionPromptToolName,
            StrictMcpConfig = overrides.StrictMcpConfig || _options.StrictMcpConfig,
            NoHooks = overrides.NoHooks || _options.NoHooks,
            User = overrides.User ?? _options.User,
            AdditionalDataPaths = overrides.AdditionalDataPaths ?? _options.AdditionalDataPaths,
            OnStderr = overrides.OnStderr ?? _options.OnStderr,
            MessageChannelCapacity = overrides.MessageChannelCapacity > 0
                ? overrides.MessageChannelCapacity
                : _options.MessageChannelCapacity,
            OnMetrics = overrides.OnMetrics ?? _options.OnMetrics,
            SessionDisplayName = overrides.SessionDisplayName ?? _options.SessionDisplayName
        };
    }

    /// <summary>
    ///     Handles ResultMessage to fire metrics callback for one-shot queries.
    /// </summary>
    private static void HandleResultMessageForMetrics(
        ResultMessage result,
        Func<MetricsEvent, ValueTask> callback,
        string? displayName = null,
        string? model = null)
    {
        var evt = MetricsHelper.FromResultMessage(result, displayName: displayName, model: model);
        MetricsHelper.FireAndForget(evt, callback);
    }
}
