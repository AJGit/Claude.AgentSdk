namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Session metadata returned by <see cref="ClaudeSessions.ListSessions" />.
/// </summary>
/// <remarks>
///     Contains only data extractable from stat + head/tail reads — no full JSONL parsing required.
/// </remarks>
/// <param name="SessionId">Unique session identifier (UUID).</param>
/// <param name="Summary">
///     Display title for the session — custom title, auto-generated summary, or first prompt.
/// </param>
/// <param name="LastModified">Last modified time in milliseconds since epoch.</param>
public sealed record SdkSessionInfo(string SessionId, string Summary, long LastModified)
{
    /// <summary>
    ///     Session file size in bytes. Only populated for local JSONL storage.
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    ///     Session title — user-set custom title or AI-generated title.
    /// </summary>
    public string? CustomTitle { get; init; }

    /// <summary>
    ///     First meaningful user prompt in the session.
    /// </summary>
    public string? FirstPrompt { get; init; }

    /// <summary>
    ///     Git branch at the end of the session.
    /// </summary>
    public string? GitBranch { get; init; }

    /// <summary>
    ///     Working directory for the session.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    ///     User-set session tag.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    ///     Creation time in milliseconds since epoch, extracted from the first entry's ISO
    ///     timestamp field. More reliable than a filesystem birth time, which is unsupported on
    ///     some filesystems.
    /// </summary>
    public long? CreatedAt { get; init; }
}

/// <summary>
///     A user or assistant message from a session transcript.
/// </summary>
/// <remarks>
///     Returned by <see cref="ClaudeSessions.GetSessionMessages" /> and
///     <see cref="ClaudeSessions.GetSubagentMessages" /> for reading historical session data.
/// </remarks>
/// <param name="Type">Message type — <c>"user"</c> or <c>"assistant"</c>.</param>
/// <param name="Uuid">Unique message identifier.</param>
/// <param name="SessionId">ID of the session this message belongs to.</param>
/// <param name="Message">Raw Anthropic API message (role, content, etc.), or <c>null</c> if absent.</param>
public sealed record SessionMessage(string Type, string Uuid, string SessionId, JsonElement? Message)
{
    /// <summary>
    ///     For messages returned by <see cref="ClaudeSessions.GetSubagentMessages" />, the id of
    ///     the Agent tool-use block in the parent session that spawned the subagent (recovered
    ///     from the subagent's metadata sidecar; <c>null</c> if that metadata is unavailable).
    ///     Always <c>null</c> for top-level <see cref="ClaudeSessions.GetSessionMessages" /> results.
    /// </summary>
    public string? ParentToolUseId { get; init; }

    /// <summary>
    ///     For subagent messages, the agent id of the subagent that spawned this subagent, or
    ///     <c>null</c> if it was spawned by the main session (or the metadata is unavailable).
    ///     Always <c>null</c> for top-level session messages.
    /// </summary>
    public string? ParentAgentId { get; init; }
}

/// <summary>
///     Result of a <see cref="ClaudeSessions.ForkSession" /> operation.
/// </summary>
/// <param name="SessionId">UUID of the new forked session.</param>
public sealed record ForkSessionResult(string SessionId);
