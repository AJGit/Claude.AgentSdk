using System.Text.Json;
using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for <see cref="ClaudeSessions.GetSessionMessages" />. Ported from the Python SDK's
///     <c>TestGetSessionMessages</c>, <c>TestBuildConversationChain</c>, and
///     <c>TestSessionMessageType</c> test classes.
/// </summary>
[UnitTest]
[Collection(SessionsTestCollection.Name)]
public sealed class GetSessionMessagesTests : IDisposable
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

    [Fact]
    public void InvalidSessionId_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.GetSessionMessages("not-a-uuid"));
        Assert.Empty(ClaudeSessions.GetSessionMessages(""));
    }

    [Fact]
    public void NonexistentSession_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.GetSessionMessages(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void SimpleChain_UserAssistantUserAssistant()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var u2 = Guid.NewGuid().ToString();
        var a2 = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hello"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hi!"),
            SessionTestHelpers.MakeTranscriptEntry("user", u2, a1, sid, "thanks"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a2, u2, sid, "welcome")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(4, messages.Count);

        Assert.Equal("user", messages[0].Type);
        Assert.Equal(u1, messages[0].Uuid);
        Assert.Equal(sid, messages[0].SessionId);
        Assert.Equal("hello", messages[0].Message!.Value.GetProperty("content").GetString());
        Assert.Null(messages[0].ParentToolUseId);

        Assert.Equal("assistant", messages[1].Type);
        Assert.Equal(a1, messages[1].Uuid);

        Assert.Equal("user", messages[2].Type);
        Assert.Equal(u2, messages[2].Uuid);

        Assert.Equal("assistant", messages[3].Type);
        Assert.Equal(a2, messages[3].Uuid);
    }

    [Fact]
    public void FiltersMetaMessages_FromOutput()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var meta = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hello"),
            SessionTestHelpers.MakeTranscriptEntry(
                "user", meta, u1, sid, "meta", new Dictionary<string, object?> { ["isMeta"] = true }),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, meta, sid, "hi")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
        Assert.Equal(a1, messages[1].Uuid);
    }

    [Fact]
    public void FiltersNonUserAssistant_FromChain()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var prog = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hello"),
            SessionTestHelpers.MakeTranscriptEntry("progress", prog, u1, sid),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, prog, sid, "hi")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
        Assert.Equal(a1, messages[1].Uuid);
    }

    [Fact]
    public void KeepsCompactSummary()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry(
                "user", u1, null, sid, "compact summary",
                new Dictionary<string, object?> { ["isCompactSummary"] = true }),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hi")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
    }

    [Fact]
    public void LimitAndOffset_Pagination()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var uuids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid().ToString()).ToList();
        var entries = new List<System.Text.Json.Nodes.JsonObject>();
        for (var i = 0; i < uuids.Count; i++)
        {
            var parent = i > 0 ? uuids[i - 1] : null;
            var type = i % 2 == 0 ? "user" : "assistant";
            entries.Add(SessionTestHelpers.MakeTranscriptEntry(type, uuids[i], parent, sid, $"m{i}"));
        }

        SessionTestHelpers.WriteTranscript(projectDir, sid, entries);

        var all = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(6, all.Count);

        var page = ClaudeSessions.GetSessionMessages(sid, projectPath, limit: 2);
        Assert.Equal(2, page.Count);
        Assert.Equal(uuids[0], page[0].Uuid);
        Assert.Equal(uuids[1], page[1].Uuid);

        page = ClaudeSessions.GetSessionMessages(sid, projectPath, limit: 2, offset: 2);
        Assert.Equal(2, page.Count);
        Assert.Equal(uuids[2], page[0].Uuid);
        Assert.Equal(uuids[3], page[1].Uuid);

        page = ClaudeSessions.GetSessionMessages(sid, projectPath, offset: 4);
        Assert.Equal(2, page.Count);
        Assert.Equal(uuids[4], page[0].Uuid);
        Assert.Equal(uuids[5], page[1].Uuid);

        page = ClaudeSessions.GetSessionMessages(sid, projectPath, limit: 0);
        Assert.Equal(6, page.Count);

        page = ClaudeSessions.GetSessionMessages(sid, projectPath, offset: 100);
        Assert.Empty(page);
    }

    [Fact]
    public void PicksMainChain_OverSidechain()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var root = Guid.NewGuid().ToString();
        var mainLeaf = Guid.NewGuid().ToString();
        var sideLeaf = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", root, null, sid, "root"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", mainLeaf, root, sid, "main"),
            SessionTestHelpers.MakeTranscriptEntry(
                "assistant", sideLeaf, root, sid, "side",
                new Dictionary<string, object?> { ["isSidechain"] = true })
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(root, messages[0].Uuid);
        Assert.Equal(mainLeaf, messages[1].Uuid);
    }

    [Fact]
    public void PicksLatestLeaf_ByFilePosition()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var root = Guid.NewGuid().ToString();
        var oldLeaf = Guid.NewGuid().ToString();
        var newLeaf = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", root, null, sid, "root"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", oldLeaf, root, sid, "old"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", newLeaf, root, sid, "new")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(root, messages[0].Uuid);
        Assert.Equal(newLeaf, messages[1].Uuid);
    }

    [Fact]
    public void TerminalNonMessage_WalkedBackToLeaf()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var prog = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello"),
            SessionTestHelpers.MakeTranscriptEntry("progress", prog, a1, sid)
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
        Assert.Equal(a1, messages[1].Uuid);
    }

    [Fact]
    public void CorruptLines_AreSkipped()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        var lines = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi").ToJsonString(),
            "not valid json {{{",
            "",
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello").ToJsonString()
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public void SearchesAllProjects_WhenNoDirectory()
    {
        var proj1 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/path/one");
        var proj2 = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/path/two");
        _ = proj1;

        var sid = Guid.NewGuid().ToString();
        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        SessionTestHelpers.WriteTranscript(proj2, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
    }

    [Fact]
    public void CycleDetection_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        // a1 -> u1 -> a1 (cycle!)
        SessionTestHelpers.WriteTranscript(projectDir, sid,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, a1, sid, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello")
        ]);

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Empty(messages);
    }

    [Fact]
    public void EmptyTranscriptFile_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), "");

        Assert.Empty(ClaudeSessions.GetSessionMessages(sid, projectPath));
    }

    [Fact]
    public void IgnoresNonTranscriptTypes()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();

        var lines = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi").ToJsonString(),
            "{\"type\":\"summary\",\"summary\":\"A nice chat\"}",
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello").ToJsonString()
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var messages = ClaudeSessions.GetSessionMessages(sid, projectPath);
        Assert.Equal(2, messages.Count);
    }

    // -------------------------------------------------------------------
    // BuildConversationChain unit tests
    // -------------------------------------------------------------------

    [Fact]
    public void BuildConversationChain_EmptyInput()
    {
        Assert.Empty(TranscriptReader.BuildConversationChain([]));
    }

    [Fact]
    public void BuildConversationChain_LinearChain()
    {
        var entries = new List<TranscriptEntry>
        {
            MakeEntry("user", "a", null),
            MakeEntry("assistant", "b", "a"),
            MakeEntry("user", "c", "b")
        };
        var result = TranscriptReader.BuildConversationChain(entries);
        Assert.Equal(["a", "b", "c"], result.Select(e => e.Uuid));
    }

    [Fact]
    public void BuildConversationChain_OnlyProgressEntries_ReturnsEmpty()
    {
        var entries = new List<TranscriptEntry>
        {
            MakeEntry("progress", "a", null),
            MakeEntry("progress", "b", "a")
        };
        Assert.Empty(TranscriptReader.BuildConversationChain(entries));
    }

    private static TranscriptEntry MakeEntry(string type, string uuid, string? parentUuid)
    {
        using var doc = JsonDocument.Parse("{}");
        return new TranscriptEntry
        {
            Type = type,
            Uuid = uuid,
            ParentUuid = parentUuid,
            Raw = doc.RootElement.Clone()
        };
    }

    // -------------------------------------------------------------------
    // SessionMessage type tests
    // -------------------------------------------------------------------

    [Fact]
    public void SessionMessage_Creation()
    {
        using var doc = JsonDocument.Parse("{\"role\":\"user\",\"content\":\"hi\"}");
        var msg = new SessionMessage("user", "abc", "sess", doc.RootElement.Clone());
        Assert.Equal("user", msg.Type);
        Assert.Equal("abc", msg.Uuid);
        Assert.Equal("sess", msg.SessionId);
        Assert.Equal("hi", msg.Message!.Value.GetProperty("content").GetString());
        Assert.Null(msg.ParentToolUseId);
        Assert.Null(msg.ParentAgentId);
    }

    [Fact]
    public void SessionMessage_ParentIds()
    {
        var msg = new SessionMessage("assistant", "abc", "sess", null)
        {
            ParentToolUseId = "toolu_1",
            ParentAgentId = "agent-1"
        };
        Assert.Equal("toolu_1", msg.ParentToolUseId);
        Assert.Equal("agent-1", msg.ParentAgentId);
    }
}
