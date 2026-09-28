using System.Globalization;
using System.Text.RegularExpressions;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Fluent builder for constructing Claude CLI command-line arguments.
/// </summary>
/// <remarks>
///     The CLI is always driven in streaming mode (<c>--input-format stream-json</c>): prompts, agents and
///     other large configuration travel over stdin via the control protocol rather than argv.
/// </remarks>
internal sealed partial class CliArgumentsBuilder
{
    // cmd.exe metacharacters (plus its quote toggle and delayed-expansion '!'). Rejected in values that
    // applications commonly take from external input so they stay inert on Windows.
    private const string CmdExeMetacharacters = "&|<>^%!\"";

    private readonly List<string> _args = [];
    private readonly ClaudeAgentOptions _options;

    private CliArgumentsBuilder(ClaudeAgentOptions options)
    {
        _options = options;
    }

    /// <summary>
    ///     Creates a new builder instance.
    /// </summary>
    /// <param name="options">The agent options.</param>
    /// <param name="prompt">
    ///     Ignored. Prompts are always sent over stdin; the parameter is kept for source compatibility.
    /// </param>
    public static CliArgumentsBuilder Create(ClaudeAgentOptions options, string? prompt = null)
    {
        _ = prompt;
        return new CliArgumentsBuilder(options);
    }

    /// <summary>
    ///     Builds all arguments based on the configured options.
    /// </summary>
    public List<string> Build()
    {
        var (allowedTools, settingSources) = ResolveSkillsDefaults(_options);

        return AddBaseArgs()
            .AddSystemPrompt()
            .AddTools(allowedTools)
            .AddLimits()
            .AddModel()
            .AddBetas()
            .AddPermissions()
            .AddSessionOptions()
            .AddSettings()
            .AddDirectories()
            .AddMcpServers()
            .AddStreamingFlags()
            .AddSettingSources(settingSources)
            .AddAdditionalDataPaths()
            .AddPlugins()
            .AddMiscOptions()
            .AddExtraArgs()
            .AddThinking()
            .AddOutputFormat()
            .AddInputFormat()
            .GetArgs();
    }

    /// <summary>
    ///     Returns the built argument list.
    /// </summary>
    public List<string> GetArgs()
    {
        return [.. _args];
    }

    /// <summary>
    ///     Computes the effective allowed tools and setting sources after applying <see cref="ClaudeAgentOptions.Skills" />.
    /// </summary>
    /// <remarks>
    ///     <see cref="AllSkillsConfig" /> adds a bare <c>Skill</c> rule; a skill list adds <c>Skill(name)</c> per entry.
    ///     Either way, setting sources default to user + project so the CLI discovers installed skills.
    /// </remarks>
    internal static (IReadOnlyList<string> AllowedTools, IReadOnlyList<string>? SettingSources) ResolveSkillsDefaults(
        ClaudeAgentOptions options)
    {
        var allowedTools = new List<string>(options.AllowedTools);
        List<string>? settingSources = options.SettingSources?.Select(s => s.ToJsonString()).ToList();

        switch (options.Skills)
        {
            case null:
                return (allowedTools, settingSources);

            case AllSkillsConfig:
                if (!allowedTools.Contains("Skill"))
                {
                    allowedTools.Add("Skill");
                }

                break;

            case SkillListConfig list:
                foreach (var name in list.Names)
                {
                    ValidateSkillName(name);
                    var pattern = $"Skill({name})";
                    if (!allowedTools.Contains(pattern))
                    {
                        allowedTools.Add(pattern);
                    }
                }

                break;
        }

        settingSources ??= ["user", "project"];
        return (allowedTools, settingSources);
    }

    /// <summary>
    ///     Rejects skill names that cannot ride safely inside a <c>Skill(name)</c> rule in <c>--allowedTools</c>.
    /// </summary>
    /// <remarks>
    ///     The CLI splits <c>--allowedTools</c> on commas and spaces outside parentheses without honoring escapes,
    ///     so a name carrying a delimiter could inject extra permission rules. Names that tokenize cleanly but can
    ///     never match a skill are rejected too, so a dead rule fails loudly instead of silently granting nothing.
    /// </remarks>
    internal static void ValidateSkillName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Skill names must be non-empty strings", nameof(name));
        }

        if (name.Any(char.IsSurrogate) && !IsWellFormedUtf16(name))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': contains an unpaired surrogate, which can never match a skill.",
                nameof(name));
        }

        if (name != name.Trim())
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': leading or trailing whitespace can never match — the Skill tool trims the invoked name.",
                nameof(name));
        }

        if (InvalidSkillCharsRegex().IsMatch(name))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': parentheses, commas, control characters, and byte-order marks are not allowed. " +
                "Names match the skill's directory name, or 'plugin:skill' for plugin-qualified skills.",
                nameof(name));
        }

        if (name == "*")
        {
            throw new ArgumentException("Invalid skill name '*': use SkillsConfig.All to enable every skill.",
                nameof(name));
        }

        if (name.EndsWith(":*", StringComparison.Ordinal) || name.EndsWith(" *", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': wildcard-suffix names are not allowed; list each skill by its exact name.",
                nameof(name));
        }

        if (name.StartsWith('/'))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': skill names may not start with '/'. Use the canonical name, not the slash-command form.",
                nameof(name));
        }

        if (name.Contains(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}': consecutive backslashes are not allowed — the rule parser collapses them.",
                nameof(name));
        }

        if (name.EndsWith('\\'))
        {
            throw new ArgumentException($"Invalid skill name '{name}': names may not end with an unpaired backslash.",
                nameof(name));
        }
    }

    /// <summary>
    ///     On Windows, rejects values containing cmd.exe metacharacters or newlines (defense in depth).
    /// </summary>
    internal static void RejectWindowsCmdMetacharacters(string optionName, string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var bad = value.Where(c => CmdExeMetacharacters.Contains(c) || c is '\r' or '\n')
            .Distinct()
            .OrderBy(c => c)
            .ToArray();
        if (bad.Length > 0)
        {
            throw new ArgumentException(
                $"{optionName} value '{value}' contains characters that are unsafe to pass on a Windows command line: " +
                string.Join(", ", bad.Select(c => $"'{c}'")),
                optionName);
        }
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private CliArgumentsBuilder AddBaseArgs()
    {
        _args.Add("--output-format");
        _args.Add("stream-json");
        _args.Add("--verbose");
        return this;
    }

    private CliArgumentsBuilder AddSystemPrompt()
    {
        switch (_options.SystemPrompt)
        {
            case null:
                // No system prompt configured means an empty system prompt (matches the Python/TypeScript SDKs).
                _args.Add("--system-prompt");
                _args.Add("");
                break;

            case CustomSystemPrompt custom:
                _args.Add("--system-prompt");
                _args.Add(custom.Prompt);
                break;

            case FileSystemPrompt file:
                _args.Add("--system-prompt-file");
                _args.Add(file.Path);
                break;

            case PresetSystemPrompt preset:
                // The preset is the CLI default prompt; only the appended text travels on argv.
                if (!string.IsNullOrEmpty(preset.Append))
                {
                    _args.Add("--append-system-prompt");
                    _args.Add(preset.Append);
                }

                break;
        }

        return this;
    }

    private CliArgumentsBuilder AddTools(IReadOnlyList<string> allowedTools)
    {
        switch (_options.Tools)
        {
            case ToolsList toolsList:
                _args.Add("--tools");
                _args.Add(toolsList.Tools.Count == 0 ? "" : string.Join(",", toolsList.Tools));
                break;

            case ToolsPreset:
                // The 'claude_code' preset maps to the CLI's default tool set.
                _args.Add("--tools");
                _args.Add("default");
                break;
        }

        if (allowedTools.Count > 0)
        {
            _args.Add("--allowedTools");
            _args.Add(string.Join(",", allowedTools));
        }

        return this;
    }

    private CliArgumentsBuilder AddLimits()
    {
        if (_options.MaxTurns is > 0)
        {
            _args.Add("--max-turns");
            _args.Add(_options.MaxTurns.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_options.MaxBudgetUsd.HasValue)
        {
            _args.Add("--max-budget-usd");
            _args.Add(_options.MaxBudgetUsd.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_options.DisallowedTools.Count > 0)
        {
            _args.Add("--disallowedTools");
            _args.Add(string.Join(",", _options.DisallowedTools));
        }

        if (_options.TaskBudget.HasValue)
        {
            _args.Add("--task-budget");
            _args.Add(_options.TaskBudget.Value.ToString(CultureInfo.InvariantCulture));
        }

        return this;
    }

    private CliArgumentsBuilder AddModel()
    {
        // ModelId takes precedence over Model for the strongly-typed API
        var model = _options.ModelId?.Value ?? _options.Model;
        if (!string.IsNullOrEmpty(model))
        {
            _args.Add("--model");
            _args.Add(model);
        }

        // FallbackModelId takes precedence over FallbackModel
        var fallbackModel = _options.FallbackModelId?.Value ?? _options.FallbackModel;
        if (!string.IsNullOrEmpty(fallbackModel))
        {
            _args.Add("--fallback-model");
            _args.Add(fallbackModel);
        }

        return this;
    }

    private CliArgumentsBuilder AddBetas()
    {
        if (_options.Betas is { Count: > 0 })
        {
            _args.Add("--betas");
            _args.Add(string.Join(",", _options.Betas));
        }

        return this;
    }

    private CliArgumentsBuilder AddPermissions()
    {
        // A CanUseTool callback routes permission prompts over the control protocol ("stdio").
        var permissionPromptTool = _options.CanUseTool is not null ? "stdio" : _options.PermissionPromptToolName;
        if (!string.IsNullOrEmpty(permissionPromptTool))
        {
            _args.Add("--permission-prompt-tool");
            _args.Add(permissionPromptTool);
        }

        if (_options.PermissionMode.HasValue)
        {
            _args.Add("--permission-mode");
            _args.Add(_options.PermissionMode.Value.ToJsonString());
        }

        return this;
    }

    private CliArgumentsBuilder AddSessionOptions()
    {
        if (_options.ContinueConversation)
        {
            _args.Add("--continue");
        }

        // Pass these as --flag=value rather than two argv tokens: the CLI declares --resume with an optional
        // value, so in the two-token form a dash-leading value would parse as a separate flag.
        if (!string.IsNullOrEmpty(_options.Resume))
        {
            RejectWindowsCmdMetacharacters(nameof(ClaudeAgentOptions.Resume), _options.Resume);
            _args.Add($"--resume={_options.Resume}");
        }

        if (!string.IsNullOrEmpty(_options.SessionId))
        {
            RejectWindowsCmdMetacharacters(nameof(ClaudeAgentOptions.SessionId), _options.SessionId);
            _args.Add($"--session-id={_options.SessionId}");
        }

        return this;
    }

    private CliArgumentsBuilder AddSettings()
    {
        var settingsValue = BuildSettingsValue();
        if (!string.IsNullOrEmpty(settingsValue))
        {
            _args.Add("--settings");
            _args.Add(settingsValue);
        }

        return this;
    }

    /// <summary>
    ///     Builds the <c>--settings</c> value, merging sandbox settings into it when present.
    /// </summary>
    private string? BuildSettingsValue()
    {
        var hasSettings = _options.Settings is not null;
        var hasOverrides = _options.Sandbox is not null || _options.NoHooks;

        if (!hasSettings && !hasOverrides)
        {
            return null;
        }

        if (hasSettings && !hasOverrides)
        {
            return _options.Settings;
        }

        var settingsObj = new Dictionary<string, object?>();

        if (hasSettings)
        {
            settingsObj = LoadSettingsObject(_options.Settings!.Trim());
        }

        if (_options.Sandbox is not null)
        {
            settingsObj["sandbox"] = _options.Sandbox.ToSettingsDictionary();
        }

        if (_options.NoHooks)
        {
            settingsObj["disableAllHooks"] = true;
        }

        return JsonSerializer.Serialize(settingsObj);
    }

    private static Dictionary<string, object?> LoadSettingsObject(string settings)
    {
        if (settings.StartsWith('{') && settings.EndsWith('}'))
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(settings) ?? [];
            }
            catch (JsonException)
            {
                // Not valid JSON - fall through and try it as a path
            }
        }

        if (File.Exists(settings))
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(settings)) ?? [];
        }

        // A missing settings file is tolerated: only the sandbox settings are passed.
        return [];
    }

    private CliArgumentsBuilder AddDirectories()
    {
        foreach (var dir in _options.AddDirectories)
        {
            _args.Add("--add-dir");
            _args.Add(dir);
        }

        return this;
    }

    private CliArgumentsBuilder AddMcpServers()
    {
        if (_options.McpServers is not { Count: > 0 })
        {
            return this;
        }

        var serversForCli = new Dictionary<string, object>();

        foreach (var (name, config) in _options.McpServers)
        {
            if (config.ToCliDictionary() is { } serverConfig)
            {
                serversForCli[name] = serverConfig;
            }
        }

        if (serversForCli.Count > 0)
        {
            _args.Add("--mcp-config");
            _args.Add(JsonSerializer.Serialize(new Dictionary<string, object> { ["mcpServers"] = serversForCli }));
        }

        return this;
    }

    private CliArgumentsBuilder AddStreamingFlags()
    {
        if (_options.IncludePartialMessages)
        {
            _args.Add("--include-partial-messages");
        }

        if (_options.IncludeHookEvents)
        {
            _args.Add("--include-hook-events");
        }

        if (_options.StrictMcpConfig)
        {
            _args.Add("--strict-mcp-config");
        }

        if (_options.ForkSession)
        {
            _args.Add("--fork-session");
        }

        // Equals form so the value can never be parsed as a separate flag.
        if (!string.IsNullOrEmpty(_options.ResumeSessionAt))
        {
            RejectWindowsCmdMetacharacters(nameof(ClaudeAgentOptions.ResumeSessionAt), _options.ResumeSessionAt);
            _args.Add($"--resume-session-at={_options.ResumeSessionAt}");
        }

        // `is not null`, not emptiness: an empty string is forwarded so the CLI rejects it as malformed
        // instead of the SDK silently disarming the guard the caller believes is armed.
        if (_options.ResumeDropsTurn is not null)
        {
            RejectWindowsCmdMetacharacters(nameof(ClaudeAgentOptions.ResumeDropsTurn), _options.ResumeDropsTurn);
            _args.Add($"--resume-drops-turn={_options.ResumeDropsTurn}");
        }

        // Ask the CLI to emit transcript_mirror frames for the session store.
        if (_options.SessionStore is not null)
        {
            _args.Add("--session-mirror");
        }

        return this;
    }

    private CliArgumentsBuilder AddSettingSources(IReadOnlyList<string>? settingSources)
    {
        // An empty list is meaningful: it disables all filesystem settings (SDK isolation mode).
        if (settingSources is not null)
        {
            _args.Add($"--setting-sources={string.Join(",", settingSources)}");
        }

        return this;
    }

    private CliArgumentsBuilder AddAdditionalDataPaths()
    {
        if (_options.AdditionalDataPaths is not { Count: > 0 })
        {
            return this;
        }

        // The CLI has no dedicated flag; --add-dir directories are where it loads extra CLAUDE.md files from.
        foreach (var path in _options.AdditionalDataPaths)
        {
            if (!_options.AddDirectories.Contains(path))
            {
                _args.Add("--add-dir");
                _args.Add(path);
            }
        }

        return this;
    }

    private CliArgumentsBuilder AddPlugins()
    {
        if (_options.Plugins is not { Count: > 0 })
        {
            return this;
        }

        foreach (var plugin in _options.Plugins)
        {
            if (!string.Equals(plugin.Type, "local", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unsupported plugin type: {plugin.Type}");
            }

            _args.Add("--plugin-dir");
            _args.Add(plugin.Path);
        }

        return this;
    }

    private CliArgumentsBuilder AddMiscOptions()
    {
        if (_options.DangerouslySkipPermissions)
        {
            _args.Add("--dangerously-skip-permissions");
        }

        // NoHooks travels through --settings (disableAllHooks); User has no CLI equivalent.
        return this;
    }

    private CliArgumentsBuilder AddExtraArgs()
    {
        foreach (var (key, value) in _options.ExtraArgs)
        {
            if (value is null)
            {
                // Boolean flag without value
                _args.Add($"--{key}");
            }
            else if (value.StartsWith('-'))
            {
                // A dash-leading value in two-token form would not bind to its flag when the CLI declares
                // the option with an optional value; the equals form always binds.
                _args.Add($"--{key}={value}");
            }
            else
            {
                _args.Add($"--{key}");
                _args.Add(value);
            }
        }

        return this;
    }

    private CliArgumentsBuilder AddThinking()
    {
        // Thinking takes precedence over the deprecated MaxThinkingTokens.
        switch (_options.Thinking)
        {
            case AdaptiveThinkingConfig adaptive:
                _args.Add("--thinking");
                _args.Add("adaptive");
                AddThinkingDisplay(adaptive.Display);
                break;

            case EnabledThinkingConfig enabled:
                _args.Add("--max-thinking-tokens");
                _args.Add(enabled.BudgetTokens.ToString(CultureInfo.InvariantCulture));
                AddThinkingDisplay(enabled.Display);
                break;

            case DisabledThinkingConfig:
                _args.Add("--thinking");
                _args.Add("disabled");
                break;

            case null when _options.MaxThinkingTokens.HasValue:
                _args.Add("--max-thinking-tokens");
                _args.Add(_options.MaxThinkingTokens.Value.ToString(CultureInfo.InvariantCulture));
                break;
        }

        if (_options.Effort is { } effort)
        {
            _args.Add("--effort");
            _args.Add(effort.ToJsonString());
        }

        return this;
    }

    private void AddThinkingDisplay(ThinkingDisplay? display)
    {
        if (display is { } value)
        {
            _args.Add("--thinking-display");
            _args.Add(value.ToJsonString());
        }
    }

    private CliArgumentsBuilder AddOutputFormat()
    {
        if (_options.OutputFormat.HasValue)
        {
            _args.Add("--json-schema");
            _args.Add(ExtractInnerSchema(_options.OutputFormat.Value));
        }

        return this;
    }

    private CliArgumentsBuilder AddInputFormat()
    {
        // Always use streaming mode with stdin (matches the Python/TypeScript SDKs).
        _args.Add("--input-format");
        _args.Add("stream-json");
        return this;
    }

    /// <summary>
    ///     Extracts the JSON schema from an output format.
    /// </summary>
    /// <remarks>
    ///     Accepts the Messages API shape <c>{"type":"json_schema","schema":{...}}</c>, the OpenAI-style
    ///     <c>{"type":"json_schema","json_schema":{"schema":{...}}}</c>, or a raw schema (passed through as-is).
    /// </remarks>
    private static string ExtractInnerSchema(JsonElement schemaElement)
    {
        if (schemaElement.ValueKind == JsonValueKind.Object &&
            schemaElement.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String &&
            typeElement.GetString() == "json_schema")
        {
            if (schemaElement.TryGetProperty("schema", out var schema))
            {
                return schema.GetRawText();
            }

            if (schemaElement.TryGetProperty("json_schema", out var jsonSchemaWrapper) &&
                jsonSchemaWrapper.TryGetProperty("schema", out var innerSchema))
            {
                return innerSchema.GetRawText();
            }
        }

        return schemaElement.GetRawText();
    }

    // Parentheses and commas are --allowedTools delimiters; control characters (C0, DEL, C1) and U+FEFF
    // never appear in a skill directory name.
    [GeneratedRegex("[(),\\x00-\\x1f\\x7f-\\x9f\\ufeff]")]
    private static partial Regex InvalidSkillCharsRegex();
}
