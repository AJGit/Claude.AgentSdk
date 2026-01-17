using System.Text.Json;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.DamageControl.Security;

/// <summary>
///     Result of a security check operation.
/// </summary>
public sealed record CheckResult(bool Blocked, bool Ask, string Reason)
{
    /// <summary>
    ///     Creates an allow result (operation permitted).
    /// </summary>
    public static CheckResult Allow => new(false, false, "");

    /// <summary>
    ///     Creates a block result with the specified reason.
    /// </summary>
    public static CheckResult Block(string reason) => new(true, false, reason);

    /// <summary>
    ///     Creates an ask result that triggers user confirmation.
    /// </summary>
    public static CheckResult AskUser(string reason) => new(false, true, reason);

    /// <summary>
    ///     Converts this result to a HookOutput.
    /// </summary>
    public HookOutput ToHookOutput()
    {
        if (Blocked)
        {
            return new SyncHookOutput
            {
                Continue = false,
                Decision = "block",
                Reason = Reason,
                SystemMessage = $"[DamageControl] BLOCKED: {Reason}"
            };
        }

        if (Ask)
        {
            // Create hook-specific output to trigger permission dialog
            // Format matches the original Python implementation:
            // hookEventName, permissionDecision, permissionDecisionReason
            var hookSpecificJson = JsonSerializer.SerializeToElement(new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "ask",
                permissionDecisionReason = Reason
            });

            return new SyncHookOutput
            {
                Continue = true,
                Reason = Reason,
                SystemMessage = $"[DamageControl] Confirmation required: {Reason}",
                HookSpecificOutput = hookSpecificJson
            };
        }

        return new SyncHookOutput { Continue = true };
    }
}
