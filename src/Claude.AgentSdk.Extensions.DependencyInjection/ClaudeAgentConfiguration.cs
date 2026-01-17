using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     Configuration options for Claude agent that can be bound from appsettings.json.
/// </summary>
/// <remarks>
///     <para>
///         Example appsettings.json:
///         <code>
///     {
///       "Claude": {
///         "Model": "sonnet",
///         "MaxTurns": 10,
///         "MaxBudgetUsd": 1.0,
///         "WorkingDirectory": "C:/Projects/MyApp",
///         "AllowedTools": ["Read", "Write", "Bash"],
///         "DisallowedTools": ["WebSearch"],
///         "SystemPrompt": "You are a helpful assistant.",
///         "PermissionMode": "AcceptEdits",
///         "McpServers": {
///           "tools": {
///             "Type": "Stdio",
///             "Command": "python",
///             "Args": ["server.py"]
///           }
///         }
///       }
///     }
///     </code>
///     </para>
/// </remarks>
public class ClaudeAgentConfiguration
{
    /// <summary>
    ///     The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Claude";

    /// <summary>
    ///     The model to use (e.g., "sonnet", "opus", "haiku").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    ///     Fallback model if primary is unavailable.
    /// </summary>
    public string? FallbackModel { get; set; }

    /// <summary>
    ///     Maximum number of conversation turns.
    /// </summary>
    public int? MaxTurns { get; set; }

    /// <summary>
    ///     Maximum budget in USD.
    /// </summary>
    public double? MaxBudgetUsd { get; set; }

    /// <summary>
    ///     Working directory for the agent.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    ///     Path to the Claude CLI executable.
    /// </summary>
    public string? CliPath { get; set; }

    /// <summary>
    ///     Tools to allow (whitelist).
    /// </summary>
    public List<string>? AllowedTools { get; set; }

    /// <summary>
    ///     Tools to disallow (blacklist).
    /// </summary>
    public List<string>? DisallowedTools { get; set; }

    /// <summary>
    ///     Custom system prompt.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Whether to use the claude_code system prompt preset.
    /// </summary>
    public bool UseClaudeCodePreset { get; set; }

    /// <summary>
    ///     Text to append to the claude_code preset prompt.
    /// </summary>
    public string? ClaudeCodePresetAppend { get; set; }

    /// <summary>
    ///     Permission mode (Default, AcceptEdits, BypassPermissions, etc.).
    /// </summary>
    public string? PermissionMode { get; set; }

    /// <summary>
    ///     Maximum thinking tokens.
    /// </summary>
    public int? MaxThinkingTokens { get; set; }

    /// <summary>
    ///     Enable file checkpointing for rewind support.
    /// </summary>
    public bool EnableFileCheckpointing { get; set; }

    /// <summary>
    ///     Beta features to enable.
    /// </summary>
    public List<string>? Betas { get; set; }

    /// <summary>
    ///     Additional directories to add to the agent's scope.
    /// </summary>
    public List<string>? AddDirectories { get; set; }

    /// <summary>
    ///     Environment variables to set.
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    ///     MCP server configurations.
    /// </summary>
    public Dictionary<string, McpServerConfiguration>? McpServers { get; set; }

    /// <summary>
    ///     User identifier for analytics.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    ///     Channel capacity for message streaming.
    /// </summary>
    public int MessageChannelCapacity { get; set; } = 256;

    /// <summary>
    ///     Converts to ClaudeAgentOptions.
    /// </summary>
    public ClaudeAgentOptions ToOptions()
    {
        // Build system prompt
        SystemPromptConfig? systemPrompt = null;
        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            systemPrompt = new CustomSystemPrompt(SystemPrompt);
        }
        else if (UseClaudeCodePreset)
        {
            systemPrompt = SystemPromptConfig.ClaudeCode(ClaudeCodePresetAppend);
        }

        // Parse permission mode
        PermissionMode? permissionMode = null;
        if (!string.IsNullOrEmpty(PermissionMode) &&
            Enum.TryParse(PermissionMode, true, out PermissionMode mode))
        {
            permissionMode = mode;
        }

        // Build MCP servers dictionary
        IReadOnlyDictionary<string, McpServerConfig>? mcpServers = null;
        if (McpServers is { Count: > 0 })
        {
            mcpServers = McpServers.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToConfig());
        }

        // Create options with all init-only properties in the initializer
        return new ClaudeAgentOptions
        {
            MaxTurns = MaxTurns,
            MaxBudgetUsd = MaxBudgetUsd,
            WorkingDirectory = WorkingDirectory,
            CliPath = CliPath,
            AllowedTools = AllowedTools ?? [],
            DisallowedTools = DisallowedTools ?? [],
            MaxThinkingTokens = MaxThinkingTokens,
            EnableFileCheckpointing = EnableFileCheckpointing,
            Betas = Betas,
            AddDirectories = AddDirectories ?? [],
            Environment = Environment ?? new Dictionary<string, string>(),
            User = User,
            MessageChannelCapacity = MessageChannelCapacity,
            ModelId = !string.IsNullOrEmpty(Model) ? new ModelIdentifier(Model) : default(ModelIdentifier?),
            FallbackModelId = !string.IsNullOrEmpty(FallbackModel) ? new ModelIdentifier(FallbackModel) : default(ModelIdentifier?),
            SystemPrompt = systemPrompt,
            PermissionMode = permissionMode,
            McpServers = mcpServers
        };
    }
}

/// <summary>
///     MCP server configuration for appsettings.json binding.
/// </summary>
public class McpServerConfiguration
{
    /// <summary>
    ///     Server type: Stdio, Sse, or Http.
    /// </summary>
    public string Type { get; set; } = "Stdio";

    /// <summary>
    ///     Command to execute (for Stdio servers).
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    ///     Arguments for the command (for Stdio servers).
    /// </summary>
    public List<string>? Args { get; set; }

    /// <summary>
    ///     URL for remote servers (Sse or Http).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Environment variables (for Stdio servers).
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    ///     Headers (for Sse and Http servers).
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    ///     Converts to McpServerConfig.
    /// </summary>
    public McpServerConfig ToConfig()
    {
        return Type.ToLowerInvariant() switch
        {
            "stdio" => new McpStdioServerConfig
            {
                Command = Command ?? throw new InvalidOperationException("Stdio server requires Command"),
                Args = Args,
                Env = Environment
            },
            "sse" => new McpSseServerConfig
            {
                Url = Url ?? throw new InvalidOperationException("Sse server requires Url"),
                Headers = Headers
            },
            "http" => new McpHttpServerConfig
            {
                Url = Url ?? throw new InvalidOperationException("Http server requires Url"),
                Headers = Headers
            },
            _ => throw new InvalidOperationException($"Unknown MCP server type: {Type}")
        };
    }
}
