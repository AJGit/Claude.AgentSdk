using System.Text;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Exceptions;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     <see cref="ISessionStore" />-backed counterparts of the local-disk session helpers.
/// </summary>
public static partial class ClaudeSessions
{
    // Bound on concurrent per-session loads when deriving listings, so large listings don't exhaust adapter
    // connection pools or hit backend rate limits.
    private const int StoreListLoadConcurrency = 16;

    // -------------------------------------------------------------------
    // Read helpers
    // -------------------------------------------------------------------

    /// <summary>
    ///     Lists sessions from a session store, sorted by last-modified descending.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If the store implements <see cref="ISessionStoreSummaries" />, this is one summary call plus one cheap
    ///         <see cref="ISessionStoreListing.ListSessionsAsync" /> enumeration to gap-fill sessions whose sidecar is
    ///         missing or stale; zero per-session loads when sidecars are complete and fresh. Otherwise it loads each
    ///         session (at most 16 concurrently), which can be expensive on remote backends. Gap-fill requires
    ///         <see cref="ISessionStoreListing" />.
    ///     </para>
    ///     <para>Git worktrees are a filesystem concept and are not considered: the store uses a single project key.</para>
    /// </remarks>
    /// <param name="store">The store to read from.</param>
    /// <param name="directory">Project directory used to compute the project key. Defaults to the working directory.</param>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="offset">Number of sessions to skip from the start of the sorted result set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SessionStoreException">The store implements neither listing capability.</exception>
    public static async Task<IReadOnlyList<SdkSessionInfo>> ListSessionsFromStoreAsync(
        ISessionStore store, string? directory = null, int? limit = null, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var projectPath = SessionPaths.CanonicalizePath(directory ?? ".");
        var projectKey = SessionPaths.SanitizePath(projectPath);
        var listing = store as ISessionStoreListing;

        if (store is ISessionStoreSummaries summariesStore)
        {
            var summaries = await summariesStore.ListSessionSummariesAsync(projectKey, cancellationToken)
                .ConfigureAwait(false);

            var sessions = listing is null
                ? []
                : (await listing.ListSessionsAsync(projectKey, cancellationToken).ConfigureAwait(false)).ToList();
            var knownMtimes = new Dictionary<string, long>();
            foreach (var session in sessions)
            {
                knownMtimes[session.SessionId] = session.Mtime;
            }

            // Fresh summaries resolve up front; sessions without a sidecar, or with a stale one (older than the
            // listing's mtime), get a placeholder resolved by gap-fill. Summary-backed sidechain/empty sessions
            // are dropped before pagination, matching the disk path's filter-then-paginate semantics.
            var slots = new List<(long Mtime, string SessionId, SdkSessionInfo? Info)>();
            var freshIds = new HashSet<string>();
            foreach (var summary in summaries)
            {
                if (listing is not null)
                {
                    if (!knownMtimes.TryGetValue(summary.SessionId, out var known) || summary.Mtime < known)
                    {
                        continue;
                    }
                }

                freshIds.Add(summary.SessionId);
                if (SessionSummary.ToSdkSessionInfo(summary, projectPath) is { } info)
                {
                    slots.Add((summary.Mtime, summary.SessionId, info));
                }
            }

            slots.AddRange(sessions.Where(s => !freshIds.Contains(s.SessionId))
                .Select(s => (s.Mtime, s.SessionId, (SdkSessionInfo?)null)));

            // Paginate before loading so gap-fill loads are bounded by the page size.
            IEnumerable<(long Mtime, string SessionId, SdkSessionInfo? Info)> ordered =
                slots.OrderByDescending(s => s.Mtime);
            if (offset > 0)
            {
                ordered = ordered.Skip(offset);
            }

            if (limit is > 0)
            {
                ordered = ordered.Take(limit.Value);
            }

            var page = ordered.ToList();
            var toFill = page.Where(s => s.Info is null)
                .Select(s => new SessionStoreListEntry(s.SessionId, s.Mtime))
                .ToList();
            var filled = toFill.Count == 0
                ? new Dictionary<string, SdkSessionInfo>()
                : (await DeriveInfosViaLoadAsync(store, toFill, directory, projectPath, cancellationToken)
                    .ConfigureAwait(false)).ToDictionary(i => i.SessionId);

            // Placeholders that resolve to nothing (sidechain / no summary) are dropped after pagination; only
            // that case can short-page.
            return page
                .Select(s => s.Info ?? filled.GetValueOrDefault(s.SessionId))
                .Where(i => i is not null)
                .Select(i => i!)
                .ToList();
        }

        if (listing is null)
        {
            throw new SessionStoreException(
                "The session store implements neither ISessionStoreSummaries nor ISessionStoreListing -- cannot " +
                "list sessions.");
        }

        var all = (await listing.ListSessionsAsync(projectKey, cancellationToken).ConfigureAwait(false)).ToList();
        var results = await DeriveInfosViaLoadAsync(store, all, directory, projectPath, cancellationToken)
            .ConfigureAwait(false);
        return ApplySortLimitOffset(results, limit, offset);
    }

    /// <summary>
    ///     Reads metadata for a single session from a session store.
    /// </summary>
    /// <returns>
    ///     The info, or null if the session is not found, the id is not a UUID, it is a sidechain session, or it
    ///     has no extractable summary.
    /// </returns>
    public static async Task<SdkSessionInfo?> GetSessionInfoFromStoreAsync(ISessionStore store, string sessionId,
        string? directory = null, CancellationToken cancellationToken = default)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return null;
        }

        var jsonl = await LoadStoreEntriesAsJsonlAsync(store, sessionId, directory, cancellationToken)
            .ConfigureAwait(false);
        if (jsonl is null)
        {
            return null;
        }

        var projectPath = SessionPaths.CanonicalizePath(directory ?? ".");
        return TranscriptReader.ParseSessionInfoFromLite(sessionId, JsonlToLite(jsonl, MtimeFromJsonlTail(jsonl)),
            projectPath);
    }

    /// <summary>
    ///     Reads a session's conversation messages from a session store, in chronological order.
    /// </summary>
    /// <returns>The messages; empty if the session is not found or the id is not a UUID.</returns>
    public static async Task<IReadOnlyList<SessionMessage>> GetSessionMessagesFromStoreAsync(ISessionStore store,
        string sessionId, string? directory = null, int? limit = null, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return [];
        }

        var entries = await store.LoadAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId),
            cancellationToken).ConfigureAwait(false);
        if (entries is not { Count: > 0 })
        {
            return [];
        }

        return EntriesToSessionMessages(FilterTranscriptEntries(entries), limit, offset);
    }

    /// <summary>
    ///     Lists subagent ids for a session from a session store.
    /// </summary>
    /// <exception cref="SessionStoreException">The store does not implement <see cref="ISessionStoreSubkeys" />.</exception>
    public static async Task<IReadOnlyList<string>> ListSubagentsFromStoreAsync(ISessionStore store,
        string sessionId, string? directory = null, CancellationToken cancellationToken = default)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return [];
        }

        if (store is not ISessionStoreSubkeys subkeys)
        {
            throw new SessionStoreException(
                "The session store does not implement ISessionStoreSubkeys -- cannot list subagents.");
        }

        var subpaths = await subkeys.ListSubkeysAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId),
            cancellationToken).ConfigureAwait(false);

        var ids = new List<string>();
        foreach (var subpath in subpaths)
        {
            if (!subpath.StartsWith("subagents/", StringComparison.Ordinal))
            {
                continue;
            }

            var last = subpath[(subpath.LastIndexOf('/') + 1)..];
            if (last.StartsWith("agent-", StringComparison.Ordinal) && !ids.Contains(last["agent-".Length..]))
            {
                ids.Add(last["agent-".Length..]);
            }
        }

        return ids;
    }

    /// <summary>
    ///     Reads a subagent's conversation messages from a session store.
    /// </summary>
    /// <remarks>
    ///     Subagents may live at <c>subagents/agent-&lt;id&gt;</c> or nested (e.g. under
    ///     <c>subagents/workflows/&lt;runId&gt;/</c>); subkeys are scanned when the store implements
    ///     <see cref="ISessionStoreSubkeys" />, otherwise the direct path is tried. Parent ids come from the
    ///     subagent's <c>agent_metadata</c> entry (null if absent).
    /// </remarks>
    public static async Task<IReadOnlyList<SessionMessage>> GetSubagentMessagesFromStoreAsync(ISessionStore store,
        string sessionId, string agentId, string? directory = null, int? limit = null, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null || string.IsNullOrEmpty(agentId))
        {
            return [];
        }

        var projectKey = ProjectKeyForDirectory(directory);
        var subpath = $"subagents/agent-{agentId}";
        if (store is ISessionStoreSubkeys subkeys)
        {
            var target = $"agent-{agentId}";
            var match = (await subkeys.ListSubkeysAsync(new SessionKey(projectKey, sessionId), cancellationToken)
                    .ConfigureAwait(false))
                .FirstOrDefault(s => s.StartsWith("subagents/", StringComparison.Ordinal) &&
                                     s[(s.LastIndexOf('/') + 1)..] == target);
            if (match is null)
            {
                return [];
            }

            subpath = match;
        }

        var entries = await store.LoadAsync(new SessionKey(projectKey, sessionId, subpath), cancellationToken)
            .ConfigureAwait(false);
        if (entries is not { Count: > 0 })
        {
            return [];
        }

        // The agent_metadata entry (the store's copy of the .meta.json sidecar) records which Agent tool_use
        // spawned this subagent; it is not a transcript line.
        var (metadata, transcript) = SplitAgentMetadata(entries);
        if (transcript.Count == 0)
        {
            return [];
        }

        var (parentToolUseId, parentAgentId) = TranscriptReader.ParentIdsFromAgentMetadata(metadata);
        return EntriesToSubagentMessages(FilterTranscriptEntries(transcript), limit, offset, parentToolUseId,
            parentAgentId);
    }

    // -------------------------------------------------------------------
    // Mutations
    // -------------------------------------------------------------------

    /// <summary>
    ///     Renames a session by appending a custom-title entry to the store.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid session id, or a title that is empty after trimming.</exception>
    public static Task RenameSessionViaStoreAsync(ISessionStore store, string sessionId, string title,
        string? directory = null, CancellationToken cancellationToken = default)
    {
        ValidateSessionIdArgument(sessionId);
        var stripped = title.Trim();
        if (stripped.Length == 0)
        {
            throw new ArgumentException("title must be non-empty", nameof(title));
        }

        var entry = new JsonObject
        {
            ["type"] = "custom-title",
            ["customTitle"] = stripped,
            ["sessionId"] = sessionId,
            ["uuid"] = Guid.NewGuid().ToString(),
            ["timestamp"] = IsoNow()
        };
        return store.AppendAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId),
            [JsonSerializer.SerializeToElement(entry)], cancellationToken);
    }

    /// <summary>
    ///     Tags a session by appending a tag entry to the store. Pass null to clear the tag. Tags are
    ///     Unicode-sanitized.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid session id, or a tag that is empty after sanitization.</exception>
    public static Task TagSessionViaStoreAsync(ISessionStore store, string sessionId, string? tag,
        string? directory = null, CancellationToken cancellationToken = default)
    {
        ValidateSessionIdArgument(sessionId);
        if (tag is not null)
        {
            var sanitized = SessionMutationHelpers.SanitizeUnicode(tag).Trim();
            if (sanitized.Length == 0)
            {
                throw new ArgumentException("tag must be non-empty (use null to clear)", nameof(tag));
            }

            tag = sanitized;
        }

        var entry = new JsonObject
        {
            ["type"] = "tag",
            ["tag"] = tag ?? "",
            ["sessionId"] = sessionId,
            ["uuid"] = Guid.NewGuid().ToString(),
            ["timestamp"] = IsoNow()
        };
        return store.AppendAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId),
            [JsonSerializer.SerializeToElement(entry)], cancellationToken);
    }

    /// <summary>
    ///     Deletes a session from the store. A no-op when the store does not implement
    ///     <see cref="ISessionStoreDeletion" /> (appropriate for append-only backends). Whether subagent transcripts
    ///     are removed depends on the store's cascade semantics.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid session id.</exception>
    public static Task DeleteSessionViaStoreAsync(ISessionStore store, string sessionId, string? directory = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionIdArgument(sessionId);
        return store is ISessionStoreDeletion deletion
            ? deletion.DeleteAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId), cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    ///     Forks a session into a new branch with fresh UUIDs, reading from and writing to the store.
    /// </summary>
    /// <remarks>
    ///     A storage-layer copy is not sufficient: the transform remaps every UUID, rewrites <c>sessionId</c> on each
    ///     entry and stamps <c>forkedFrom</c>, so the data passes through this process once.
    /// </remarks>
    /// <exception cref="ArgumentException">Invalid session id or <paramref name="upToMessageId" />.</exception>
    /// <exception cref="FileNotFoundException">The source session is not in the store.</exception>
    /// <exception cref="InvalidOperationException">The session has no messages to fork.</exception>
    public static async Task<ForkSessionResult> ForkSessionViaStoreAsync(ISessionStore store, string sessionId,
        string? directory = null, string? upToMessageId = null, string? title = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionIdArgument(sessionId);
        if (!string.IsNullOrEmpty(upToMessageId) && SessionPaths.ValidateUuid(upToMessageId) is null)
        {
            throw new ArgumentException($"Invalid up_to_message_id: {upToMessageId}", nameof(upToMessageId));
        }

        var projectKey = ProjectKeyForDirectory(directory);
        var loaded = await store.LoadAsync(new SessionKey(projectKey, sessionId), cancellationToken)
            .ConfigureAwait(false);
        if (loaded is not { Count: > 0 })
        {
            throw new FileNotFoundException($"Session {sessionId} not found");
        }

        var raw = loaded.Select(e => JsonNode.Parse(e.GetRawText())).OfType<JsonObject>().ToList();
        var transcript = new List<JsonObject>();
        var contentReplacements = new List<JsonNode?>();
        foreach (var entry in raw)
        {
            var type = GetString(entry, "type");
            if (type is not null && SessionMutationHelpers.TranscriptTypes.Contains(type) &&
                GetString(entry, "uuid") is not null)
            {
                transcript.Add(entry);
            }
            else if (type == "content-replacement" && GetString(entry, "sessionId") == sessionId &&
                     entry["replacements"] is JsonArray replacements)
            {
                contentReplacements.AddRange(replacements.Select(r => r?.DeepClone()));
            }
        }

        var (forkedSessionId, lines) = SessionMutationHelpers.BuildForkLines(transcript, contentReplacements,
            sessionId, string.IsNullOrEmpty(upToMessageId) ? null : upToMessageId, title,
            () => DeriveTitleFromEntries(loaded));

        // Re-parse to objects so the store receives the same shape as from the live mirror.
        var forkEntries = lines.Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToList();
        await store.AppendAsync(new SessionKey(projectKey, forkedSessionId), forkEntries, cancellationToken)
            .ConfigureAwait(false);
        return new ForkSessionResult(forkedSessionId);
    }

    /// <summary>
    ///     Replays a local on-disk session transcript (and optionally its subagent transcripts and metadata) into a
    ///     session store.
    /// </summary>
    /// <remarks>
    ///     Useful for migrating local sessions to a remote store, or catching a store up after a <c>mirror_error</c>.
    ///     The destination project key is the on-disk project directory name, the same key the live mirror uses,
    ///     so an imported session is resumable via <see cref="ClaudeAgentOptions.SessionStore" /> from the original
    ///     working directory. Adapters should treat entry uuids as idempotency keys so re-import is duplicate-safe.
    /// </remarks>
    /// <param name="sessionId">UUID of the session to import.</param>
    /// <param name="store">Destination store.</param>
    /// <param name="directory">Project directory (same semantics as <see cref="ListSessions" />); null searches all projects.</param>
    /// <param name="includeSubagents">Also import subagent transcripts and their <c>.meta.json</c> sidecars.</param>
    /// <param name="batchSize">Maximum entries per append (also flushed at 1 MiB of line text).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Invalid session id.</exception>
    /// <exception cref="FileNotFoundException">The session transcript is not on disk.</exception>
    public static async Task ImportSessionToStoreAsync(string sessionId, ISessionStore store,
        string? directory = null, bool includeSubagents = true,
        int batchSize = TranscriptMirrorBatcher.MaxPendingEntries, CancellationToken cancellationToken = default)
    {
        ValidateSessionIdArgument(sessionId);
        var resolved = ResolveSessionFilePath(sessionId, directory)
                       ?? throw new FileNotFoundException($"Session {sessionId} not found");

        var projectKey = Path.GetFileName(Path.GetDirectoryName(resolved))!;
        if (batchSize <= 0)
        {
            batchSize = TranscriptMirrorBatcher.MaxPendingEntries;
        }

        await AppendJsonlFileInBatchesAsync(resolved, new SessionKey(projectKey, sessionId), store, batchSize,
            cancellationToken).ConfigureAwait(false);

        if (!includeSubagents)
        {
            return;
        }

        // Subagent transcripts live at <projectDir>/<sessionId>/subagents/**.
        var sessionDir = resolved[..^".jsonl".Length];
        foreach (var file in CollectJsonlFiles(Path.Combine(sessionDir, "subagents")))
        {
            // Subpath: relative to the session dir, '/'-joined, without .jsonl (round-trips with the mirror keys).
            var relative = Path.GetRelativePath(sessionDir, file).Replace('\\', '/');
            var subKey = new SessionKey(projectKey, sessionId, relative[..^".jsonl".Length]);
            await AppendJsonlFileInBatchesAsync(file, subKey, store, batchSize, cancellationToken)
                .ConfigureAwait(false);

            // The .jsonl never contains agent_metadata entries (live mirrors get them; disk keeps a sidecar).
            // Import the sidecar so resume can recreate it. A missing or corrupt sidecar is treated as absent.
            JsonElement? meta;
            try
            {
                meta = TranscriptReader.ReadAgentMetadataSidecar(file);
            }
            catch (IOException)
            {
                meta = null;
            }

            if (meta is { } metadata)
            {
                var metaEntry = new JsonObject();
                foreach (var property in metadata.EnumerateObject())
                {
                    metaEntry[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                }

                // Synthetic discriminator last so a stray "type" key in the sidecar can never shadow it.
                metaEntry["type"] = "agent_metadata";
                await store.AppendAsync(subKey, [JsonSerializer.SerializeToElement(metaEntry)], cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    /// <summary>
    ///     Separates the synthetic <c>agent_metadata</c> entry (last one wins) from transcript lines.
    /// </summary>
    internal static (JsonElement? Metadata, List<JsonElement> Transcript) SplitAgentMetadata(
        IReadOnlyList<JsonElement> entries)
    {
        JsonElement? metadata = null;
        var transcript = new List<JsonElement>();
        foreach (var entry in entries)
        {
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String && type.GetString() == "agent_metadata")
            {
                metadata = entry;
            }
            else
            {
                transcript.Add(entry);
            }
        }

        return (metadata, transcript);
    }

    private static void ValidateSessionIdArgument(string sessionId)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        }
    }

    private static List<TranscriptEntry> FilterTranscriptEntries(IEnumerable<JsonElement> entries)
    {
        return entries.Select(TranscriptReader.FromElement).Where(e => e is not null).Select(e => e!).ToList();
    }

    private static async Task<string?> LoadStoreEntriesAsJsonlAsync(ISessionStore store, string sessionId,
        string? directory, CancellationToken cancellationToken)
    {
        var entries = await store.LoadAsync(new SessionKey(ProjectKeyForDirectory(directory), sessionId),
            cancellationToken).ConfigureAwait(false);
        return entries is { Count: > 0 } ? EntriesToJsonl(entries) : null;
    }

    /// <summary>
    ///     Serializes entries to JSONL with <c>type</c> hoisted first: stores may reorder keys (e.g. Postgres JSONB)
    ///     but the lite parser matches a <c>{"type":"tag"</c> line prefix, so this matches the disk byte shape.
    /// </summary>
    private static string EntriesToJsonl(IEnumerable<JsonElement> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("type", out var type))
            {
                var ordered = new JsonObject { ["type"] = JsonNode.Parse(type.GetRawText()) };
                foreach (var property in entry.EnumerateObject().Where(p => p.Name != "type"))
                {
                    ordered[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                }

                builder.Append(ordered.ToJsonString());
            }
            else
            {
                builder.Append(entry.GetRawText());
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds the lite head/tail/size shape from in-memory JSONL, matching the disk reader's byte semantics.
    /// </summary>
    private static LiteSessionFile JsonlToLite(string jsonl, long mtime)
    {
        var bytes = Encoding.UTF8.GetBytes(jsonl);
        var size = bytes.Length;
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(size, SessionPaths.LiteReadBufSize));
        var tail = size > SessionPaths.LiteReadBufSize
            ? Encoding.UTF8.GetString(bytes, size - SessionPaths.LiteReadBufSize, SessionPaths.LiteReadBufSize)
            : head;
        return new LiteSessionFile { Mtime = mtime, Size = size, Head = head, Tail = tail };
    }

    /// <summary>
    ///     Best-effort mtime from the last entry's timestamp; falls back to now.
    /// </summary>
    private static long MtimeFromJsonlTail(string jsonl)
    {
        var trimmed = jsonl.TrimEnd();
        var lastLine = trimmed[(trimmed.LastIndexOf('\n') + 1)..];
        try
        {
            using var document = JsonDocument.Parse(lastLine);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("timestamp", out var timestamp) &&
                SessionSummary.IsoToEpochMs(timestamp) is { } ms)
            {
                return ms;
            }
        }
        catch (JsonException)
        {
            // Fall through
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    ///     Derives listing info per session via bounded concurrent loads. Adapter errors degrade that row to an
    ///     empty summary instead of failing the whole list; sidechain and no-summary sessions are dropped.
    /// </summary>
    private static async Task<List<SdkSessionInfo>> DeriveInfosViaLoadAsync(ISessionStore store,
        List<SessionStoreListEntry> listing, string? directory, string projectPath,
        CancellationToken cancellationToken)
    {
        using var limiter = new SemaphoreSlim(StoreListLoadConcurrency);
        var outcomes = await Task.WhenAll(listing.Select(async entry =>
        {
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return (Jsonl: await LoadStoreEntriesAsJsonlAsync(store, entry.SessionId, directory,
                    cancellationToken).ConfigureAwait(false), Failed: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (Jsonl: (string?)null, Failed: true);
            }
            finally
            {
                limiter.Release();
            }
        })).ConfigureAwait(false);

        var results = new List<SdkSessionInfo>();
        for (var i = 0; i < listing.Count; i++)
        {
            var (sessionId, mtime) = (listing[i].SessionId, listing[i].Mtime);
            var (jsonl, failed) = outcomes[i];
            if (failed)
            {
                results.Add(new SdkSessionInfo(sessionId, "", mtime));
                continue;
            }

            if (jsonl is null ||
                TranscriptReader.ParseSessionInfoFromLite(sessionId, JsonlToLite(jsonl, mtime), projectPath) is not
                    { } parsed)
            {
                continue;
            }

            results.Add(parsed with { LastModified = mtime, FileSize = null });
        }

        return results;
    }

    /// <summary>
    ///     Mirrors the disk title scan over parsed entries: last customTitle, then last aiTitle, then the first prompt.
    /// </summary>
    private static string? DeriveTitleFromEntries(IReadOnlyList<JsonElement> entries)
    {
        string? custom = null;
        string? ai = null;
        foreach (var entry in entries.Where(e => e.ValueKind == JsonValueKind.Object))
        {
            if (entry.TryGetProperty("customTitle", out var ct) && ct.ValueKind == JsonValueKind.String &&
                ct.GetString() is { Length: > 0 } customTitle)
            {
                custom = customTitle;
            }

            if (entry.TryGetProperty("aiTitle", out var at) && at.ValueKind == JsonValueKind.String &&
                at.GetString() is { Length: > 0 } aiTitle)
            {
                ai = aiTitle;
            }
        }

        if (custom is not null || ai is not null)
        {
            return custom ?? ai;
        }

        var firstPrompt = TranscriptReader.ExtractFirstPromptFromHead(EntriesToJsonl(entries));
        return firstPrompt.Length > 0 ? firstPrompt : null;
    }

    private static async Task AppendJsonlFileInBatchesAsync(string filePath, SessionKey key, ISessionStore store,
        int batchSize, CancellationToken cancellationToken)
    {
        var batch = new List<JsonElement>();
        long bytes = 0;
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            batch.Add(JsonDocument.Parse(line).RootElement.Clone());
            bytes += line.Length;
            if (batch.Count >= batchSize || bytes >= TranscriptMirrorBatcher.MaxPendingBytes)
            {
                await store.AppendAsync(key, batch, cancellationToken).ConfigureAwait(false);
                batch = [];
                bytes = 0;
            }
        }

        if (batch.Count > 0)
        {
            await store.AppendAsync(key, batch, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Recursively yields <c>*.jsonl</c> files, sorted per directory for deterministic order.
    /// </summary>
    private static IEnumerable<string> CollectJsonlFiles(string baseDir)
    {
        if (!Directory.Exists(baseDir))
        {
            yield break;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(baseDir).Order(StringComparer.Ordinal))
        {
            if (Directory.Exists(entry))
            {
                foreach (var nested in CollectJsonlFiles(entry))
                {
                    yield return nested;
                }
            }
            else if (entry.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                yield return entry;
            }
        }
    }

    private static string? GetString(JsonObject obj, string key)
    {
        return obj[key] is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;
    }

    private static string IsoNow()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }
}
