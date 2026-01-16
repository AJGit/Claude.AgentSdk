namespace Claude.AgentSdk.Types;

/// <summary>
///     Strongly-typed MCP server name identifier.
///     Provides type safety while maintaining backward compatibility via implicit conversions.
/// </summary>
/// <remarks>
///     <para>
///     SDK servers can be created using the <see cref="Sdk"/> factory method.
///     Implicit conversions allow seamless migration from string-based server names.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     // Old way (still works)
///     var serverName = "excel-tools";
///
///     // New strongly-typed way
///     var serverName = McpServerName.Sdk("excel-tools");
///     </code>
///     </para>
/// </remarks>
public readonly struct McpServerName : IEquatable<McpServerName>
{
    private readonly string _value;

    /// <summary>
    ///     Creates an MCP server name from a string value.
    /// </summary>
    /// <param name="value">The server name string.</param>
    public McpServerName(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    ///     Gets the underlying string value of the server name.
    /// </summary>
    public string Value => _value ?? string.Empty;

    #region Factory Methods

    /// <summary>
    ///     Creates a server name for an SDK-defined MCP server.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <returns>A new McpServerName instance.</returns>
    public static McpServerName Sdk(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Server name cannot be null or empty.", nameof(name));

        return new McpServerName(name);
    }

    /// <summary>
    ///     Creates a server name from a custom string value.
    /// </summary>
    /// <param name="name">The server name string.</param>
    /// <returns>A new McpServerName instance.</returns>
    public static McpServerName Custom(string name) => new(name);

    /// <summary>
    ///     Creates a server name from a nullable string.
    /// </summary>
    /// <param name="name">The server name string, or null.</param>
    /// <returns>An McpServerName if the string is not null, otherwise null.</returns>
    public static McpServerName? FromNullable(string? name) =>
        name is null ? (McpServerName?)null : new McpServerName(name);

    #endregion

    #region Tool Name Helpers

    /// <summary>
    ///     Creates a tool name for a tool on this MCP server.
    /// </summary>
    /// <param name="toolName">The tool name on the server.</param>
    /// <returns>A ToolName in the format "mcp__{serverName}__{toolName}".</returns>
    public ToolName Tool(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty.", nameof(toolName));

        return ToolName.Mcp(this, toolName);
    }

    /// <summary>
    ///     Creates multiple tool names for tools on this MCP server.
    /// </summary>
    /// <param name="toolNames">The tool names on the server.</param>
    /// <returns>An array of ToolName instances.</returns>
    public ToolName[] Tools(params string[] toolNames)
    {
        if (toolNames is null)
            throw new ArgumentNullException(nameof(toolNames));

        var result = new ToolName[toolNames.Length];
        for (var i = 0; i < toolNames.Length; i++)
        {
            result[i] = Tool(toolNames[i]);
        }
        return result;
    }

    #endregion

    #region Implicit Conversions

    /// <summary>
    ///     Implicitly converts a string to an McpServerName for backward compatibility.
    /// </summary>
    public static implicit operator McpServerName(string value) => new(value);

    /// <summary>
    ///     Implicitly converts an McpServerName to a string.
    /// </summary>
    public static implicit operator string(McpServerName server) => server.Value;

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(McpServerName other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is McpServerName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value?.GetHashCode() ?? 0;

    /// <summary>
    ///     Compares two server names for equality.
    /// </summary>
    public static bool operator ==(McpServerName left, McpServerName right) =>
        left.Equals(right);

    /// <summary>
    ///     Compares two server names for inequality.
    /// </summary>
    public static bool operator !=(McpServerName left, McpServerName right) =>
        !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
