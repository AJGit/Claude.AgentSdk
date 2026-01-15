namespace Claude.AgentSdk.Tools;

/// <summary>
///     Interface for in-process MCP tool servers.
/// </summary>
public interface IMcpToolServer
{
    /// <summary>
    ///     Name of the MCP server.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Handle a JSONRPC request.
    /// </summary>
    Task<object?> HandleRequestAsync(JsonElement request, CancellationToken cancellationToken = default);
}
