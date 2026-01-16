using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Builders;

/// <summary>
///     Fluent builder for configuring MCP server collections.
/// </summary>
/// <remarks>
///     <para>
///     This builder provides a more ergonomic way to configure multiple MCP servers
///     compared to manually constructing dictionaries.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     var servers = new McpServerBuilder()
///         .AddStdio("file-tools", "python", "file_tools.py")
///             .WithEnvironment("DEBUG", "true")
///         .AddSse("remote-api", "https://api.example.com/mcp")
///             .WithHeaders("Authorization", "Bearer token")
///         .AddSdk("excel-tools", excelToolServer)
///         .Build();
///
///     var options = new ClaudeAgentOptions { McpServers = servers };
///     </code>
///     </para>
/// </remarks>
public sealed class McpServerBuilder
{
    private readonly Dictionary<string, McpServerConfig> _servers = new();
    private readonly Dictionary<string, Dictionary<string, string>> _envVars = new();
    private readonly Dictionary<string, Dictionary<string, string>> _headers = new();
    private string? _currentServerName;

    /// <summary>
    ///     Adds a stdio-based MCP server that runs a local command.
    /// </summary>
    /// <param name="name">Unique name for the server.</param>
    /// <param name="command">Command to execute (e.g., "python", "node").</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder AddStdio(string name, string command, params string[] args)
    {
        _servers[name] = new McpStdioServerConfig
        {
            Command = command,
            Args = args.Length > 0 ? args : null
        };
        _currentServerName = name;
        return this;
    }

    /// <summary>
    ///     Adds a Server-Sent Events (SSE) based MCP server.
    /// </summary>
    /// <param name="name">Unique name for the server.</param>
    /// <param name="url">URL of the SSE endpoint.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder AddSse(string name, string url)
    {
        _servers[name] = new McpSseServerConfig { Url = url };
        _currentServerName = name;
        return this;
    }

    /// <summary>
    ///     Adds an HTTP-based MCP server.
    /// </summary>
    /// <param name="name">Unique name for the server.</param>
    /// <param name="url">URL of the HTTP endpoint.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder AddHttp(string name, string url)
    {
        _servers[name] = new McpHttpServerConfig { Url = url };
        _currentServerName = name;
        return this;
    }

    /// <summary>
    ///     Adds an in-process SDK MCP server.
    /// </summary>
    /// <param name="name">Unique name for the server.</param>
    /// <param name="instance">The MCP tool server instance.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder AddSdk(string name, IMcpToolServer instance)
    {
        _servers[name] = new McpSdkServerConfig { Name = name, Instance = instance };
        _currentServerName = name;
        return this;
    }

    /// <summary>
    ///     Adds an environment variable to the current server.
    ///     Only applies to stdio servers.
    /// </summary>
    /// <param name="key">Environment variable name.</param>
    /// <param name="value">Environment variable value.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no server has been added yet.</exception>
    public McpServerBuilder WithEnvironment(string key, string value)
    {
        EnsureCurrentServer();

        if (!_envVars.TryGetValue(_currentServerName!, out var vars))
        {
            vars = new Dictionary<string, string>();
            _envVars[_currentServerName!] = vars;
        }

        vars[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple environment variables to the current server.
    ///     Only applies to stdio servers.
    /// </summary>
    /// <param name="variables">Dictionary of environment variable names and values.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder WithEnvironment(IReadOnlyDictionary<string, string> variables)
    {
        EnsureCurrentServer();

        if (!_envVars.TryGetValue(_currentServerName!, out var vars))
        {
            vars = new Dictionary<string, string>();
            _envVars[_currentServerName!] = vars;
        }

        foreach (var (key, value) in variables)
        {
            vars[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Adds a header to the current server.
    ///     Only applies to SSE and HTTP servers.
    /// </summary>
    /// <param name="key">Header name.</param>
    /// <param name="value">Header value.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no server has been added yet.</exception>
    public McpServerBuilder WithHeaders(string key, string value)
    {
        EnsureCurrentServer();

        if (!_headers.TryGetValue(_currentServerName!, out var hdrs))
        {
            hdrs = new Dictionary<string, string>();
            _headers[_currentServerName!] = hdrs;
        }

        hdrs[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple headers to the current server.
    ///     Only applies to SSE and HTTP servers.
    /// </summary>
    /// <param name="headers">Dictionary of header names and values.</param>
    /// <returns>This builder for chaining.</returns>
    public McpServerBuilder WithHeaders(IReadOnlyDictionary<string, string> headers)
    {
        EnsureCurrentServer();

        if (!_headers.TryGetValue(_currentServerName!, out var hdrs))
        {
            hdrs = new Dictionary<string, string>();
            _headers[_currentServerName!] = hdrs;
        }

        foreach (var (key, value) in headers)
        {
            hdrs[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Builds the server configuration dictionary.
    /// </summary>
    /// <returns>A dictionary of server configurations keyed by name.</returns>
    public IReadOnlyDictionary<string, McpServerConfig> Build()
    {
        var result = new Dictionary<string, McpServerConfig>();

        foreach (var (name, config) in _servers)
        {
            var finalConfig = ApplyModifiers(name, config);
            result[name] = finalConfig;
        }

        return result;
    }

    private McpServerConfig ApplyModifiers(string name, McpServerConfig config)
    {
        switch (config)
        {
            case McpStdioServerConfig stdio when _envVars.TryGetValue(name, out var env):
                return stdio with { Env = env };

            case McpSseServerConfig sse when _headers.TryGetValue(name, out var hdrs):
                return sse with { Headers = hdrs };

            case McpHttpServerConfig http when _headers.TryGetValue(name, out var hdrs):
                return http with { Headers = hdrs };

            default:
                return config;
        }
    }

    private void EnsureCurrentServer()
    {
        if (_currentServerName is null)
        {
            throw new InvalidOperationException(
                "No server has been added yet. Call AddStdio, AddSse, AddHttp, or AddSdk first.");
        }
    }
}
