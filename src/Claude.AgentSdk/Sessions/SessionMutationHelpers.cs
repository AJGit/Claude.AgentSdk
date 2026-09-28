using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Low-level helpers backing the session mutation functions (rename/tag/delete/fork).
///     Mirrors the Python SDK's <c>_internal/session_mutations.py</c> helpers.
/// </summary>
/// <remarks>
///     Concurrent writers: unlike the CLI (which uses POSIX <c>O_APPEND</c> semantics for
///     race-free appends across processes), .NET has no directly portable equivalent, so
///     <see cref="TryAppend" /> is a best-effort, same-process-safe append rather than a
///     cross-process-atomic one.
/// </remarks>
internal static partial class SessionMutationHelpers
{
    internal static readonly HashSet<string> TranscriptTypes =
        ["user", "assistant", "attachment", "system", "progress"];

    // -------------------------------------------------------------------
    // Low-level append
    // -------------------------------------------------------------------

    /// <summary>
    ///     Tries appending to a path. Returns <c>true</c> on success, <c>false</c> if the file
    ///     does not exist or is a 0-byte stub (a "session not here, keep searching" signal).
    ///     Other I/O errors propagate.
    /// </summary>
    internal static bool TryAppend(string path, string data)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }

        using (stream)
        {
            if (stream.Length == 0)
            {
                return false;
            }

            stream.Seek(0, SeekOrigin.End);
            var bytes = Encoding.UTF8.GetBytes(data);
            stream.Write(bytes, 0, bytes.Length);
            return true;
        }
    }

    /// <summary>
    ///     Appends data to an existing session file. Searches candidate paths (directory + its
    ///     git worktrees, or all project directories when <paramref name="directory" /> is
    ///     <c>null</c>) and tries the append directly — no existence check.
    /// </summary>
    internal static void AppendToSession(string sessionId, string data, string? directory)
    {
        var fileName = $"{sessionId}.jsonl";

        if (directory is not null)
        {
            var canonical = SessionPaths.CanonicalizePath(directory);

            var projectDir = SessionPaths.FindProjectDir(canonical);
            if (projectDir is not null && TryAppend(Path.Combine(projectDir, fileName), data))
            {
                return;
            }

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
                if (wtProjectDir is not null && TryAppend(Path.Combine(wtProjectDir, fileName), data))
                {
                    return;
                }
            }

            throw new FileNotFoundException(
                $"Session {sessionId} not found in project directory for {directory}");
        }

        var projectsDir = SessionPaths.GetProjectsDir();
        List<string> dirents;
        try
        {
            dirents = Directory.EnumerateDirectories(projectsDir).ToList();
        }
        catch (IOException ex)
        {
            throw new FileNotFoundException($"Session {sessionId} not found (no projects directory)", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FileNotFoundException($"Session {sessionId} not found (no projects directory)", ex);
        }

        foreach (var entry in dirents)
        {
            if (TryAppend(Path.Combine(entry, fileName), data))
            {
                return;
            }
        }

        throw new FileNotFoundException($"Session {sessionId} not found in any project directory");
    }

    // -------------------------------------------------------------------
    // Session file lookup (shared by delete/fork)
    // -------------------------------------------------------------------

    /// <summary>Finds the path to a session's JSONL file, or <c>null</c> if not found.</summary>
    internal static string? FindSessionFile(string sessionId, string? directory)
    {
        return FindSessionFileWithDir(sessionId, directory)?.FilePath;
    }

    /// <summary>
    ///     Finds a session file and its containing project directory. The fork operation needs
    ///     the project dir to write the new file adjacent to the source.
    /// </summary>
    internal static (string FilePath, string ProjectDir)? FindSessionFileWithDir(
        string sessionId, string? directory)
    {
        var fileName = $"{sessionId}.jsonl";

        (string, string)? TryDir(string projectDir)
        {
            var path = Path.Combine(projectDir, fileName);
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 0)
                {
                    return (path, projectDir);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        if (directory is not null)
        {
            var canonical = SessionPaths.CanonicalizePath(directory);
            var projectDir = SessionPaths.FindProjectDir(canonical);
            if (projectDir is not null)
            {
                var result = TryDir(projectDir);
                if (result is not null)
                {
                    return result;
                }
            }

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
                    var result = TryDir(wtProjectDir);
                    if (result is not null)
                    {
                        return result;
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
            var result = TryDir(entry);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    // -------------------------------------------------------------------
    // Unicode sanitization
    // -------------------------------------------------------------------

    // Explicit ranges for dangerous Unicode characters: zero-width spaces/LTR-RTL marks,
    // directional formatting characters, directional isolates, BOM, and BMP private use.
    [GeneratedRegex("[​-‏‪-‮⁦-⁩﻿-]")]
    private static partial Regex UnicodeStripRegex();

    /// <summary>
    ///     Sanitizes a string by removing dangerous Unicode characters. Iteratively applies NFKC
    ///     normalization and strips format/private-use/unassigned characters until no more
    ///     changes occur (max 10 iterations).
    /// </summary>
    internal static string SanitizeUnicode(string value)
    {
        var current = value;
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var previous = current;
            current = current.Normalize(NormalizationForm.FormKC);

            var sb = new StringBuilder(current.Length);
            var i = 0;
            while (i < current.Length)
            {
                var isSurrogatePair = char.IsSurrogatePair(current, i);
                var len = isSurrogatePair ? 2 : 1;
                var category = CharUnicodeInfo.GetUnicodeCategory(current, i);
                if (category is not (UnicodeCategory.Format or UnicodeCategory.PrivateUse
                    or UnicodeCategory.OtherNotAssigned))
                {
                    sb.Append(current, i, len);
                }

                i += len;
            }

            current = sb.ToString();
            current = UnicodeStripRegex().Replace(current, "");

            if (current == previous)
            {
                break;
            }
        }

        return current;
    }

    // -------------------------------------------------------------------
    // Fork transcript parsing
    // -------------------------------------------------------------------

    private static string? GetStringProp(JsonObject obj, string key)
    {
        return obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<string>(out var s)
            ? s
            : null;
    }

    private static bool GetBoolProp(JsonObject obj, string key)
    {
        return obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<bool>(out var b) &&
               b;
    }

    /// <summary>
    ///     Parses JSONL content into transcript entries + content-replacement records. Only
    ///     keeps entries that have a uuid and are transcript message types. Content-replacement
    ///     entries (matching <paramref name="sessionId" />) are collected for re-emission in the
    ///     fork.
    /// </summary>
    internal static (List<JsonObject> Transcript, List<JsonNode?> ContentReplacements) ParseForkTranscript(
        byte[] content, string sessionId)
    {
        var transcript = new List<JsonObject>();
        var contentReplacements = new List<JsonNode?>();

        var text = Encoding.UTF8.GetString(content);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (node is not JsonObject obj)
            {
                continue;
            }

            var type = GetStringProp(obj, "type");
            if (type is not null && TranscriptTypes.Contains(type) && GetStringProp(obj, "uuid") is not null)
            {
                transcript.Add(obj);
            }
            else if (type == "content-replacement" &&
                     GetStringProp(obj, "sessionId") == sessionId &&
                     obj.TryGetPropertyValue("replacements", out var replNode) &&
                     replNode is JsonArray replArray)
            {
                foreach (var item in replArray)
                {
                    contentReplacements.Add(item?.DeepClone());
                }
            }
        }

        return (transcript, contentReplacements);
    }

    /// <summary>
    ///     Core fork transform — remaps UUIDs and produces serialized JSONL lines. Shared entry
    ///     point for the disk-backed fork implementation.
    /// </summary>
    /// <remarks>
    ///     <paramref name="deriveTitle" /> is invoked only when no explicit <paramref name="title" />
    ///     is given.
    /// </remarks>
    internal static (string ForkedSessionId, List<string> Lines) BuildForkLines(
        List<JsonObject> transcript,
        List<JsonNode?> contentReplacements,
        string sessionId,
        string? upToMessageId,
        string? title,
        Func<string?> deriveTitle)
    {
        // Filter out sidechains (subagent sessions with separate parentUuid graphs). Keep
        // isMeta entries — they're interleaved in the main chain.
        transcript = transcript.Where(e => !GetBoolProp(e, "isSidechain")).ToList();

        if (transcript.Count == 0)
        {
            throw new InvalidOperationException($"Session {sessionId} has no messages to fork");
        }

        if (upToMessageId is not null)
        {
            var cutoff = -1;
            for (var i = 0; i < transcript.Count; i++)
            {
                if (GetStringProp(transcript[i], "uuid") == upToMessageId)
                {
                    cutoff = i;
                    break;
                }
            }

            if (cutoff == -1)
            {
                throw new ArgumentException(
                    $"Message {upToMessageId} not found in session {sessionId}", nameof(upToMessageId));
            }

            transcript = transcript.Take(cutoff + 1).ToList();
        }

        // Include progress entries in the mapping — needed for parentUuid chain walk.
        var uuidMapping = new Dictionary<string, string>();
        foreach (var entry in transcript)
        {
            uuidMapping[GetStringProp(entry, "uuid")!] = Guid.NewGuid().ToString();
        }

        // Filter out progress messages from written output. They're UI-only chain links; not
        // needed in a fresh fork.
        var writable = transcript.Where(e => GetStringProp(e, "type") != "progress").ToList();
        if (writable.Count == 0)
        {
            throw new InvalidOperationException($"Session {sessionId} has no messages to fork");
        }

        var byUuid = new Dictionary<string, JsonObject>();
        foreach (var entry in transcript)
        {
            byUuid[GetStringProp(entry, "uuid")!] = entry;
        }

        var forkedSessionId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        var lines = new List<string>();

        for (var i = 0; i < writable.Count; i++)
        {
            var original = writable[i];
            var originalUuid = GetStringProp(original, "uuid")!;
            var newUuid = uuidMapping[originalUuid];

            // Resolve parentUuid, skipping progress ancestors.
            string? newParentUuid = null;
            var parentId = GetStringProp(original, "parentUuid");
            while (!string.IsNullOrEmpty(parentId))
            {
                if (!byUuid.TryGetValue(parentId, out var parent))
                {
                    break;
                }

                if (GetStringProp(parent, "type") != "progress")
                {
                    newParentUuid = uuidMapping.GetValueOrDefault(parentId);
                    break;
                }

                parentId = GetStringProp(parent, "parentUuid");
            }

            // Only update timestamp on the last message (leaf detection on resume).
            JsonNode? timestampNode;
            if (i == writable.Count - 1)
            {
                timestampNode = JsonValue.Create(now);
            }
            else if (original.TryGetPropertyValue("timestamp", out var tsNode))
            {
                timestampNode = tsNode?.DeepClone();
            }
            else
            {
                timestampNode = JsonValue.Create(now);
            }

            // Remap logicalParentUuid (compact-boundary backpointer).
            var logicalParent = GetStringProp(original, "logicalParentUuid");
            var newLogicalParent = !string.IsNullOrEmpty(logicalParent)
                ? uuidMapping.GetValueOrDefault(logicalParent)
                : logicalParent;

            var forked = (JsonObject)original.DeepClone()!;
            forked["uuid"] = newUuid;
            forked["parentUuid"] = newParentUuid;
            forked["logicalParentUuid"] = newLogicalParent;
            forked["sessionId"] = forkedSessionId;
            forked["timestamp"] = timestampNode;
            // Clear session-specific fields from the spread.
            forked["isSidechain"] = false;
            forked["forkedFrom"] = new JsonObject { ["sessionId"] = sessionId, ["messageUuid"] = originalUuid };

            // Remove fields that would leak state from the source session.
            foreach (var key in new[] { "teamName", "agentName", "slug", "sourceToolAssistantUUID" })
            {
                forked.Remove(key);
            }

            lines.Add(forked.ToJsonString());
        }

        // Append content-replacement entry (if any) with the fork's sessionId.
        if (contentReplacements.Count > 0)
        {
            var replEntry = new JsonObject
            {
                ["type"] = "content-replacement",
                ["sessionId"] = forkedSessionId,
                ["replacements"] = new JsonArray(contentReplacements.Select(r => r?.DeepClone()).ToArray()),
                ["uuid"] = Guid.NewGuid().ToString(),
                ["timestamp"] = now
            };
            lines.Add(replEntry.ToJsonString());
        }

        // Derive title: explicit > original customTitle > original aiTitle > first prompt.
        // Suffix with " (fork)" for derived titles. ListSessions reads the LAST custom-title
        // from the tail, so this entry is what surfaces.
        var forkTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : null;
        if (string.IsNullOrEmpty(forkTitle))
        {
            var derived = deriveTitle();
            forkTitle = $"{(string.IsNullOrEmpty(derived) ? "Forked session" : derived)} (fork)";
        }

        var titleEntry = new JsonObject
        {
            ["type"] = "custom-title",
            ["sessionId"] = forkedSessionId,
            ["customTitle"] = forkTitle,
            ["uuid"] = Guid.NewGuid().ToString(),
            ["timestamp"] = now
        };
        lines.Add(titleEntry.ToJsonString());

        return (forkedSessionId, lines);
    }
}
