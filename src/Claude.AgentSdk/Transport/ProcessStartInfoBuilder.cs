using System.Diagnostics;
using System.Reflection;
using System.Text;
using Claude.AgentSdk.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Builder for creating ProcessStartInfo configured for the Claude CLI.
/// </summary>
internal sealed partial class ProcessStartInfoBuilder
{
    /// <summary>
    ///     Asks the CLI to emit <c>session_state_changed</c> frames the SDK uses to decide when to close stdin.
    /// </summary>
    internal const string SdkReadsSessionStateEnv = "CLAUDE_CODE_SDK_READS_SESSION_STATE";

    private readonly List<string> _args;
    private readonly string _cliPath;
    private readonly Dictionary<string, string> _environment = new();
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _sdkEnvironment = new();
    private string _workingDirectory = Environment.CurrentDirectory;
    private bool _workingDirectoryExplicit;

    private ProcessStartInfoBuilder(string cliPath, List<string> args, ILogger? logger)
    {
        _cliPath = cliPath;
        _args = args;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    ///     The SDK version reported to the CLI via <c>CLAUDE_AGENT_SDK_VERSION</c>.
    /// </summary>
    internal static string SdkVersion { get; } =
        typeof(ProcessStartInfoBuilder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? typeof(ProcessStartInfoBuilder).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    ///     Creates a new builder instance.
    /// </summary>
    public static ProcessStartInfoBuilder Create(string cliPath, List<string> args, ILogger? logger = null)
    {
        return new ProcessStartInfoBuilder(cliPath, args, logger);
    }

    /// <summary>
    ///     Sets the working directory for the process.
    /// </summary>
    public ProcessStartInfoBuilder WithWorkingDirectory(string? workingDirectory)
    {
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            _workingDirectory = workingDirectory;
            _workingDirectoryExplicit = true;
        }

        return this;
    }

    /// <summary>
    ///     Adds user-supplied environment variables. These override inherited values and
    ///     <c>CLAUDE_CODE_ENTRYPOINT</c>, but not SDK-owned variables such as <c>CLAUDE_AGENT_SDK_VERSION</c>.
    /// </summary>
    public ProcessStartInfoBuilder WithEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        foreach (var (key, value) in environment)
        {
            _environment[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Adds a single SDK-controlled environment variable (applied after user variables).
    /// </summary>
    public ProcessStartInfoBuilder WithEnvironmentVariable(string key, string value)
    {
        _sdkEnvironment[key] = value;
        return this;
    }

    /// <summary>
    ///     Builds the ProcessStartInfo with all configurations applied.
    /// </summary>
    public ProcessStartInfo Build()
    {
        var (fileName, argumentList) = ResolveExecutable();

        LogExecutingFilenameArgs(fileName, string.Join(" ", argumentList));

        var startInfo = CreateBaseStartInfo(fileName);
        AddArguments(startInfo, argumentList);
        AddEnvironmentVariables(startInfo);

        return startInfo;
    }

    /// <summary>
    ///     Whether any path component names a .bat/.cmd batch script on Windows.
    /// </summary>
    /// <remarks>
    ///     Every component is classified (not only the final one) because Win32 path normalization
    ///     (dot collapsing, trailing dot/space trimming, NTFS stream specs) makes re-deriving the effective
    ///     file name error-prone. No legitimate claude.exe lives beneath a directory named like a batch file.
    /// </remarks>
    internal static bool IsWindowsBatchPath(string cliPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return cliPath.Replace('\\', '/')
            .Split('/')
            .SelectMany(component => component.Split(':'))
            .Any(segment =>
            {
                var trimmed = segment.TrimEnd('.', ' ');
                return trimmed.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                       trimmed.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
            });
    }

    private (string fileName, List<string> argumentList) ResolveExecutable()
    {
        if (IsWindowsBatchPath(_cliPath))
        {
            return ResolveWindowsBatchExecutable();
        }

        return (_cliPath, [.. _args]);
    }

    /// <summary>
    ///     Resolves npm's claude.cmd shim to <c>node cli.js</c>, or refuses to run it.
    /// </summary>
    /// <remarks>
    ///     Windows runs .bat/.cmd files via cmd.exe, which re-parses the whole command line; cmd.exe
    ///     metacharacters inside an argument (e.g. a session title passed to --resume) can execute injected
    ///     commands, and no reliable escaping exists (CVE-2024-27980 "BatBadBut"). Running cli.js directly with
    ///     node avoids cmd.exe entirely; otherwise the shim is refused.
    /// </remarks>
    private (string fileName, List<string> argumentList) ResolveWindowsBatchExecutable()
    {
        var npmDir = Path.GetDirectoryName(_cliPath);
        if (npmDir is not null)
        {
            var cliJsPath = Path.Combine(npmDir, "node_modules", "@anthropic-ai", "claude-code", "cli.js");
            if (File.Exists(cliJsPath))
            {
                LogUsingNodeJsDirectlyWithCliJsPath(cliJsPath);
                var argumentList = new List<string> { cliJsPath };
                argumentList.AddRange(_args);
                return ("node", argumentList);
            }
        }

        throw new CliNotFoundException(
            $"Refusing to execute batch script '{_cliPath}': Windows runs .bat/.cmd files via cmd.exe, which can " +
            "execute commands injected through CLI arguments, and no reliable escaping for cmd.exe exists. " +
            "Use a native claude executable instead: install Claude Code natively " +
            "(irm https://claude.ai/install.ps1 | iex), or point ClaudeAgentOptions.CliPath at a claude.exe.");
    }

    private ProcessStartInfo CreateBaseStartInfo(string fileName)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            WorkingDirectory = _workingDirectory
        };
    }

    private static void AddArguments(ProcessStartInfo startInfo, List<string> argumentList)
    {
        foreach (var arg in argumentList)
        {
            startInfo.ArgumentList.Add(arg);
        }
    }

    private void AddEnvironmentVariables(ProcessStartInfo startInfo)
    {
        // Don't let the CLI think it is running inside a Claude Code parent.
        startInfo.Environment.Remove("CLAUDECODE");

        // Entrypoint defaults to sdk-csharp regardless of the inherited env; user env can override it.
        startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";

        AddTraceContext(startInfo);

        foreach (var (key, value) in _environment)
        {
            startInfo.Environment[key] = value;
        }

        startInfo.Environment["CLAUDE_AGENT_SDK_VERSION"] = SdkVersion;

        // The SDK waits for the CLI's "idle" session state before closing stdin on runs that serve control
        // requests; ask for those frames unless the caller chose a value.
        if (!startInfo.Environment.Keys.Any(k =>
                string.Equals(k, SdkReadsSessionStateEnv, StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.Environment[SdkReadsSessionStateEnv] = "1";
        }

        foreach (var (key, value) in _sdkEnvironment)
        {
            startInfo.Environment[key] = value;
        }

        if (_workingDirectoryExplicit)
        {
            startInfo.Environment["PWD"] = _workingDirectory;
        }
    }

    /// <summary>
    ///     Propagates the active W3C trace context (<see cref="Activity.Current" />) to the CLI so its spans
    ///     parent under the caller's distributed trace. Explicit user environment always wins.
    /// </summary>
    private void AddTraceContext(ProcessStartInfo startInfo)
    {
        var activity = Activity.Current;
        if (activity is not { IdFormat: ActivityIdFormat.W3C } || string.IsNullOrEmpty(activity.Id))
        {
            return;
        }

        // Scrub stale inherited context so an inherited TRACESTATE isn't paired with a fresh TRACEPARENT.
        foreach (var key in new[] { "TRACEPARENT", "TRACESTATE" })
        {
            if (!_environment.ContainsKey(key))
            {
                startInfo.Environment.Remove(key);
            }
        }

        if (!_environment.ContainsKey("TRACEPARENT"))
        {
            startInfo.Environment["TRACEPARENT"] = activity.Id;
        }

        if (!string.IsNullOrEmpty(activity.TraceStateString) && !_environment.ContainsKey("TRACESTATE"))
        {
            startInfo.Environment["TRACESTATE"] = activity.TraceStateString;
        }
    }

    [LoggerMessage(LogLevel.Debug, "Executing: {fileName} {args}")]
    partial void LogExecutingFilenameArgs(string fileName, string args);

    [LoggerMessage(LogLevel.Debug, "Using Node.js directly with cli.js: {path}")]
    partial void LogUsingNodeJsDirectlyWithCliJsPath(string path);
}
