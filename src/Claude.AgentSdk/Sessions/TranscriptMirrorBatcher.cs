using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Accumulates <c>transcript_mirror</c> frames from the CLI and flushes them to an <see cref="ISessionStore" />.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Enqueue" /> is fire-and-forget; <see cref="FlushAsync" /> is called when a result arrives. The
///         pending queue is bounded: past <c>maxPendingEntries</c> / <c>maxPendingBytes</c> an eager flush runs in
///         the background so memory stays flat during long turns. Appends are serialized, so ordering holds.
///     </para>
///     <para>
///         Adapter failures are retried (3 attempts total) with short backoff; timeouts are not retried because the
///         in-flight call may still land. After the final attempt the batch is dropped and reported via
///         <c>onError</c>. Nothing here throws: the local transcript is already durable, so the session continues.
///     </para>
/// </remarks>
internal sealed class TranscriptMirrorBatcher
{
    internal const int MaxPendingEntries = 500;
    internal const int MaxPendingBytes = 1 << 20;
    internal const int MaxAppendAttempts = 3;

    internal static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan[] _backoff = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800)];

    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly ILogger? _logger;
    private readonly int _maxPendingBytes;
    private readonly int _maxPendingEntries;
    private readonly Func<SessionKey?, string, Task> _onError;
#if NET9_0_OR_GREATER
    private readonly Lock _pendingLock = new();
#else
    private readonly object _pendingLock = new();
#endif
    private readonly ISessionStore _store;
    private List<(string FilePath, List<JsonElement> Entries)> _pending = [];
    private long _pendingBytes;
    private int _pendingEntries;

    public TranscriptMirrorBatcher(
        ISessionStore store,
        string projectsDir,
        Func<SessionKey?, string, Task> onError,
        SessionStoreFlushMode flushMode = SessionStoreFlushMode.Batched,
        ILogger? logger = null,
        TimeSpan? sendTimeout = null)
    {
        _store = store;
        ProjectsDir = projectsDir;
        _onError = onError;
        _logger = logger;
        SendTimeout = sendTimeout ?? DefaultSendTimeout;

        // Eager mode zeroes the thresholds so every frame schedules a background flush.
        var eager = flushMode == SessionStoreFlushMode.Eager;
        _maxPendingEntries = eager ? 0 : MaxPendingEntries;
        _maxPendingBytes = eager ? 0 : MaxPendingBytes;
    }

    /// <summary>
    ///     The projects directory transcript paths are resolved against.
    /// </summary>
    public string ProjectsDir { get; }

    /// <summary>
    ///     Timeout for a single <see cref="ISessionStore.AppendAsync" /> call.
    /// </summary>
    public TimeSpan SendTimeout { get; }

    /// <summary>
    ///     The most recently scheduled background flush (for tests).
    /// </summary>
    internal Task? BackgroundFlush { get; private set; }

    /// <summary>
    ///     Buffers a frame; schedules a background flush when thresholds are exceeded.
    /// </summary>
    public void Enqueue(string filePath, IReadOnlyList<JsonElement> entries)
    {
        // Approximate wire size; one measurement per frame keeps this cheap.
        var size = entries.Sum(e => (long)e.GetRawText().Length);
        bool overflow;

        lock (_pendingLock)
        {
            _pending.Add((filePath, entries.Select(e => e.Clone()).ToList()));
            _pendingEntries += entries.Count;
            _pendingBytes += size;
            overflow = _pendingEntries > _maxPendingEntries || _pendingBytes > _maxPendingBytes;
        }

        if (overflow)
        {
            BackgroundFlush = Task.Run(DrainAsync);
        }
    }

    /// <summary>
    ///     Flushes all pending entries, serialized after any in-flight background flush. Never throws.
    /// </summary>
    public Task FlushAsync()
    {
        return DrainAsync();
    }

    /// <summary>
    ///     Detaches the pending buffer (so enqueueing can continue), then sends it under the flush lock.
    /// </summary>
    private async Task DrainAsync()
    {
        List<(string FilePath, List<JsonElement> Entries)> items;
        lock (_pendingLock)
        {
            items = _pending;
            _pending = [];
            _pendingEntries = 0;
            _pendingBytes = 0;
        }

        var errors = new List<(SessionKey Key, string Message)>();
        await _flushLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (items.Count == 0)
            {
                return;
            }

            await SendAsync(items, errors).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[TranscriptMirrorBatcher] flush raised");
            return;
        }
        finally
        {
            _flushLock.Release();
        }

        // Report after releasing the lock so a slow callback cannot block later flushes.
        foreach (var (key, message) in errors)
        {
            try
            {
                await _onError(key, message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[TranscriptMirrorBatcher] onError callback raised");
            }
        }
    }

    private async Task SendAsync(List<(string FilePath, List<JsonElement> Entries)> items,
        List<(SessionKey Key, string Message)> errors)
    {
        // Coalesce by file path: one append per file per flush, first-seen order, entries in enqueue order.
        var byPath = new List<(string FilePath, List<JsonElement> Entries)>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (filePath, entries) in items)
        {
            if (index.TryGetValue(filePath, out var i))
            {
                byPath[i].Entries.AddRange(entries);
            }
            else
            {
                index[filePath] = byPath.Count;
                byPath.Add((filePath, [.. entries]));
            }
        }

        foreach (var (filePath, entries) in byPath)
        {
            if (entries.Count == 0)
            {
                continue;
            }

            var key = SessionKey.FromTranscriptPath(filePath, ProjectsDir);
            if (key is null)
            {
                _logger?.LogWarning(
                    "[SessionStore] dropping mirror frame: filePath {FilePath} is not under {ProjectsDir} -- " +
                    "the CLI's CLAUDE_CONFIG_DIR likely differs from the SDK's", filePath, ProjectsDir);
                continue;
            }

            if (await AppendWithRetryAsync(key, entries).ConfigureAwait(false) is { } error)
            {
                _logger?.LogError("[TranscriptMirrorBatcher] flush failed for {FilePath}: {Error}", filePath, error);
                errors.Add((key, error));
            }
        }
    }

    /// <returns>Null on success, otherwise the final error message.</returns>
    private async Task<string?> AppendWithRetryAsync(SessionKey key, List<JsonElement> entries)
    {
        string? lastError = null;
        for (var attempt = 0; attempt < MaxAppendAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(_backoff[attempt - 1]).ConfigureAwait(false);
            }

            using var cts = new CancellationTokenSource(SendTimeout);
            try
            {
                // WaitAsync bounds adapters that ignore the token.
                await _store.AppendAsync(key, entries, cts.Token).WaitAsync(SendTimeout).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (ex is TimeoutException ||
                                       (ex is OperationCanceledException && cts.IsCancellationRequested))
            {
                // No retry on timeout: the in-flight call may still land, and a retry would duplicate it.
                return $"SessionStore.AppendAsync timed out after {SendTimeout.TotalSeconds:0.#}s";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger?.LogDebug(ex, "[TranscriptMirrorBatcher] append attempt {Attempt}/{Max} failed for {Key}",
                    attempt + 1, MaxAppendAttempts, key);
            }
        }

        return lastError;
    }
}
