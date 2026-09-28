using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Logging;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Transport that communicates with the Claude CLI via subprocess stdin/stdout.
/// </summary>
/// <remarks>
///     The CLI always runs in streaming mode; the prompt (if any) is written to stdin by the caller.
/// </remarks>
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

    // Live CLI subprocesses, terminated when the host process exits so callers that crash or exit without
    // disposing don't leak orphaned `claude` processes.
    private static readonly ConcurrentDictionary<Process, byte> _activeChildren = new();

    private readonly ILogger<SubprocessTransport>? _logger = logger;
    private readonly ClaudeAgentOptions _options = options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    static SubprocessTransport()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillActiveChildren();
    }
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

        _ = prompt; // Prompts are sent over stdin by the protocol handler.

        var args = CliArgumentsBuilder
            .Create(_options)
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

                await ThrowIfProcessFailedAsync(cancellationToken).ConfigureAwait(false);
                yield break;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Some CLI builds write non-JSON diagnostics (e.g. "[SandboxDebug] ...") to stdout; skip them.
            // A line that starts like JSON but does not parse is corrupt and still raises below.
            if (!line.StartsWith('{'))
            {
                _logger?.LogDebug("Skipping non-JSON line from CLI stdout: {Line}",
                    line.Length > 200 ? line[..200] : line);
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
            finally
            {
                if (_process.HasExited)
                {
                    _activeChildren.TryRemove(_process, out _);
                }
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

        if (_process is not null)
        {
            _activeChildren.TryRemove(_process, out _);
        }

        _stdout?.Dispose();
        _process?.Dispose();
        _writeLock.Dispose();
        _logScope?.Dispose();
    }

    private static void KillActiveChildren()
    {
        foreach (var process in _activeChildren.Keys)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // Best effort during host shutdown
            }
        }

        _activeChildren.Clear();
    }

    /// <summary>
    ///     After stdout EOF, raises <see cref="CliProcessException" /> if the CLI exited with a non-zero code.
    /// </summary>
    private async Task ThrowIfProcessFailedAsync(CancellationToken cancellationToken)
    {
        if (_process is null || _disposed)
        {
            return;
        }

        int exitCode;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            exitCode = _process.ExitCode;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Stdout closed but the process lingers or its state is unavailable; nothing to report.
            return;
        }

        if (exitCode != 0 && !WasProcessKilled)
        {
            throw new CliProcessException($"Command failed with exit code {exitCode}", exitCode);
        }
    }

    private void ConfigureStderrHandler()
    {
        _process!.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            // Isolate the user callback so one failure doesn't silence every later stderr line.
            try
            {
                _options.OnStderr?.Invoke(e.Data);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "OnStderr callback threw; continuing");
            }

            if (_logger is not null)
            {
                Log.CliStderr(_logger, e.Data);
            }
        };
    }

    private Task StartProcessAsync(CancellationToken cancellationToken)
    {
        if (!_process!.Start())
        {
            throw new TransportException("Failed to start CLI process");
        }

        _activeChildren.TryAdd(_process, 0);

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
        _process.BeginErrorReadLine();

        // Create logging scope with CLI PID for correlation
        if (_logger is not null)
        {
            _logScope = _logger.BeginCliScope(_process.Id);
            Log.CliProcessStarted(_logger, _process.Id);
        }

        return Task.CompletedTask;
    }
}
