using Claude.AgentSdk.Sessions;
using Claude.AgentSdk.Transport;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk;

/// <summary>
///     Validation shared by one-shot queries and interactive sessions before the CLI is started.
/// </summary>
internal static class ClaudeAgentOptionsValidation
{
    /// <summary>
    ///     Validates the options and logs advisory warnings.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Both <see cref="ClaudeAgentOptions.CanUseTool" /> and <see cref="ClaudeAgentOptions.PermissionPromptToolName" />
    ///     are set, or a skill name is invalid.
    /// </exception>
    public static void Validate(ClaudeAgentOptions options, ILogger? logger)
    {
        // Validates skill names (throws for delimiter-carrying or malformed names).
        CliArgumentsBuilder.ResolveSkillsDefaults(options);
        ValidateSessionStore(options);

        if (options.CanUseTool is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(options.PermissionPromptToolName))
        {
            throw new ArgumentException(
                "CanUseTool callback cannot be used with PermissionPromptToolName. Please use one or the other.",
                nameof(options));
        }

        if (GetCanUseToolShadowedWarning(options) is { } warning)
        {
            logger?.LogWarning("{Warning}", warning);
        }
    }

    /// <summary>
    ///     Rejects invalid <see cref="ClaudeAgentOptions.SessionStore" /> combinations before the CLI is spawned.
    /// </summary>
    internal static void ValidateSessionStore(ClaudeAgentOptions options)
    {
        if (options.SessionStore is not { } store)
        {
            return;
        }

        // With an explicit resume, the store is never listed (resume wins over continue).
        if (options.ContinueConversation && options.Resume is null && store is not ISessionStoreListing)
        {
            throw new ArgumentException(
                "ContinueConversation with SessionStore requires the store to implement ISessionStoreListing",
                nameof(options));
        }

        if (options.EnableFileCheckpointing)
        {
            throw new ArgumentException(
                "SessionStore cannot be combined with EnableFileCheckpointing (checkpoints are local-disk only and " +
                "would diverge from the mirrored transcript)", nameof(options));
        }
    }

    /// <summary>
    ///     Returns a warning when options visibly prevent the CanUseTool callback from ever firing, else null.
    /// </summary>
    /// <remarks>
    ///     Advisory only: shadowing can be intentional (e.g. a callback used solely for tools outside
    ///     AllowedTools). Allow rules from settings files can also shadow the callback but are not visible here.
    /// </remarks>
    internal static string? GetCanUseToolShadowedWarning(ClaudeAgentOptions options)
    {
        if (options.CanUseTool is null)
        {
            return null;
        }

        if (options.PermissionMode == PermissionMode.BypassPermissions || options.DangerouslySkipPermissions)
        {
            return "CanUseTool will not be invoked: bypassPermissions auto-approves every tool call (except " +
                   "explicit deny rules) before the callback is consulted. To gate every tool call, use a " +
                   "PreToolUse hook instead.";
        }

        // SkillsConfig.All makes the SDK add a bare "Skill" rule, which shadows the callback like a hand-written one.
        var allowedTools = options.Skills is AllSkillsConfig && !options.AllowedTools.Contains("Skill")
            ? [.. options.AllowedTools, "Skill"]
            : options.AllowedTools;

        var shadowed = allowedTools
            .Select(WholeToolAllowed)
            .Where(tool => tool is not null)
            .Distinct()
            .ToList();

        if (shadowed.Count == 0)
        {
            return null;
        }

        return $"CanUseTool will not be invoked for: {string.Join(", ", shadowed)}. An AllowedTools entry that " +
               "allows a whole tool auto-approves it before the callback is consulted. To gate every tool call, " +
               "use a PreToolUse hook; or narrow the entry so calls fall through to CanUseTool.";
    }

    /// <summary>
    ///     Returns the tool an AllowedTools entry allows outright ("Read", "Read()", "Read(*)"), else null.
    ///     A real specifier ("Bash(ls:*)") only allows matching invocations.
    /// </summary>
    internal static string? WholeToolAllowed(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return null;
        }

        var openIndex = entry.IndexOf('(');
        if (openIndex == -1)
        {
            return entry;
        }

        if (openIndex == 0 || !entry.EndsWith(')'))
        {
            return null;
        }

        var specifier = entry[(openIndex + 1)..^1];
        return specifier is "" or "*" ? entry[..openIndex] : null;
    }
}
