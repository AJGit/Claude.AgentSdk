using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Attributes;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Identifies a session transcript, or a subagent transcript, in a <see cref="ISessionStore" />.
/// </summary>
/// <param name="ProjectKey">
///     Caller-defined scope. Default: the sanitized working directory (see
///     <see cref="ClaudeSessions.ProjectKeyForDirectory" />). Multi-tenant deployments can use a tenant id.
/// </param>
/// <param name="SessionId">The session id.</param>
/// <param name="Subpath">
///     Null for the main transcript; set for subagent files (e.g. <c>subagents/agent-abc</c>), mirroring the
///     on-disk layout. An empty string is invalid. Opaque to adapters — use it as a storage key suffix.
/// </param>
public sealed record SessionKey(
    [property: JsonPropertyName("project_key")] string ProjectKey,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("subpath")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Subpath = null)
{
    /// <summary>
    ///     The main-transcript key for this session (drops <see cref="Subpath" />).
    /// </summary>
    [JsonIgnore]
    public SessionKey MainKey => Subpath is null ? this : this with { Subpath = null };

    /// <summary>
    ///     Derives a key from an absolute transcript path under <paramref name="projectsDir" />.
    /// </summary>
    /// <remarks>
    ///     Main transcripts: <c>&lt;projectsDir&gt;/&lt;projectKey&gt;/&lt;sessionId&gt;.jsonl</c>.
    ///     Subagent transcripts: <c>&lt;projectsDir&gt;/&lt;projectKey&gt;/&lt;sessionId&gt;/subagents/.../agent-&lt;id&gt;.jsonl</c>.
    ///     Subpaths are always '/'-joined so keys are portable across platforms.
    /// </remarks>
    /// <returns>The key, or null when the path is not under <paramref name="projectsDir" /> or has another shape.</returns>
    public static SessionKey? FromTranscriptPath(string filePath, string projectsDir)
    {
        string relative;
        try
        {
            relative = Path.GetRelativePath(projectsDir, filePath);
        }
        catch (ArgumentException)
        {
            return null;
        }

        // Different drives on Windows yield a rooted "relative" path.
        if (Path.IsPathRooted(relative))
        {
            return null;
        }

        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] == "..")
        {
            return null;
        }

        var projectKey = parts[0];
        var second = parts[1];

        if (parts.Length == 2 && second.EndsWith(".jsonl", StringComparison.Ordinal))
        {
            return new SessionKey(projectKey, second[..^".jsonl".Length]);
        }

        if (parts.Length >= 4)
        {
            var subpathParts = parts[2..];
            if (subpathParts[^1].EndsWith(".jsonl", StringComparison.Ordinal))
            {
                subpathParts[^1] = subpathParts[^1][..^".jsonl".Length];
            }

            return new SessionKey(projectKey, second, string.Join('/', subpathParts));
        }

        return null;
    }

    /// <summary>
    ///     A composite <c>projectKey/sessionId[/subpath]</c> string, convenient as a storage key.
    /// </summary>
    public override string ToString()
    {
        return Subpath is null ? $"{ProjectKey}/{SessionId}" : $"{ProjectKey}/{SessionId}/{Subpath}";
    }
}

/// <summary>
///     Entry returned by <see cref="ISessionStoreListing.ListSessionsAsync" />.
/// </summary>
/// <param name="SessionId">The session id.</param>
/// <param name="Mtime">
///     Last-modified time in Unix epoch milliseconds. Adapters without native modification time must maintain
///     their own index.
/// </param>
public sealed record SessionStoreListEntry(string SessionId, long Mtime);

/// <summary>
///     Incrementally maintained session summary (see <see cref="SessionSummary.Fold" />).
/// </summary>
/// <param name="SessionId">The session id.</param>
/// <param name="Mtime">
///     Storage write time of the sidecar, in Unix epoch milliseconds. Must use the same clock as the mtime
///     returned by <see cref="ISessionStoreListing.ListSessionsAsync" /> for this session. Do NOT derive it
///     from entry timestamps; stamp it after persisting.
/// </param>
/// <param name="Data">Opaque SDK-owned summary state. Persist verbatim; do not interpret.</param>
public sealed record SessionSummaryEntry(string SessionId, long Mtime, JsonObject Data);

/// <summary>
///     When transcript-mirror entries are flushed to <see cref="ClaudeAgentOptions.SessionStore" />.
/// </summary>
[GenerateEnumStrings]
public enum SessionStoreFlushMode
{
    /// <summary>
    ///     Buffer entries and flush once per turn (on the result message) or when the pending buffer exceeds
    ///     500 entries / 1 MiB. Keeps adapter latency off the streaming hot path.
    /// </summary>
    [EnumMember(Value = "batched")] Batched,

    /// <summary>
    ///     Schedule a background flush after every transcript frame so the store sees entries in near real time.
    ///     Appends are still serialized in order; a slow adapter sees frames coalesced while it is busy.
    /// </summary>
    [EnumMember(Value = "eager")] Eager
}

/// <summary>
///     Adapter for mirroring session transcripts to external storage.
/// </summary>
/// <remarks>
///     <para>
///         The CLI still writes transcripts to local disk under <c>CLAUDE_CONFIG_DIR</c>; the adapter receives a
///         secondary copy. The SDK never deletes from your store unless you call
///         <see cref="ClaudeSessions.DeleteSessionViaStoreAsync" /> on a store implementing
///         <see cref="ISessionStoreDeletion" />. Retention (TTL, lifecycle policies) is the adapter's responsibility.
///     </para>
///     <para>
///         Only <see cref="AppendAsync" /> and <see cref="LoadAsync" /> are required. Implement
///         <see cref="ISessionStoreListing" />, <see cref="ISessionStoreSummaries" />, <see cref="ISessionStoreDeletion" />
///         and <see cref="ISessionStoreSubkeys" /> to enable the corresponding features.
///     </para>
///     <para>
///         Entries are JSON objects with at least a string <c>type</c>; treat them as opaque pass-through blobs.
///         Returned entries must be deep-equal to what was appended (byte-equal serialization is not required).
///         Use <see cref="Testing.SessionStoreConformance" /> to verify an adapter.
///     </para>
/// </remarks>
public interface ISessionStore
{
    /// <summary>
    ///     Mirrors a batch of transcript entries.
    /// </summary>
    /// <remarks>
    ///     Called after the CLI's local write succeeds, at roughly per-turn cadence (or per frame in eager mode).
    ///     Persist entries in call order. Most entries carry a stable <c>uuid</c>; treat it as an idempotency
    ///     key (upsert / ignore duplicates) since a retried batch may overlap a partial write. Entries without
    ///     a uuid (titles, tags, ...) should be appended without dedup. Exceptions are retried (3 attempts
    ///     total) and then surfaced as a <c>mirror_error</c> system message; the session continues unaffected.
    /// </remarks>
    Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a full transcript, or returns null for a key that was never written.
    /// </summary>
    Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key, CancellationToken cancellationToken = default);
}

/// <summary>
///     Optional: list sessions for a project key. Required for <see cref="ClaudeAgentOptions.ContinueConversation" />
///     with a store, and for <see cref="ClaudeSessions.ListSessionsFromStoreAsync" /> without summaries.
/// </summary>
public interface ISessionStoreListing
{
    /// <summary>
    ///     Lists main-transcript sessions (no subpaths) for <paramref name="projectKey" />. Order is unspecified.
    /// </summary>
    Task<IReadOnlyList<SessionStoreListEntry>> ListSessionsAsync(string projectKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Optional: return incrementally maintained summaries for all sessions of a project in one call.
/// </summary>
/// <remarks>
///     Maintain them with <see cref="SessionSummary.Fold" /> inside <see cref="ISessionStore.AppendAsync" />, skipping
///     keys with a subpath. If appends can race for the same session, serialize the read-fold-write.
/// </remarks>
public interface ISessionStoreSummaries
{
    /// <summary>
    ///     Returns the persisted summaries for <paramref name="projectKey" /> (main transcripts only).
    /// </summary>
    Task<IReadOnlyList<SessionSummaryEntry>> ListSessionSummariesAsync(string projectKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Optional: delete sessions. Without it, store-backed deletion is a no-op (appropriate for append-only backends).
/// </summary>
public interface ISessionStoreDeletion
{
    /// <summary>
    ///     Deletes a key. Deleting a main-transcript key must cascade to every subkey of that session; a key with
    ///     an explicit subpath removes only that entry.
    /// </summary>
    Task DeleteAsync(SessionKey key, CancellationToken cancellationToken = default);
}

/// <summary>
///     Optional: enumerate subagent transcripts. Without it, resume only materializes the main transcript.
/// </summary>
public interface ISessionStoreSubkeys
{
    /// <summary>
    ///     Lists all subpaths under a session (e.g. <c>subagents/agent-abc</c>). <see cref="SessionKey.Subpath" /> is ignored.
    /// </summary>
    Task<IReadOnlyList<string>> ListSubkeysAsync(SessionKey key, CancellationToken cancellationToken = default);
}

/// <summary>
///     In-memory <see cref="ISessionStore" /> implementing every optional capability. For tests and development:
///     data is lost when the process exits.
/// </summary>
public sealed class InMemorySessionStore :
    ISessionStore, ISessionStoreListing, ISessionStoreSummaries, ISessionStoreDeletion, ISessionStoreSubkeys
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Dictionary<string, long> _mtimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<JsonElement>> _store = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ProjectKey, string SessionId), SessionSummaryEntry> _summaries = new();
    private long _lastMtime;

    /// <summary>
    ///     Number of stored sessions (main transcripts only).
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _store.Keys.Count(k =>
                {
                    var first = k.IndexOf('/');
                    return first != -1 && k.IndexOf('/', first + 1) == -1;
                });
            }
        }
    }

    /// <inheritdoc />
    public Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var k = key.ToString();
            if (!_store.TryGetValue(k, out var list))
            {
                list = [];
                _store[k] = list;
            }

            list.AddRange(entries.Select(e => e.Clone()));
            var now = NextMtime();

            // Subagent subpaths don't contribute to the main session's summary.
            if (key.Subpath is null)
            {
                var summaryKey = (key.ProjectKey, key.SessionId);
                _summaries.TryGetValue(summaryKey, out var previous);
                var folded = SessionSummary.Fold(previous, key, entries);
                // Stamp with this adapter's storage write time: the same clock ListSessionsAsync exposes.
                _summaries[summaryKey] = folded with { Mtime = now };
            }

            _mtimes[k] = now;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<JsonElement>?>(
                _store.TryGetValue(key.ToString(), out var list) ? list.ToList() : null);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionStoreListEntry>> ListSessionsAsync(string projectKey,
        CancellationToken cancellationToken = default)
    {
        var prefix = projectKey + "/";
        lock (_lock)
        {
            IReadOnlyList<SessionStoreListEntry> result = _store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !k[prefix.Length..].Contains('/'))
                .Select(k => new SessionStoreListEntry(k[prefix.Length..], _mtimes.GetValueOrDefault(k)))
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionSummaryEntry>> ListSessionSummariesAsync(string projectKey,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<SessionSummaryEntry> result = _summaries
                .Where(kv => kv.Key.ProjectKey == projectKey)
                .Select(kv => kv.Value with { Data = (JsonObject)kv.Value.Data.DeepClone() })
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var k = key.ToString();
            _store.Remove(k);
            _mtimes.Remove(k);

            // Deleting the main transcript cascades to subagent transcripts so they aren't orphaned.
            if (key.Subpath is null)
            {
                _summaries.Remove((key.ProjectKey, key.SessionId));
                var prefix = k + "/";
                foreach (var storeKey in _store.Keys.Where(s => s.StartsWith(prefix, StringComparison.Ordinal))
                             .ToList())
                {
                    _store.Remove(storeKey);
                    _mtimes.Remove(storeKey);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListSubkeysAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        var prefix = key.MainKey + "/";
        lock (_lock)
        {
            IReadOnlyList<string> result = _store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .Select(k => k[prefix.Length..])
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <summary>
    ///     Test helper: all entries for a key (empty if absent).
    /// </summary>
    public IReadOnlyList<JsonElement> GetEntries(SessionKey key)
    {
        lock (_lock)
        {
            return _store.TryGetValue(key.ToString(), out var list) ? list.ToList() : [];
        }
    }

    /// <summary>
    ///     Test helper: removes all stored data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
            _mtimes.Clear();
            _summaries.Clear();
            _lastMtime = 0;
        }
    }

    // Strictly increasing so back-to-back appends always get distinct mtimes (real backends get this from
    // commit ordering).
    private long NextMtime()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now <= _lastMtime)
        {
            now = _lastMtime + 1;
        }

        _lastMtime = now;
        return now;
    }
}
