using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for <see cref="ClaudeSessions.ListSubagents" /> and
///     <see cref="ClaudeSessions.GetSubagentMessages" />. Ported from the Python SDK's
///     <c>TestListSubagents</c> and <c>TestGetSubagentMessages</c> test classes.
/// </summary>
[UnitTest]
[Collection(SessionsTestCollection.Name)]
public sealed class SubagentTests : IDisposable
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

    private (string SessionId, string SubagentsDir) MakeSessionWithSubagents(
        string projectPath, IReadOnlyList<string>? agentIds = null)
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir);
        var subagentsDir = Path.Combine(projectDir, sid, "subagents");
        Directory.CreateDirectory(subagentsDir);
        foreach (var agentId in agentIds ?? [])
        {
            File.WriteAllText(
                Path.Combine(subagentsDir, $"agent-{agentId}.jsonl"),
                "{\"type\":\"user\",\"uuid\":\"u\",\"parentUuid\":null}\n");
        }

        return (sid, subagentsDir);
    }

    // -------------------------------------------------------------------
    // ListSubagents
    // -------------------------------------------------------------------

    [Fact]
    public void ListSubagents_InvalidSessionId_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.ListSubagents("not-a-uuid"));
        Assert.Empty(ClaudeSessions.ListSubagents(""));
    }

    [Fact]
    public void ListSubagents_NonexistentSession_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.ListSubagents(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void ListSubagents_SessionExistsNoSubagentsDir_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir);

        Assert.Empty(ClaudeSessions.ListSubagents(sid, projectPath));
    }

    [Fact]
    public void ListSubagents_EmptySubagentsDir_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var (sid, _) = MakeSessionWithSubagents(projectPath, []);

        Assert.Empty(ClaudeSessions.ListSubagents(sid, projectPath));
    }

    [Fact]
    public void ListSubagents_HappyPath_ReturnsAgentIds()
    {
        var projectPath = MakeProjectPath();
        var (sid, _) = MakeSessionWithSubagents(projectPath, ["abc123", "def456"]);

        var result = ClaudeSessions.ListSubagents(sid, projectPath);
        Assert.Equal(["abc123", "def456"], result.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ListSubagents_IgnoresNonAgentFiles()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath, ["keep"]);
        File.WriteAllText(Path.Combine(subagentsDir, "agent-keep.meta.json"), "{}");
        File.WriteAllText(Path.Combine(subagentsDir, "other.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(subagentsDir, "agent-noext"), "{}");

        Assert.Equal(["keep"], ClaudeSessions.ListSubagents(sid, projectPath));
    }

    [Fact]
    public void ListSubagents_RecursesIntoSubdirectories()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath, ["top"]);
        var nested = Path.Combine(subagentsDir, "workflows", "run-1");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "agent-nested.jsonl"), "{}\n");

        var result = ClaudeSessions.ListSubagents(sid, projectPath);
        Assert.Equal(["nested", "top"], result.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ListSubagents_SearchesAllProjects_WithoutDirectory()
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/some/project");
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir);
        var subagentsDir = Path.Combine(projectDir, sid, "subagents");
        Directory.CreateDirectory(subagentsDir);
        File.WriteAllText(Path.Combine(subagentsDir, "agent-x.jsonl"), "{}\n");

        Assert.Equal(["x"], ClaudeSessions.ListSubagents(sid));
    }

    // -------------------------------------------------------------------
    // GetSubagentMessages
    // -------------------------------------------------------------------

    [Fact]
    public void GetSubagentMessages_InvalidSessionId_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.GetSubagentMessages("not-a-uuid", "abc"));
        Assert.Empty(ClaudeSessions.GetSubagentMessages("", "abc"));
    }

    [Fact]
    public void GetSubagentMessages_EmptyAgentId_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.GetSubagentMessages(Guid.NewGuid().ToString(), ""));
    }

    [Fact]
    public void GetSubagentMessages_NonexistentSession_ReturnsEmpty()
    {
        Assert.Empty(ClaudeSessions.GetSubagentMessages(Guid.NewGuid().ToString(), "abc"));
    }

    [Fact]
    public void GetSubagentMessages_NonexistentAgent_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var (sid, _) = MakeSessionWithSubagents(projectPath, ["other"]);

        Assert.Empty(ClaudeSessions.GetSubagentMessages(sid, "missing", projectPath));
    }

    [Fact]
    public void GetSubagentMessages_SimpleChain()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var u2 = Guid.NewGuid().ToString();
        var a2 = Guid.NewGuid().ToString();

        var entries = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "task"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "working"),
            SessionTestHelpers.MakeTranscriptEntry("user", u2, a1, sid, "continue"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a2, u2, sid, "done")
        };
        File.WriteAllText(
            Path.Combine(subagentsDir, "agent-abc.jsonl"),
            string.Join("\n", entries.Select(e => e.ToJsonString())) + "\n");

        var messages = ClaudeSessions.GetSubagentMessages(sid, "abc", projectPath);
        Assert.Equal(4, messages.Count);
        Assert.Equal([u1, a1, u2, a2], messages.Select(m => m.Uuid));
        Assert.Equal("user", messages[0].Type);
        Assert.Equal(sid, messages[0].SessionId);
        Assert.Equal("task", messages[0].Message!.Value.GetProperty("content").GetString());
        Assert.Null(messages[0].ParentToolUseId);
        Assert.Equal("assistant", messages[3].Type);
    }

    [Fact]
    public void GetSubagentMessages_FindsAgentInNestedSubdirectory()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        var nested = Path.Combine(subagentsDir, "workflows", "run-1");
        Directory.CreateDirectory(nested);

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var entries = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello")
        };
        File.WriteAllText(
            Path.Combine(nested, "agent-deep.jsonl"),
            string.Join("\n", entries.Select(e => e.ToJsonString())) + "\n");

        var messages = ClaudeSessions.GetSubagentMessages(sid, "deep", projectPath);
        Assert.Equal(2, messages.Count);
        Assert.Equal(u1, messages[0].Uuid);
        Assert.Equal(a1, messages[1].Uuid);
    }

    private static void WriteAgent(string subagentsDir, string agentId, string sid, string? meta)
    {
        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var entries = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "hello")
        };
        File.WriteAllText(
            Path.Combine(subagentsDir, $"agent-{agentId}.jsonl"),
            string.Join("\n", entries.Select(e => e.ToJsonString())) + "\n");

        if (meta is not null)
        {
            File.WriteAllText(Path.Combine(subagentsDir, $"agent-{agentId}.meta.json"), meta);
        }
    }

    [Fact]
    public void GetSubagentMessages_ParentIdsRecoveredFromMetaSidecar()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        WriteAgent(
            subagentsDir, "abc", sid,
            "{\"agentType\":\"general-purpose\",\"toolUseId\":\"toolu_01ABC\"," +
            "\"parentAgentId\":\"a-parent\",\"spawnDepth\":2}");

        var messages = ClaudeSessions.GetSubagentMessages(sid, "abc", projectPath);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal("toolu_01ABC", m.ParentToolUseId));
        Assert.All(messages, m => Assert.Equal("a-parent", m.ParentAgentId));
    }

    [Fact]
    public void GetSubagentMessages_ParentIdsFromNestedMetaSidecar()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        var nested = Path.Combine(subagentsDir, "workflows", "run-1");
        Directory.CreateDirectory(nested);
        WriteAgent(nested, "deep", sid, "{\"toolUseId\":\"toolu_nested\"}");

        var messages = ClaudeSessions.GetSubagentMessages(sid, "deep", projectPath);
        Assert.All(messages, m => Assert.Equal("toolu_nested", m.ParentToolUseId));
        Assert.All(messages, m => Assert.Null(m.ParentAgentId));
    }

    public static IEnumerable<object?[]> UnusableSidecarMetaCases()
    {
        yield return [null]; // no sidecar
        yield return ["not json {"]; // unreadable sidecar
        yield return ["{\"agentType\":\"general-purpose\"}"]; // sidecar without ids
        yield return ["{\"toolUseId\":42,\"parentAgentId\":[\"x\"]}"]; // wrong types
    }

    [Theory]
    [MemberData(nameof(UnusableSidecarMetaCases))]
    public void GetSubagentMessages_ParentIdsNull_WhenSidecarMissingOrUnusable(string? meta)
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        WriteAgent(subagentsDir, "x", sid, meta);

        var messages = ClaudeSessions.GetSubagentMessages(sid, "x", projectPath);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Null(m.ParentToolUseId));
        Assert.All(messages, m => Assert.Null(m.ParentAgentId));
    }

    [Fact]
    public void GetSubagentMessages_UnreadableSidecar_DegradesToNull()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        WriteAgent(subagentsDir, "x", sid, null);
        // A sidecar path that is a directory instead of a file — must not throw.
        Directory.CreateDirectory(Path.Combine(subagentsDir, "agent-x.meta.json"));

        var messages = ClaudeSessions.GetSubagentMessages(sid, "x", projectPath);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Null(m.ParentToolUseId));
        Assert.All(messages, m => Assert.Null(m.ParentAgentId));
    }

    [Fact]
    public void GetSubagentMessages_SkipsCorruptLines()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);

        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var lines = new[]
        {
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sid, "hi").ToJsonString(),
            "not valid json {",
            "",
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sid, "ok").ToJsonString()
        };
        File.WriteAllText(Path.Combine(subagentsDir, "agent-x.jsonl"), string.Join("\n", lines) + "\n");

        var messages = ClaudeSessions.GetSubagentMessages(sid, "x", projectPath);
        Assert.Equal([u1, a1], messages.Select(m => m.Uuid));
    }

    [Fact]
    public void GetSubagentMessages_LimitAndOffset()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);

        var uuids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid().ToString()).ToList();
        var entries = new List<System.Text.Json.Nodes.JsonObject>();
        for (var i = 0; i < uuids.Count; i++)
        {
            var parent = i > 0 ? uuids[i - 1] : null;
            var type = i % 2 == 0 ? "user" : "assistant";
            entries.Add(SessionTestHelpers.MakeTranscriptEntry(type, uuids[i], parent, sid, $"m{i}"));
        }

        File.WriteAllText(
            Path.Combine(subagentsDir, "agent-p.jsonl"),
            string.Join("\n", entries.Select(e => e.ToJsonString())) + "\n");

        var all = ClaudeSessions.GetSubagentMessages(sid, "p", projectPath);
        Assert.Equal(6, all.Count);

        var page = ClaudeSessions.GetSubagentMessages(sid, "p", projectPath, limit: 2);
        Assert.Equal(uuids[..2], page.Select(m => m.Uuid));

        page = ClaudeSessions.GetSubagentMessages(sid, "p", projectPath, limit: 2, offset: 2);
        Assert.Equal(uuids[2..4], page.Select(m => m.Uuid));

        page = ClaudeSessions.GetSubagentMessages(sid, "p", projectPath, offset: 4);
        Assert.Equal(uuids[4..], page.Select(m => m.Uuid));

        page = ClaudeSessions.GetSubagentMessages(sid, "p", projectPath, limit: 0);
        Assert.Equal(6, page.Count);
    }

    [Fact]
    public void GetSubagentMessages_EmptyAgentFile_ReturnsEmpty()
    {
        var projectPath = MakeProjectPath();
        var (sid, subagentsDir) = MakeSessionWithSubagents(projectPath);
        File.WriteAllText(Path.Combine(subagentsDir, "agent-empty.jsonl"), "");

        Assert.Empty(ClaudeSessions.GetSubagentMessages(sid, "empty", projectPath));
    }
}
