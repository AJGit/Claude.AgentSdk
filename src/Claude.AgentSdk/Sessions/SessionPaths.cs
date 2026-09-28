using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Claude.AgentSdk.Sessions;

/// <summary>
///     Path resolution and sanitization helpers shared by the session-listing and
///     session-mutation implementations. Mirrors the Python SDK's
///     <c>_internal/sessions.py</c> path-handling helpers.
/// </summary>
internal static partial class SessionPaths
{
    /// <summary>Size of the head/tail buffer for lite metadata reads.</summary>
    internal const int LiteReadBufSize = 65536;

    /// <summary>
    ///     Maximum length for a single filesystem path component. Most filesystems limit
    ///     individual components to 255 bytes. 200 leaves room for the hash suffix and separator.
    /// </summary>
    internal const int MaxSanitizedLength = 200;

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRegex();

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex SanitizeRegex();

    /// <summary>Returns the string if it is a valid UUID, else <c>null</c>.</summary>
    internal static string? ValidateUuid(string maybeUuid)
    {
        return UuidRegex().IsMatch(maybeUuid) ? maybeUuid : null;
    }

    /// <summary>32-bit integer hash to base36, matching the CLI's directory naming.</summary>
    internal static string SimpleHash(string s)
    {
        var h = 0;
        foreach (var ch in s)
        {
            unchecked
            {
                h = (h << 5) - h + ch;
            }
        }

        long abs = h < 0 ? -(long)h : h;
        if (abs == 0)
        {
            return "0";
        }

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        var n = abs;
        while (n > 0)
        {
            sb.Insert(0, digits[(int)(n % 36)]);
            n /= 36;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Makes a string safe for use as a directory name. Replaces all non-alphanumeric
    ///     characters with hyphens. For strings exceeding <see cref="MaxSanitizedLength" />,
    ///     truncates and appends a hash suffix.
    /// </summary>
    internal static string SanitizePath(string name)
    {
        var sanitized = SanitizeRegex().Replace(name, "-");
        if (sanitized.Length <= MaxSanitizedLength)
        {
            return sanitized;
        }

        var h = SimpleHash(name);
        return $"{sanitized[..MaxSanitizedLength]}-{h}";
    }

    /// <summary>Returns the Claude config directory (respects CLAUDE_CONFIG_DIR).</summary>
    internal static string GetClaudeConfigHomeDir()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            return configDir.Normalize(NormalizationForm.FormC);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude").Normalize(NormalizationForm.FormC);
    }

    /// <summary>Returns the projects directory (<c>&lt;config-dir&gt;/projects</c>).</summary>
    internal static string GetProjectsDir()
    {
        return Path.Combine(GetClaudeConfigHomeDir(), "projects");
    }

    /// <summary>Returns the sanitized project directory path for a given project path.</summary>
    internal static string GetProjectDir(string projectPath)
    {
        return Path.Combine(GetProjectsDir(), SanitizePath(projectPath));
    }

    /// <summary>Resolves a directory path to its canonical form using realpath + NFC.</summary>
    internal static string CanonicalizePath(string d)
    {
        try
        {
            var resolved = Path.GetFullPath(d);
            if (Directory.Exists(resolved))
            {
                var target = Directory.ResolveLinkTarget(resolved, returnFinalTarget: true);
                if (target is not null)
                {
                    resolved = target.FullName;
                }
            }
            else if (File.Exists(resolved))
            {
                var target = File.ResolveLinkTarget(resolved, returnFinalTarget: true);
                if (target is not null)
                {
                    resolved = target.FullName;
                }
            }

            return resolved.Normalize(NormalizationForm.FormC);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                        or NotSupportedException or ArgumentException)
        {
            return d.Normalize(NormalizationForm.FormC);
        }
    }

    /// <summary>
    ///     Finds the project directory for a given path. Tolerates hash mismatches for long
    ///     paths (&gt;200 chars) by falling back to prefix-based scanning.
    /// </summary>
    internal static string? FindProjectDir(string projectPath)
    {
        var exact = GetProjectDir(projectPath);
        if (Directory.Exists(exact))
        {
            return exact;
        }

        var sanitized = SanitizePath(projectPath);
        if (sanitized.Length <= MaxSanitizedLength)
        {
            return null;
        }

        var prefix = sanitized[..MaxSanitizedLength];
        var projectsDir = GetProjectsDir();
        try
        {
            foreach (var entry in Directory.EnumerateDirectories(projectsDir))
            {
                var name = Path.GetFileName(entry);
                if (name.StartsWith(prefix + "-", StringComparison.Ordinal))
                {
                    return entry;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    /// <summary>
    ///     Returns absolute worktree paths for the git repo containing <paramref name="cwd" />.
    ///     Returns an empty list if git is unavailable or <paramref name="cwd" /> is not in a repo.
    /// </summary>
    internal static IReadOnlyList<string> GetWorktreePaths(string cwd)
    {
        string stdout;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "worktree list --porcelain",
                    WorkingDirectory = cwd,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return [];
            }

            stdout = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0 || string.IsNullOrEmpty(stdout))
            {
                return [];
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception)
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var line in stdout.Split('\n'))
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                var path = line["worktree ".Length..].TrimEnd('\r');
                paths.Add(path.Normalize(NormalizationForm.FormC));
            }
        }

        return paths;
    }
}
