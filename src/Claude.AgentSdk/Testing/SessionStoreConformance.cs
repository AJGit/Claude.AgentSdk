using System.Text.Json.Nodes;
using Claude.AgentSdk.Sessions;

namespace Claude.AgentSdk.Testing;

/// <summary>
///     Optional <see cref="ISessionStore" /> capabilities that <see cref="SessionStoreConformance" /> can skip.
/// </summary>
[Flags]
public enum SessionStoreCapability
{
    /// <summary>No capability.</summary>
    None = 0,

    /// <summary><see cref="ISessionStoreListing" />.</summary>
    ListSessions = 1,

    /// <summary><see cref="ISessionStoreSummaries" />.</summary>
    ListSessionSummaries = 2,

    /// <summary><see cref="ISessionStoreDeletion" />.</summary>
    Delete = 4,

    /// <summary><see cref="ISessionStoreSubkeys" />.</summary>
    ListSubkeys = 8
}

/// <summary>
///     Thrown when a store violates a <see cref="SessionStoreConformance" /> contract.
/// </summary>
public sealed class SessionStoreConformanceException(string message) : Exception(message);

/// <summary>
///     Shared conformance suite for <see cref="ISessionStore" /> adapters: asserts the 14 behavioral contracts every
///     adapter must satisfy (the same contracts as the Python and TypeScript SDKs).
/// </summary>
/// <example>
///     <code>
/// [Fact]
/// public Task MyRedisStore_IsConformant() =>
///     SessionStoreConformance.RunAsync(() => new MyRedisStore(connection));
/// </code>
/// </example>
public static class SessionStoreConformance
{
    private static readonly SessionKey _key = new("proj", "sess");

    /// <summary>
    ///     Runs every contract; contracts for optional capabilities run only when the store implements them and
    ///     they are not listed in <paramref name="skip" />.
    /// </summary>
    /// <param name="makeStore">Creates a fresh, empty store. Invoked once per contract for isolation.</param>
    /// <param name="skip">Optional capabilities to skip even if implemented.</param>
    /// <exception cref="SessionStoreConformanceException">A contract was violated.</exception>
    public static Task RunAsync(Func<ISessionStore> makeStore,
        SessionStoreCapability skip = SessionStoreCapability.None)
    {
        return RunAsync(() => Task.FromResult(makeStore()), skip);
    }

    /// <inheritdoc cref="RunAsync(Func{ISessionStore}, SessionStoreCapability)" />
    public static async Task RunAsync(Func<Task<ISessionStore>> makeStore,
        SessionStoreCapability skip = SessionStoreCapability.None)
    {
        var probe = await makeStore().ConfigureAwait(false);
        var hasListSessions = probe is ISessionStoreListing && !skip.HasFlag(SessionStoreCapability.ListSessions);
        var hasSummaries = probe is ISessionStoreSummaries &&
                           !skip.HasFlag(SessionStoreCapability.ListSessionSummaries);
        var hasDelete = probe is ISessionStoreDeletion && !skip.HasFlag(SessionStoreCapability.Delete);
        var hasSubkeys = probe is ISessionStoreSubkeys && !skip.HasFlag(SessionStoreCapability.ListSubkeys);

        // --- Required: append + load ---

        // 1. append then load returns the same entries in the same order (deep-equal, not byte-equal)
        var store = await makeStore().ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"uuid":"b","n":1}"""), E("""{"uuid":"a","n":2}""")])
            .ConfigureAwait(false);
        AssertEntries(await store.LoadAsync(_key).ConfigureAwait(false),
            ["""{"uuid":"b","n":1}""", """{"uuid":"a","n":2}"""], "1. append then load");

        // 2. load of an unknown key (or subpath) returns null
        store = await makeStore().ConfigureAwait(false);
        Check(await store.LoadAsync(new SessionKey("proj", "nope")).ConfigureAwait(false) is null,
            "2. load of an unknown key must return null");
        await store.AppendAsync(_key, [E("""{"uuid":"x","n":1}""")]).ConfigureAwait(false);
        Check(await store.LoadAsync(_key with { Subpath = "nope" }).ConfigureAwait(false) is null,
            "2. load of an unknown subpath must return null");

        // 3. multiple appends preserve call order
        store = await makeStore().ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"uuid":"z","n":1}""")]).ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"uuid":"a","n":2}"""), E("""{"uuid":"m","n":3}""")])
            .ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"uuid":"b","n":4}""")]).ConfigureAwait(false);
        AssertEntries(await store.LoadAsync(_key).ConfigureAwait(false),
        [
            """{"uuid":"z","n":1}""", """{"uuid":"a","n":2}""", """{"uuid":"m","n":3}""", """{"uuid":"b","n":4}"""
        ], "3. multiple appends preserve order");

        // 4. append([]) is a no-op
        store = await makeStore().ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"uuid":"a","n":1}""")]).ConfigureAwait(false);
        await store.AppendAsync(_key, []).ConfigureAwait(false);
        AssertEntries(await store.LoadAsync(_key).ConfigureAwait(false), ["""{"uuid":"a","n":1}"""],
            "4. empty append is a no-op");

        // 5. subpath keys are stored independently of the main transcript
        store = await makeStore().ConfigureAwait(false);
        var sub = _key with { Subpath = "subagents/agent-1" };
        await store.AppendAsync(_key, [E("""{"uuid":"m","n":1}""")]).ConfigureAwait(false);
        await store.AppendAsync(sub, [E("""{"uuid":"s","n":1}""")]).ConfigureAwait(false);
        AssertEntries(await store.LoadAsync(_key).ConfigureAwait(false), ["""{"uuid":"m","n":1}"""],
            "5. main key independent of subpath");
        AssertEntries(await store.LoadAsync(sub).ConfigureAwait(false), ["""{"uuid":"s","n":1}"""],
            "5. subpath key independent of main");

        // 6. project key isolation
        store = await makeStore().ConfigureAwait(false);
        await store.AppendAsync(new SessionKey("A", "s1"), [E("""{"from":"A"}""")]).ConfigureAwait(false);
        await store.AppendAsync(new SessionKey("B", "s1"), [E("""{"from":"B"}""")]).ConfigureAwait(false);
        AssertEntries(await store.LoadAsync(new SessionKey("A", "s1")).ConfigureAwait(false), ["""{"from":"A"}"""],
            "6. project isolation (A)");
        AssertEntries(await store.LoadAsync(new SessionKey("B", "s1")).ConfigureAwait(false), ["""{"from":"B"}"""],
            "6. project isolation (B)");
        if (hasListSessions)
        {
            var listing = (ISessionStoreListing)store;
            Check((await listing.ListSessionsAsync("A").ConfigureAwait(false)).Count == 1,
                "6. ListSessionsAsync(\"A\") must return one session");
            Check((await listing.ListSessionsAsync("B").ConfigureAwait(false)).Count == 1,
                "6. ListSessionsAsync(\"B\") must return one session");
        }

        if (hasListSessions)
        {
            // 7. list sessions for a project, with epoch-ms mtimes
            store = await makeStore().ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("proj", "a"), [E("""{"n":1}""")]).ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("proj", "b"), [E("""{"n":1}""")]).ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("other", "c"), [E("""{"n":1}""")]).ConfigureAwait(false);
            var listing = (ISessionStoreListing)store;
            var sessions = await listing.ListSessionsAsync("proj").ConfigureAwait(false);
            Check(sessions.Select(s => s.SessionId).Order(StringComparer.Ordinal).SequenceEqual(["a", "b"]),
                "7. ListSessionsAsync must return exactly the project's sessions");
            // > 1e12 rules out epoch seconds.
            Check(sessions.All(s => s.Mtime > 1_000_000_000_000), "7. mtime must be Unix epoch milliseconds");
            Check((await listing.ListSessionsAsync("never-appended-project").ConfigureAwait(false)).Count == 0,
                "7. ListSessionsAsync for an unknown project must be empty");

            // 8. list sessions excludes subagent subpaths
            store = await makeStore().ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("proj", "main"), [E("""{"n":1}""")]).ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("proj", "main", "subagents/agent-1"), [E("""{"n":1}""")])
                .ConfigureAwait(false);
            Check((await ((ISessionStoreListing)store).ListSessionsAsync("proj").ConfigureAwait(false))
                .Select(s => s.SessionId).SequenceEqual(["main"]),
                "8. ListSessionsAsync must exclude subagent subpaths");
        }

        if (hasSummaries)
        {
            await CheckSummariesAsync(await makeStore().ConfigureAwait(false), hasListSessions, hasDelete)
                .ConfigureAwait(false);
        }

        if (hasDelete)
        {
            await CheckDeleteAsync(makeStore, hasListSessions, hasSubkeys).ConfigureAwait(false);
        }

        if (hasSubkeys)
        {
            // 12. list subkeys returns the session's subpaths only
            store = await makeStore().ConfigureAwait(false);
            await store.AppendAsync(_key, [E("""{"n":1}""")]).ConfigureAwait(false);
            await store.AppendAsync(_key with { Subpath = "subagents/agent-1" }, [E("""{"n":1}""")])
                .ConfigureAwait(false);
            await store.AppendAsync(_key with { Subpath = "subagents/agent-2" }, [E("""{"n":1}""")])
                .ConfigureAwait(false);
            await store.AppendAsync(new SessionKey("proj", "other-sess", "subagents/agent-x"), [E("""{"n":1}""")])
                .ConfigureAwait(false);
            var subkeys = await ((ISessionStoreSubkeys)store).ListSubkeysAsync(_key).ConfigureAwait(false);
            Check(subkeys.Order(StringComparer.Ordinal).SequenceEqual(["subagents/agent-1", "subagents/agent-2"]),
                "12. ListSubkeysAsync must return exactly this session's subpaths");

            // 13. list subkeys excludes the main transcript
            store = await makeStore().ConfigureAwait(false);
            await store.AppendAsync(_key, [E("""{"n":1}""")]).ConfigureAwait(false);
            var subkeysStore = (ISessionStoreSubkeys)store;
            Check((await subkeysStore.ListSubkeysAsync(_key).ConfigureAwait(false)).Count == 0,
                "13. ListSubkeysAsync must exclude the main transcript");
            Check((await subkeysStore.ListSubkeysAsync(new SessionKey("proj", "never-appended"))
                    .ConfigureAwait(false)).Count == 0,
                "13. ListSubkeysAsync for an unknown session must be empty");
        }
    }

    // 14. summaries round-trip through the fold, share a clock with ListSessionsAsync, and ignore subagents
    private static async Task CheckSummariesAsync(ISessionStore store, bool hasListSessions, bool hasDelete)
    {
        var summaries = (ISessionStoreSummaries)store;
        var key = new SessionKey("proj", "summ-sess");
        await store.AppendAsync(key,
        [
            E("""{"timestamp":"2024-01-01T00:00:00.000Z","customTitle":"first"}"""),
            E("""{"timestamp":"2024-01-01T00:00:01.000Z"}""")
        ]).ConfigureAwait(false);
        await store.AppendAsync(key, [E("""{"timestamp":"2024-01-01T00:00:02.000Z","customTitle":"second"}""")])
            .ConfigureAwait(false);
        await store.AppendAsync(new SessionKey("other", "elsewhere"), [E("""{"timestamp":"2024-01-01T00:00:00.000Z"}""")])
            .ConfigureAwait(false);

        var list = await summaries.ListSessionSummariesAsync("proj").ConfigureAwait(false);
        Check(list.Select(s => s.SessionId).SequenceEqual(["summ-sess"]),
            "14. ListSessionSummariesAsync must return exactly the project's sessions");
        var summary = list[0];
        Check(summary.Mtime > 1_000_000_000_000, "14. summary mtime must be Unix epoch milliseconds");

        if (hasListSessions)
        {
            var listed = await ((ISessionStoreListing)store).ListSessionsAsync("proj").ConfigureAwait(false);
            Check(summary.Mtime >= listed.Single(s => s.SessionId == "summ-sess").Mtime,
                "14. summary mtime must be storage write time on the same clock as ListSessionsAsync");
        }

        var refolded = SessionSummary.Fold(summary, key, [E("""{"timestamp":"2024-01-01T00:00:03.000Z"}""")]);
        Check(refolded.SessionId == "summ-sess" && refolded.Mtime == summary.Mtime,
            "14. persisted summary must round-trip through SessionSummary.Fold");

        await store.AppendAsync(key with { Subpath = "subagents/agent-1" },
            [E("""{"timestamp":"2024-01-01T00:00:09.000Z","customTitle":"subagent"}""")]).ConfigureAwait(false);
        var afterSub = (await summaries.ListSessionSummariesAsync("proj").ConfigureAwait(false))
            .Single(s => s.SessionId == "summ-sess");
        Check(JsonNode.DeepEquals(afterSub.Data, summary.Data),
            "14. subagent appends must not affect the main session's summary");
        Check((await summaries.ListSessionSummariesAsync("never-appended-project").ConfigureAwait(false)).Count == 0,
            "14. ListSessionSummariesAsync for an unknown project must be empty");

        if (hasDelete)
        {
            await ((ISessionStoreDeletion)store).DeleteAsync(key).ConfigureAwait(false);
            Check((await summaries.ListSessionSummariesAsync("proj").ConfigureAwait(false)).Count == 0,
                "14. deleting a session must remove its summary");
        }
    }

    private static async Task CheckDeleteAsync(Func<Task<ISessionStore>> makeStore, bool hasListSessions,
        bool hasSubkeys)
    {
        // 9. delete main then load returns null (deleting a never-written key is fine)
        var store = await makeStore().ConfigureAwait(false);
        var deletion = (ISessionStoreDeletion)store;
        await deletion.DeleteAsync(new SessionKey("proj", "never-written")).ConfigureAwait(false);
        await store.AppendAsync(_key, [E("""{"n":1}""")]).ConfigureAwait(false);
        await deletion.DeleteAsync(_key).ConfigureAwait(false);
        Check(await store.LoadAsync(_key).ConfigureAwait(false) is null, "9. load after delete must return null");

        // 10. deleting the main key cascades to subkeys only of that session
        store = await makeStore().ConfigureAwait(false);
        deletion = (ISessionStoreDeletion)store;
        var sub1 = _key with { Subpath = "subagents/agent-1" };
        var sub2 = _key with { Subpath = "subagents/agent-2" };
        var other = new SessionKey("proj", "sess2");
        var otherProject = new SessionKey("other-proj", _key.SessionId);
        foreach (var k in new[] { _key, sub1, sub2, other, otherProject })
        {
            await store.AppendAsync(k, [E("""{"n":1}""")]).ConfigureAwait(false);
        }

        await deletion.DeleteAsync(_key).ConfigureAwait(false);
        Check(await store.LoadAsync(_key).ConfigureAwait(false) is null, "10. main key must be deleted");
        Check(await store.LoadAsync(sub1).ConfigureAwait(false) is null, "10. delete must cascade to subkeys");
        Check(await store.LoadAsync(sub2).ConfigureAwait(false) is null, "10. delete must cascade to subkeys");
        Check((await store.LoadAsync(other).ConfigureAwait(false))?.Count == 1,
            "10. delete must not touch other sessions");
        Check((await store.LoadAsync(otherProject).ConfigureAwait(false))?.Count == 1,
            "10. delete must not touch other projects");
        if (hasSubkeys)
        {
            Check((await ((ISessionStoreSubkeys)store).ListSubkeysAsync(_key).ConfigureAwait(false)).Count == 0,
                "10. no subkeys may remain after a cascading delete");
        }

        if (hasListSessions)
        {
            Check((await ((ISessionStoreListing)store).ListSessionsAsync(_key.ProjectKey).ConfigureAwait(false))
                .All(s => s.SessionId != _key.SessionId), "10. deleted session must not be listed");
        }

        // 11. deleting a subpath removes only that subkey
        store = await makeStore().ConfigureAwait(false);
        deletion = (ISessionStoreDeletion)store;
        foreach (var k in new[] { _key, sub1, sub2 })
        {
            await store.AppendAsync(k, [E("""{"n":1}""")]).ConfigureAwait(false);
        }

        await deletion.DeleteAsync(sub1).ConfigureAwait(false);
        Check(await store.LoadAsync(sub1).ConfigureAwait(false) is null, "11. targeted subpath must be deleted");
        Check((await store.LoadAsync(sub2).ConfigureAwait(false))?.Count == 1,
            "11. sibling subpath must survive a targeted delete");
        Check((await store.LoadAsync(_key).ConfigureAwait(false))?.Count == 1,
            "11. main transcript must survive a targeted subpath delete");
        if (hasSubkeys)
        {
            Check((await ((ISessionStoreSubkeys)store).ListSubkeysAsync(_key).ConfigureAwait(false))
                .SequenceEqual(["subagents/agent-2"]), "11. only the sibling subkey may remain");
        }
    }

    /// <summary>
    ///     A test entry satisfying the entry shape (<c>type</c> is required; its value is irrelevant).
    /// </summary>
    private static JsonElement E(string json)
    {
        var node = (JsonObject)JsonNode.Parse(json)!;
        var entry = new JsonObject { ["type"] = "x" };
        foreach (var (name, value) in node)
        {
            entry[name] = value?.DeepClone();
        }

        return JsonSerializer.SerializeToElement(entry);
    }

    private static void AssertEntries(IReadOnlyList<JsonElement>? actual, string[] expectedJson, string contract)
    {
        var expected = expectedJson.Select(E).ToList();
        var equal = actual is not null && actual.Count == expected.Count &&
                    actual.Zip(expected).All(pair =>
                        JsonNode.DeepEquals(JsonNode.Parse(pair.First.GetRawText()),
                            JsonNode.Parse(pair.Second.GetRawText())));
        Check(equal,
            $"{contract}: expected [{string.Join(",", expected.Select(e => e.GetRawText()))}] but got " +
            (actual is null ? "null" : $"[{string.Join(",", actual.Select(e => e.GetRawText()))}]"));
    }

    private static void Check(bool condition, string contract)
    {
        if (!condition)
        {
            throw new SessionStoreConformanceException($"SessionStore contract violated: {contract}");
        }
    }
}
