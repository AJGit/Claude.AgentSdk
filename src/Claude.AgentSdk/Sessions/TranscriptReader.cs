using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Result of reading a session file's head, tail, mtime and size (a "lite" read used to
///     extract listing metadata without parsing the full transcript).
/// </summary>
internal sealed class LiteSessionFile
{
    public required long Mtime { get; init; }
    public required long Size { get; init; }
    public required string Head { get; init; }
    public required string Tail { get; init; }
}

/// <summary>
///     A parsed JSONL transcript entry — mirrors the fields the chain-building and listing
///     logic care about (type, uuid, parentUuid, sessionId, message, isMeta, isSidechain,
///     isCompactSummary, teamName), keeping the raw <see cref="JsonElement" /> for message
///     extraction.
/// </summary>
internal sealed class TranscriptEntry
{
    public required string Type { get; init; }
    public required string Uuid { get; init; }
    public string? ParentUuid { get; init; }
    public string? SessionId { get; init; }
    public bool IsMeta { get; init; }
    public bool IsSidechain { get; init; }
    public bool IsCompactSummary { get; init; }
    public string? TeamName { get; init; }
    public required JsonElement Raw { get; init; }
}

/// <summary>
///     JSONL transcript reading, lite head/tail parsing, and conversation-chain reconstruction.
///     Mirrors the Python SDK's <c>_internal/sessions.py</c> transcript-handling helpers.
/// </summary>
internal static partial class TranscriptReader
{
    private static readonly HashSet<string> TranscriptEntryTypes =
        ["user", "assistant", "progress", "system", "attachment"];

    [GeneratedRegex(
        "^(?:<local-command-stdout>|<session-start-hook>|<tick>|<goal>|" +
        @"\[Request interrupted by user[^\]]*\]|" +
        @"\s*<ide_opened_file>[\s\S]*</ide_opened_file>\s*$|" +
        @"\s*<ide_selection>[\s\S]*</ide_selection>\s*$)")]
    internal static partial Regex SkipFirstPromptPattern();

    [GeneratedRegex("<command-name>(.*?)</command-name>")]
    internal static partial Regex CommandNameRegex();

    // -------------------------------------------------------------------
    // JSON string field extraction — no full parse, works on truncated lines
    // -------------------------------------------------------------------

    private static string UnescapeJsonString(string raw)
    {
        if (!raw.Contains('\\'))
        {
            return raw;
        }

        try
        {
            using var doc = JsonDocument.Parse("\"" + raw + "\"");
            return doc.RootElement.ValueKind == JsonValueKind.String
                ? doc.RootElement.GetString() ?? raw
                : raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    /// <summary>
    ///     Extracts a simple JSON string field value without full parsing. Looks for
    ///     <c>"key":"value"</c> or <c>"key": "value"</c> patterns. Returns the first match, or
    ///     <c>null</c> if not found.
    /// </summary>
    internal static string? ExtractJsonStringField(string text, string key)
    {
        string[] patterns = [$"\"{key}\":\"", $"\"{key}\": \""];
        foreach (var pattern in patterns)
        {
            var idx = text.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var valueStart = idx + pattern.Length;
            var i = valueStart;
            while (i < text.Length)
            {
                if (text[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (text[i] == '"')
                {
                    return UnescapeJsonString(text[valueStart..i]);
                }

                i++;
            }
        }

        return null;
    }

    /// <summary>Like <see cref="ExtractJsonStringField" /> but finds the LAST occurrence.</summary>
    internal static string? ExtractLastJsonStringField(string text, string key)
    {
        string[] patterns = [$"\"{key}\":\"", $"\"{key}\": \""];
        string? lastValue = null;
        foreach (var pattern in patterns)
        {
            var searchFrom = 0;
            while (true)
            {
                var idx = text.IndexOf(pattern, searchFrom, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                var valueStart = idx + pattern.Length;
                var i = valueStart;
                while (i < text.Length)
                {
                    if (text[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (text[i] == '"')
                    {
                        lastValue = UnescapeJsonString(text[valueStart..i]);
                        break;
                    }

                    i++;
                }

                searchFrom = i + 1;
            }
        }

        return lastValue;
    }

    // -------------------------------------------------------------------
    // First prompt extraction from head chunk
    // -------------------------------------------------------------------

    /// <summary>
    ///     Extracts the first meaningful user prompt from a JSONL head chunk. Skips tool_result
    ///     messages, isMeta, isCompactSummary, command-name messages, and auto-generated
    ///     patterns. Truncates to 200 chars.
    /// </summary>
    internal static string ExtractFirstPromptFromHead(string head)
    {
        var start = 0;
        var commandFallback = "";
        var headLen = head.Length;

        while (start < headLen)
        {
            string line;
            var newlineIdx = head.IndexOf('\n', start);
            if (newlineIdx >= 0)
            {
                line = head[start..newlineIdx];
                start = newlineIdx + 1;
            }
            else
            {
                line = head[start..];
                start = headLen;
            }

            if (!line.Contains("\"type\":\"user\"", StringComparison.Ordinal) &&
                !line.Contains("\"type\": \"user\"", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("\"tool_result\"", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("\"isMeta\":true", StringComparison.Ordinal) ||
                line.Contains("\"isMeta\": true", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("\"isCompactSummary\":true", StringComparison.Ordinal) ||
                line.Contains("\"isCompactSummary\": true", StringComparison.Ordinal))
            {
                continue;
            }

            JsonElement entry;
            try
            {
                using var doc = JsonDocument.Parse(line);
                entry = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                typeEl.GetString() != "user")
            {
                continue;
            }

            if (!entry.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var texts = new List<string>();
            if (message.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                {
                    texts.Add(content.GetString() ?? "");
                }
                else if (content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in content.EnumerateArray())
                    {
                        if (block.ValueKind == JsonValueKind.Object &&
                            block.TryGetProperty("type", out var blockType) &&
                            blockType.ValueKind == JsonValueKind.String &&
                            blockType.GetString() == "text" &&
                            block.TryGetProperty("text", out var textEl) &&
                            textEl.ValueKind == JsonValueKind.String)
                        {
                            texts.Add(textEl.GetString() ?? "");
                        }
                    }
                }
            }

            foreach (var raw in texts)
            {
                var result = raw.Replace('\n', ' ').Trim();
                if (result.Length == 0)
                {
                    continue;
                }

                var cmdMatch = CommandNameRegex().Match(result);
                if (cmdMatch.Success)
                {
                    if (commandFallback.Length == 0)
                    {
                        commandFallback = cmdMatch.Groups[1].Value;
                    }

                    continue;
                }

                if (SkipFirstPromptPattern().IsMatch(result))
                {
                    continue;
                }

                if (result.Length > 200)
                {
                    result = result[..200].TrimEnd() + "…";
                }

                return result;
            }
        }

        return commandFallback.Length > 0 ? commandFallback : "";
    }

    // -------------------------------------------------------------------
    // File I/O — read head and tail of a file
    // -------------------------------------------------------------------

    /// <summary>
    ///     Opens a session file, stats it, and reads head + tail. Returns <c>null</c> on any
    ///     error or if the file is empty.
    /// </summary>
    internal static LiteSessionFile? ReadSessionLite(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var size = stream.Length;
            var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeMilliseconds();

            var headBuf = new byte[Math.Min(SessionPaths.LiteReadBufSize, size)];
            var headRead = ReadFully(stream, headBuf);
            if (headRead == 0)
            {
                return null;
            }

            var head = Encoding.UTF8.GetString(headBuf, 0, headRead);

            var tailOffset = Math.Max(0, size - SessionPaths.LiteReadBufSize);
            string tail;
            if (tailOffset == 0)
            {
                tail = head;
            }
            else
            {
                stream.Seek(tailOffset, SeekOrigin.Begin);
                var tailBuf = new byte[Math.Min(SessionPaths.LiteReadBufSize, size - tailOffset)];
                var tailRead = ReadFully(stream, tailBuf);
                tail = Encoding.UTF8.GetString(tailBuf, 0, tailRead);
            }

            return new LiteSessionFile { Mtime = mtime, Size = size, Head = head, Tail = tail };
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

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    // -------------------------------------------------------------------
    // Field extraction — shared by list_sessions and get_session_info
    // -------------------------------------------------------------------

    /// <summary>
    ///     Parses <see cref="SdkSessionInfo" /> fields from a lite session read (head/tail/stat).
    ///     Returns <c>null</c> for sidechain sessions or metadata-only sessions with no
    ///     extractable summary. Shared by <see cref="ClaudeSessions.ListSessions" /> and
    ///     <see cref="ClaudeSessions.GetSessionInfo" />.
    /// </summary>
    internal static SdkSessionInfo? ParseSessionInfoFromLite(
        string sessionId, LiteSessionFile lite, string? projectPath = null)
    {
        var head = lite.Head;
        var tail = lite.Tail;

        var firstNewline = head.IndexOf('\n');
        var firstLine = firstNewline >= 0 ? head[..firstNewline] : head;
        if (firstLine.Contains("\"isSidechain\":true", StringComparison.Ordinal) ||
            firstLine.Contains("\"isSidechain\": true", StringComparison.Ordinal))
        {
            return null;
        }

        var customTitle =
            ExtractLastJsonStringField(tail, "customTitle") ??
            ExtractLastJsonStringField(head, "customTitle") ??
            ExtractLastJsonStringField(tail, "aiTitle") ??
            ExtractLastJsonStringField(head, "aiTitle");

        var firstPromptRaw = ExtractFirstPromptFromHead(head);
        var firstPrompt = firstPromptRaw.Length > 0 ? firstPromptRaw : null;

        var summary =
            customTitle ??
            ExtractLastJsonStringField(tail, "lastPrompt") ??
            ExtractLastJsonStringField(tail, "summary") ??
            firstPrompt;

        if (string.IsNullOrEmpty(summary))
        {
            return null;
        }

        var gitBranch =
            ExtractLastJsonStringField(tail, "gitBranch") ??
            ExtractJsonStringField(head, "gitBranch");

        var sessionCwd = ExtractJsonStringField(head, "cwd") ?? projectPath;

        string? tagLine = null;
        var tailLines = tail.Split('\n');
        for (var i = tailLines.Length - 1; i >= 0; i--)
        {
            if (tailLines[i].StartsWith("{\"type\":\"tag\"", StringComparison.Ordinal))
            {
                tagLine = tailLines[i];
                break;
            }
        }

        var tag = tagLine is not null
            ? ExtractLastJsonStringField(tagLine, "tag")
            : null;
        if (tag is { Length: 0 })
        {
            tag = null;
        }

        long? createdAt = null;
        var firstTimestamp = ExtractJsonStringField(head, "timestamp");
        if (firstTimestamp is not null &&
            DateTimeOffset.TryParse(
                firstTimestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            createdAt = dto.ToUnixTimeMilliseconds();
        }

        return new SdkSessionInfo(sessionId, summary, lite.Mtime)
        {
            FileSize = lite.Size,
            CustomTitle = customTitle,
            FirstPrompt = firstPrompt,
            GitBranch = gitBranch,
            Cwd = sessionCwd,
            Tag = tag,
            CreatedAt = createdAt
        };
    }

    // -------------------------------------------------------------------
    // get_session_messages — full transcript reconstruction
    // -------------------------------------------------------------------

    /// <summary>
    ///     Parses JSONL content into transcript entries. Only keeps entries that have a uuid and
    ///     are transcript message types (user/assistant/progress/system/attachment). Skips
    ///     corrupt lines.
    /// </summary>
    internal static List<TranscriptEntry> ParseTranscriptEntries(string content)
    {
        var entries = new List<TranscriptEntry>();
        var start = 0;
        var length = content.Length;

        while (start < length)
        {
            var end = content.IndexOf('\n', start);
            if (end == -1)
            {
                end = length;
            }

            var line = content[start..end].Trim();
            start = end + 1;
            if (line.Length == 0)
            {
                continue;
            }

            var entry = TryParseTranscriptEntry(line);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static TranscriptEntry? TryParseTranscriptEntry(string line)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(line);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        return FromElement(root);
    }

    /// <summary>
    ///     Converts an already-parsed entry (e.g. from a session store) into a transcript entry. Returns null
    ///     unless it is a transcript message type with a uuid.
    /// </summary>
    internal static TranscriptEntry? FromElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var type = typeEl.GetString()!;
        if (!TranscriptEntryTypes.Contains(type))
        {
            return null;
        }

        if (!root.TryGetProperty("uuid", out var uuidEl) || uuidEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return new TranscriptEntry
        {
            Type = type,
            Uuid = uuidEl.GetString()!,
            ParentUuid = GetStringOrNull(root, "parentUuid"),
            SessionId = GetStringOrNull(root, "sessionId"),
            IsMeta = GetBoolOrFalse(root, "isMeta"),
            IsSidechain = GetBoolOrFalse(root, "isSidechain"),
            IsCompactSummary = GetBoolOrFalse(root, "isCompactSummary"),
            TeamName = GetStringOrNull(root, "teamName"),
            Raw = root
        };
    }

    private static string? GetStringOrNull(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBoolOrFalse(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) &&
               value.ValueKind is JsonValueKind.True &&
               value.GetBoolean();
    }

    /// <summary>
    ///     Builds the conversation chain by finding the leaf and walking <c>parentUuid</c>.
    ///     Returns messages in chronological order (root -&gt; leaf).
    /// </summary>
    /// <remarks>
    ///     <c>logicalParentUuid</c> (set on compact_boundary entries) is intentionally NOT
    ///     followed. This matches VS Code IDE behavior — post-compaction, the isCompactSummary
    ///     message replaces earlier messages, so following logical parents would duplicate
    ///     content.
    /// </remarks>
    internal static List<TranscriptEntry> BuildConversationChain(IReadOnlyList<TranscriptEntry> entries)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var byUuid = new Dictionary<string, TranscriptEntry>();
        foreach (var entry in entries)
        {
            byUuid[entry.Uuid] = entry;
        }

        var entryIndex = new Dictionary<string, int>();
        for (var i = 0; i < entries.Count; i++)
        {
            entryIndex[entries[i].Uuid] = i;
        }

        var parentUuids = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (entry.ParentUuid is { Length: > 0 } parent)
            {
                parentUuids.Add(parent);
            }
        }

        var terminals = entries.Where(e => !parentUuids.Contains(e.Uuid)).ToList();

        var leaves = new List<TranscriptEntry>();
        foreach (var terminal in terminals)
        {
            TranscriptEntry? walkCur = terminal;
            var walkSeen = new HashSet<string>();
            while (walkCur is not null)
            {
                var uid = walkCur.Uuid;
                if (!walkSeen.Add(uid))
                {
                    break;
                }

                if (walkCur.Type is "user" or "assistant")
                {
                    leaves.Add(walkCur);
                    break;
                }

                walkCur = walkCur.ParentUuid is { Length: > 0 } p ? byUuid.GetValueOrDefault(p) : null;
            }
        }

        if (leaves.Count == 0)
        {
            return [];
        }

        var mainLeaves = leaves
            .Where(l => !l.IsSidechain && l.TeamName is null && !l.IsMeta)
            .ToList();

        TranscriptEntry PickBest(List<TranscriptEntry> candidates)
        {
            var best = candidates[0];
            var bestIdx = entryIndex.GetValueOrDefault(best.Uuid, -1);
            foreach (var cur in candidates.Skip(1))
            {
                var curIdx = entryIndex.GetValueOrDefault(cur.Uuid, -1);
                if (curIdx > bestIdx)
                {
                    best = cur;
                    bestIdx = curIdx;
                }
            }

            return best;
        }

        var leaf = mainLeaves.Count > 0 ? PickBest(mainLeaves) : PickBest(leaves);

        var chain = new List<TranscriptEntry>();
        var chainSeen = new HashSet<string>();
        TranscriptEntry? chainCur = leaf;
        while (chainCur is not null)
        {
            var uid = chainCur.Uuid;
            if (!chainSeen.Add(uid))
            {
                break;
            }

            chain.Add(chainCur);
            chainCur = chainCur.ParentUuid is { Length: > 0 } p ? byUuid.GetValueOrDefault(p) : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>Returns <c>true</c> if the entry should be included in the returned messages.</summary>
    internal static bool IsVisibleMessage(TranscriptEntry entry)
    {
        if (entry.Type is not ("user" or "assistant"))
        {
            return false;
        }

        if (entry.IsMeta || entry.IsSidechain)
        {
            return false;
        }

        // Note: isCompactSummary messages are intentionally included. They contain the
        // summarized content from compacted conversations and are the only representation of
        // that content post-compaction. This matches VS Code IDE behavior.
        return entry.TeamName is null;
    }

    /// <summary>Converts a transcript entry into a <see cref="SessionMessage" />.</summary>
    internal static SessionMessage ToSessionMessage(
        TranscriptEntry entry, string? parentToolUseId = null, string? parentAgentId = null)
    {
        var msgType = entry.Type == "user" ? "user" : "assistant";
        JsonElement? message = entry.Raw.TryGetProperty("message", out var m) ? m : null;
        return new SessionMessage(msgType, entry.Uuid, entry.SessionId ?? "", message)
        {
            ParentToolUseId = parentToolUseId,
            ParentAgentId = parentAgentId
        };
    }

    // -------------------------------------------------------------------
    // list_subagents / get_subagent_messages — subagent transcript reading
    // -------------------------------------------------------------------

    /// <summary>
    ///     Builds the conversation chain for a subagent transcript. Subagent transcripts are
    ///     simpler than main sessions — no compaction, no sidechains, no preserved segments. Find
    ///     the last user/assistant entry and walk <c>parentUuid</c> links back to the root.
    /// </summary>
    internal static List<TranscriptEntry> BuildSubagentChain(IReadOnlyList<TranscriptEntry> entries)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var byUuid = new Dictionary<string, TranscriptEntry>();
        foreach (var entry in entries)
        {
            byUuid[entry.Uuid] = entry;
        }

        TranscriptEntry? leaf = null;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Type is "user" or "assistant")
            {
                leaf = entries[i];
                break;
            }
        }

        if (leaf is null)
        {
            return [];
        }

        var chain = new List<TranscriptEntry>();
        var seen = new HashSet<string>();
        TranscriptEntry? current = leaf;
        while (current is not null)
        {
            var uid = current.Uuid;
            if (!seen.Add(uid))
            {
                break;
            }

            chain.Add(current);
            current = current.ParentUuid is { Length: > 0 } p ? byUuid.GetValueOrDefault(p) : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    ///     Recursively collects <c>agent-*.jsonl</c> files from a directory tree. Subagent
    ///     transcripts may live directly in <c>subagents/</c> or in nested subdirectories such as
    ///     <c>subagents/workflows/&lt;runId&gt;/</c>.
    /// </summary>
    internal static List<(string AgentId, string FilePath)> CollectAgentFiles(string baseDir)
    {
        var results = new List<(string, string)>();
        Walk(baseDir);
        return results;

        void Walk(string currentDir)
        {
            List<string> dirents;
            try
            {
                dirents = Directory.EnumerateFileSystemEntries(currentDir)
                    .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                    .ToList();
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (var entry in dirents)
            {
                var name = Path.GetFileName(entry);
                if (File.Exists(entry) && name.StartsWith("agent-", StringComparison.Ordinal) &&
                    name.EndsWith(".jsonl", StringComparison.Ordinal))
                {
                    var agentId = name["agent-".Length..^".jsonl".Length];
                    results.Add((agentId, entry));
                }
                else if (Directory.Exists(entry))
                {
                    Walk(entry);
                }
            }
        }
    }

    /// <summary><c>agent-&lt;id&gt;.jsonl</c> -&gt; <c>agent-&lt;id&gt;.meta.json</c> (same directory).</summary>
    internal static string AgentMetadataSidecarPath(string transcriptPath)
    {
        var dir = Path.GetDirectoryName(transcriptPath) ?? "";
        var name = Path.GetFileName(transcriptPath);
        var baseName = name.EndsWith(".jsonl", StringComparison.Ordinal)
            ? name[..^".jsonl".Length]
            : name;
        return Path.Combine(dir, baseName + ".meta.json");
    }

    /// <summary>
    ///     Reads the <c>.meta.json</c> sidecar beside a subagent transcript. Returns <c>null</c>
    ///     when the sidecar is missing, not valid JSON, or not a JSON object. Other IO errors
    ///     (e.g. the sidecar path being a directory) propagate — callers degrade those to "no
    ///     metadata" as a best-effort read.
    /// </summary>
    internal static JsonElement? ReadAgentMetadataSidecar(string transcriptPath)
    {
        string text;
        try
        {
            text = File.ReadAllText(AgentMetadataSidecarPath(transcriptPath), Encoding.UTF8);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Extract <c>(toolUseId, parentAgentId)</c> from an agent metadata JSON object. Works
    ///     for the on-disk <c>.meta.json</c> sidecar.
    /// </summary>
    internal static (string? ToolUseId, string? ParentAgentId) ParentIdsFromAgentMetadata(JsonElement? meta)
    {
        if (meta is not { } m || m.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        var toolUseId = m.TryGetProperty("toolUseId", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;
        var parentAgentId = m.TryGetProperty("parentAgentId", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        return (toolUseId, parentAgentId);
    }
}
