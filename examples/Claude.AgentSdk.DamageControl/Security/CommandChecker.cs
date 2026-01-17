using System.Text.RegularExpressions;

namespace Claude.AgentSdk.DamageControl.Security;

/// <summary>
///     Checks bash commands against security patterns.
/// </summary>
public sealed class CommandChecker
{
    private readonly SecurityConfig _config;
    private readonly PathMatcher _pathMatcher;
    private readonly List<CompiledPattern> _compiledPatterns;

    // Regex patterns to extract paths from commands for different operations
    private static readonly (string Pattern, string Operation)[] WriteOperationPatterns =
    [
        (@">\s*([^\s;|&]+)", "redirect"),                               // > file (write/overwrite)
        (@">>\s*([^\s;|&]+)", "append"),                                // >> file (append)
        (@"\bcp\s+.*?\s+([^\s;|&]+)\s*$", "copy"),                      // cp ... dest
        (@"\bmv\s+.*?\s+([^\s;|&]+)\s*$", "move"),                      // mv ... dest
        (@"\btouch\s+([^\s;|&]+)", "touch"),                            // touch file
        (@"\bmkdir\s+.*?([^\s;|&]+)", "mkdir"),                         // mkdir dir
        (@"\becho\s+.*>\s*([^\s;|&]+)", "echo"),                        // echo ... > file
        (@"\btee\s+(?!-a\b)(?:-[^\s]*\s+)*([^\s;|&]+)", "tee-write"),   // tee file (without -a, overwrites)
        (@"\btee\s+-a\s+(?:-[^\s]*\s+)*([^\s;|&]+)", "tee-append"),     // tee -a file (append)
    ];

    private static readonly (string Pattern, string Operation)[] DeleteOperationPatterns =
    [
        (@"\brm\s+(?:-[^\s]*\s+)*([^\s;|&-][^\s;|&]*)", "rm"),    // rm [flags] file
        (@"\brmdir\s+([^\s;|&]+)", "rmdir"),                       // rmdir dir
        (@"\bunlink\s+([^\s;|&]+)", "unlink"),                     // unlink file
    ];

    private static readonly (string Pattern, string Operation)[] ReadOperationPatterns =
    [
        (@"\bcat\s+([^\s;|&]+)", "cat"),                  // cat file
        (@"\bless\s+([^\s;|&]+)", "less"),                // less file
        (@"\bmore\s+([^\s;|&]+)", "more"),                // more file
        (@"\bhead\s+(?:-[^\s]*\s+)*([^\s;|&]+)", "head"), // head file
        (@"\btail\s+(?:-[^\s]*\s+)*([^\s;|&]+)", "tail"), // tail file
        (@"\bgrep\s+.*?([^\s;|&]+)\s*$", "grep"),         // grep ... file
    ];

    // In-place edit patterns (treated as write operations)
    private static readonly (string Pattern, string Operation)[] InPlaceEditPatterns =
    [
        (@"\bsed\s+-[^\s]*i[^\s]*\s+.*?([^\s;|&]+)\s*$", "sed-inplace"),      // sed -i file
        (@"\bperl\s+-[^\s]*i[^\s]*\s+.*?([^\s;|&]+)\s*$", "perl-inplace"),    // perl -i file
        (@"\bawk\s+-i\s+inplace\s+.*?([^\s;|&]+)\s*$", "awk-inplace"),        // awk -i inplace file
    ];

    // Permission change patterns (treated as write operations)
    private static readonly (string Pattern, string Operation)[] PermissionChangePatterns =
    [
        (@"\bchmod\s+(?:[^\s]+\s+)+([^\s;|&]+)", "chmod"),    // chmod mode file
        (@"\bchown\s+(?:[^\s]+\s+)+([^\s;|&]+)", "chown"),    // chown owner file
        (@"\bchgrp\s+(?:[^\s]+\s+)+([^\s;|&]+)", "chgrp"),    // chgrp group file
    ];

    // Truncate patterns (treated as write operations)
    private static readonly (string Pattern, string Operation)[] TruncatePatterns =
    [
        (@"\btruncate\s+(?:-[^\s]*\s+)*([^\s;|&]+)", "truncate"),    // truncate file
        (@":\s*>\s*([^\s;|&]+)", "truncate-redirect"),               // :> file (bash truncate idiom)
    ];

    private sealed record CompiledPattern(Regex Regex, string Reason, bool Ask);

    public CommandChecker(SecurityConfig config)
    {
        _config = config;
        _pathMatcher = new PathMatcher(config);
        _compiledPatterns = CompilePatterns(config.BashToolPatterns);
    }

    /// <summary>
    ///     Checks a bash command against all security patterns.
    /// </summary>
    public CheckResult Check(string? command)
    {
        if (string.IsNullOrEmpty(command))
            return CheckResult.Allow;

        // 1. Check against bash patterns
        foreach (var pattern in _compiledPatterns)
        {
            if (pattern.Regex.IsMatch(command))
            {
                return pattern.Ask
                    ? CheckResult.AskUser(pattern.Reason)
                    : CheckResult.Block(pattern.Reason);
            }
        }

        // 2. Check paths in command for zero-access (blocks all operations)
        var allPaths = ExtractAllPaths(command);
        foreach (var path in allPaths)
        {
            var result = _pathMatcher.CheckForRead(path);
            if (result.Blocked)
            {
                return result;
            }
        }

        // 3. Check for write operations against read-only paths
        // This includes: direct writes, in-place edits, permission changes, and truncation
        var writePatternSets = new[]
        {
            WriteOperationPatterns,
            InPlaceEditPatterns,
            PermissionChangePatterns,
            TruncatePatterns
        };

        foreach (var patternSet in writePatternSets)
        {
            var writePaths = ExtractPathsForOperation(command, patternSet);
            foreach (var path in writePaths)
            {
                var result = _pathMatcher.CheckForEdit(path);
                if (result.Blocked)
                {
                    return result;
                }
            }
        }

        // 4. Check for delete operations against no-delete paths
        var deletePaths = ExtractPathsForOperation(command, DeleteOperationPatterns);
        foreach (var path in deletePaths)
        {
            var result = _pathMatcher.CheckForDelete(path);
            if (result.Blocked)
            {
                return result;
            }
        }

        return CheckResult.Allow;
    }

    private static List<CompiledPattern> CompilePatterns(IEnumerable<BashPattern> patterns)
    {
        var compiled = new List<CompiledPattern>();

        foreach (var pattern in patterns)
        {
            try
            {
                var regex = new Regex(pattern.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                compiled.Add(new CompiledPattern(regex, pattern.Reason, pattern.Ask));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DamageControl] Warning: Invalid pattern '{pattern.Pattern}': {ex.Message}");
            }
        }

        return compiled;
    }

    private static HashSet<string> ExtractPathsForOperation(
        string command,
        (string Pattern, string Operation)[] patterns)
    {
        var paths = new HashSet<string>();

        foreach (var (pattern, _) in patterns)
        {
            try
            {
                var matches = Regex.Matches(command, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var path = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(path) && !path.StartsWith("-"))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
            catch
            {
                // Ignore pattern errors
            }
        }

        return paths;
    }

    private static HashSet<string> ExtractAllPaths(string command)
    {
        var paths = new HashSet<string>();

        // Combine all operation patterns
        var allPatterns = WriteOperationPatterns
            .Concat(DeleteOperationPatterns)
            .Concat(ReadOperationPatterns)
            .Concat(InPlaceEditPatterns)
            .Concat(PermissionChangePatterns)
            .Concat(TruncatePatterns);

        foreach (var (pattern, _) in allPatterns)
        {
            try
            {
                var matches = Regex.Matches(command, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var path = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(path) && !path.StartsWith("-"))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
            catch
            {
                // Ignore pattern errors
            }
        }

        return paths;
    }
}
