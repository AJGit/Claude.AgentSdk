using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.DamageControl.Security;
using Claude.AgentSdk.Protocol;

using HookEvent = Claude.AgentSdk.Attributes.HookEvent;

namespace Claude.AgentSdk.DamageControl.Hooks;

/// <summary>
///     Damage control hooks that protect against dangerous operations.
/// </summary>
/// <remarks>
///     This class provides defense-in-depth protection for Claude agent sessions by:
///     <list type="bullet">
///         <item>Blocking dangerous bash commands (rm -rf, git reset --hard, terraform destroy, etc.)</item>
///         <item>Protecting sensitive paths with three access levels</item>
///         <item>Triggering confirmation dialogs for risky-but-valid operations</item>
///     </list>
/// </remarks>
[GenerateHookRegistration]
public partial class DamageControlHooks
{
    private readonly CommandChecker _commandChecker;
    private readonly PathMatcher _pathMatcher;

    /// <summary>
    ///     Creates a new instance of DamageControlHooks.
    /// </summary>
    /// <param name="config">The security configuration to use.</param>
    public DamageControlHooks(SecurityConfig config)
    {
        _commandChecker = new CommandChecker(config);
        _pathMatcher = new PathMatcher(config);
    }

    /// <summary>
    ///     Prints a formatted hook result to the console.
    /// </summary>
    private static void PrintHookResult(string toolName, string? target, CheckResult result)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[DamageControl] ");
        Console.ResetColor();
        Console.WriteLine($"{toolName}: {target}");

        if (result.Blocked)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [X] BLOCKED: {result.Reason}");
        }
        else if (result.Ask)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [?] CONFIRMATION REQUIRED: {result.Reason}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [OK] ALLOWED");
        }
        Console.ResetColor();
    }

    /// <summary>
    ///     Hook handler for Bash tool calls.
    ///     Checks commands against bash patterns and path restrictions.
    /// </summary>
    [HookHandler(HookEvent.PreToolUse, Matcher = "Bash")]
    public Task<HookOutput> OnBashToolUse(
        HookInput input,
        string? toolUseId,
        HookContext ctx,
        CancellationToken ct)
    {
        if (input is not PreToolUseHookInput preInput)
        {
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        }

        string? command = null;
        if (preInput.ToolInput.TryGetProperty("command", out var commandElement))
        {
            command = commandElement.GetString();
        }

        var result = _commandChecker.Check(command);
        PrintHookResult("Bash", command, result);

        return Task.FromResult(result.ToHookOutput());
    }

    /// <summary>
    ///     Hook handler for Edit tool calls.
    ///     Checks file paths against zero-access and read-only restrictions.
    /// </summary>
    [HookHandler(HookEvent.PreToolUse, Matcher = "Edit")]
    public Task<HookOutput> OnEditToolUse(
        HookInput input,
        string? toolUseId,
        HookContext ctx,
        CancellationToken ct)
    {
        if (input is not PreToolUseHookInput preInput)
        {
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        }

        string? filePath = null;
        if (preInput.ToolInput.TryGetProperty("file_path", out var pathElement))
        {
            filePath = pathElement.GetString();
        }

        var result = _pathMatcher.CheckForEdit(filePath);
        PrintHookResult("Edit", filePath, result);

        return Task.FromResult(result.ToHookOutput());
    }

    /// <summary>
    ///     Hook handler for Write tool calls.
    ///     Checks file paths against zero-access and read-only restrictions.
    /// </summary>
    [HookHandler(HookEvent.PreToolUse, Matcher = "Write")]
    public Task<HookOutput> OnWriteToolUse(
        HookInput input,
        string? toolUseId,
        HookContext ctx,
        CancellationToken ct)
    {
        if (input is not PreToolUseHookInput preInput)
        {
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        }

        string? filePath = null;
        if (preInput.ToolInput.TryGetProperty("file_path", out var pathElement))
        {
            filePath = pathElement.GetString();
        }

        var result = _pathMatcher.CheckForWrite(filePath);
        PrintHookResult("Write", filePath, result);

        return Task.FromResult(result.ToHookOutput());
    }

    /// <summary>
    ///     Hook handler for NotebookEdit tool calls.
    ///     Checks notebook paths against zero-access and read-only restrictions.
    /// </summary>
    [HookHandler(HookEvent.PreToolUse, Matcher = "NotebookEdit")]
    public Task<HookOutput> OnNotebookEditToolUse(
        HookInput input,
        string? toolUseId,
        HookContext ctx,
        CancellationToken ct)
    {
        if (input is not PreToolUseHookInput preInput)
        {
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        }

        string? notebookPath = null;
        if (preInput.ToolInput.TryGetProperty("notebook_path", out var pathElement))
        {
            notebookPath = pathElement.GetString();
        }

        var result = _pathMatcher.CheckForEdit(notebookPath);
        PrintHookResult("NotebookEdit", notebookPath, result);

        return Task.FromResult(result.ToHookOutput());
    }
}
