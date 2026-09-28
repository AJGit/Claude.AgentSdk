using System.Text.Json;
using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for the session mutation functions (rename, tag, delete, fork) and the low-level
///     <c>TryAppend</c> helper. Ported from the Python SDK's <c>TestTryAppend</c>,
///     <c>TestRenameSession</c>, <c>TestTagSession</c>, <c>TestDeleteSession</c>, and
///     <c>TestForkSession</c> test classes.
/// </summary>
[UnitTest]
[Collection(SessionsTestCollection.Name)]
public sealed class SessionMutationsTests : IDisposable
{
    private readonly ClaudeConfigDirFixture _config = new();

    public void Dispose()
    {
        _config.Dispose();
    }

    private static string MakeProjectPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"), "proj");
        Directory.CreateDirectory(path);
        return path;
    }

    private static (string SessionId, string FilePath) MakeSimpleSessionFile(
        string projectDir, string? sessionId = null, string firstPrompt = "Hello Claude")
    {
        var sid = sessionId ?? Guid.NewGuid().ToString();
        var filePath = Path.Combine(projectDir, $"{sid}.jsonl");
        var lines = new[]
        {
            $"{{\"type\":\"user\",\"message\":{{\"role\":\"user\",\"content\":\"{firstPrompt}\"}}}}",
            "{\"type\":\"assistant\",\"message\":{\"role\":\"assistant\",\"content\":\"Hi!\"}}"
        };
        File.WriteAllText(filePath, string.Join("\n", lines) + "\n");
        return (sid, filePath);
    }

    // -------------------------------------------------------------------
    // TryAppend
    // -------------------------------------------------------------------

    [Fact]
    public void TryAppend_ToExistingFile_Appends()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "test.jsonl");
        File.WriteAllText(f, "line1\n");

        var result = SessionMutationHelpers.TryAppend(f, "line2\n");
        Assert.True(result);
        Assert.Equal("line1\nline2\n", File.ReadAllText(f));
    }

    [Fact]
    public void TryAppend_MissingFile_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "nonexistent.jsonl");

        Assert.False(SessionMutationHelpers.TryAppend(f, "data\n"));
        Assert.False(File.Exists(f));
    }

    [Fact]
    public void TryAppend_MissingParentDir_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        var f = Path.Combine(dir, "nonexistent", "file.jsonl");

        Assert.False(SessionMutationHelpers.TryAppend(f, "data\n"));
    }

    [Fact]
    public void TryAppend_ZeroByteFile_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "stub.jsonl");
        File.WriteAllText(f, "");

        Assert.False(SessionMutationHelpers.TryAppend(f, "data\n"));
        Assert.Equal("", File.ReadAllText(f));
    }

    [Fact]
    public void TryAppend_MultipleAppends_LandInOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "test.jsonl");
        File.WriteAllText(f, "line1\n");

        SessionMutationHelpers.TryAppend(f, "line2\n");
        SessionMutationHelpers.TryAppend(f, "line3\n");
        Assert.Equal("line1\nline2\nline3\n", File.ReadAllText(f));
    }

    // -------------------------------------------------------------------
    // RenameSession
    // -------------------------------------------------------------------

    [Fact]
    public void RenameSession_InvalidSessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaudeSessions.RenameSession("not-a-uuid", "title"));
        Assert.Throws<ArgumentException>(() => ClaudeSessions.RenameSession("", "title"));
    }

    [Fact]
    public void RenameSession_EmptyTitle_Throws()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = MakeSimpleSessionFile(projectDir);

        Assert.Throws<ArgumentException>(() => ClaudeSessions.RenameSession(sid, "", projectPath));
        Assert.Throws<ArgumentException>(() => ClaudeSessions.RenameSession(sid, "   ", projectPath));
        Assert.Throws<ArgumentException>(() => ClaudeSessions.RenameSession(sid, "\n\t", projectPath));
    }

    [Fact]
    public void RenameSession_SessionNotFound_Throws()
    {
        var projectPath = MakeProjectPath();
        SessionTestHelpers.MakeProjectDir(_config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));

        Assert.Throws<FileNotFoundException>(
            () => ClaudeSessions.RenameSession(Guid.NewGuid().ToString(), "title", projectPath));
    }

    [Fact]
    public void RenameSession_NoProjectsDir_Throws()
    {
        Environment.SetEnvironmentVariable(
            "CLAUDE_CONFIG_DIR", Path.Combine(_config.ConfigDir, "nonexistent-" + Guid.NewGuid().ToString("N")));
        var ex = Assert.Throws<FileNotFoundException>(
            () => ClaudeSessions.RenameSession(Guid.NewGuid().ToString(), "title"));
        Assert.Contains("no projects directory", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameSession_AppendsCustomTitleEntry()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.RenameSession(sid, "My New Title", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("custom-title", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("My New Title", doc.RootElement.GetProperty("customTitle").GetString());
        Assert.Equal(sid, doc.RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public void RenameSession_TitleTrimmedBeforeStoring()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.RenameSession(sid, "  Trimmed Title  ", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("Trimmed Title", doc.RootElement.GetProperty("customTitle").GetString());
    }

    [Fact]
    public void RenameSession_LastWins_ViaListSessions()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = MakeSimpleSessionFile(projectDir, firstPrompt: "original");

        ClaudeSessions.RenameSession(sid, "First Title", projectPath);
        ClaudeSessions.RenameSession(sid, "Second Title", projectPath);
        ClaudeSessions.RenameSession(sid, "Final Title", projectPath);

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("Final Title", s.CustomTitle);
        Assert.Equal("Final Title", s.Summary);
    }

    [Fact]
    public void RenameSession_SearchesAllProjects()
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/some/project");
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.RenameSession(sid, "Found Without Dir");

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("Found Without Dir", doc.RootElement.GetProperty("customTitle").GetString());
    }

    [Fact]
    public void RenameSession_SkipsZeroByteStub()
    {
        var projA = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/aaa/project");
        var projZ = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/zzz/project");

        var sid = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(projA, $"{sid}.jsonl"), "");
        MakeSimpleSessionFile(projZ, sid, "real");

        ClaudeSessions.RenameSession(sid, "New Title");

        Assert.Equal("", File.ReadAllText(Path.Combine(projA, $"{sid}.jsonl")));
        var realContent = File.ReadAllText(Path.Combine(projZ, $"{sid}.jsonl"));
        Assert.Contains("\"customTitle\":\"New Title\"", realContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameSession_CompactJsonFormat()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.RenameSession(sid, "Title", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        Assert.Equal($"{{\"type\":\"custom-title\",\"customTitle\":\"Title\",\"sessionId\":\"{sid}\"}}", lastLine);
    }

    // -------------------------------------------------------------------
    // TagSession
    // -------------------------------------------------------------------

    [Fact]
    public void TagSession_InvalidSessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaudeSessions.TagSession("not-a-uuid", "tag"));
        Assert.Throws<ArgumentException>(() => ClaudeSessions.TagSession("", "tag"));
    }

    [Fact]
    public void TagSession_EmptyTag_Throws()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = MakeSimpleSessionFile(projectDir);

        Assert.Throws<ArgumentException>(() => ClaudeSessions.TagSession(sid, "", projectPath));
        Assert.Throws<ArgumentException>(() => ClaudeSessions.TagSession(sid, "   ", projectPath));
    }

    [Fact]
    public void TagSession_SessionNotFound_Throws()
    {
        var projectPath = MakeProjectPath();
        SessionTestHelpers.MakeProjectDir(_config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));

        Assert.Throws<FileNotFoundException>(
            () => ClaudeSessions.TagSession(Guid.NewGuid().ToString(), "tag", projectPath));
    }

    [Fact]
    public void TagSession_AppendsTagEntry()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.TagSession(sid, "experiment", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("tag", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("experiment", doc.RootElement.GetProperty("tag").GetString());
        Assert.Equal(sid, doc.RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public void TagSession_TagTrimmed()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.TagSession(sid, "  my-tag  ", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("my-tag", doc.RootElement.GetProperty("tag").GetString());
    }

    [Fact]
    public void TagSession_NullClearsTag()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.TagSession(sid, "original-tag", projectPath);
        ClaudeSessions.TagSession(sid, null, projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("tag", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("", doc.RootElement.GetProperty("tag").GetString());
        Assert.Equal(sid, doc.RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public void TagSession_LastWins()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.TagSession(sid, "first", projectPath);
        ClaudeSessions.TagSession(sid, "second", projectPath);
        ClaudeSessions.TagSession(sid, "third", projectPath);

        var allLines = File.ReadAllText(filePath).Trim().Split('\n');
        var lastLine = allLines[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("third", doc.RootElement.GetProperty("tag").GetString());

        var tagLines = allLines
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "tag")
            .ToList();
        Assert.Equal(3, tagLines.Count);
    }

    [Fact]
    public void TagSession_CompactJsonFormat()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        ClaudeSessions.TagSession(sid, "mytag", projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        Assert.Equal($"{{\"type\":\"tag\",\"tag\":\"mytag\",\"sessionId\":\"{sid}\"}}", lastLine);
    }

    [Fact]
    public void TagSession_UnicodeSanitization()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);

        var dirtyTag = "clean​tag﻿";
        ClaudeSessions.TagSession(sid, dirtyTag, projectPath);

        var lastLine = File.ReadAllText(filePath).Trim().Split('\n')[^1];
        using var doc = JsonDocument.Parse(lastLine);
        Assert.Equal("cleantag", doc.RootElement.GetProperty("tag").GetString());
    }

    [Fact]
    public void TagSession_SanitizationRejectsPureInvisible()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = MakeSimpleSessionFile(projectDir);

        Assert.Throws<ArgumentException>(
            () => ClaudeSessions.TagSession(sid, "​‌﻿", projectPath));
    }

    // -------------------------------------------------------------------
    // DeleteSession
    // -------------------------------------------------------------------

    [Fact]
    public void DeleteSession_InvalidSessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaudeSessions.DeleteSession("not-a-uuid"));
    }

    [Fact]
    public void DeleteSession_SessionNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ClaudeSessions.DeleteSession(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void DeleteSession_DeletesSessionFile()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);
        Assert.True(File.Exists(filePath));

        ClaudeSessions.DeleteSession(sid, projectPath);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void DeleteSession_RemovesSubagentTranscriptDir()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);
        var subagentDir = Path.Combine(projectDir, sid);
        Directory.CreateDirectory(subagentDir);
        File.WriteAllText(Path.Combine(subagentDir, $"{Guid.NewGuid()}.jsonl"), "{}\n");

        ClaudeSessions.DeleteSession(sid, projectPath);

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(subagentDir));
    }

    [Fact]
    public void DeleteSession_WithoutDirectory_SearchesAllProjects()
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/any/project");
        var (sid, filePath) = MakeSimpleSessionFile(projectDir);
        Assert.True(File.Exists(filePath));

        ClaudeSessions.DeleteSession(sid);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void DeleteSession_NoLongerInListSessions()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = MakeSimpleSessionFile(projectDir);

        Assert.Contains(ClaudeSessions.ListSessions(projectPath), s => s.SessionId == sid);
        ClaudeSessions.DeleteSession(sid, projectPath);
        Assert.DoesNotContain(ClaudeSessions.ListSessions(projectPath), s => s.SessionId == sid);
    }

    // -------------------------------------------------------------------
    // ForkSession
    // -------------------------------------------------------------------

    private static (string SessionId, string FilePath, List<string> Uuids) MakeTranscriptSession(
        string projectDir, string? sessionId = null, int numTurns = 2)
    {
        var sid = sessionId ?? Guid.NewGuid().ToString();
        var filePath = Path.Combine(projectDir, $"{sid}.jsonl");
        var uuids = new List<string>();
        var lines = new List<string>();
        string? parentUuid = null;

        for (var i = 0; i < numTurns; i++)
        {
            var userUuid = Guid.NewGuid().ToString();
            uuids.Add(userUuid);
            lines.Add(
                $"{{\"type\":\"user\",\"uuid\":\"{userUuid}\",\"parentUuid\":{(parentUuid is null ? "null" : $"\"{parentUuid}\"")}," +
                $"\"sessionId\":\"{sid}\",\"timestamp\":\"2026-03-01T00:00:00Z\"," +
                $"\"message\":{{\"role\":\"user\",\"content\":\"Turn {i + 1} question\"}}}}");
            parentUuid = userUuid;

            var asstUuid = Guid.NewGuid().ToString();
            uuids.Add(asstUuid);
            lines.Add(
                $"{{\"type\":\"assistant\",\"uuid\":\"{asstUuid}\",\"parentUuid\":\"{parentUuid}\"," +
                $"\"sessionId\":\"{sid}\",\"timestamp\":\"2026-03-01T00:00:00Z\"," +
                $"\"message\":{{\"role\":\"assistant\",\"content\":[{{\"type\":\"text\",\"text\":\"Turn {i + 1} answer\"}}]}}}}");
            parentUuid = asstUuid;
        }

        File.WriteAllText(filePath, string.Join("\n", lines) + "\n");
        return (sid, filePath, uuids);
    }

    [Fact]
    public void ForkSession_InvalidSessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaudeSessions.ForkSession("not-a-uuid"));
    }

    [Fact]
    public void ForkSession_SessionNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ClaudeSessions.ForkSession(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void ForkSession_InvalidUpToMessageId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => ClaudeSessions.ForkSession(Guid.NewGuid().ToString(), upToMessageId: "not-valid"));
    }

    [Fact]
    public void ForkSession_CreatesNewSession()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath);
        Assert.NotEqual(sid, result.SessionId);
        Assert.True(File.Exists(Path.Combine(projectDir, $"{result.SessionId}.jsonl")));
    }

    [Fact]
    public void ForkSession_RemapsUuids()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, originalUuids) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath);
        var forkPath = Path.Combine(projectDir, $"{result.SessionId}.jsonl");

        foreach (var line in File.ReadAllText(forkPath).Trim().Split('\n'))
        {
            using var doc = JsonDocument.Parse(line);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type is "user" or "assistant")
            {
                Assert.DoesNotContain(doc.RootElement.GetProperty("uuid").GetString(), originalUuids);
                var parentUuid = doc.RootElement.GetProperty("parentUuid");
                if (parentUuid.ValueKind != JsonValueKind.Null)
                {
                    Assert.DoesNotContain(parentUuid.GetString(), originalUuids);
                }
            }
        }
    }

    [Fact]
    public void ForkSession_PreservesMessageCount()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir, numTurns: 3);

        var result = ClaudeSessions.ForkSession(sid, projectPath);

        var originalMsgs = ClaudeSessions.GetSessionMessages(sid, projectPath);
        var forkMsgs = ClaudeSessions.GetSessionMessages(result.SessionId, projectPath);
        Assert.Equal(originalMsgs.Count, forkMsgs.Count);
    }

    [Fact]
    public void ForkSession_UpToMessageId_SlicesInclusive()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, uuids) = MakeTranscriptSession(projectDir, numTurns: 3);

        var cutoffUuid = uuids[1];
        var result = ClaudeSessions.ForkSession(sid, projectPath, upToMessageId: cutoffUuid);

        var forkMsgs = ClaudeSessions.GetSessionMessages(result.SessionId, projectPath);
        Assert.Equal(2, forkMsgs.Count);
    }

    [Fact]
    public void ForkSession_UpToMessageIdNotFound_Throws()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var ex = Assert.Throws<ArgumentException>(
            () => ClaudeSessions.ForkSession(sid, projectPath, upToMessageId: Guid.NewGuid().ToString()));
        Assert.Contains("not found in session", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForkSession_CustomTitle_IsUsed()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath, title: "My Fork");

        var sessions = ClaudeSessions.ListSessions(projectPath);
        var forkInfo = sessions.Single(s => s.SessionId == result.SessionId);
        Assert.Equal("My Fork", forkInfo.CustomTitle);
    }

    [Fact]
    public void ForkSession_DefaultTitle_HasForkSuffix()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath);

        var sessions = ClaudeSessions.ListSessions(projectPath);
        var forkInfo = sessions.Single(s => s.SessionId == result.SessionId);
        Assert.NotNull(forkInfo.CustomTitle);
        Assert.EndsWith("(fork)", forkInfo.CustomTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void ForkSession_SessionIdStampedOnEveryEntry()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath);
        var forkPath = Path.Combine(projectDir, $"{result.SessionId}.jsonl");

        foreach (var line in File.ReadAllText(forkPath).Trim().Split('\n'))
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal(result.SessionId, doc.RootElement.GetProperty("sessionId").GetString());
        }
    }

    [Fact]
    public void ForkSession_ForkedFromField_PointsToSource()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid, projectPath);
        var forkPath = Path.Combine(projectDir, $"{result.SessionId}.jsonl");

        foreach (var line in File.ReadAllText(forkPath).Trim().Split('\n'))
        {
            using var doc = JsonDocument.Parse(line);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type is "user" or "assistant")
            {
                Assert.Equal(sid, doc.RootElement.GetProperty("forkedFrom").GetProperty("sessionId").GetString());
            }
        }
    }

    [Fact]
    public void ForkSession_WithoutDirectory_SearchesAllProjects()
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/any/project");
        var (sid, _, _) = MakeTranscriptSession(projectDir);

        var result = ClaudeSessions.ForkSession(sid);
        Assert.True(File.Exists(Path.Combine(projectDir, $"{result.SessionId}.jsonl")));
    }

    [Fact]
    public void ForkSession_ClearsStaleFields()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var entry =
            $"{{\"type\":\"user\",\"uuid\":\"{Guid.NewGuid()}\",\"parentUuid\":null,\"sessionId\":\"{sid}\"," +
            "\"timestamp\":\"2026-03-01T00:00:00Z\",\"teamName\":\"test-team\",\"agentName\":\"test-agent\"," +
            "\"slug\":\"test-slug\",\"message\":{\"role\":\"user\",\"content\":\"Hello\"}}";
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), entry + "\n");

        var result = ClaudeSessions.ForkSession(sid, projectPath);
        var forkPath = Path.Combine(projectDir, $"{result.SessionId}.jsonl");

        foreach (var line in File.ReadAllText(forkPath).Trim().Split('\n'))
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.GetProperty("type").GetString() == "user")
            {
                Assert.False(doc.RootElement.TryGetProperty("teamName", out _));
                Assert.False(doc.RootElement.TryGetProperty("agentName", out _));
            }
        }
    }
}
