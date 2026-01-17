using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Claude.AgentSdk.DamageControl.Security;

/// <summary>
///     Matches file paths against security patterns.
/// </summary>
public sealed class PathMatcher
{
    private readonly SecurityConfig _config;
    private readonly string _homeDirectory;
    private readonly bool _isCaseInsensitive;

    public PathMatcher(SecurityConfig config)
    {
        _config = config;
        _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _isCaseInsensitive = OperatingSystem.IsWindows();
    }

    /// <summary>
    ///     Checks if a file path can be edited (used by Edit tool).
    /// </summary>
    public CheckResult CheckForEdit(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return CheckResult.Allow;

        var normalizedPath = NormalizePath(filePath);

        // Check zero-access paths (no operations allowed)
        if (MatchesAnyPattern(normalizedPath, _config.ZeroAccessPaths))
        {
            return CheckResult.Block($"Zero-access path: {GetMatchedPattern(normalizedPath, _config.ZeroAccessPaths)}");
        }

        // Check read-only paths (no writes allowed)
        if (MatchesAnyPattern(normalizedPath, _config.ReadOnlyPaths))
        {
            return CheckResult.Block($"Read-only path: {GetMatchedPattern(normalizedPath, _config.ReadOnlyPaths)}");
        }

        return CheckResult.Allow;
    }

    /// <summary>
    ///     Checks if a file path can be written (used by Write tool).
    /// </summary>
    public CheckResult CheckForWrite(string? filePath)
    {
        // Same logic as edit - both are write operations
        return CheckForEdit(filePath);
    }

    /// <summary>
    ///     Checks if a file path can be read (used by Read tool in Bash).
    /// </summary>
    public CheckResult CheckForRead(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return CheckResult.Allow;

        var normalizedPath = NormalizePath(filePath);

        // Only zero-access paths block reads
        if (MatchesAnyPattern(normalizedPath, _config.ZeroAccessPaths))
        {
            return CheckResult.Block($"Zero-access path: {GetMatchedPattern(normalizedPath, _config.ZeroAccessPaths)}");
        }

        return CheckResult.Allow;
    }

    /// <summary>
    ///     Checks if a file path can be deleted (used by Bash tool).
    /// </summary>
    public CheckResult CheckForDelete(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return CheckResult.Allow;

        var normalizedPath = NormalizePath(filePath);

        // Check zero-access paths
        if (MatchesAnyPattern(normalizedPath, _config.ZeroAccessPaths))
        {
            return CheckResult.Block($"Zero-access path: {GetMatchedPattern(normalizedPath, _config.ZeroAccessPaths)}");
        }

        // Check read-only paths (no delete)
        if (MatchesAnyPattern(normalizedPath, _config.ReadOnlyPaths))
        {
            return CheckResult.Block($"Read-only path: {GetMatchedPattern(normalizedPath, _config.ReadOnlyPaths)}");
        }

        // Check no-delete paths
        if (MatchesAnyPattern(normalizedPath, _config.NoDeletePaths))
        {
            return CheckResult.Block($"No-delete path: {GetMatchedPattern(normalizedPath, _config.NoDeletePaths)}");
        }

        return CheckResult.Allow;
    }

    /// <summary>
    ///     Normalizes a path by expanding ~ and converting separators.
    /// </summary>
    public string NormalizePath(string path)
    {
        // Expand home directory
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            path = Path.Combine(_homeDirectory, path[2..]);
        }
        else if (path == "~")
        {
            path = _homeDirectory;
        }

        // Normalize separators
        path = path.Replace('\\', '/');

        return path;
    }

    private bool MatchesAnyPattern(string path, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(path, pattern))
                return true;
        }
        return false;
    }

    private string? GetMatchedPattern(string path, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(path, pattern))
                return pattern;
        }
        return null;
    }

    private bool MatchesPattern(string path, string pattern)
    {
        var normalizedPattern = NormalizePath(pattern);
        var comparison = _isCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // Directory prefix match (e.g., ~/.ssh/, /etc/)
        if (normalizedPattern.EndsWith("/"))
        {
            if (path.StartsWith(normalizedPattern, comparison))
                return true;
            if ((path + "/").StartsWith(normalizedPattern, comparison))
                return true;
        }

        // Exact match
        if (path.Equals(normalizedPattern, comparison))
            return true;

        // Glob pattern matching
        if (IsGlobPattern(normalizedPattern))
        {
            return MatchesGlob(path, normalizedPattern);
        }

        // Filename match (pattern without path matches end of path)
        if (!normalizedPattern.Contains('/'))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Equals(normalizedPattern, comparison))
                return true;

            // Also try glob matching on filename
            if (IsGlobPattern(normalizedPattern) && MatchesGlob(fileName, normalizedPattern))
                return true;
        }

        return false;
    }

    private static bool IsGlobPattern(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?') || pattern.Contains('[');
    }

    private bool MatchesGlob(string path, string pattern)
    {
        try
        {
            // Convert glob pattern to regex for flexible matching
            var regexPattern = GlobToRegex(pattern);
            var options = _isCaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;

            // Try matching against full path
            if (Regex.IsMatch(path, regexPattern, options))
                return true;

            // Try matching against filename only
            var fileName = Path.GetFileName(path);
            if (Regex.IsMatch(fileName, regexPattern, options))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string GlobToRegex(string glob)
    {
        // Escape regex special characters except glob ones
        var escaped = Regex.Escape(glob);

        // Convert glob patterns to regex
        // \*\* -> .* (match across directories)
        escaped = escaped.Replace(@"\*\*", ".*");
        // \* -> [^/]* (match within single directory)
        escaped = escaped.Replace(@"\*", @"[^/]*");
        // \? -> [^/] (match single character)
        escaped = escaped.Replace(@"\?", @"[^/]");

        return "^" + escaped + "$";
    }
}
