using Claude.AgentSdk.Types;

namespace Claude.AgentSdk;

/// <summary>
///     Information about an available slash command.
/// </summary>
public sealed record SlashCommand
{
    /// <summary>
    ///     The command name (e.g., "help", "clear", "compact").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    ///     Description of what the command does.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    ///     Hint about expected arguments (e.g., "[query]", "&lt;file&gt;").
    /// </summary>
    [JsonPropertyName("argumentHint")]
    public string ArgumentHint { get; init; } = "";
}

/// <summary>
///     Information about an available model.
/// </summary>
public sealed record ModelInfo
{
    /// <summary>
    ///     The model identifier used in API calls (e.g., "claude-sonnet-4-20250514").
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>
    ///     Human-readable display name (e.g., "Claude Sonnet 4").
    /// </summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>
    ///     Description of the model's capabilities.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
}

/// <summary>
///     Status of a connected MCP server.
/// </summary>
public sealed record McpServerStatusInfo
{
    /// <summary>
    ///     The server name as configured.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    ///     Connection status: "connected", "failed", "needs-auth", or "pending".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    ///     Gets the strongly-typed status enum value.
    /// </summary>
    [JsonIgnore]
    public McpServerStatusType StatusEnum => EnumStringMappings.ParseMcpServerStatusType(Status);

    /// <summary>
    ///     Server information if connected.
    /// </summary>
    [JsonPropertyName("serverInfo")]
    public McpServerInfo? ServerInfo { get; init; }

    /// <summary>
    ///     Error message if connection failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    ///     Server configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public JsonElement? Config { get; init; }

    /// <summary>
    ///     Server scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>
    ///     Tools provided by this server.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<string>? Tools { get; init; }
}

/// <summary>
///     Information about the MCP server itself.
/// </summary>
public sealed record McpServerInfo
{
    /// <summary>
    ///     The server's reported name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    ///     The server's version string.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

/// <summary>
///     Account information for the authenticated user.
/// </summary>
public sealed record AccountInfo
{
    /// <summary>
    ///     The user's email address.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    ///     The organization name.
    /// </summary>
    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    /// <summary>
    ///     The subscription type (e.g., "pro", "team", "enterprise").
    /// </summary>
    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; init; }

    /// <summary>
    ///     Source of the authentication token.
    /// </summary>
    [JsonPropertyName("tokenSource")]
    public string? TokenSource { get; init; }

    /// <summary>
    ///     Source of the API key.
    /// </summary>
    [JsonPropertyName("apiKeySource")]
    public string? ApiKeySource { get; init; }
}
