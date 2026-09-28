using System.Text.Json.Nodes;
using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Shared fixture builders for Sessions tests — JSONL session/transcript file writers
///     mirroring the Python SDK test suite's <c>_make_session_file</c> / <c>_write_transcript</c>
///     helpers.
/// </summary>
internal static class SessionTestHelpers
{
    /// <summary>Creates a sanitized project directory under <c>&lt;configDir&gt;/projects</c>.</summary>
    internal static string MakeProjectDir(string configDir, string projectPath)
    {
        var sanitized = SessionPaths.SanitizePath(projectPath);
        var projectDir = Path.Combine(configDir, "projects", sanitized);
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }

    /// <summary>Creates a <c>.jsonl</c> session file with the given metadata.</summary>
    internal static (string SessionId, string FilePath) MakeSessionFile(
        string projectDir,
        string? sessionId = null,
        string firstPrompt = "Hello Claude",
        string? summary = null,
        string? customTitle = null,
        string? gitBranch = null,
        string? cwd = null,
        bool isSidechain = false,
        bool isMetaOnly = false,
        DateTime? mtimeUtc = null)
    {
        var sid = sessionId ?? Guid.NewGuid().ToString();
        var filePath = Path.Combine(projectDir, $"{sid}.jsonl");

        var firstEntry = new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = firstPrompt }
        };
        if (cwd is not null)
        {
            firstEntry["cwd"] = cwd;
        }

        if (gitBranch is not null)
        {
            firstEntry["gitBranch"] = gitBranch;
        }

        if (isSidechain)
        {
            firstEntry["isSidechain"] = true;
        }

        if (isMetaOnly)
        {
            firstEntry["isMeta"] = true;
        }

        var assistantEntry = new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "Hi there!" }
        };

        var tailEntry = new JsonObject { ["type"] = "summary" };
        if (summary is not null)
        {
            tailEntry["summary"] = summary;
        }

        if (customTitle is not null)
        {
            tailEntry["customTitle"] = customTitle;
        }

        if (gitBranch is not null)
        {
            tailEntry["gitBranch"] = gitBranch;
        }

        var lines = new[] { firstEntry.ToJsonString(), assistantEntry.ToJsonString(), tailEntry.ToJsonString() };
        File.WriteAllText(filePath, string.Join("\n", lines) + "\n");

        if (mtimeUtc is not null)
        {
            File.SetLastWriteTimeUtc(filePath, mtimeUtc.Value);
        }

        return (sid, filePath);
    }

    /// <summary>Builds a transcript entry dict matching the CLI's JSONL format.</summary>
    internal static JsonObject MakeTranscriptEntry(
        string entryType,
        string entryUuid,
        string? parentUuid,
        string sessionId,
        string? content = null,
        IReadOnlyDictionary<string, object?>? extras = null)
    {
        var entry = new JsonObject
        {
            ["type"] = entryType,
            ["uuid"] = entryUuid,
            ["parentUuid"] = parentUuid,
            ["sessionId"] = sessionId
        };

        if (content is not null)
        {
            var role = entryType is "user" or "assistant" ? entryType : "user";
            entry["message"] = new JsonObject { ["role"] = role, ["content"] = content };
        }

        if (extras is not null)
        {
            foreach (var (key, value) in extras)
            {
                entry[key] = value switch
                {
                    null => null,
                    bool b => JsonValue.Create(b),
                    string s => JsonValue.Create(s),
                    JsonNode n => n,
                    _ => throw new ArgumentException($"Unsupported extra value type for '{key}'")
                };
            }
        }

        return entry;
    }

    /// <summary>Writes a JSONL transcript file from a set of entries.</summary>
    internal static string WriteTranscript(string projectDir, string sessionId, IEnumerable<JsonObject> entries)
    {
        var filePath = Path.Combine(projectDir, $"{sessionId}.jsonl");
        var lines = entries.Select(e => e.ToJsonString());
        File.WriteAllText(filePath, string.Join("\n", lines) + "\n");
        return filePath;
    }
}
