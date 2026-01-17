using Claude.AgentSdk.Exceptions;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Resolves the path to the Claude CLI executable.
/// </summary>
internal sealed class CliPathResolver
{
    private static readonly string[] _windowsCliNames = ["claude.exe", "claude.cmd"];
    private static readonly string[] _unixCliNames = ["claude"];
    private readonly string? _explicitPath;
    private readonly ILogger? _logger;

    private CliPathResolver(string? explicitPath, ILogger? logger)
    {
        _explicitPath = explicitPath;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a new resolver instance.
    /// </summary>
    public static CliPathResolver Create(string? explicitPath = null, ILogger? logger = null)
    {
        return new CliPathResolver(explicitPath, logger);
    }

    /// <summary>
    ///     Resolves the CLI path by checking explicit path, PATH environment, and common locations.
    /// </summary>
    public string Resolve()
    {
        // 1. Check explicit path
        if (TryResolveExplicitPath(out var explicitResult))
        {
            return explicitResult;
        }

        var cliNames = GetCliNamesForPlatform();

        // 2. Search PATH environment
        if (TryResolveFromPath(cliNames, out var pathResult))
        {
            return pathResult;
        }

        // 3. Check common locations
        if (TryResolveFromCommonLocations(cliNames, out var commonResult))
        {
            return commonResult;
        }

        throw new CliNotFoundException(
            "Claude CLI not found. Ensure 'claude' is installed and in your PATH. " +
            "Install with: npm install -g @anthropic-ai/claude-code");
    }

    private bool TryResolveExplicitPath(out string result)
    {
        result = string.Empty;

        if (string.IsNullOrEmpty(_explicitPath))
        {
            return false;
        }

        if (File.Exists(_explicitPath))
        {
            result = _explicitPath;
            return true;
        }

        throw new CliNotFoundException($"CLI not found at specified path: {_explicitPath}");
    }

    private bool TryResolveFromPath(string[] cliNames, out string result)
    {
        result = string.Empty;
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(Path.PathSeparator);

        foreach (var dir in pathDirs)
        foreach (var cliName in cliNames)
        {
            var fullPath = Path.Combine(dir, cliName);
            if (File.Exists(fullPath))
            {
                _logger?.LogDebug("Found CLI in PATH: {Path}", fullPath);
                result = fullPath;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveFromCommonLocations(string[] cliNames, out string result)
    {
        result = string.Empty;
        var commonPaths = GetCommonPathsForPlatform(cliNames);

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("Found CLI at common location: {Path}", path);
                result = path;
                return true;
            }
        }

        return false;
    }

    private static string[] GetCliNamesForPlatform()
    {
        return OperatingSystem.IsWindows() ? _windowsCliNames : _unixCliNames;
    }

    private static List<string> GetCommonPathsForPlatform(string[] cliNames)
    {
        var paths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            AddWindowsCommonPaths(paths, cliNames);
        }
        else
        {
            AddUnixCommonPaths(paths);
        }

        return paths;
    }

    private static void AddWindowsCommonPaths(List<string> paths, string[] cliNames)
    {
        // Windows npm global location: %APPDATA%\npm
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            foreach (var cliName in cliNames)
            {
                paths.Add(Path.Combine(appData, "npm", cliName));
            }
        }

        // Also check user profile .npm-global for custom npm prefix
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var cliName in cliNames)
        {
            paths.Add(Path.Combine(userProfile, ".npm-global", "bin", cliName));
        }
    }

    private static void AddUnixCommonPaths(List<string> paths)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        paths.Add(Path.Combine(userProfile, ".npm-global", "bin", "claude"));
        paths.Add(Path.Combine(userProfile, ".local", "bin", "claude"));
        paths.Add("/usr/local/bin/claude");
        paths.Add("/usr/bin/claude");
    }
}
