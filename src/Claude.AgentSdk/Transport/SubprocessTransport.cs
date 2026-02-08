using System.Diagnostics;
using System.Runtime.CompilerServices;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Logging;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Transport that communicates with the Claude CLI via subprocess stdin/stdout.
/// </summary>
internal sealed class SubprocessTransport(
    ClaudeAgentOptions options,
    string? prompt = null,
    ILogger<SubprocessTransport>? logger = null)
    : ITransport
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly ILogger<SubprocessTransport>? _logger = logger;
    private readonly ClaudeAgentOptions _options = options;
    private readonly string? _prompt = prompt;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;
    private bool _inputEnded;
    private IDisposable? _logScope;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;

    /// <summary>
    ///     Gets a value indicating whether the CLI process was forcibly killed during shutdown.
    /// </summary>
    public bool WasProcessKilled { get; private set; }

    /// <summary>
    ///     Gets the exit code of the CLI process, or null if the process hasn't exited.
    /// </summary>
    public int? ExitCode => _process?.HasExited == true ? _process.ExitCode : null;

    public bool IsReady => _process is not null && !_process.HasExited;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = CliPathResolver
            .Create(_options.CliPath, _logger)
            .Resolve();

        var args = CliArgumentsBuilder
            .Create(_options, _prompt)
            .Build();

        if (_logger is not null)
        {
            Log.CliStarting(_logger, cliPath, string.Join(" ", args));
        }

        var builder = ProcessStartInfoBuilder
            .Create(cliPath, args, _logger)
            .WithWorkingDirectory(_options.WorkingDirectory)
            .WithEnvironment(_options.Environment);

        if (_options.EnableFileCheckpointing)
        {
            builder.WithEnvironmentVariable("CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING", "true");
        }

        var startInfo = builder.Build();

        _process = new Process { StartInfo = startInfo };
        ConfigureStderrHandler();

        try
        {
            await StartProcessAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not TransportException)
        {
            throw new TransportException($"Failed to start CLI: {ex.Message}", ex);
        }
    }

    public async Task WriteAsync(JsonDocument message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stdin is null || _inputEnded)
        {
            throw new TransportException("Transport is not connected or input has ended");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = message.RootElement.GetRawText();
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
            _options.OnMessageSent?.Invoke(json);
            if (_logger is not null)
            {
                Log.MessageSent(_logger, json);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stdin is null || _inputEnded)
        {
            throw new TransportException("Transport is not connected or input has ended");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
            _options.OnMessageSent?.Invoke(json);
            if (_logger is not null)
            {
                Log.MessageSent(_logger, json);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async IAsyncEnumerable<JsonDocument> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stdout is null)
        {
            throw new TransportException("Transport is not connected");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _stdout.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (line is null)
            {
                if (_logger is not null)
                {
                    Log.CliStdoutClosed(_logger);
                }

                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            _options.OnMessageReceived?.Invoke(line);
            if (_logger is not null)
            {
                Log.MessageReceived(_logger, line);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                if (_logger is not null)
                {
                    Log.JsonParseError(_logger, ex, line);
                }

                throw new MessageParseException($"Failed to parse JSON: {ex.Message}", ex, line);
            }

            yield return doc;
        }
    }

    public async Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        if (_stdin is null || _inputEnded)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stdin.Close();
            _inputEnded = true;
            if (_logger is not null)
            {
                Log.StdinClosed(_logger);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
        {
            return;
        }

        await EndInputAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (_logger is not null)
                {
                    Log.CliKillingProcess(_logger);
                }

                _process.Kill(true);
                WasProcessKilled = true;
            }

            // Log exit information
            if (_process.HasExited && _logger is not null)
            {
                Log.CliProcessExited(_logger, _process.ExitCode, WasProcessKilled);
            }
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                Log.CliCloseError(_logger, ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await CloseAsync().ConfigureAwait(false);

        // Dispose streams unconditionally if they exist
        // CloseAsync already closed stdin via EndInputAsync, but we still need to dispose
        if (_stdin is not null)
        {
            await _stdin.DisposeAsync().ConfigureAwait(false);
        }

        _stdout?.Dispose();
        _process?.Dispose();
        _writeLock.Dispose();
        _logScope?.Dispose();
    }

    private void ConfigureStderrHandler()
    {
        _process!.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _options.OnStderr?.Invoke(e.Data);
                if (_logger is not null)
                {
                    Log.CliStderr(_logger, e.Data);
                }
            }
        };
    }

    private Task StartProcessAsync(CancellationToken cancellationToken)
    {
        if (!_process!.Start())
        {
            throw new TransportException("Failed to start CLI process");
        }

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
        _process.BeginErrorReadLine();

        // Create logging scope with CLI PID for correlation
        if (_logger is not null)
        {
            _logScope = _logger.BeginCliScope(_process.Id);
            Log.CliProcessStarted(_logger, _process.Id);
        }

        // SDK MCP servers require bidirectional mode for control protocol communication
        var hasSdkMcpServers = _options.McpServers?.Values.Any(c => c is McpSdkServerConfig) ?? false;

        // For one-shot mode (--print), close stdin immediately to signal EOF
        // But keep stdin open if SDK MCP servers are present (they need bidirectional communication)
        if (!string.IsNullOrEmpty(_prompt) && !hasSdkMcpServers)
        {
            if (_logger is not null)
            {
                Log.ClosingStdinOneShot(_logger);
            }

            _stdin.Close();
            _inputEnded = true;
        }

        return Task.CompletedTask;
    }
}
