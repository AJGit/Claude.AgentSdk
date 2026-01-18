using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Builder for creating ProcessStartInfo configured for the Claude CLI.
/// </summary>
internal sealed partial class ProcessStartInfoBuilder
{
    private readonly List<string> _args;
    private readonly string _cliPath;
    private readonly Dictionary<string, string> _environment = new();
    private readonly ILogger _logger;
    private string _workingDirectory = Environment.CurrentDirectory;

    private ProcessStartInfoBuilder(string cliPath, List<string> args, ILogger? logger)
    {
        _cliPath = cliPath;
        _args = args;
        _logger = logger ?? NullLogger.Instance;
    }

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
        }

        return this;
    }

    /// <summary>
    ///     Adds environment variables from a dictionary.
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
    ///     Adds a single environment variable.
    /// </summary>
    public ProcessStartInfoBuilder WithEnvironmentVariable(string key, string value)
    {
        _environment[key] = value;
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

    private (string fileName, List<string> argumentList) ResolveExecutable()
    {
        if (OperatingSystem.IsWindows() && _cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveWindowsCmdExecutable();
        }

        return (_cliPath, [.. _args]);
    }

    private (string fileName, List<string> argumentList) ResolveWindowsCmdExecutable()
    {
        // Try to find cli.js and run directly with node to avoid cmd.exe argument parsing issues
        var npmDir = Path.GetDirectoryName(_cliPath)!;
        var cliJsPath = Path.Combine(npmDir, "node_modules", "@anthropic-ai", "claude-code", "cli.js");

        if (File.Exists(cliJsPath))
        {
            LogUsingNodeJsDirectlyWithCliJsPath(cliJsPath);
            var argumentList = new List<string> { cliJsPath };
            argumentList.AddRange(_args);
            return ("node", argumentList);
        }

        // Fallback to cmd.exe /c
        LogCliJsNotFoundAtPathFallingBackToCmdExe(cliJsPath);
        return CreateCmdExeFallback();
    }

    private (string fileName, List<string> argumentList) CreateCmdExeFallback()
    {
        var argumentList = new List<string> { "/c" };
        var commandParts = new List<string> { QuoteArgument(_cliPath) };
        commandParts.AddRange(_args.Select(QuoteArgument));
        argumentList.Add(string.Join(" ", commandParts));
        return ("cmd.exe", argumentList);
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
        foreach (var (key, value) in _environment)
        {
            startInfo.Environment[key] = value;
        }

        if (!string.IsNullOrEmpty(_workingDirectory))
        {
            startInfo.Environment["PWD"] = _workingDirectory;
        }
        // Always set SDK entrypoint for telemetry
        startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";
    }

    /// <summary>
    ///     Quotes an argument for use with cmd.exe /c.
    /// </summary>
    private static string QuoteArgument(string arg)
    {
        if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('&') ||
            arg.Contains('|') || arg.Contains('<') || arg.Contains('>') ||
            arg.Contains('^'))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        return arg;
    }

    [LoggerMessage(LogLevel.Debug, "Executing: {fileName} {args}")]
    partial void LogExecutingFilenameArgs(string fileName, string args);

    [LoggerMessage(LogLevel.Debug, "Using Node.js directly with cli.js: {path}")]
    partial void LogUsingNodeJsDirectlyWithCliJsPath(string path);

    [LoggerMessage(LogLevel.Information,
        "cli.js not found at {path}, falling back to cmd.exe. Max command line length of 8191 characters")]
    partial void LogCliJsNotFoundAtPathFallingBackToCmdExe(string path);
}
