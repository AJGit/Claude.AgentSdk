using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Tests for <see cref="ClaudeSessions.GetSessionInfo" />. Ported from the Python SDK's
///     <c>TestGetSessionInfo</c> test class.
/// </summary>
[UnitTest]
[Collection(SessionsTestCollection.Name)]
public sealed class GetSessionInfoTests : IDisposable
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
    public void InvalidSessionId_ReturnsNull()
    {
        Assert.Null(ClaudeSessions.GetSessionInfo("not-a-uuid"));
        Assert.Null(ClaudeSessions.GetSessionInfo(""));
    }

    [Fact]
    public void NonexistentSession_ReturnsNull()
    {
        Assert.Null(ClaudeSessions.GetSessionInfo(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void FoundWithDirectory()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "hello", gitBranch: "main");

        var info = ClaudeSessions.GetSessionInfo(sid, projectPath);
        Assert.NotNull(info);
        Assert.Equal(sid, info!.SessionId);
        Assert.Equal("hello", info.Summary);
        Assert.Equal("main", info.GitBranch);
    }

    [Fact]
    public void FoundWithoutDirectory()
    {
        var projectDir = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, "/some/project");
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "search all");

        var info = ClaudeSessions.GetSessionInfo(sid);
        Assert.NotNull(info);
        Assert.Equal(sid, info!.SessionId);
        Assert.Equal("search all", info.Summary);
    }

    [Fact]
    public void ReturnsNull_ForSidechain()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var (sid, _) = SessionTestHelpers.MakeSessionFile(projectDir, firstPrompt: "sidechain", isSidechain: true);

        Assert.Null(ClaudeSessions.GetSessionInfo(sid, projectPath));
    }

    [Fact]
    public void DirectoryNotContainingSession_ReturnsNull()
    {
        var projectA = MakeProjectPath();
        var projectB = MakeProjectPath();
        var dirA = SessionTestHelpers.MakeProjectDir(_config.ConfigDir, SessionPaths.CanonicalizePath(projectA));
        SessionTestHelpers.MakeProjectDir(_config.ConfigDir, SessionPaths.CanonicalizePath(projectB));
        var (sid, _) = SessionTestHelpers.MakeSessionFile(dirA, firstPrompt: "in A only");

        Assert.Null(ClaudeSessions.GetSessionInfo(sid, projectB));
        Assert.NotNull(ClaudeSessions.GetSessionInfo(sid));
    }

    [Fact]
    public void IncludesTag()
    {
        var projectPath = MakeProjectPath();
        var projectDir = SessionTestHelpers.MakeProjectDir(
            _config.ConfigDir, SessionPaths.CanonicalizePath(projectPath));
        var sid = Guid.NewGuid().ToString();
        var lines = new[]
        {
            "{\"type\":\"user\",\"message\":{\"content\":\"hello\"}}",
            $"{{\"type\":\"tag\",\"tag\":\"urgent\",\"sessionId\":\"{sid}\"}}"
        };
        File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"), string.Join("\n", lines) + "\n");

        var info = ClaudeSessions.GetSessionInfo(sid, projectPath);
        Assert.NotNull(info);
        Assert.Equal("urgent", info!.Tag);
    }
}
