using System.Text;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Local-disk session listing, reading, and mutation for the Claude CLI's on-disk
///     transcript store (<c>~/.claude/projects/&lt;sanitized-cwd&gt;/*.jsonl</c>, respecting
///     <c>CLAUDE_CONFIG_DIR</c>).
/// </summary>
/// <remarks>
///     Ported from the Python Agent SDK's <c>_internal/sessions.py</c> and
///     <c>_internal/session_mutations.py</c> local-disk functions. Functions that go through a
///     pluggable <c>SessionStore</c> backend are intentionally not part of this port — see the
///     porting notes in the repository history for this file.
/// </remarks>
public static partial class ClaudeSessions
{
    // -------------------------------------------------------------------
    // list_sessions
    // -------------------------------------------------------------------

    /// <summary>
    ///     Lists sessions with metadata extracted from stat + head/tail reads.
    /// </summary>
    /// <remarks>
    ///     When <paramref name="directory" /> is provided, returns sessions for that project
    ///     directory and its git worktrees. When omitted, returns sessions across all projects.
    ///     Use <paramref name="limit" /> and <paramref name="offset" /> for pagination.
    /// </remarks>
    /// <param name="directory">
    ///     Directory to list sessions for. When provided, returns sessions for this project
    ///     directory (and optionally its git worktree paths). When omitted, returns sessions
    ///     across all projects.
    /// </param>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="offset">
    ///     Number of sessions to skip from the start of the sorted result set. Use with
    ///     <paramref name="limit" /> for pagination. Defaults to 0.
    /// </param>
    /// <param name="includeWorktrees">
    ///     When <paramref name="directory" /> is provided and the directory is inside a git
    ///     repository, include sessions from all git worktree paths. Defaults to <c>true</c>.
    /// </param>
    /// <returns>List of <see cref="SdkSessionInfo" /> sorted by <c>LastModified</c> descending.</returns>
    public static IReadOnlyList<SdkSessionInfo> ListSessions(
        string? directory = null, int? limit = null, int offset = 0, bool includeWorktrees = true)
    {
        return directory is not null
            ? ListSessionsForProject(directory, limit, offset, includeWorktrees)
            : ListAllSessions(limit, offset);
    }

    private static List<SdkSessionInfo> ReadSessionsFromDir(string projectDir, string? projectPath = null)
    {
        List<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(projectDir).ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        var results = new List<SdkSessionInfo>();
        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (!name.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                continue;
            }

            var sessionId = SessionPaths.ValidateUuid(name[..^".jsonl".Length]);
            if (sessionId is null)
            {
                continue;
            }

            var lite = TranscriptReader.ReadSessionLite(entry);
            if (lite is null)
            {
                continue;
            }

            var info = TranscriptReader.ParseSessionInfoFromLite(sessionId, lite, projectPath);
            if (info is not null)
            {
                results.Add(info);
            }
        }

        return results;
    }

    private static List<SdkSessionInfo> DeduplicateBySessionId(IEnumerable<SdkSessionInfo> sessions)
    {
        var byId = new Dictionary<string, SdkSessionInfo>();
        foreach (var s in sessions)
        {
            if (!byId.TryGetValue(s.SessionId, out var existing) || s.LastModified > existing.LastModified)
            {
                byId[s.SessionId] = s;
            }
        }

        return byId.Values.ToList();
    }

    internal static List<SdkSessionInfo> ApplySortLimitOffset(
        List<SdkSessionInfo> sessions, int? limit, int offset = 0)
    {
        // OrderByDescending is a stable sort, matching Python's stable list.sort().
        IEnumerable<SdkSessionInfo> result = sessions.OrderByDescending(s => s.LastModified);
        if (offset > 0)
        {
            result = result.Skip(offset);
        }

        if (limit is > 0)
        {
            result = result.Take(limit.Value);
        }

        return result.ToList();
    }

    private static List<SdkSessionInfo> ListSessionsForProject(
        string directory, int? limit, int offset, bool includeWorktrees)
    {
        var canonicalDir = SessionPaths.CanonicalizePath(directory);

        IReadOnlyList<string> worktreePaths = [];
        if (includeWorktrees)
        {
            try
            {
                worktreePaths = SessionPaths.GetWorktreePaths(canonicalDir);
            }
            catch (Exception)
            {
                worktreePaths = [];
            }
        }

        // No worktrees (or git not available / scanning disabled) — just scan the single
        // project dir.
        if (worktreePaths.Count <= 1)
        {
            var projectDir = SessionPaths.FindProjectDir(canonicalDir);
            if (projectDir is null)
            {
                return [];
            }

            var sessions = ReadSessionsFromDir(projectDir, canonicalDir);
            return ApplySortLimitOffset(sessions, limit, offset);
        }

        // Worktree-aware scanning: find all project dirs matching any worktree.
        var projectsDir = SessionPaths.GetProjectsDir();
        var caseInsensitive = OperatingSystem.IsWindows();

        // Sort worktree paths by sanitized prefix length (longest first) so more specific
        // matches take priority over shorter ones.
        var indexed = worktreePaths
            .Select(wt =>
            {
                var sanitized = SessionPaths.SanitizePath(wt);
                var prefix = caseInsensitive ? sanitized.ToLowerInvariant() : sanitized;
                return (Wt: wt, Prefix: prefix);
            })
            .OrderByDescending(x => x.Prefix.Length)
            .ToList();

        List<string> allDirents;
        try
        {
            allDirents = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException)
        {
            var projectDir = SessionPaths.FindProjectDir(canonicalDir);
            if (projectDir is null)
            {
                return ApplySortLimitOffset([], limit, offset);
            }

            var fallbackSessions = ReadSessionsFromDir(projectDir, canonicalDir);
            return ApplySortLimitOffset(fallbackSessions, limit, offset);
        }

        var allSessions = new List<SdkSessionInfo>();
        var seenDirs = new HashSet<string>();

        // Always include the user's actual directory (handles subdirectories that won't match
        // worktree root prefixes).
        var canonicalProjectDir = SessionPaths.FindProjectDir(canonicalDir);
        if (canonicalProjectDir is not null)
        {
            var dirBase = Path.GetFileName(canonicalProjectDir);
            seenDirs.Add(caseInsensitive ? dirBase.ToLowerInvariant() : dirBase);
            allSessions.AddRange(ReadSessionsFromDir(canonicalProjectDir, canonicalDir));
        }

        foreach (var entry in allDirents)
        {
            var rawName = Path.GetFileName(entry);
            var dirName = caseInsensitive ? rawName.ToLowerInvariant() : rawName;
            if (seenDirs.Contains(dirName))
            {
                continue;
            }

            foreach (var (wtPath, prefix) in indexed)
            {
                // Only use startswith for truncated paths (>MaxSanitizedLength) where a hash
                // suffix follows. For short paths, require exact match to avoid /root/project
                // matching /root/project-foo.
                var isMatch = dirName == prefix ||
                              (prefix.Length >= SessionPaths.MaxSanitizedLength &&
                               dirName.StartsWith(prefix + "-", StringComparison.Ordinal));
                if (isMatch)
                {
                    seenDirs.Add(dirName);
                    allSessions.AddRange(ReadSessionsFromDir(entry, wtPath));
                    break;
                }
            }
        }

        var deduped = DeduplicateBySessionId(allSessions);
        return ApplySortLimitOffset(deduped, limit, offset);
    }

    private static List<SdkSessionInfo> ListAllSessions(int? limit, int offset)
    {
        var projectsDir = SessionPaths.GetProjectsDir();

        List<string> projectDirs;
        try
        {
            projectDirs = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        var allSessions = new List<SdkSessionInfo>();
        foreach (var projectDir in projectDirs)
        {
            allSessions.AddRange(ReadSessionsFromDir(projectDir));
        }

        var deduped = DeduplicateBySessionId(allSessions);
        return ApplySortLimitOffset(deduped, limit, offset);
    }

    // -------------------------------------------------------------------
    // get_session_info
    // -------------------------------------------------------------------

    /// <summary>
    ///     Reads metadata for a single session by ID. Wraps a lite head/tail read for one file —
    ///     no O(n) directory scan.
    /// </summary>
    /// <remarks>
    ///     Directory resolution matches <see cref="GetSessionMessages" />: <paramref name="directory" />
    ///     is the project path; when omitted, all project directories are searched for the
    ///     session file.
    /// </remarks>
    /// <param name="sessionId">UUID of the session to look up.</param>
    /// <param name="directory">
    ///     Project directory path (same semantics as <see cref="ListSessions" />'s
    ///     <c>directory</c> parameter). When omitted, all project directories are searched for
    ///     the session file.
    /// </param>
    /// <returns>
    ///     <see cref="SdkSessionInfo" /> for the session, or <c>null</c> if the session file is
    ///     not found, is a sidechain session, or has no extractable summary.
    /// </returns>
    public static SdkSessionInfo? GetSessionInfo(string sessionId, string? directory = null)
    {
        var uuid = SessionPaths.ValidateUuid(sessionId);
        if (uuid is null)
        {
            return null;
        }

        var fileName = $"{uuid}.jsonl";

        if (directory is not null)
        {
            var canonical = SessionPaths.CanonicalizePath(directory);
            var projectDir = SessionPaths.FindProjectDir(canonical);
            if (projectDir is not null)
            {
                var lite = TranscriptReader.ReadSessionLite(Path.Combine(projectDir, fileName));
                if (lite is not null)
                {
                    return TranscriptReader.ParseSessionInfoFromLite(uuid, lite, canonical);
                }
            }

            // Worktree fallback — matches GetSessionMessages semantics. Sessions may live under
            // a different worktree root.
            IReadOnlyList<string> worktreePaths;
            try
            {
                worktreePaths = SessionPaths.GetWorktreePaths(canonical);
            }
            catch (Exception)
            {
                worktreePaths = [];
            }

            foreach (var wt in worktreePaths)
            {
                if (wt == canonical)
                {
                    continue;
                }

                var wtProjectDir = SessionPaths.FindProjectDir(wt);
                if (wtProjectDir is not null)
                {
                    var lite = TranscriptReader.ReadSessionLite(Path.Combine(wtProjectDir, fileName));
                    if (lite is not null)
                    {
                        return TranscriptReader.ParseSessionInfoFromLite(uuid, lite, wt);
                    }
                }
            }

            return null;
        }

        // No directory — search all project directories for the session file.
        var projectsDir = SessionPaths.GetProjectsDir();
        List<string> dirents;
        try
        {
            dirents = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var entry in dirents)
        {
            var lite = TranscriptReader.ReadSessionLite(Path.Combine(entry, fileName));
            if (lite is not null)
            {
                return TranscriptReader.ParseSessionInfoFromLite(uuid, lite);
            }
        }

        return null;
    }

    // -------------------------------------------------------------------
    // get_session_messages
    // -------------------------------------------------------------------

    private static string? TryReadSessionFile(string projectDir, string fileName)
    {
        try
        {
            return File.ReadAllText(Path.Combine(projectDir, fileName), Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Finds and reads the session JSONL file. If <paramref name="directory" /> is provided,
    ///     looks in that project directory and its git worktrees. Otherwise, searches all
    ///     project directories. Returns the file content, or <c>null</c> if not found.
    /// </summary>
    private static string? ReadSessionFile(string sessionId, string? directory)
    {
        var fileName = $"{sessionId}.jsonl";

        if (directory is not null)
        {
            var canonicalDir = SessionPaths.CanonicalizePath(directory);

            var projectDir = SessionPaths.FindProjectDir(canonicalDir);
            if (projectDir is not null)
            {
                var content = TryReadSessionFile(projectDir, fileName);
                if (!string.IsNullOrEmpty(content))
                {
                    return content;
                }
            }

            IReadOnlyList<string> worktreePaths;
            try
            {
                worktreePaths = SessionPaths.GetWorktreePaths(canonicalDir);
            }
            catch (Exception)
            {
                worktreePaths = [];
            }

            foreach (var wt in worktreePaths)
            {
                if (wt == canonicalDir)
                {
                    continue;
                }

                var wtProjectDir = SessionPaths.FindProjectDir(wt);
                if (wtProjectDir is not null)
                {
                    var content = TryReadSessionFile(wtProjectDir, fileName);
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }
            }

            return null;
        }

        var projectsDir = SessionPaths.GetProjectsDir();
        List<string> dirents;
        try
        {
            dirents = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var entry in dirents)
        {
            var content = TryReadSessionFile(entry, fileName);
            if (!string.IsNullOrEmpty(content))
            {
                return content;
            }
        }

        return null;
    }

    internal static List<SessionMessage> EntriesToSessionMessages(
        List<TranscriptEntry> entries, int? limit, int offset)
    {
        var chain = TranscriptReader.BuildConversationChain(entries);
        var messages = chain
            .Where(TranscriptReader.IsVisibleMessage)
            .Select(e => TranscriptReader.ToSessionMessage(e))
            .ToList();

        if (limit is > 0)
        {
            return messages.Skip(offset).Take(limit.Value).ToList();
        }

        if (offset > 0)
        {
            return messages.Skip(offset).ToList();
        }

        return messages;
    }

    /// <summary>
    ///     Reads a session's conversation messages from its JSONL transcript file. Parses the
    ///     full JSONL, builds the conversation chain via <c>parentUuid</c> links, and returns
    ///     user/assistant messages in chronological order.
    /// </summary>
    /// <param name="sessionId">UUID of the session to read.</param>
    /// <param name="directory">
    ///     Project directory to find the session in. If omitted, searches all project
    ///     directories under <c>~/.claude/projects/</c>.
    /// </param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="offset">Number of messages to skip from the start.</param>
    /// <returns>
    ///     List of <see cref="SessionMessage" /> objects in chronological order. Returns an
    ///     empty list if the session is not found, <paramref name="sessionId" /> is not a valid
    ///     UUID, or the transcript contains no visible messages.
    /// </returns>
    public static IReadOnlyList<SessionMessage> GetSessionMessages(
        string sessionId, string? directory = null, int? limit = null, int offset = 0)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return [];
        }

        var content = ReadSessionFile(sessionId, directory);
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var entries = TranscriptReader.ParseTranscriptEntries(content);
        return EntriesToSessionMessages(entries, limit, offset);
    }

    // -------------------------------------------------------------------
    // list_subagents / get_subagent_messages
    // -------------------------------------------------------------------

    private static string? ResolveSessionFilePath(string sessionId, string? directory)
    {
        var fileName = $"{sessionId}.jsonl";

        string? StatCandidate(string projectDir)
        {
            var candidate = Path.Combine(projectDir, fileName);
            try
            {
                var info = new FileInfo(candidate);
                return info.Exists && info.Length > 0 ? candidate : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        if (directory is not null)
        {
            var canonicalDir = SessionPaths.CanonicalizePath(directory);

            var projectDir = SessionPaths.FindProjectDir(canonicalDir);
            if (projectDir is not null)
            {
                var found = StatCandidate(projectDir);
                if (found is not null)
                {
                    return found;
                }
            }

            IReadOnlyList<string> worktreePaths;
            try
            {
                worktreePaths = SessionPaths.GetWorktreePaths(canonicalDir);
            }
            catch (Exception)
            {
                worktreePaths = [];
            }

            foreach (var wt in worktreePaths)
            {
                if (wt == canonicalDir)
                {
                    continue;
                }

                var wtProjectDir = SessionPaths.FindProjectDir(wt);
                if (wtProjectDir is not null)
                {
                    var found = StatCandidate(wtProjectDir);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        var projectsDir = SessionPaths.GetProjectsDir();
        List<string> dirents;
        try
        {
            dirents = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var entry in dirents)
        {
            var found = StatCandidate(entry);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Resolves the subagents directory for a given session. The session file lives at
    ///     <c>&lt;projectDir&gt;/&lt;sessionId&gt;.jsonl</c> and the subagents directory at
    ///     <c>&lt;projectDir&gt;/&lt;sessionId&gt;/subagents/</c>. Returns <c>null</c> if the
    ///     session cannot be found.
    /// </summary>
    private static string? ResolveSubagentsDir(string sessionId, string? directory)
    {
        var resolved = ResolveSessionFilePath(sessionId, directory);
        if (resolved is null)
        {
            return null;
        }

        var sessionDir = resolved[..^".jsonl".Length];
        return Path.Combine(sessionDir, "subagents");
    }

    /// <summary>
    ///     Lists subagent IDs for a given session by scanning the subagents directory.
    /// </summary>
    /// <remarks>
    ///     Subagent transcripts are stored at
    ///     <c>~/.claude/projects/&lt;project&gt;/&lt;sessionId&gt;/subagents/agent-&lt;agentId&gt;.jsonl</c>
    ///     (and may be nested in subdirectories such as <c>workflows/&lt;runId&gt;/</c>).
    /// </remarks>
    /// <param name="sessionId">UUID of the parent session.</param>
    /// <param name="directory">
    ///     Project directory to find the session in. If omitted, searches all project
    ///     directories under <c>~/.claude/projects/</c>.
    /// </param>
    /// <returns>
    ///     List of subagent ID strings. Returns an empty list if the session is not found,
    ///     <paramref name="sessionId" /> is not a valid UUID, or the session has no subagents.
    /// </returns>
    public static IReadOnlyList<string> ListSubagents(string sessionId, string? directory = null)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return [];
        }

        var subagentsDir = ResolveSubagentsDir(sessionId, directory);
        if (subagentsDir is null)
        {
            return [];
        }

        return TranscriptReader.CollectAgentFiles(subagentsDir).Select(x => x.AgentId).ToList();
    }

    internal static List<SessionMessage> EntriesToSubagentMessages(
        List<TranscriptEntry> entries, int? limit, int offset, string? parentToolUseId, string? parentAgentId)
    {
        var chain = TranscriptReader.BuildSubagentChain(entries);
        var messages = chain
            .Where(e => e.Type is "user" or "assistant")
            .Select(e => TranscriptReader.ToSessionMessage(e, parentToolUseId, parentAgentId))
            .ToList();

        if (limit is > 0)
        {
            return messages.Skip(offset).Take(limit.Value).ToList();
        }

        if (offset > 0)
        {
            return messages.Skip(offset).ToList();
        }

        return messages;
    }

    /// <summary>
    ///     Reads a subagent's conversation messages from its JSONL transcript file. Parses the
    ///     subagent transcript, builds the conversation chain via <c>parentUuid</c> links, and
    ///     returns user/assistant messages in chronological order.
    /// </summary>
    /// <remarks>
    ///     Each message's <see cref="SessionMessage.ParentToolUseId" /> is the id of the Agent
    ///     tool-use in the parent session that spawned this subagent (and
    ///     <see cref="SessionMessage.ParentAgentId" /> the spawning subagent, for nested
    ///     subagents), read from the <c>agent-&lt;agentId&gt;.meta.json</c> sidecar next to the
    ///     transcript; both are <c>null</c> if the sidecar is missing or unusable.
    /// </remarks>
    /// <param name="sessionId">UUID of the parent session.</param>
    /// <param name="agentId">ID of the subagent (as returned by <see cref="ListSubagents" />).</param>
    /// <param name="directory">
    ///     Project directory to find the session in. If omitted, searches all project
    ///     directories under <c>~/.claude/projects/</c>.
    /// </param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="offset">Number of messages to skip from the start.</param>
    /// <returns>
    ///     List of <see cref="SessionMessage" /> objects in chronological order. Returns an
    ///     empty list if the session or subagent is not found, <paramref name="sessionId" /> is
    ///     not a valid UUID, or the transcript contains no user/assistant messages.
    /// </returns>
    public static IReadOnlyList<SessionMessage> GetSubagentMessages(
        string sessionId, string agentId, string? directory = null, int? limit = null, int offset = 0)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            return [];
        }

        if (string.IsNullOrEmpty(agentId))
        {
            return [];
        }

        var subagentsDir = ResolveSubagentsDir(sessionId, directory);
        if (subagentsDir is null)
        {
            return [];
        }

        // The agent file may be directly in subagents/ or in a nested subdirectory — scan to
        // find it.
        string? match = null;
        foreach (var (foundId, filePath) in TranscriptReader.CollectAgentFiles(subagentsDir))
        {
            if (foundId == agentId)
            {
                match = filePath;
                break;
            }
        }

        if (match is null)
        {
            return [];
        }

        string content;
        try
        {
            content = File.ReadAllText(match, Encoding.UTF8);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        if (content.Length == 0)
        {
            return [];
        }

        // The .meta.json sidecar next to the transcript records which Agent tool_use spawned
        // this subagent (and, for nested subagents, the parent agent id). Like the transcript
        // read above, any failure to read it (missing, unreadable, corrupt) degrades to "no
        // metadata" rather than raising from this best-effort read helper.
        JsonElement? meta;
        try
        {
            meta = TranscriptReader.ReadAgentMetadataSidecar(match);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            meta = null;
        }

        var (parentToolUseId, parentAgentId) = TranscriptReader.ParentIdsFromAgentMetadata(meta);

        var entries = TranscriptReader.ParseTranscriptEntries(content);
        return EntriesToSubagentMessages(entries, limit, offset, parentToolUseId, parentAgentId);
    }

    // -------------------------------------------------------------------
    // project_key_for_directory
    // -------------------------------------------------------------------

    /// <summary>
    ///     Derives a project key for a directory, using the same realpath + NFC normalization +
    ///     hashed sanitization the CLI uses for project directory names.
    /// </summary>
    /// <param name="directory">Directory to key. Defaults to the current working directory.</param>
    public static string ProjectKeyForDirectory(string? directory = null)
    {
        var absPath = SessionPaths.CanonicalizePath(directory ?? ".");
        return SessionPaths.SanitizePath(absPath);
    }

    // -------------------------------------------------------------------
    // rename_session
    // -------------------------------------------------------------------

    private sealed record CustomTitleEntry(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("customTitle")] string CustomTitle,
        [property: JsonPropertyName("sessionId")] string SessionId);

    /// <summary>
    ///     Renames a session by appending a custom-title entry.
    /// </summary>
    /// <remarks>
    ///     <see cref="ListSessions" /> reads the LAST custom-title from the file tail, so
    ///     repeated calls are safe — the most recent wins.
    /// </remarks>
    /// <param name="sessionId">UUID of the session to rename.</param>
    /// <param name="title">
    ///     New session title. Leading/trailing whitespace is stripped. Must be non-empty after
    ///     stripping.
    /// </param>
    /// <param name="directory">
    ///     Project directory path (same semantics as <see cref="ListSessions" />'s
    ///     <c>directory</c> parameter). When omitted, all project directories are searched for
    ///     the session file.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="sessionId" /> is not a valid UUID, or <paramref name="title" /> is
    ///     empty/whitespace-only.
    /// </exception>
    /// <exception cref="FileNotFoundException">The session file cannot be found.</exception>
    public static void RenameSession(string sessionId, string title, string? directory = null)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        }

        // Matches CLI guard — empty/whitespace titles are rejected rather than overloaded as
        // "clear title".
        var stripped = title.Trim();
        if (stripped.Length == 0)
        {
            throw new ArgumentException("title must be non-empty", nameof(title));
        }

        var data = JsonSerializer.Serialize(new CustomTitleEntry("custom-title", stripped, sessionId)) + "\n";
        SessionMutationHelpers.AppendToSession(sessionId, data, directory);
    }

    // -------------------------------------------------------------------
    // tag_session
    // -------------------------------------------------------------------

    private sealed record TagEntry(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("sessionId")] string SessionId);

    /// <summary>
    ///     Tags a session. Pass <c>null</c> to clear the tag.
    /// </summary>
    /// <remarks>
    ///     Appends a <c>{type:'tag',tag:&lt;tag&gt;,sessionId:&lt;id&gt;}</c> JSONL entry.
    ///     <see cref="ListSessions" /> reads the LAST tag from the file tail — most recent wins.
    ///     Passing <c>null</c> appends an empty-string tag entry which <see cref="ListSessions" />
    ///     treats as <c>null</c> (cleared). Tags are Unicode-sanitized before storing (removes
    ///     zero-width chars, directional marks, private-use characters, etc.) for CLI filter
    ///     compatibility.
    /// </remarks>
    /// <param name="sessionId">UUID of the session to tag.</param>
    /// <param name="tag">
    ///     Tag string, or <c>null</c> to clear. Leading/trailing whitespace is stripped. Must be
    ///     non-empty after sanitization and stripping (unless <c>null</c>).
    /// </param>
    /// <param name="directory">
    ///     Project directory path (same semantics as <see cref="ListSessions" />'s
    ///     <c>directory</c> parameter). When omitted, all project directories are searched for
    ///     the session file.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="sessionId" /> is not a valid UUID, or <paramref name="tag" /> is
    ///     empty/whitespace-only after sanitization.
    /// </exception>
    /// <exception cref="FileNotFoundException">The session file cannot be found.</exception>
    public static void TagSession(string sessionId, string? tag, string? directory = null)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        }

        var sanitizedTag = tag;
        if (tag is not null)
        {
            var sanitized = SessionMutationHelpers.SanitizeUnicode(tag).Trim();
            if (sanitized.Length == 0)
            {
                throw new ArgumentException("tag must be non-empty (use None to clear)", nameof(tag));
            }

            sanitizedTag = sanitized;
        }

        var data = JsonSerializer.Serialize(new TagEntry("tag", sanitizedTag ?? "", sessionId)) + "\n";
        SessionMutationHelpers.AppendToSession(sessionId, data, directory);
    }

    // -------------------------------------------------------------------
    // delete_session
    // -------------------------------------------------------------------

    /// <summary>
    ///     Deletes a session by removing its JSONL file and subagent transcripts.
    /// </summary>
    /// <remarks>
    ///     This is a hard delete — the <c>{session_id}.jsonl</c> file is removed permanently,
    ///     along with the sibling <c>{session_id}/</c> subdirectory that holds subagent
    ///     transcripts (if it exists). Callers who need soft-delete semantics can use
    ///     <c>TagSession(id, "__hidden")</c> and filter on listing instead.
    /// </remarks>
    /// <param name="sessionId">UUID of the session to delete.</param>
    /// <param name="directory">
    ///     Project directory path (same semantics as <see cref="ListSessions" />'s
    ///     <c>directory</c> parameter). When omitted, all project directories are searched for
    ///     the session file.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="sessionId" /> is not a valid UUID.</exception>
    /// <exception cref="FileNotFoundException">The session file cannot be found.</exception>
    public static void DeleteSession(string sessionId, string? directory = null)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        }

        var found = SessionMutationHelpers.FindSessionFileWithDir(sessionId, directory);
        if (found is null)
        {
            throw new FileNotFoundException(
                $"Session {sessionId} not found" +
                (directory is not null ? $" in project directory for {directory}" : ""));
        }

        var (filePath, projectDir) = found.Value;
        File.Delete(filePath);

        // Subagent transcripts live in a sibling {session_id}/ dir; often absent.
        try
        {
            Directory.Delete(Path.Combine(projectDir, sessionId), recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // -------------------------------------------------------------------
    // fork_session
    // -------------------------------------------------------------------

    /// <summary>
    ///     Forks a session into a new branch with fresh UUIDs.
    /// </summary>
    /// <remarks>
    ///     Copies transcript messages from the source session into a new session file,
    ///     remapping every message UUID and preserving the <c>parentUuid</c> chain. Supports
    ///     <paramref name="upToMessageId" /> for branching from a specific point in the
    ///     conversation. Forked sessions start without undo history (file-history snapshots are
    ///     not copied).
    /// </remarks>
    /// <param name="sessionId">UUID of the source session to fork.</param>
    /// <param name="directory">
    ///     Project directory path (same semantics as <see cref="ListSessions" />'s
    ///     <c>directory</c> parameter). When omitted, all project directories are searched for
    ///     the session file.
    /// </param>
    /// <param name="upToMessageId">
    ///     Slice the transcript up to this message UUID (inclusive). If omitted, copies the full
    ///     transcript.
    /// </param>
    /// <param name="title">
    ///     Custom title for the fork. If omitted, derives from the original title + " (fork)".
    /// </param>
    /// <returns><see cref="ForkSessionResult" /> with the new session's UUID.</returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="sessionId" /> or <paramref name="upToMessageId" /> is not a valid
    ///     UUID, or <paramref name="upToMessageId" /> is not found in the transcript.
    /// </exception>
    /// <exception cref="FileNotFoundException">The source session file cannot be found.</exception>
    /// <exception cref="InvalidOperationException">The session has no messages to fork.</exception>
    public static ForkSessionResult ForkSession(
        string sessionId, string? directory = null, string? upToMessageId = null, string? title = null)
    {
        if (SessionPaths.ValidateUuid(sessionId) is null)
        {
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        }

        if (upToMessageId is not null && SessionPaths.ValidateUuid(upToMessageId) is null)
        {
            throw new ArgumentException($"Invalid up_to_message_id: {upToMessageId}", nameof(upToMessageId));
        }

        var source = SessionMutationHelpers.FindSessionFileWithDir(sessionId, directory);
        if (source is null)
        {
            throw new FileNotFoundException(
                $"Session {sessionId} not found" +
                (directory is not null ? $" in project directory for {directory}" : ""));
        }

        var (filePath, projectDir) = source.Value;
        var content = File.ReadAllBytes(filePath);
        if (content.Length == 0)
        {
            throw new InvalidOperationException($"Session {sessionId} has no messages to fork");
        }

        var (transcript, contentReplacements) = SessionMutationHelpers.ParseForkTranscript(content, sessionId);

        string? DeriveTitle()
        {
            var bufLen = content.Length;
            var head = Encoding.UTF8.GetString(content, 0, Math.Min(bufLen, SessionPaths.LiteReadBufSize));
            var tailStart = Math.Max(0, bufLen - SessionPaths.LiteReadBufSize);
            var tail = Encoding.UTF8.GetString(content, tailStart, bufLen - tailStart);
            var firstPrompt = TranscriptReader.ExtractFirstPromptFromHead(head);
            return TranscriptReader.ExtractLastJsonStringField(tail, "customTitle") ??
                   TranscriptReader.ExtractLastJsonStringField(head, "customTitle") ??
                   TranscriptReader.ExtractLastJsonStringField(tail, "aiTitle") ??
                   TranscriptReader.ExtractLastJsonStringField(head, "aiTitle") ??
                   (firstPrompt.Length > 0 ? firstPrompt : null);
        }

        var (forkedSessionId, lines) = SessionMutationHelpers.BuildForkLines(
            transcript, contentReplacements, sessionId, upToMessageId, title, DeriveTitle);

        var forkPath = Path.Combine(projectDir, $"{forkedSessionId}.jsonl");
        using (var stream = new FileStream(forkPath, FileMode.CreateNew, FileAccess.Write))
        {
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }

        return new ForkSessionResult(forkedSessionId);
    }
}
