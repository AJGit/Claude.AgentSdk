using System.Globalization;
using System.Text.Json.Nodes;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Incremental session-summary derivation for <see cref="ISessionStore" /> adapters.
/// </summary>
/// <remarks>
///     <see cref="Fold" /> lets a store maintain a per-session <see cref="SessionSummaryEntry" /> sidecar inside
///     <see cref="ISessionStore.AppendAsync" /> so <see cref="ClaudeSessions.ListSessionsFromStoreAsync" /> can fetch
///     all metadata with one <see cref="ISessionStoreSummaries.ListSessionSummariesAsync" /> call instead of one load
///     per session. Every derived field is set-once or last-wins, so previously appended entries are never re-read.
///     The <c>Data</c> keys match the Python and TypeScript SDKs, so a store can be shared across them.
/// </remarks>
public static class SessionSummary
{
    // JSONL entry key -> summary data key, for last-wins string fields.
    private static readonly (string Source, string Target)[] _lastWinsFields =
    [
        ("customTitle", "custom_title"),
        ("aiTitle", "ai_title"),
        ("lastPrompt", "last_prompt"),
        ("summary", "summary_hint"),
        ("gitBranch", "git_branch")
    ];

    /// <summary>
    ///     Folds a batch of appended entries into the running summary for <paramref name="key" />.
    /// </summary>
    /// <remarks>
    ///     Do not call this for keys with a subpath: subagent transcripts must not contribute to the main session's
    ///     summary. <see cref="SessionSummaryEntry.Mtime" /> is not touched (a new session gets 0 as a placeholder);
    ///     the adapter stamps it with its storage write time after persisting.
    /// </remarks>
    /// <param name="previous">The previous summary for the same key, or null for the first append.</param>
    /// <param name="key">The session key.</param>
    /// <param name="entries">The appended entries.</param>
    public static SessionSummaryEntry Fold(SessionSummaryEntry? previous, SessionKey key,
        IReadOnlyList<JsonElement> entries)
    {
        var data = previous is null ? new JsonObject() : (JsonObject)previous.Data.DeepClone();

        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var ms = entry.TryGetProperty("timestamp", out var timestamp) ? IsoToEpochMs(timestamp) : null;

            if (!data.ContainsKey("is_sidechain"))
            {
                data["is_sidechain"] = entry.TryGetProperty("isSidechain", out var sidechain) &&
                                       sidechain.ValueKind == JsonValueKind.True;
            }

            if (!data.ContainsKey("created_at") && ms is not null)
            {
                data["created_at"] = ms.Value;
            }

            if (!data.ContainsKey("cwd") && GetString(entry, "cwd") is { Length: > 0 } cwd)
            {
                data["cwd"] = cwd;
            }

            FoldFirstPrompt(data, entry);

            foreach (var (source, target) in _lastWinsFields)
            {
                if (GetString(entry, source) is { } value)
                {
                    data[target] = value;
                }
            }

            if (GetString(entry, "type") == "tag")
            {
                if (GetString(entry, "tag") is { Length: > 0 } tag)
                {
                    data["tag"] = tag;
                }
                else
                {
                    // An empty or absent tag clears it.
                    data.Remove("tag");
                }
            }
        }

        return new SessionSummaryEntry(previous?.SessionId ?? key.SessionId, previous?.Mtime ?? 0, data);
    }

    /// <summary>
    ///     Converts a summary to <see cref="SdkSessionInfo" />; null for sidechain sessions or sessions with no
    ///     extractable summary (matching the local-disk listing).
    /// </summary>
    internal static SdkSessionInfo? ToSdkSessionInfo(SessionSummaryEntry entry, string? projectPath)
    {
        var data = entry.Data;
        if (GetBool(data, "is_sidechain"))
        {
            return null;
        }

        var firstPrompt = NullIfEmpty(GetBool(data, "first_prompt_locked")
            ? GetString(data, "first_prompt")
            : GetString(data, "command_fallback"));
        var customTitle = NullIfEmpty(GetString(data, "custom_title")) ?? NullIfEmpty(GetString(data, "ai_title"));
        var summary = customTitle ??
                      NullIfEmpty(GetString(data, "last_prompt")) ??
                      NullIfEmpty(GetString(data, "summary_hint")) ??
                      firstPrompt;
        if (summary is null)
        {
            return null;
        }

        return new SdkSessionInfo(entry.SessionId, summary, entry.Mtime)
        {
            // A JSONL byte count only means something for local disk; stores have no equivalent.
            FileSize = null,
            CustomTitle = customTitle,
            FirstPrompt = firstPrompt,
            GitBranch = NullIfEmpty(GetString(data, "git_branch")),
            Cwd = NullIfEmpty(GetString(data, "cwd")) ?? NullIfEmpty(projectPath),
            Tag = NullIfEmpty(GetString(data, "tag")),
            CreatedAt = data.TryGetPropertyValue("created_at", out var created) && created is JsonValue v &&
                        v.TryGetValue<long>(out var createdAt)
                ? createdAt
                : null
        };
    }

    internal static long? IsoToEpochMs(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : null;
    }

    /// <summary>
    ///     Mirrors the disk head scan for the first meaningful user prompt, over a single parsed entry.
    /// </summary>
    private static void FoldFirstPrompt(JsonObject data, JsonElement entry)
    {
        if (GetBool(data, "first_prompt_locked") || GetString(entry, "type") != "user")
        {
            return;
        }

        if (IsTrue(entry, "isMeta") || IsTrue(entry, "isCompactSummary"))
        {
            return;
        }

        if (!entry.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var texts = new List<string>();
        if (message.TryGetProperty("content", out var content))
        {
            if (content.ValueKind == JsonValueKind.String)
            {
                texts.Add(content.GetString()!);
            }
            else if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var blockType = GetString(block, "type");
                    if (blockType == "tool_result")
                    {
                        // Tool-result carrying user messages never provide the first prompt.
                        return;
                    }

                    if (blockType == "text" && GetString(block, "text") is { } text)
                    {
                        texts.Add(text);
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

            var commandMatch = TranscriptReader.CommandNameRegex().Match(result);
            if (commandMatch.Success)
            {
                if (string.IsNullOrEmpty(GetString(data, "command_fallback")))
                {
                    data["command_fallback"] = commandMatch.Groups[1].Value;
                }

                continue;
            }

            if (TranscriptReader.SkipFirstPromptPattern().IsMatch(result))
            {
                continue;
            }

            if (result.Length > 200)
            {
                result = result[..200].TrimEnd() + "…";
            }

            data["first_prompt"] = result;
            data["first_prompt_locked"] = true;
            return;
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool IsTrue(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static string? GetString(JsonObject data, string property)
    {
        return data.TryGetPropertyValue(property, out var node) && node is JsonValue value &&
               value.TryGetValue<string>(out var s)
            ? s
            : null;
    }

    private static bool GetBool(JsonObject data, string property)
    {
        return data.TryGetPropertyValue(property, out var node) && node is JsonValue value &&
               value.TryGetValue<bool>(out var b) && b;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
