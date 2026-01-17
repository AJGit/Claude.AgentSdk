using System.Globalization;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     Fluent builder for constructing Claude CLI command-line arguments.
/// </summary>
internal sealed class CliArgumentsBuilder
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly List<string> _args = [];
    private readonly ClaudeAgentOptions _options;
    private readonly string? _prompt;

    private CliArgumentsBuilder(ClaudeAgentOptions options, string? prompt)
    {
        _options = options;
        _prompt = prompt;
    }

    /// <summary>
    ///     Creates a new builder instance.
    /// </summary>
    public static CliArgumentsBuilder Create(ClaudeAgentOptions options, string? prompt = null)
    {
        return new CliArgumentsBuilder(options, prompt);
    }

    /// <summary>
    ///     Builds all arguments based on the configured options.
    /// </summary>
    public List<string> Build()
    {
        return AddBaseArgs()
            .AddPromptOrInputFormat()
            .AddModel()
            .AddTools()
            .AddSystemPrompt()
            .AddSettingSources()
            .AddAdditionalDataPaths()
            .AddPermissionMode()
            .AddBetas()
            .AddSandbox()
            .AddMiscOptions()
            .AddSessionOptions()
            .AddOutputOptions()
            .AddAgents()
            .AddPlugins()
            .AddMcpServers()
            .AddExtraArgs()
            .GetArgs();
    }

    /// <summary>
    ///     Returns the built argument list.
    /// </summary>
    public List<string> GetArgs()
    {
        return [.. _args];
    }

    private CliArgumentsBuilder AddBaseArgs()
    {
        _args.Add("--output-format");
        _args.Add("stream-json");
        _args.Add("--verbose");
        return this;
    }

    private CliArgumentsBuilder AddPromptOrInputFormat()
    {
        // SDK MCP servers require bidirectional mode for control protocol communication
        var hasSdkMcpServers = _options.McpServers?.Values.Any(c => c is McpSdkServerConfig) ?? false;

        if (!string.IsNullOrEmpty(_prompt) && !hasSdkMcpServers)
        {
            _args.Add("--print");
            _args.Add(_prompt);
        }
        else
        {
            _args.Add("--input-format");
            _args.Add("stream-json");
        }

        return this;
    }

    private CliArgumentsBuilder AddModel()
    {
        // ModelId takes precedence over Model for the new strongly-typed API
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

    private CliArgumentsBuilder AddTools()
    {
        switch (_options.Tools)
        {
            case ToolsList toolsList:
                _args.Add("--tools");
                _args.Add(toolsList.Tools.Count == 0 ? "" : string.Join(",", toolsList.Tools));
                break;

            case ToolsPreset toolsPreset:
                _args.Add("--tools");
                _args.Add(JsonSerializer.Serialize(new { type = "preset", preset = toolsPreset.Preset }));
                break;
        }

        if (_options.AllowedTools.Count > 0)
        {
            _args.Add("--allowedTools");
            _args.Add(string.Join(",", _options.AllowedTools));
        }

        if (_options.DisallowedTools.Count > 0)
        {
            _args.Add("--disallowedTools");
            _args.Add(string.Join(",", _options.DisallowedTools));
        }

        return this;
    }

    private CliArgumentsBuilder AddSystemPrompt()
    {
        switch (_options.SystemPrompt)
        {
            case CustomSystemPrompt custom:
                _args.Add("--system-prompt");
                _args.Add(custom.Prompt);
                break;

            case PresetSystemPrompt preset:
                _args.Add("--system-prompt");
                _args.Add(preset.Preset);
                if (!string.IsNullOrEmpty(preset.Append))
                {
                    _args.Add("--append-system-prompt");
                    _args.Add(preset.Append);
                }

                break;
        }

        return this;
    }

    private CliArgumentsBuilder AddSettingSources()
    {
        if (_options.SettingSources is not { Count: > 0 })
        {
            return this;
        }

        var sources = string.Join(",", _options.SettingSources.Select(s => s switch
        {
            SettingSource.Project => "project",
            SettingSource.User => "user",
            SettingSource.Local => "local",
            _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown setting source")
        }));
        _args.Add("--setting-sources");
        _args.Add(sources);
        return this;
    }

    private CliArgumentsBuilder AddAdditionalDataPaths()
    {
        if (_options.AdditionalDataPaths is not { Count: > 0 })
        {
            return this;
        }

        foreach (var path in _options.AdditionalDataPaths)
        {
            _args.Add("--additional-data-path");
            _args.Add(path);
        }

        return this;
    }

    private CliArgumentsBuilder AddPermissionMode()
    {
        if (!_options.PermissionMode.HasValue)
        {
            return this;
        }

        _args.Add("--permission-mode");
        _args.Add(_options.PermissionMode.Value switch
        {
            PermissionMode.Default => "default",
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            PermissionMode.DontAsk => "dontAsk",
            _ => "default"
        });
        return this;
    }

    private CliArgumentsBuilder AddBetas()
    {
        if (_options.Betas is not { Count: > 0 })
        {
            return this;
        }

        _args.Add("--betas");
        _args.Add(string.Join(",", _options.Betas));

        return this;
    }

    private CliArgumentsBuilder AddSandbox()
    {
        if (_options.Sandbox is null)
        {
            return this;
        }

        switch (_options.Sandbox)
        {
            case SimpleSandboxConfig simple:
                AddSimpleSandboxArg(simple.Mode);
                break;

            case SandboxSettings settings:
                AddDetailedSandboxArg(settings);
                break;
        }

        return this;
    }

    private void AddSimpleSandboxArg(SandboxMode mode)
    {
        _args.Add("--sandbox");
        _args.Add(mode switch
        {
            SandboxMode.Off => "off",
            SandboxMode.Permissive => "permissive",
            SandboxMode.Strict => "strict",
            _ => "off"
        });
    }

    private void AddDetailedSandboxArg(SandboxSettings settings)
    {
        var sandboxConfig = settings.ToCliDictionary();
        if (sandboxConfig.Count > 0)
        {
            _args.Add("--sandbox-config");
            _args.Add(JsonSerializer.Serialize(sandboxConfig, _jsonOptions));
        }
    }

    private CliArgumentsBuilder AddMiscOptions()
    {
        if (!string.IsNullOrEmpty(_options.PermissionPromptToolName))
        {
            _args.Add("--permission-prompt-tool");
            _args.Add(_options.PermissionPromptToolName);
        }

        if (_options.StrictMcpConfig)
        {
            _args.Add("--strict-mcp-config");
        }

        if (_options.DangerouslySkipPermissions)
        {
            _args.Add("--dangerously-skip-permissions");
        }

        if (_options.NoHooks)
        {
            _args.Add("--no-hooks");
        }

        if (!string.IsNullOrEmpty(_options.User))
        {
            _args.Add("--user");
            _args.Add(_options.User);
        }

        if (_options.MaxTurns.HasValue)
        {
            _args.Add("--max-turns");
            _args.Add(_options.MaxTurns.Value.ToString());
        }

        if (_options.MaxBudgetUsd.HasValue)
        {
            _args.Add("--max-budget-usd");
            _args.Add(_options.MaxBudgetUsd.Value.ToString(CultureInfo.InvariantCulture));
        }

        return this;
    }

    private CliArgumentsBuilder AddSessionOptions()
    {
        if (!string.IsNullOrEmpty(_options.Resume))
        {
            _args.Add("--resume");
            _args.Add(_options.Resume);

            if (_options.ForkSession)
            {
                _args.Add("--fork-session");
            }

            if (!string.IsNullOrEmpty(_options.ResumeSessionAt))
            {
                _args.Add("--resume-session-at");
                _args.Add(_options.ResumeSessionAt);
            }
        }

        if (_options.ContinueConversation)
        {
            _args.Add("--continue");
        }

        foreach (var dir in _options.AddDirectories)
        {
            _args.Add("--add-dir");
            _args.Add(dir);
        }

        return this;
    }

    private CliArgumentsBuilder AddOutputOptions()
    {
        if (_options.IncludePartialMessages)
        {
            _args.Add("--include-partial-messages");
        }

        if (_options.MaxThinkingTokens.HasValue)
        {
            _args.Add("--max-thinking-tokens");
            _args.Add(_options.MaxThinkingTokens.Value.ToString());
        }

        if (_options.OutputFormat.HasValue)
        {
            _args.Add("--json-schema");
            var schemaJson = ExtractInnerSchema(_options.OutputFormat.Value);
            _args.Add(schemaJson);
        }

        return this;
    }

    private CliArgumentsBuilder AddAgents()
    {
        if (_options.Agents is not { Count: > 0 })
        {
            return this;
        }

        var agentsConfig = new Dictionary<string, object>();

        foreach (var (name, agent) in _options.Agents)
        {
            var agentConfig = new Dictionary<string, object>
            {
                ["description"] = agent.Description,
                ["prompt"] = agent.Prompt
            };

            if (agent.Tools is { Count: > 0 })
            {
                agentConfig["tools"] = agent.Tools;
            }

            if (!string.IsNullOrEmpty(agent.Model))
            {
                agentConfig["model"] = agent.Model;
            }

            agentsConfig[name] = agentConfig;
        }

        var agentsJson = JsonSerializer.Serialize(agentsConfig, new JsonSerializerOptions { WriteIndented = false });
        _args.Add("--agents");
        _args.Add(agentsJson);
        return this;
    }

    private CliArgumentsBuilder AddPlugins()
    {
        if (_options.Plugins is not { Count: > 0 })
        {
            return this;
        }

        var pluginsConfig = _options.Plugins.Select(p => new
        {
            type = p.Type,
            path = p.Path
        }).ToList();

        var pluginsJson = JsonSerializer.Serialize(pluginsConfig, new JsonSerializerOptions { WriteIndented = false });
        _args.Add("--plugins");
        _args.Add(pluginsJson);
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
            var serverConfig = config.ToCliDictionary();
            if (serverConfig is not null)
            {
                serversForCli[name] = serverConfig;
            }
        }

        if (serversForCli.Count > 0)
        {
            var mcpConfigJson = JsonSerializer.Serialize(
                new { mcpServers = serversForCli },
                new JsonSerializerOptions { WriteIndented = false });

            _args.Add("--mcp-config");
            _args.Add(mcpConfigJson);
        }

        return this;
    }

    private CliArgumentsBuilder AddExtraArgs()
    {
        foreach (var (key, value) in _options.ExtraArgs)
        {
            _args.Add($"--{key}");
            if (value is not null)
            {
                _args.Add(value);
            }
        }

        return this;
    }

    /// <summary>
    ///     Extracts the inner schema from a wrapped json_schema format.
    ///     If already a raw schema, returns as-is.
    /// </summary>
    private static string ExtractInnerSchema(JsonElement schemaElement)
    {
        if (schemaElement.TryGetProperty("type", out var typeElement) &&
            typeElement.GetString() == "json_schema" &&
            schemaElement.TryGetProperty("json_schema", out var jsonSchemaWrapper) &&
            jsonSchemaWrapper.TryGetProperty("schema", out var innerSchema))
        {
            return innerSchema.GetRawText();
        }

        return schemaElement.GetRawText();
    }
}
