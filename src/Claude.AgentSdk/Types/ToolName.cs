namespace Claude.AgentSdk.Types;

/// <summary>
///     Strongly-typed tool name identifier that replaces magic string tool names.
///     Provides type safety while maintaining backward compatibility via implicit conversions.
/// </summary>
/// <remarks>
///     <para>
///     Built-in Claude Code tools are available as static properties.
///     MCP server tools can be created using the <see cref="Mcp(string, string)"/> factory method.
///     </para>
///     <para>
///     Implicit conversions allow seamless migration from string-based tool names:
///     <code>
///     // Old way (still works)
///     AllowedTools = new[] { "Read", "Write", "Bash" }
///
///     // New strongly-typed way
///     AllowedTools = new[] { ToolName.Read, ToolName.Write, ToolName.Bash }
///     </code>
///     </para>
/// </remarks>
public readonly struct ToolName : IEquatable<ToolName>
{
    private readonly string _value;

    /// <summary>
    ///     Creates a tool name from a string value.
    /// </summary>
    /// <param name="value">The tool name string.</param>
    public ToolName(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    ///     Gets the underlying string value of the tool name.
    /// </summary>
    public string Value => _value ?? string.Empty;

    #region Built-in Claude Code Tools

    /// <summary>
    ///     Read tool - reads files from the filesystem.
    /// </summary>
    public static ToolName Read => new("Read");

    /// <summary>
    ///     Write tool - writes files to the filesystem.
    /// </summary>
    public static ToolName Write => new("Write");

    /// <summary>
    ///     Edit tool - performs precise edits to existing files.
    /// </summary>
    public static ToolName Edit => new("Edit");

    /// <summary>
    ///     MultiEdit tool - performs multiple edits in a single operation.
    /// </summary>
    public static ToolName MultiEdit => new("MultiEdit");

    /// <summary>
    ///     Bash tool - executes bash commands.
    /// </summary>
    public static ToolName Bash => new("Bash");

    /// <summary>
    ///     Grep tool - searches file contents using regular expressions.
    /// </summary>
    public static ToolName Grep => new("Grep");

    /// <summary>
    ///     Glob tool - finds files matching patterns.
    /// </summary>
    public static ToolName Glob => new("Glob");

    /// <summary>
    ///     Task tool - spawns subagents for parallel or specialized work.
    /// </summary>
    public static ToolName Task => new("Task");

    /// <summary>
    ///     WebFetch tool - fetches and analyzes web content.
    /// </summary>
    public static ToolName WebFetch => new("WebFetch");

    /// <summary>
    ///     WebSearch tool - searches the web for information.
    /// </summary>
    public static ToolName WebSearch => new("WebSearch");

    /// <summary>
    ///     TodoRead tool - reads the current todo list.
    /// </summary>
    public static ToolName TodoRead => new("TodoRead");

    /// <summary>
    ///     TodoWrite tool - creates or updates the todo list.
    /// </summary>
    public static ToolName TodoWrite => new("TodoWrite");

    /// <summary>
    ///     NotebookEdit tool - edits Jupyter notebook cells.
    /// </summary>
    public static ToolName NotebookEdit => new("NotebookEdit");

    /// <summary>
    ///     AskUserQuestion tool - asks the user a question with options.
    /// </summary>
    public static ToolName AskUserQuestion => new("AskUserQuestion");

    /// <summary>
    ///     Skill tool - invokes a skill/slash command.
    /// </summary>
    public static ToolName Skill => new("Skill");

    /// <summary>
    ///     TaskOutput tool - retrieves output from background tasks.
    /// </summary>
    public static ToolName TaskOutput => new("TaskOutput");

    /// <summary>
    ///     KillShell tool - terminates a background shell.
    /// </summary>
    public static ToolName KillShell => new("KillShell");

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a tool name for an MCP server tool.
    /// </summary>
    /// <param name="serverName">The MCP server name.</param>
    /// <param name="toolName">The tool name on the server.</param>
    /// <returns>A ToolName in the format "mcp__{serverName}__{toolName}".</returns>
    public static ToolName Mcp(string serverName, string toolName)
    {
        if (string.IsNullOrEmpty(serverName))
            throw new ArgumentException("Server name cannot be null or empty.", nameof(serverName));
        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty.", nameof(toolName));

        return new ToolName($"mcp__{serverName}__{toolName}");
    }

    /// <summary>
    ///     Creates a tool name for an MCP server tool using a strongly-typed server name.
    /// </summary>
    /// <param name="serverName">The MCP server name.</param>
    /// <param name="toolName">The tool name on the server.</param>
    /// <returns>A ToolName in the format "mcp__{serverName}__{toolName}".</returns>
    public static ToolName Mcp(McpServerName serverName, string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty.", nameof(toolName));

        return new ToolName($"mcp__{serverName.Value}__{toolName}");
    }

    /// <summary>
    ///     Creates a tool name from a custom string value.
    /// </summary>
    /// <param name="name">The tool name string.</param>
    /// <returns>A new ToolName instance.</returns>
    public static ToolName Custom(string name) => new(name);

    /// <summary>
    ///     Creates a tool name from a nullable string.
    /// </summary>
    /// <param name="name">The tool name string, or null.</param>
    /// <returns>A ToolName if the string is not null, otherwise null.</returns>
    public static ToolName? FromNullable(string? name) =>
        name is null ? (ToolName?)null : new ToolName(name);

    #endregion

    #region MCP Tool Helpers

    /// <summary>
    ///     Returns true if this is an MCP server tool (starts with "mcp__").
    /// </summary>
    public bool IsMcpTool => _value?.StartsWith("mcp__", StringComparison.Ordinal) == true;

    /// <summary>
    ///     Extracts the server name from an MCP tool name.
    /// </summary>
    /// <returns>The server name, or null if this is not an MCP tool.</returns>
    public string? GetMcpServerName()
    {
        if (!IsMcpTool || _value is null)
            return null;

        var parts = _value.Split(new[] { "__" }, StringSplitOptions.None);
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    ///     Extracts the tool name from an MCP tool name.
    /// </summary>
    /// <returns>The tool name portion, or null if this is not an MCP tool.</returns>
    public string? GetMcpToolName()
    {
        if (!IsMcpTool || _value is null)
            return null;

        var parts = _value.Split(new[] { "__" }, StringSplitOptions.None);
        return parts.Length >= 3 ? parts[2] : null;
    }

    #endregion

    #region Implicit Conversions

    /// <summary>
    ///     Implicitly converts a string to a ToolName for backward compatibility.
    /// </summary>
    public static implicit operator ToolName(string value) => new(value);

    /// <summary>
    ///     Implicitly converts a ToolName to a string.
    /// </summary>
    public static implicit operator string(ToolName tool) => tool.Value;

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(ToolName other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ToolName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value?.GetHashCode() ?? 0;

    /// <summary>
    ///     Compares two tool names for equality.
    /// </summary>
    public static bool operator ==(ToolName left, ToolName right) =>
        left.Equals(right);

    /// <summary>
    ///     Compares two tool names for inequality.
    /// </summary>
    public static bool operator !=(ToolName left, ToolName right) =>
        !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
