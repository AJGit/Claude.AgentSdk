using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Claude.AgentSdk.Exceptions;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     A store-backed resume written to a temporary directory laid out like <c>~/.claude/</c>.
/// </summary>
/// <param name="ConfigDir">The temporary directory; point the CLI at it via <c>CLAUDE_CONFIG_DIR</c>.</param>
/// <param name="ResumeSessionId">Session id to pass as <c>--resume</c> (resolved for continue-conversation).</param>
internal sealed record MaterializedResume(string ConfigDir, string ResumeSessionId)
{
    /// <summary>
    ///     Removes <see cref="ConfigDir" /> (best effort; it holds a credentials copy). Call after the CLI exits.
    /// </summary>
    public Task CleanupAsync()
    {
        return SessionResumeMaterializer.DeleteDirectoryWithRetryAsync(ConfigDir);
    }
}

/// <summary>
///     Bridges <see cref="ClaudeAgentOptions.SessionStore" /> and the CLI, which can only resume from a local file:
///     loads the session from the store and writes it (plus subagent transcripts and the caller's auth/user config)
///     into a temporary config directory.
/// </summary>
internal static partial class SessionResumeMaterializer
{
    // User-settings keys that misbehave under a redirected config dir: plugin declarations reconcile against the
    // always-empty temp plugin cache and would re-install every declared marketplace on each resume.
    private static readonly string[] _strippedSettingsKeys = ["enabledPlugins", "extraKnownMarketplaces"];

    /// <summary>
    ///     Returns the options repointed at a materialized config dir: <c>CLAUDE_CONFIG_DIR</c> set, resume set to the
    ///     resolved session id, continue-conversation cleared.
    /// </summary>
    public static ClaudeAgentOptions Apply(ClaudeAgentOptions options, MaterializedResume materialized)
    {
        var environment = new Dictionary<string, string>(options.Environment)
        {
            ["CLAUDE_CONFIG_DIR"] = materialized.ConfigDir
        };

        return options with
        {
            Environment = environment,
            Resume = materialized.ResumeSessionId,
            ContinueConversation = false
        };
    }

    /// <summary>
    ///     The projects directory the CLI will write under for these options (honors <c>CLAUDE_CONFIG_DIR</c> in
    ///     <see cref="ClaudeAgentOptions.Environment" />, then the process environment).
    /// </summary>
    public static string ResolveProjectsDir(ClaudeAgentOptions options)
    {
        var configDir = options.Environment.TryGetValue("CLAUDE_CONFIG_DIR", out var fromOptions) &&
                        !string.IsNullOrEmpty(fromOptions)
            ? fromOptions.Normalize(NormalizationForm.FormC)
            : SessionPaths.GetClaudeConfigHomeDir();
        return Path.Combine(configDir, "projects");
    }

    /// <summary>
    ///     Loads the session from the store and writes it to a temp dir.
    /// </summary>
    /// <returns>
    ///     Null when nothing needs materializing (no store, no resume/continue, empty store entry, or a resume value
    ///     that is not a UUID): the caller then falls through to a normal spawn.
    /// </returns>
    /// <exception cref="SessionStoreException">A store call failed or timed out.</exception>
    public static async Task<MaterializedResume?> MaterializeAsync(ClaudeAgentOptions options, ILogger? logger,
        CancellationToken cancellationToken)
    {
        var store = options.SessionStore;
        if (store is null || (options.Resume is null && !options.ContinueConversation))
        {
            return null;
        }

        var timeout = options.SessionStoreLoadTimeout;
        var projectKey = ClaudeSessions.ProjectKeyForDirectory(options.WorkingDirectory);

        (string SessionId, IReadOnlyList<JsonElement> Entries)? resolved;
        if (options.Resume is not null)
        {
            // The id becomes a path component: reject anything that is not a UUID.
            if (SessionPaths.ValidateUuid(options.Resume) is null)
            {
                return null;
            }

            resolved = await LoadCandidateAsync(store, projectKey, options.Resume, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            resolved = await ResolveContinueCandidateAsync(store, projectKey, timeout, cancellationToken)
                .ConfigureAwait(false);
        }

        if (resolved is not { } session)
        {
            return null;
        }

        var tempBase = Path.Combine(Path.GetTempPath(), "claude-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);
        RestrictToOwner(tempBase, isDirectory: true);

        try
        {
            var projectDir = Path.Combine(tempBase, "projects", projectKey);
            Directory.CreateDirectory(projectDir);
            WriteJsonl(Path.Combine(projectDir, session.SessionId + ".jsonl"), session.Entries);

            // The CLI runs with CLAUDE_CONFIG_DIR=tempBase; seed it with the caller's auth so it can authenticate.
            CopyAuthFiles(tempBase, options.Environment, logger);

            if (store is ISessionStoreSubkeys subkeys)
            {
                await MaterializeSubkeysAsync(store, subkeys, projectDir, projectKey, session.SessionId, timeout,
                    logger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // The dir may already hold a credentials copy; don't leave it behind.
            await DeleteDirectoryWithRetryAsync(tempBase).ConfigureAwait(false);
            throw;
        }

        return new MaterializedResume(tempBase, session.SessionId);
    }

    /// <summary>
    ///     Best-effort recursive delete, retrying briefly when antivirus/indexers hold a handle (common on Windows).
    /// </summary>
    internal static async Task DeleteDirectoryWithRetryAsync(string path, int retries = 4)
    {
        for (var attempt = 0; attempt < retries; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            }
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Give up; the OS temp cleaner will eventually remove it.
        }
    }

    /// <summary>
    ///     Rejects subpaths that are empty, rooted, drive-prefixed, contain '.'/'..' segments or NUL, or resolve
    ///     outside <paramref name="sessionDir" />. Subpaths come from an external store and become file paths.
    /// </summary>
    internal static bool IsSafeSubpath(string subpath, string sessionDir)
    {
        if (string.IsNullOrEmpty(subpath) || subpath.Contains('\0'))
        {
            return false;
        }

        if (subpath.StartsWith('/') || subpath.StartsWith('\\') || Path.IsPathRooted(subpath) ||
            DrivePrefixRegex().IsMatch(subpath))
        {
            return false;
        }

        if (subpath.Split('/', '\\').Any(part => part is "." or ".."))
        {
            return false;
        }

        try
        {
            var root = Path.GetFullPath(sessionDir);
            var target = Path.GetFullPath(Path.Combine(root, subpath) + ".jsonl");
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            return target.StartsWith(rootWithSeparator,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Drops settings keys that misbehave under a redirected config dir (plugin declarations and
    ///     <c>env.CLAUDE_CONFIG_DIR</c>). Content that is not a JSON object is returned untouched.
    /// </summary>
    internal static byte[] StripSettingsForResume(byte[] content)
    {
        JsonObject? parsed;
        try
        {
            // Tolerate a UTF-8 BOM (PowerShell writes one), as the CLI's own settings reader does.
            var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
            parsed = JsonNode.Parse(text) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or DecoderFallbackException)
        {
            return content;
        }

        if (parsed is null)
        {
            return content;
        }

        var stripped = false;
        foreach (var key in _strippedSettingsKeys)
        {
            stripped |= parsed.Remove(key);
        }

        if (parsed["env"] is JsonObject env)
        {
            stripped |= env.Remove("CLAUDE_CONFIG_DIR");
        }

        return stripped ? Encoding.UTF8.GetBytes(parsed.ToJsonString()) : content;
    }

    /// <summary>
    ///     Removes <c>claudeAiOauth.refreshToken</c>: if the redirected CLI refreshed, the single-use refresh token
    ///     would be consumed and the new tokens written where the parent never reads them, revoking its credentials.
    /// </summary>
    internal static string RedactRefreshToken(string credentialsJson)
    {
        try
        {
            if (JsonNode.Parse(credentialsJson) is JsonObject root &&
                root["claudeAiOauth"] is JsonObject oauth &&
                oauth.Remove("refreshToken"))
            {
                return root.ToJsonString();
            }
        }
        catch (JsonException)
        {
            // Unparseable: write through; the CLI will fail to parse it too.
        }

        return credentialsJson;
    }

    private static async Task<(string, IReadOnlyList<JsonElement>)?> LoadCandidateAsync(ISessionStore store,
        string projectKey, string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var entries = await WithTimeoutAsync(
                ct => store.LoadAsync(new SessionKey(projectKey, sessionId), ct),
                timeout, $"SessionStore.LoadAsync() for session {sessionId}", cancellationToken)
            .ConfigureAwait(false);
        return entries is { Count: > 0 } ? (sessionId, entries) : null;
    }

    /// <summary>
    ///     Picks the most recently modified non-sidechain session. Sidechain transcripts are mirrored as top-level
    ///     keys and often have the newest mtime, so each candidate is loaded (needed anyway) and sidechains skipped.
    /// </summary>
    private static async Task<(string, IReadOnlyList<JsonElement>)?> ResolveContinueCandidateAsync(
        ISessionStore store, string projectKey, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (store is not ISessionStoreListing listing)
        {
            // Validated before spawn; kept defensive.
            throw new SessionStoreException(
                "ContinueConversation with SessionStore requires the store to implement ISessionStoreListing");
        }

        var sessions = await WithTimeoutAsync(ct => listing.ListSessionsAsync(projectKey, ct), timeout,
            "SessionStore.ListSessionsAsync()", cancellationToken).ConfigureAwait(false);

        foreach (var candidate in sessions.OrderByDescending(s => s.Mtime))
        {
            if (SessionPaths.ValidateUuid(candidate.SessionId) is null)
            {
                continue;
            }

            var loaded = await LoadCandidateAsync(store, projectKey, candidate.SessionId, timeout,
                cancellationToken).ConfigureAwait(false);
            if (loaded is not { } found)
            {
                continue;
            }

            var first = found.Item2[0];
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("isSidechain", out var sidechain) &&
                sidechain.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            return found;
        }

        return null;
    }

    private static async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> call, TimeSpan timeout,
        string what, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await call(cts.Token).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            throw new SessionStoreException(
                $"{what} timed out after {(int)timeout.TotalMilliseconds}ms during resume materialization", ex);
        }
        catch (Exception ex) when (ex is not SessionStoreException)
        {
            throw new SessionStoreException($"{what} failed during resume materialization: {ex.Message}", ex);
        }
    }

    private static async Task MaterializeSubkeysAsync(ISessionStore store, ISessionStoreSubkeys subkeys,
        string projectDir, string projectKey, string sessionId, TimeSpan timeout, ILogger? logger,
        CancellationToken cancellationToken)
    {
        var sessionDir = Path.Combine(projectDir, sessionId);
        var subpaths = await WithTimeoutAsync(
            ct => subkeys.ListSubkeysAsync(new SessionKey(projectKey, sessionId), ct), timeout,
            $"SessionStore.ListSubkeysAsync() for session {sessionId}", cancellationToken).ConfigureAwait(false);

        foreach (var subpath in subpaths)
        {
            if (!IsSafeSubpath(subpath, sessionDir))
            {
                logger?.LogWarning("[SessionStore] skipping unsafe subpath from ListSubkeysAsync: {Subpath}", subpath);
                continue;
            }

            var entries = await WithTimeoutAsync(
                ct => store.LoadAsync(new SessionKey(projectKey, sessionId, subpath), ct), timeout,
                $"SessionStore.LoadAsync() for session {sessionId} subpath {subpath}",
                cancellationToken).ConfigureAwait(false);
            if (entries is not { Count: > 0 })
            {
                continue;
            }

            // agent_metadata entries describe the .meta.json sidecar (last wins); the rest is transcript.
            var (metadata, transcript) = ClaudeSessions.SplitAgentMetadata(entries);
            var subFile = Path.Combine(sessionDir, subpath) + ".jsonl";
            if (transcript.Count > 0)
            {
                WriteJsonl(subFile, transcript);
            }

            if (metadata is { } meta)
            {
                var content = new JsonObject();
                foreach (var property in meta.EnumerateObject().Where(p => p.Name != "type"))
                {
                    content[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                }

                var metaFile = TranscriptReader.AgentMetadataSidecarPath(subFile);
                Directory.CreateDirectory(Path.GetDirectoryName(metaFile)!);
                File.WriteAllText(metaFile, content.ToJsonString());
                RestrictToOwner(metaFile);
            }
        }
    }

    private static void WriteJsonl(string path, IReadOnlyList<JsonElement> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            foreach (var entry in entries)
            {
                writer.Write(JsonSerializer.Serialize(entry));
                writer.Write('\n');
            }
        }

        RestrictToOwner(path);
    }

    /// <summary>
    ///     Seeds the temp dir with the caller's <c>.credentials.json</c> (refresh token redacted), <c>.claude.json</c>
    ///     and user <c>settings.json</c> / <c>cowork_settings.json</c> (plugin declarations stripped). Missing files
    ///     are fine (API-key auth, etc.).
    /// </summary>
    private static void CopyAuthFiles(string tempBase, IReadOnlyDictionary<string, string> optionsEnvironment,
        ILogger? logger)
    {
        var callerConfigDir = optionsEnvironment.TryGetValue("CLAUDE_CONFIG_DIR", out var fromOptions) &&
                              !string.IsNullOrEmpty(fromOptions)
            ? fromOptions
            : Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sourceConfigDir = string.IsNullOrEmpty(callerConfigDir) ? Path.Combine(home, ".claude") : callerConfigDir;

        var credentials = ReadIfPresent(Path.Combine(sourceConfigDir, ".credentials.json"), logger) is { } bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;

        // macOS keeps OAuth tokens in the Keychain; the redirected config dir changes the service-name suffix,
        // so populate the plaintext fallback file from the parent's Keychain entry.
        if (string.IsNullOrEmpty(callerConfigDir) &&
            string.IsNullOrEmpty(GetEnv(optionsEnvironment, "ANTHROPIC_API_KEY")) &&
            string.IsNullOrEmpty(GetEnv(optionsEnvironment, "CLAUDE_CODE_OAUTH_TOKEN")) &&
            ReadKeychainCredentials() is { } keychain)
        {
            credentials = keychain;
        }

        if (credentials is not null)
        {
            var target = Path.Combine(tempBase, ".credentials.json");
            File.WriteAllText(target, RedactRefreshToken(credentials));
            RestrictToOwner(target);
        }

        var claudeJson = string.IsNullOrEmpty(callerConfigDir)
            ? Path.Combine(home, ".claude.json")
            : Path.Combine(callerConfigDir, ".claude.json");
        CopyIfPresent(claudeJson, Path.Combine(tempBase, ".claude.json"), null, logger);

        // User settings carry apiKeyHelper (another auth mechanism) plus env/hooks/permissions.
        foreach (var name in new[] { "settings.json", "cowork_settings.json" })
        {
            CopyIfPresent(Path.Combine(sourceConfigDir, name), Path.Combine(tempBase, name), StripSettingsForResume,
                logger);
        }
    }

    private static string? GetEnv(IReadOnlyDictionary<string, string> optionsEnvironment, string name)
    {
        return optionsEnvironment.TryGetValue(name, out var value) ? value : Environment.GetEnvironmentVariable(name);
    }

    private static byte[]? ReadIfPresent(string path, ILogger? logger)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort enrichment: an unreadable file must not abort the resume.
            logger?.LogWarning("[SessionStore] resume: skipping {Path} ({Error})", path, ex.Message);
            return null;
        }
    }

    private static void CopyIfPresent(string source, string target, Func<byte[], byte[]>? transform, ILogger? logger)
    {
        if (ReadIfPresent(source, logger) is not { } content)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(target, transform is null ? content : transform(content));
            RestrictToOwner(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Don't leave a truncated file behind for the CLI to misparse.
            try
            {
                File.Delete(target);
            }
            catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException)
            {
                // Ignore
            }

            logger?.LogWarning("[SessionStore] resume: skipping {Path} ({Error})", source, ex.Message);
        }
    }

    private static string? ReadKeychainCredentials()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            var user = Environment.GetEnvironmentVariable("USER") ?? Environment.UserName;
            using var process = Process.Start(new ProcessStartInfo("security")
            {
                ArgumentList = { "find-generic-password", "-a", user, "-w", "-s", "Claude Code-credentials" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000) || process.ExitCode != 0)
            {
                return null;
            }

            output = output.Trim();
            return output.Length > 0 ? output : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void RestrictToOwner(string path, bool isDirectory = false)
    {
        if (OperatingSystem.IsWindows())
        {
            // The per-user temp directory is already ACL-restricted to the current user.
            return;
        }

        try
        {
            File.SetUnixFileMode(path, isDirectory
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort
        }
    }

    [GeneratedRegex("^[A-Za-z]:")]
    private static partial Regex DrivePrefixRegex();
}
