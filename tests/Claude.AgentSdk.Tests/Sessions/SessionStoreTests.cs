using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Extensions;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Sessions;
using Claude.AgentSdk.Testing;
using Claude.AgentSdk.Transport;

namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     A store that records appends and can be configured to fail or stall.
/// </summary>
internal sealed class RecordingStore : ISessionStore
{
    public ConcurrentQueue<(SessionKey Key, List<JsonElement> Entries)> Appends { get; } = new();
    public int Attempts;
    public int FailuresBeforeSuccess;
    public TimeSpan Delay = TimeSpan.Zero;

    public async Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries,
        CancellationToken cancellationToken = default)
    {
        var attempt = Interlocked.Increment(ref Attempts);
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, CancellationToken.None);
        }

        if (attempt <= FailuresBeforeSuccess)
        {
            throw new InvalidOperationException($"boom {attempt}");
        }

        Appends.Enqueue((key, entries.ToList()));
    }

    public Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<JsonElement>?>(null);
    }
}

/// <summary>
///     A store with only the required methods (no optional capabilities).
/// </summary>
internal sealed class MinimalStore(InMemorySessionStore inner) : ISessionStore, ISessionStoreListing
{
    public Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries,
        CancellationToken cancellationToken = default)
    {
        return inner.AppendAsync(key, entries, cancellationToken);
    }

    public Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        return inner.LoadAsync(key, cancellationToken);
    }

    public Task<IReadOnlyList<SessionStoreListEntry>> ListSessionsAsync(string projectKey,
        CancellationToken cancellationToken = default)
    {
        return inner.ListSessionsAsync(projectKey, cancellationToken);
    }
}

internal static class StoreTestData
{
    public static JsonElement E(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public static JsonElement Entry(string type, string uuid, string? parentUuid, string sessionId,
        string? content = null, string? extra = null)
    {
        var node = SessionTestHelpers.MakeTranscriptEntry(type, uuid, parentUuid, sessionId, content);
        node["timestamp"] = "2024-01-01T00:00:00.000Z";
        if (extra is not null)
        {
            foreach (var (name, value) in JsonNode.Parse(extra)!.AsObject())
            {
                node[name] = value?.DeepClone();
            }
        }

        return JsonSerializer.SerializeToElement(node);
    }

    public static string TempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return SessionPaths.CanonicalizePath(dir);
    }
}

public class SessionStoreContractTests
{
    [Fact]
    public Task InMemorySessionStore_PassesConformance()
    {
        return SessionStoreConformance.RunAsync(() => new InMemorySessionStore());
    }

    [Fact]
    public Task MinimalStore_PassesRequiredContracts()
    {
        return SessionStoreConformance.RunAsync(() => new MinimalStore(new InMemorySessionStore()));
    }

    [Fact]
    public async Task Conformance_DetectsReorderingStore()
    {
        var ex = await Assert.ThrowsAsync<SessionStoreConformanceException>(() =>
            SessionStoreConformance.RunAsync(() => new ReversingStore()));
        Assert.Contains("3. multiple appends preserve order", ex.Message);
    }

    [Theory]
    [InlineData("proj/sess.jsonl", "proj", "sess", null)]
    [InlineData("proj/sess/subagents/agent-1.jsonl", "proj", "sess", "subagents/agent-1")]
    [InlineData("proj/sess/subagents/workflows/run-1/agent-2.jsonl", "proj", "sess",
        "subagents/workflows/run-1/agent-2")]
    public void SessionKey_FromTranscriptPath_ParsesLayouts(string relative, string project, string session,
        string? subpath)
    {
        var projectsDir = Path.Combine(Path.GetTempPath(), "projects");
        var key = SessionKey.FromTranscriptPath(Path.Combine(projectsDir, relative), projectsDir);

        Assert.Equal(new SessionKey(project, session, subpath), key);
    }

    [Theory]
    [InlineData("../elsewhere/sess.jsonl")]
    [InlineData("proj")]
    [InlineData("proj/sess/agent.jsonl")]
    [InlineData("proj/sess")]
    public void SessionKey_FromTranscriptPath_RejectsOtherShapes(string relative)
    {
        var projectsDir = Path.Combine(Path.GetTempPath(), "projects");

        Assert.Null(SessionKey.FromTranscriptPath(Path.Combine(projectsDir, relative), projectsDir));
    }

    [Fact]
    public void SessionKey_SerializesWithSnakeCaseAndOmitsNullSubpath()
    {
        var json = JsonSerializer.SerializeToElement(new SessionKey("p", "s"));

        Assert.Equal("p", json.GetProperty("project_key").GetString());
        Assert.Equal("s", json.GetProperty("session_id").GetString());
        Assert.False(json.TryGetProperty("subpath", out _));
    }

    [Fact]
    public void SummaryFold_TracksPromptTitleTagAndSidechain()
    {
        var key = new SessionKey("p", "s");
        var summary = SessionSummary.Fold(null, key,
        [
            StoreTestData.E("""{"type":"user","timestamp":"2024-01-01T00:00:00Z","cwd":"/w","message":{"content":"<command-name>/init</command-name>"}}"""),
            StoreTestData.E("""{"type":"user","message":{"content":[{"type":"text","text":"Fix the bug"}]}}"""),
            StoreTestData.E("""{"type":"custom-title","customTitle":"one"}"""),
            StoreTestData.E("""{"type":"tag","tag":"urgent"}""")
        ]);
        summary = SessionSummary.Fold(summary with { Mtime = 5 }, key,
        [
            StoreTestData.E("""{"type":"custom-title","customTitle":"two"}"""),
            StoreTestData.E("""{"type":"tag","tag":""}""")
        ]);

        Assert.Equal(5, summary.Mtime);
        var info = SessionSummary.ToSdkSessionInfo(summary, "/fallback")!;
        Assert.Equal("two", info.Summary);
        Assert.Equal("Fix the bug", info.FirstPrompt);
        Assert.Equal("/w", info.Cwd);
        Assert.Null(info.Tag);
        Assert.Equal(DateTimeOffset.Parse("2024-01-01T00:00:00Z").ToUnixTimeMilliseconds(), info.CreatedAt);

        var sidechain = SessionSummary.Fold(null, key,
            [StoreTestData.E("""{"type":"user","isSidechain":true,"message":{"content":"x"}}""")]);
        Assert.Null(SessionSummary.ToSdkSessionInfo(sidechain, null));
    }

    private sealed class ReversingStore : ISessionStore
    {
        private readonly Dictionary<string, List<JsonElement>> _data = new();

        public Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries,
            CancellationToken cancellationToken = default)
        {
            if (!_data.TryGetValue(key.ToString(), out var list))
            {
                _data[key.ToString()] = list = [];
            }

            list.InsertRange(0, entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<JsonElement>?>(_data.GetValueOrDefault(key.ToString()));
        }
    }
}

public class TranscriptMirrorBatcherTests
{
    private static readonly string _projectsDir = Path.Combine(Path.GetTempPath(), "mirror-projects");
    private static readonly string _mainFile = Path.Combine(_projectsDir, "proj", "sess.jsonl");
    private static readonly string _subFile = Path.Combine(_projectsDir, "proj", "sess", "subagents", "agent-1.jsonl");

    private static TranscriptMirrorBatcher Create(ISessionStore store, List<(SessionKey?, string)>? errors = null,
        SessionStoreFlushMode mode = SessionStoreFlushMode.Batched, TimeSpan? sendTimeout = null)
    {
        return new TranscriptMirrorBatcher(store, _projectsDir, (key, error) =>
        {
            errors?.Add((key, error));
            return Task.CompletedTask;
        }, mode, sendTimeout: sendTimeout);
    }

    [Fact]
    public async Task Flush_CoalescesPerFilePreservingOrder()
    {
        var store = new RecordingStore();
        var batcher = Create(store);

        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"a","n":1}""")]);
        batcher.Enqueue(_subFile, [StoreTestData.E("""{"type":"s","n":1}""")]);
        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"a","n":2}"""), StoreTestData.E("""{"type":"a","n":3}""")]);
        batcher.Enqueue(_mainFile, []);
        Assert.Empty(store.Appends); // batched: nothing until flush

        await batcher.FlushAsync();

        var appends = store.Appends.ToList();
        Assert.Equal(2, appends.Count);
        Assert.Equal(new SessionKey("proj", "sess"), appends[0].Key);
        Assert.Equal([1, 2, 3], appends[0].Entries.Select(e => e.GetProperty("n").GetInt32()));
        Assert.Equal(new SessionKey("proj", "sess", "subagents/agent-1"), appends[1].Key);
    }

    [Fact]
    public async Task Enqueue_OverEntryThreshold_FlushesInBackground()
    {
        var store = new RecordingStore();
        var batcher = Create(store);

        var entries = Enumerable.Range(0, TranscriptMirrorBatcher.MaxPendingEntries + 1)
            .Select(i => StoreTestData.E($$"""{"type":"x","n":{{i}}}""")).ToList();
        batcher.Enqueue(_mainFile, entries);
        await batcher.BackgroundFlush!;

        Assert.Equal(TranscriptMirrorBatcher.MaxPendingEntries + 1, Assert.Single(store.Appends).Entries.Count);
    }

    [Fact]
    public async Task EagerMode_FlushesEveryFrame()
    {
        var store = new RecordingStore();
        var batcher = Create(store, mode: SessionStoreFlushMode.Eager);

        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"x"}""")]);
        await batcher.BackgroundFlush!;

        Assert.Single(store.Appends);
    }

    [Fact]
    public async Task TransientFailures_AreRetried_WithoutReportingError()
    {
        var store = new RecordingStore { FailuresBeforeSuccess = 2 };
        var errors = new List<(SessionKey?, string)>();
        var batcher = Create(store, errors);

        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"x"}""")]);
        await batcher.FlushAsync();

        Assert.Equal(3, store.Attempts);
        Assert.Single(store.Appends);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExhaustedRetries_ReportErrorOnceAndNeverThrow()
    {
        var store = new RecordingStore { FailuresBeforeSuccess = int.MaxValue };
        var errors = new List<(SessionKey?, string)>();
        var batcher = Create(store, errors);

        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"x"}""")]);
        await batcher.FlushAsync();

        Assert.Equal(TranscriptMirrorBatcher.MaxAppendAttempts, store.Attempts);
        var (key, error) = Assert.Single(errors);
        Assert.Equal(new SessionKey("proj", "sess"), key);
        Assert.Contains("boom 3", error);
    }

    [Fact]
    public async Task Timeout_IsReportedAndNotRetried()
    {
        var store = new RecordingStore { Delay = TimeSpan.FromSeconds(2) };
        var errors = new List<(SessionKey?, string)>();
        var batcher = Create(store, errors, sendTimeout: TimeSpan.FromMilliseconds(100));

        batcher.Enqueue(_mainFile, [StoreTestData.E("""{"type":"x"}""")]);
        await batcher.FlushAsync();

        Assert.Equal(1, store.Attempts);
        Assert.Contains("timed out", Assert.Single(errors).Item2);
    }

    [Fact]
    public async Task PathOutsideProjectsDir_IsDropped()
    {
        var store = new RecordingStore();
        var errors = new List<(SessionKey?, string)>();
        var batcher = Create(store, errors);

        batcher.Enqueue(Path.Combine(Path.GetTempPath(), "elsewhere", "x.jsonl"), [StoreTestData.E("""{"type":"x"}""")]);
        await batcher.FlushAsync();

        Assert.Empty(store.Appends);
        Assert.Empty(errors);
    }
}

public class SessionStoreProtocolTests
{
    private static ClaudeAgentOptions OptionsWith(ISessionStore store, string configDir)
    {
        return new ClaudeAgentOptions
        {
            SessionStore = store,
            Environment = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = configDir }
        };
    }

    private static string MirrorFrame(string configDir, string entryJson)
    {
        var filePath = Path.Combine(configDir, "projects", "proj", "sess.jsonl");
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "transcript_mirror",
            ["filePath"] = filePath,
            ["entries"] = new[] { JsonDocument.Parse(entryJson).RootElement }
        });
    }

    private const string Result =
        """{"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"sess"}""";

    [Fact]
    public async Task MirrorFrames_AreNotYielded_AndStoreIsCurrentWhenResultArrives()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "cfg");
        var store = new InMemorySessionStore();
        var transport = new MockTransport();
        await using var handler = new QueryHandler(transport, OptionsWith(store, configDir));
        await handler.StartAsync();

        transport.EnqueueMessage(MirrorFrame(configDir, """{"type":"user","uuid":"u1"}"""));
        transport.EnqueueMessage(Result);
        transport.CompleteMessages();

        var messages = new List<Message>();
        var entriesAtResult = -1;
        await foreach (var message in handler.ReceiveMessagesAsync())
        {
            if (message is ResultMessage)
            {
                entriesAtResult = store.GetEntries(new SessionKey("proj", "sess")).Count;
            }

            messages.Add(message);
        }

        Assert.IsType<ResultMessage>(Assert.Single(messages));
        Assert.Equal(1, entriesAtResult);
    }

    [Fact]
    public async Task LateMirrorFrames_AreFlushedAtEndOfStream()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "cfg");
        var store = new InMemorySessionStore();
        var transport = new MockTransport();
        await using (var handler = new QueryHandler(transport, OptionsWith(store, configDir)))
        {
            await handler.StartAsync();
            transport.EnqueueMessage(Result);
            transport.EnqueueMessage(MirrorFrame(configDir, """{"type":"assistant","uuid":"a1"}"""));
            transport.CompleteMessages();

            await foreach (var _ in handler.ReceiveMessagesAsync())
            {
            }
        }

        Assert.Single(store.GetEntries(new SessionKey("proj", "sess")));
    }

    [Fact]
    public async Task FailingStore_YieldsMirrorErrorMessage()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "cfg");
        var transport = new MockTransport();
        await using var handler = new QueryHandler(transport,
            OptionsWith(new RecordingStore { FailuresBeforeSuccess = int.MaxValue }, configDir));
        await handler.StartAsync();

        transport.EnqueueMessage(MirrorFrame(configDir, """{"type":"user","uuid":"u1"}"""));
        transport.EnqueueMessage(Result);

        SystemMessage? mirrorError = null;
        await foreach (var message in handler.ReceiveMessagesAsync())
        {
            if (message is SystemMessage { Subtype: "mirror_error" } system)
            {
                mirrorError = system;
                break;
            }
        }

        Assert.NotNull(mirrorError);
        Assert.Equal(Claude.AgentSdk.Types.SystemMessageSubtype.MirrorError, mirrorError.SubtypeEnum);
        var data = mirrorError.GetData<MirrorErrorData>()!;
        Assert.Equal(new SessionKey("proj", "sess"), data.Key);
        Assert.Contains("boom", data.Error);
    }

    [Fact]
    public void CliArgs_IncludeSessionMirrorOnlyWithStore()
    {
        Assert.Contains("--session-mirror",
            CliArgumentsBuilder.Create(new ClaudeAgentOptions { SessionStore = new InMemorySessionStore() }).Build());
        Assert.DoesNotContain("--session-mirror", CliArgumentsBuilder.Create(new ClaudeAgentOptions()).Build());
    }

    [Fact]
    public void Validation_RejectsInvalidStoreCombinations()
    {
        var minimal = new RecordingStore();

        Assert.Throws<ArgumentException>(() => ClaudeAgentOptionsValidation.ValidateSessionStore(
            new ClaudeAgentOptions { SessionStore = minimal, ContinueConversation = true }));
        Assert.Throws<ArgumentException>(() => ClaudeAgentOptionsValidation.ValidateSessionStore(
            new ClaudeAgentOptions { SessionStore = new InMemorySessionStore(), EnableFileCheckpointing = true }));

        // An explicit resume wins over continue, so listing is never needed.
        ClaudeAgentOptionsValidation.ValidateSessionStore(new ClaudeAgentOptions
        {
            SessionStore = minimal, ContinueConversation = true, Resume = Guid.NewGuid().ToString()
        });
    }
}

public class SessionResumeMaterializerTests
{
    private static async Task<(InMemorySessionStore Store, string SessionId, string Cwd, string ProjectKey)>
        SeedAsync(params string[] extraEntries)
    {
        var store = new InMemorySessionStore();
        var cwd = StoreTestData.TempDir("cwd-");
        var projectKey = ClaudeSessions.ProjectKeyForDirectory(cwd);
        var sessionId = Guid.NewGuid().ToString();
        var entries = new List<JsonElement>
        {
            StoreTestData.Entry("user", Guid.NewGuid().ToString(), null, sessionId, "hello")
        };
        entries.AddRange(extraEntries.Select(StoreTestData.E));
        await store.AppendAsync(new SessionKey(projectKey, sessionId), entries);
        return (store, sessionId, cwd, projectKey);
    }

    [Fact]
    public async Task Resume_WritesTranscriptAndAuth_ThenCleanupRemovesDir()
    {
        var (store, sessionId, cwd, projectKey) = await SeedAsync();
        var callerConfig = StoreTestData.TempDir("caller-");
        await File.WriteAllTextAsync(Path.Combine(callerConfig, ".credentials.json"),
            """{"claudeAiOauth":{"accessToken":"at","refreshToken":"rt"}}""");
        await File.WriteAllTextAsync(Path.Combine(callerConfig, "settings.json"),
            "﻿" + """{"apiKeyHelper":"helper.sh","enabledPlugins":{"x":true},"env":{"CLAUDE_CONFIG_DIR":"/x","KEEP":"1"}}""");

        var options = new ClaudeAgentOptions
        {
            SessionStore = store,
            Resume = sessionId,
            WorkingDirectory = cwd,
            Environment = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = callerConfig }
        };

        var materialized = await SessionResumeMaterializer.MaterializeAsync(options, null, CancellationToken.None);

        Assert.NotNull(materialized);
        Assert.Equal(sessionId, materialized.ResumeSessionId);
        var transcript = Path.Combine(materialized.ConfigDir, "projects", projectKey, sessionId + ".jsonl");
        Assert.Single(File.ReadAllLines(transcript));

        var credentials = JsonNode.Parse(File.ReadAllText(Path.Combine(materialized.ConfigDir, ".credentials.json")))!;
        Assert.Equal("at", credentials["claudeAiOauth"]!["accessToken"]!.GetValue<string>());
        Assert.Null(credentials["claudeAiOauth"]!["refreshToken"]);

        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(materialized.ConfigDir, "settings.json")))!;
        Assert.Equal("helper.sh", settings["apiKeyHelper"]!.GetValue<string>());
        Assert.Null(settings["enabledPlugins"]);
        Assert.Null(settings["env"]!["CLAUDE_CONFIG_DIR"]);
        Assert.Equal("1", settings["env"]!["KEEP"]!.GetValue<string>());

        var applied = SessionResumeMaterializer.Apply(options with { ContinueConversation = true }, materialized);
        Assert.Equal(materialized.ConfigDir, applied.Environment["CLAUDE_CONFIG_DIR"]);
        Assert.False(applied.ContinueConversation);
        Assert.Equal(Path.Combine(materialized.ConfigDir, "projects"),
            SessionResumeMaterializer.ResolveProjectsDir(applied));

        await materialized.CleanupAsync();
        Assert.False(Directory.Exists(materialized.ConfigDir));
    }

    [Fact]
    public async Task NothingToMaterialize_ReturnsNull()
    {
        var (store, _, cwd, _) = await SeedAsync();
        var options = new ClaudeAgentOptions { SessionStore = store, WorkingDirectory = cwd };

        Assert.Null(await SessionResumeMaterializer.MaterializeAsync(options, null, default));
        Assert.Null(await SessionResumeMaterializer.MaterializeAsync(
            options with { Resume = "not-a-uuid" }, null, default));
        Assert.Null(await SessionResumeMaterializer.MaterializeAsync(
            options with { Resume = Guid.NewGuid().ToString() }, null, default));
        Assert.Null(await SessionResumeMaterializer.MaterializeAsync(
            new ClaudeAgentOptions { Resume = Guid.NewGuid().ToString() }, null, default));
    }

    [Fact]
    public async Task Continue_PicksMostRecentNonSidechainSession()
    {
        var (store, olderId, cwd, projectKey) = await SeedAsync();
        var sidechainId = Guid.NewGuid().ToString();
        await store.AppendAsync(new SessionKey(projectKey, sidechainId),
            [StoreTestData.Entry("user", Guid.NewGuid().ToString(), null, sidechainId, "sub", """{"isSidechain":true}""")]);

        var options = new ClaudeAgentOptions
        {
            SessionStore = store, ContinueConversation = true, WorkingDirectory = cwd,
            Environment = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = StoreTestData.TempDir("empty-") }
        };
        var materialized = await SessionResumeMaterializer.MaterializeAsync(options, null, default);

        Assert.Equal(olderId, materialized!.ResumeSessionId);
        await materialized.CleanupAsync();
    }

    [Fact]
    public async Task Subagents_AreWrittenWithMetadata_AndUnsafeSubpathsSkipped()
    {
        var (store, sessionId, cwd, projectKey) = await SeedAsync();
        var subKey = new SessionKey(projectKey, sessionId, "subagents/agent-7");
        await store.AppendAsync(subKey,
        [
            StoreTestData.Entry("user", Guid.NewGuid().ToString(), null, sessionId, "task"),
            StoreTestData.E("""{"type":"agent_metadata","agentType":"reviewer","toolUseId":"toolu_1"}""")
        ]);
        await store.AppendAsync(new SessionKey(projectKey, sessionId, "../escape"), [StoreTestData.E("""{"type":"x"}""")]);

        var options = new ClaudeAgentOptions
        {
            SessionStore = store, Resume = sessionId, WorkingDirectory = cwd,
            Environment = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = StoreTestData.TempDir("empty-") }
        };
        var materialized = (await SessionResumeMaterializer.MaterializeAsync(options, null, default))!;

        var sessionDir = Path.Combine(materialized.ConfigDir, "projects", projectKey, sessionId);
        Assert.Single(File.ReadAllLines(Path.Combine(sessionDir, "subagents", "agent-7.jsonl")));
        var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(sessionDir, "subagents", "agent-7.meta.json")))!;
        Assert.Equal("reviewer", meta["agentType"]!.GetValue<string>());
        Assert.Null(meta["type"]);
        Assert.False(File.Exists(Path.Combine(materialized.ConfigDir, "projects", projectKey, "escape.jsonl")));

        await materialized.CleanupAsync();
    }

    [Theory]
    [InlineData("subagents/agent-1", true)]
    [InlineData("", false)]
    [InlineData("../x", false)]
    [InlineData("subagents/../../x", false)]
    [InlineData("/abs", false)]
    [InlineData("\\abs", false)]
    [InlineData("C:x", false)]
    [InlineData("a/./b", false)]
    public void IsSafeSubpath_RejectsTraversal(string subpath, bool expected)
    {
        Assert.Equal(expected, SessionResumeMaterializer.IsSafeSubpath(subpath, Path.Combine(Path.GetTempPath(), "s")));
    }

    [Fact]
    public async Task LoadTimeout_ThrowsSessionStoreException()
    {
        var options = new ClaudeAgentOptions
        {
            SessionStore = new StallingStore(),
            Resume = Guid.NewGuid().ToString(),
            SessionStoreLoadTimeout = TimeSpan.FromMilliseconds(100)
        };

        var ex = await Assert.ThrowsAsync<SessionStoreException>(() =>
            SessionResumeMaterializer.MaterializeAsync(options, null, default));
        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public async Task ClientWithTestTransport_SkipsMaterialization()
    {
        var (store, sessionId, cwd, _) = await SeedAsync();
        var transport = new MockTransport();
        ClaudeAgentOptions? seen = null;
        var client = new ClaudeAgentClient(
            new ClaudeAgentOptions { SessionStore = store, Resume = sessionId, WorkingDirectory = cwd },
            (options, _) =>
            {
                seen = options;
                return transport;
            });

        var sessionTask = client.CreateSessionAsync();
        await Task.Delay(50);
        Assert.False(seen!.Environment.ContainsKey("CLAUDE_CONFIG_DIR"));
        Assert.Equal(sessionId, seen.Resume);
        transport.CompleteMessages();
        await Assert.ThrowsAnyAsync<Exception>(() => sessionTask);
    }

    private sealed class StallingStore : ISessionStore
    {
        public Task AppendAsync(SessionKey key, IReadOnlyList<JsonElement> entries,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<JsonElement>?> LoadAsync(SessionKey key,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return null;
        }
    }
}

[Collection(SessionsTestCollection.Name)]
public class SessionStoreHelperTests
{
    [Fact]
    public async Task ListSessionsFromStore_FastAndSlowPathsAgree()
    {
        var cwd = StoreTestData.TempDir("list-");
        var projectKey = ClaudeSessions.ProjectKeyForDirectory(cwd);
        var store = new InMemorySessionStore();
        var ids = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid().ToString();
            ids.Add(id);
            await store.AppendAsync(new SessionKey(projectKey, id),
                [StoreTestData.Entry("user", Guid.NewGuid().ToString(), null, id, $"prompt {i}")]);
        }

        await ClaudeSessions.RenameSessionViaStoreAsync(store, ids[0], "  Renamed  ", cwd);
        await ClaudeSessions.TagSessionViaStoreAsync(store, ids[1], "wip", cwd);

        var fast = await ClaudeSessions.ListSessionsFromStoreAsync(store, cwd);
        var slow = await ClaudeSessions.ListSessionsFromStoreAsync(new MinimalStore(store), cwd);

        Assert.Equal(3, fast.Count);
        Assert.Equal(fast.Select(s => (s.SessionId, s.Summary, s.Tag)), slow.Select(s => (s.SessionId, s.Summary, s.Tag)));
        Assert.Equal("Renamed", fast.Single(s => s.SessionId == ids[0]).Summary);
        Assert.Equal("wip", fast.Single(s => s.SessionId == ids[1]).Tag);
        Assert.Equal(ids[1], fast[0].SessionId); // most recently modified first

        var page = await ClaudeSessions.ListSessionsFromStoreAsync(store, cwd, limit: 1, offset: 1);
        Assert.Equal(fast[1].SessionId, Assert.Single(page).SessionId);

        var info = await ClaudeSessions.GetSessionInfoFromStoreAsync(store, ids[0], cwd);
        Assert.Equal("Renamed", info!.CustomTitle);
    }

    [Fact]
    public async Task Messages_Fork_Delete_And_Subagents_ViaStore()
    {
        var cwd = StoreTestData.TempDir("msgs-");
        var projectKey = ClaudeSessions.ProjectKeyForDirectory(cwd);
        var store = new InMemorySessionStore();
        var sessionId = Guid.NewGuid().ToString();
        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        var u2 = Guid.NewGuid().ToString();
        await store.AppendAsync(new SessionKey(projectKey, sessionId),
        [
            StoreTestData.Entry("user", u1, null, sessionId, "first"),
            StoreTestData.Entry("assistant", a1, u1, sessionId, "reply"),
            StoreTestData.Entry("user", u2, a1, sessionId, "second")
        ]);
        await store.AppendAsync(new SessionKey(projectKey, sessionId, "subagents/agent-9"),
        [
            StoreTestData.Entry("user", Guid.NewGuid().ToString(), null, sessionId, "sub task"),
            StoreTestData.E("""{"type":"agent_metadata","toolUseId":"toolu_parent","parentAgentId":"agent-0"}""")
        ]);

        var messages = await ClaudeSessions.GetSessionMessagesFromStoreAsync(store, sessionId, cwd);
        Assert.Equal([u1, a1, u2], messages.Select(m => m.Uuid));
        Assert.Equal(2, (await ClaudeSessions.GetSessionMessagesFromStoreAsync(store, sessionId, cwd, limit: 2)).Count);

        Assert.Equal(["9"], await ClaudeSessions.ListSubagentsFromStoreAsync(store, sessionId, cwd));
        var subMessages = await ClaudeSessions.GetSubagentMessagesFromStoreAsync(store, sessionId, "9", cwd);
        Assert.Equal("toolu_parent", Assert.Single(subMessages).ParentToolUseId);
        Assert.Equal("agent-0", subMessages[0].ParentAgentId);

        var fork = await ClaudeSessions.ForkSessionViaStoreAsync(store, sessionId, cwd, upToMessageId: a1,
            title: "Branch");
        var forked = await ClaudeSessions.GetSessionMessagesFromStoreAsync(store, fork.SessionId, cwd);
        Assert.Equal(2, forked.Count);
        Assert.DoesNotContain(forked, m => m.Uuid == u1 || m.Uuid == a1);
        Assert.All(forked, m => Assert.Equal(fork.SessionId, m.SessionId));
        Assert.Equal("Branch", (await ClaudeSessions.GetSessionInfoFromStoreAsync(store, fork.SessionId, cwd))!.CustomTitle);

        await ClaudeSessions.DeleteSessionViaStoreAsync(store, sessionId, cwd);
        Assert.Empty(await ClaudeSessions.GetSessionMessagesFromStoreAsync(store, sessionId, cwd));
        Assert.Empty(await ClaudeSessions.ListSubagentsFromStoreAsync(store, sessionId, cwd));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ClaudeSessions.RenameSessionViaStoreAsync(store, "bad-id", "x", cwd));
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ClaudeSessions.ForkSessionViaStoreAsync(store, Guid.NewGuid().ToString(), cwd));
        await Assert.ThrowsAsync<SessionStoreException>(() =>
            ClaudeSessions.ListSubagentsFromStoreAsync(new RecordingStore(), sessionId, cwd));
    }

    [Fact]
    public async Task ImportSessionToStore_RoundTripsWithDiskReader()
    {
        using var fixture = new ClaudeConfigDirFixture();
        var cwd = StoreTestData.TempDir("import-");
        var projectDir = SessionTestHelpers.MakeProjectDir(fixture.ConfigDir, cwd);
        var sessionId = Guid.NewGuid().ToString();
        var u1 = Guid.NewGuid().ToString();
        var a1 = Guid.NewGuid().ToString();
        SessionTestHelpers.WriteTranscript(projectDir, sessionId,
        [
            SessionTestHelpers.MakeTranscriptEntry("user", u1, null, sessionId, "hi"),
            SessionTestHelpers.MakeTranscriptEntry("assistant", a1, u1, sessionId, "hello")
        ]);
        var subDir = Path.Combine(projectDir, sessionId, "subagents");
        Directory.CreateDirectory(subDir);
        SessionTestHelpers.WriteTranscript(subDir, "agent-3",
            [SessionTestHelpers.MakeTranscriptEntry("user", Guid.NewGuid().ToString(), null, sessionId, "sub")]);
        await File.WriteAllTextAsync(Path.Combine(subDir, "agent-3.meta.json"), """{"toolUseId":"toolu_x","type":"stray"}""");

        var store = new InMemorySessionStore();
        await ClaudeSessions.ImportSessionToStoreAsync(sessionId, store, cwd, batchSize: 1);

        var fromStore = await ClaudeSessions.GetSessionMessagesFromStoreAsync(store, sessionId, cwd);
        var fromDisk = ClaudeSessions.GetSessionMessages(sessionId, cwd);
        Assert.Equal(fromDisk.Select(m => m.Uuid), fromStore.Select(m => m.Uuid));

        var subEntries = store.GetEntries(new SessionKey(Path.GetFileName(projectDir), sessionId, "subagents/agent-3"));
        Assert.Equal("agent_metadata", subEntries[^1].GetProperty("type").GetString());
        Assert.Equal("toolu_x",
            (await ClaudeSessions.GetSubagentMessagesFromStoreAsync(store, sessionId, "3", cwd))[0].ParentToolUseId);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ClaudeSessions.ImportSessionToStoreAsync(Guid.NewGuid().ToString(), store, cwd));
    }
}
