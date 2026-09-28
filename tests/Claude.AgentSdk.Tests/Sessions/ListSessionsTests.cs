using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for <see cref="ClaudeSessions.ListSessions" />. Ported from the Python SDK's
///     <c>TestListSessions</c>, <c>TestSDKSessionInfoType</c>, <c>TestTagExtraction</c>, and
///     <c>TestCreatedAtExtraction</c> test classes.
/// </summary>
[UnitTest]
[Collection(SessionsTestCollection.Name)]
public sealed class ListSessionsTests : IDisposable
{
    private readonly ClaudeConfigDirFixture _config = new();

    public void Dispose()
    {
        _config.Dispose();
    }

    private static string MakeProjectPath(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"), suffix);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void EmptyProjectsDir_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.ListSessions());
    }

    [Fact]
    public void NoConfigDir_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable(
            "CLAUDE_CONFIG_DIR", Path.Combine(_config.ConfigDir, "nonexistent-" + Guid.NewGuid().ToString("N")));
        Assert.Empty(ClaudeSessions.ListSessions());
    }

    [Fact]
    public void SingleSession_BasicMetadata()
    {
        var projectPath = MakeProjectPath("my-project");
        var canonical = SessionPaths.CanonicalizePath(projectPath);
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, canonical);
        var (sid, _) = SessionTestHelpers.MakeSessionFile(
            projectDir, firstPrompt: "What is 2+2?", gitBranch: "main", cwd: projectPath);

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal(sid, s.SessionId);
        Assert.Equal("What is 2+2?", s.FirstPrompt);
        Assert.Equal("What is 2+2?", s.Summary);
        Assert.Equal("main", s.GitBranch);
        Assert.Equal(projectPath, s.Cwd);
        Assert.True(s.FileSize > 0);
        Assert.True(s.LastModified > 0);
        Assert.Null(s.CustomTitle);
    }

    [Fact]
    public void CustomTitle_WinsOverSummaryAndFirstPrompt()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(
            projectDir, firstPrompt: "original question", summary: "auto summary",
            customTitle: "My Custom Title");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("My Custom Title", s.Summary);
        Assert.Equal("My Custom Title", s.CustomTitle);
        Assert.Equal("original question", s.FirstPrompt);
    }

    [Fact]
    public void Summary_WinsOverFirstPrompt_WhenNoCustomTitle()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "question", summary: "better summary");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("better summary", s.Summary);
        Assert.Null(s.CustomTitle);
    }

    [Fact]
    public void MultipleSessions_SortedByMtimeDescending()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));

        var epoch = DateTime.UnixEpoch;
        var (sidOld, _) = SessionTestHelpers.MakeSessionFile(
            projectDir, firstPrompt: "old", mtimeUtc: epoch.AddSeconds(1000));
        var (sidNew, _) = SessionTestHelpers.MakeSessionFile(
            projectDir, firstPrompt: "new", mtimeUtc: epoch.AddSeconds(3000));
        var (sidMid, _) = SessionTestHelpers.MakeSessionFile(
            projectDir, firstPrompt: "mid", mtimeUtc: epoch.AddSeconds(2000));

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal(3, sessions.Count);
        Assert.Equal([sidNew, sidMid, sidOld], sessions.Select(s => s.SessionId));
        Assert.Equal(3_000_000, sessions[0].LastModified);
        Assert.Equal(2_000_000, sessions[1].LastModified);
        Assert.Equal(1_000_000, sessions[2].LastModified);
    }

    [Fact]
    public void Limit_RestrictsResultCount()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var epoch = DateTime.UnixEpoch;
        for (var i = 0; i < 5; i++)
        {
            SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: $"prompt {i}", mtimeUtc: epoch.AddSeconds(1000 + i));
        }

        var sessions = ClaudeSessions.ListSessions(projectPath, limit: 2, includeWorktrees: false);
        Assert.Equal(2, sessions.Count);
        Assert.True(sessions[0].LastModified >= sessions[1].LastModified);
    }

    [Fact]
    public void Offset_SkipsForPagination()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var epoch = DateTime.UnixEpoch;
        for (var i = 0; i < 5; i++)
        {
            SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: $"prompt {i}", mtimeUtc: epoch.AddSeconds(1000 + i));
        }

        var page1 = ClaudeSessions.ListSessions(projectPath, limit: 2, offset: 0, includeWorktrees: false);
        Assert.Equal(2, page1.Count);

        var page2 = ClaudeSessions.ListSessions(projectPath, limit: 2, offset: 2, includeWorktrees: false);
        Assert.Equal(2, page2.Count);

        var page1Ids = page1.Select(s => s.SessionId).ToHashSet();
        var page2Ids = page2.Select(s => s.SessionId).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));

        Assert.True(page1[0].LastModified > page2[0].LastModified);

        var pageEmpty = ClaudeSessions.ListSessions(projectPath, offset: 100, includeWorktrees: false);
        Assert.Empty(pageEmpty);
    }

    [Fact]
    public void FiltersSidechainSessions()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "normal");
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "sidechain", isSidechain: true);

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("normal", s.FirstPrompt);
    }

    [Fact]
    public void FiltersEmptySessions()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "ignored meta", isMetaOnly: true);
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "real content");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("real content", s.FirstPrompt);
    }

    [Fact]
    public void FiltersNonUuidFilenames()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        File.WriteAllText(
            Path.Combine(projectDir, "not-a-uuid.jsonl"),
            "{\"type\":\"user\",\"message\":{\"content\":\"x\"}}\n");
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "valid session");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("valid session", s.FirstPrompt);
    }

    [Fact]
    public void IgnoresNonJsonlFiles()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        File.WriteAllText(Path.Combine(projectDir, "README.md"), "not a session");
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "session");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Single(sessions);
    }

    [Fact]
    public void ListAllSessions_NoDirectory_ListsAcrossProjects()
    {
        var proj1 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/some/path/one");
        var proj2 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/some/path/two");
        var epoch = DateTime.UnixEpoch;
        SessionTestHelpers.MakeSessionFile(proj1, firstPrompt: "from proj1", mtimeUtc: epoch.AddSeconds(1000));
        SessionTestHelpers.MakeSessionFile(proj2, firstPrompt: "from proj2", mtimeUtc: epoch.AddSeconds(2000));

        var sessions = ClaudeSessions.ListSessions();
        Assert.Equal(2, sessions.Count);
        Assert.Equal("from proj2", sessions[0].FirstPrompt);
        Assert.Equal("from proj1", sessions[1].FirstPrompt);
    }

    [Fact]
    public void ListAllSessions_DedupesBySessionId_KeepsNewest()
    {
        var proj1 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/path/one");
        var proj2 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/path/two");
        var sharedSid = Guid.NewGuid().ToString();
        var epoch = DateTime.UnixEpoch;
        SessionTestHelpers.MakeSessionFile(
            proj1, sharedSid, firstPrompt: "older", mtimeUtc: epoch.AddSeconds(1000));
        SessionTestHelpers.MakeSessionFile(
            proj2, sharedSid, firstPrompt: "newer", mtimeUtc: epoch.AddSeconds(2000));

        var sessions = ClaudeSessions.ListSessions();
        var s = Assert.Single(sessions);
        Assert.Equal("newer", s.FirstPrompt);
        Assert.Equal(2_000_000, s.LastModified);
    }

    [Fact]
    public void NonexistentProjectDir_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath("never-used");
        Assert.Empty(ClaudeSessions.ListSessions(projectPath, includeWorktrees: false));
    }

    [Fact]
    public void EmptyFile_IsFiltered()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), "");

        Assert.Empty(ClaudeSessions.ListSessions(projectPath, includeWorktrees: false));
    }

    [Fact]
    public void IncludeWorktreesDisabled_OnlyScansGivenDirectory()
    {
        var projectPath = MakeProjectPath("main-proj");
        var canonical = SessionPaths.CanonicalizePath(projectPath);

        var mainDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, canonical);
        SessionTestHelpers.MakeSessionFile(mainDir, firstPrompt: "main session");

        var otherDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, canonical + "-worktree");
        SessionTestHelpers.MakeSessionFile(otherDir, firstPrompt: "worktree session");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("main session", s.FirstPrompt);
    }

    [Fact]
    public void LimitZero_ReturnsAll()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        for (var i = 0; i < 3; i++)
        {
            SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: $"p{i}");
        }

        var sessions = ClaudeSessions.ListSessions(projectPath, limit: 0, includeWorktrees: false);
        Assert.Equal(3, sessions.Count);
    }

    [Fact]
    public void Cwd_FallsBackToProjectPath_WhenNotInHead()
    {
        var projectPath = MakeProjectPath("proj");
        var canonical = SessionPaths.CanonicalizePath(projectPath);
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, canonical);
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "no cwd field");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal(canonical, s.Cwd);
    }

    [Fact]
    public void GitBranch_FromTail_PreferredOverHead()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"gitBranch\":\"old-branch\"}",
            "{\"type\":\"summary\",\"gitBranch\":\"new-branch\"}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.Equal("new-branch", s.GitBranch);
    }

    // -------------------------------------------------------------------
    // Tag extraction
    // -------------------------------------------------------------------

    [Fact]
    public void Tag_ExtractedFromTail()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"my-tag\",\"sessionId\":\"{sid}\"}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal("my-tag", Assert.Single(sessions).Tag);
    }

    [Fact]
    public void Tag_LastWins()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"first-tag\",\"sessionId\":\"{sid}\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"second-tag\",\"sessionId\":\"{sid}\"}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal("second-tag", Assert.Single(sessions).Tag);
    }

    [Fact]
    public void Tag_EmptyStringIsNone()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"old-tag\",\"sessionId\":\"{sid}\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"\",\"sessionId\":\"{sid}\"}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Null(Assert.Single(sessions).Tag);
    }

    [Fact]
    public void Tag_AbsentIsNull()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "hello");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Null(Assert.Single(sessions).Tag);
    }

    [Fact]
    public void Tag_IgnoresToolUseInputs()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"tag this v1.0\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"real-tag\",\"sessionId\":\"{sid}\"}}",
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"mcp__docker__build\",\"input\":{\"tag\":\"myapp:v2\",\"context\":\".\"}}]}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal("real-tag", Assert.Single(sessions).Tag);
    }

    [Fact]
    public void Tag_NoneWhenOnlyToolUseTag()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"build docker\"}}",
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"input\":{\"tag\":\"prod\"}}]}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Null(Assert.Single(sessions).Tag);
    }

    [Fact]
    public void ParseSessionInfoFromLite_DirectHelperTest()
    {
        var sid = Guid.NewGuid().ToString();
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{sid}.jsonl");
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"test prompt\"},\"cwd\":\"/workspace\"}",
            $"{{\"type\":\"tag\",\"tag\":\"experiment\",\"sessionId\":\"{sid}\"}}"
        };
        File.WriteAllText(filePath, string.Join("\n", lines) + "\n");

        var lite = TranscriptReader.ReadSessionLite(filePath);
        Assert.NotNull(lite);
        var info = TranscriptReader.ParseSessionInfoFromLite(sid, lite!, "/fallback");
        Assert.NotNull(info);
        Assert.Equal(sid, info!.SessionId);
        Assert.Equal("test prompt", info.Summary);
        Assert.Equal("experiment", info.Tag);
        Assert.Equal("/workspace", info.Cwd);
    }

    // -------------------------------------------------------------------
    // created_at extraction
    // -------------------------------------------------------------------

    [Fact]
    public void CreatedAt_FromIsoTimestamp()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"timestamp\":\"2026-01-15T10:30:00.000Z\"}",
            "{\"type\":\"assistant\",\"message\":{\"content\":\"hi\"},\"timestamp\":\"2026-01-15T10:35:00.000Z\"}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal(1768473000000, Assert.Single(sessions).CreatedAt);
    }

    [Fact]
    public void CreatedAt_LessThanOrEqualToLastModified()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var filePath = Path.Combine(projectDir, $"{sid}.jsonl");
        File.WriteAllText(
            filePath,
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"timestamp\":\"2026-01-01T00:00:00.000Z\"}\n");
        File.SetLastWriteTimeUtc(filePath, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        var s = Assert.Single(sessions);
        Assert.NotNull(s.CreatedAt);
        Assert.True(s.CreatedAt <= s.LastModified);
    }

    [Fact]
    public void CreatedAt_FoundInLaterHeadRecord_WhenFirstLineLacksTimestamp()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"permission-mode\",\"permissionMode\":\"acceptEdits\"}",
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"timestamp\":\"2026-01-15T10:30:00.000Z\"}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Equal(1768473000000, Assert.Single(sessions).CreatedAt);
    }

    [Fact]
    public void CreatedAt_NoneWhenMissing()
    {
        var projectPath = MakeProjectPath("proj");
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "no timestamp");

        var sessions = ClaudeSessions.ListSessions(projectPath, includeWorktrees: false);
        Assert.Null(Assert.Single(sessions).CreatedAt);
    }

    [Fact]
    public void CreatedAt_NoneOnInvalidFormat()
    {
        var sid = Guid.NewGuid().ToString();
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{sid}.jsonl");
        File.WriteAllText(
            filePath,
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"timestamp\":\"not-a-valid-iso-date\"}\n");

        var lite = TranscriptReader.ReadSessionLite(filePath);
        Assert.NotNull(lite);
        var info = TranscriptReader.ParseSessionInfoFromLite(sid, lite!);
        Assert.NotNull(info);
        Assert.Null(info!.CreatedAt);
    }

    [Fact]
    public void CreatedAt_WithoutZSuffix()
    {
        var sid = Guid.NewGuid().ToString();
        var dir = Path.Combine(Path.GetTempPath(), "sdk-sessions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{sid}.jsonl");
        File.WriteAllText(
            filePath,
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"},\"timestamp\":\"2026-01-15T10:30:00+00:00\"}\n");

        var lite = TranscriptReader.ReadSessionLite(filePath);
        Assert.NotNull(lite);
        var info = TranscriptReader.ParseSessionInfoFromLite(sid, lite!);
        Assert.NotNull(info);
        Assert.Equal(1768473000000, info!.CreatedAt);
    }

    [Fact]
    public void SdkSessionInfo_CreatedAtDefaultsToNull()
    {
        var info = new SdkSessionInfo("abc", "test", 1000) { FileSize = 42 };
        Assert.Null(info.CreatedAt);
    }

    [Fact]
    public void SdkSessionInfo_TagDefaultsToNull()
    {
        var info = new SdkSessionInfo("abc", "test", 1000) { FileSize = 42 };
        Assert.Null(info.Tag);
    }

    [Fact]
    public void SdkSessionInfo_RequiredFieldsRoundtrip()
    {
        var info = new SdkSessionInfo("abc", "test", 1000) { FileSize = 42 };
        Assert.Equal("abc", info.SessionId);
        Assert.Equal("test", info.Summary);
        Assert.Equal(1000, info.LastModified);
        Assert.Equal(42, info.FileSize);
        Assert.Null(info.CustomTitle);
        Assert.Null(info.FirstPrompt);
        Assert.Null(info.GitBranch);
        Assert.Null(info.Cwd);
    }

    [Fact]
    public void SdkSessionInfo_AllFieldsRoundtrip()
    {
        var info = new SdkSessionInfo("abc", "test", 1000)
        {
            FileSize = 42,
            CustomTitle = "title",
            FirstPrompt = "prompt",
            GitBranch = "main",
            Cwd = "/foo"
        };
        Assert.Equal("title", info.CustomTitle);
        Assert.Equal("prompt", info.FirstPrompt);
        Assert.Equal("main", info.GitBranch);
        Assert.Equal("/foo", info.Cwd);
    }
}
