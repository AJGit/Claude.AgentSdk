using System.Text.Json.Nodes;
using Claude.AgentSdk.Schema;

namespace Claude.AgentSdk.Tools;

/// <summary>
///     Static helper functions for creating tools and MCP servers.
///     Provides a fluent API similar to the TypeScript SDK.
/// </summary>
public static class ToolHelpers
{
    /// <summary>
    ///     Creates a type-safe tool definition for use with SDK MCP servers.
    /// </summary>
    /// <typeparam name="TInput">The type representing the tool's input parameters.</typeparam>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="handler">Async function that executes the tool logic.</param>
    /// <returns>A tool definition that can be registered with an MCP server.</returns>
    /// <example>
    ///     <code>
    /// var weatherTool = ToolHelpers.Tool&lt;WeatherInput&gt;(
    ///     "get-weather",
    ///     "Gets the current weather for a location",
    ///     async (input, ct) =>
    ///     {
    ///         var weather = await GetWeatherAsync(input.Location, ct);
    ///         return ToolResult.Text($"Weather in {input.Location}: {weather}");
    ///     }
    /// );
    /// </code>
    /// </example>
    public static ToolDefinition Tool<TInput>(
        string name,
        string description,
        Func<TInput, CancellationToken, Task<ToolResult>> handler)
        where TInput : class
    {
        var schema = SchemaGenerator.Generate<TInput>(name);
        var schemaNode = JsonNode.Parse(schema.GetRawText())?.AsObject()
                         ?? throw new InvalidOperationException("Failed to parse generated schema");

        return new ToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = schemaNode,
            Handler = async (input, ct) =>
            {
                var typed = input.Deserialize<TInput>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (typed is null)
                {
                    return ToolResult.Error($"Failed to deserialize input for tool '{name}'");
                }

                return await handler(typed, ct).ConfigureAwait(false);
            }
        };
    }

    /// <summary>
    ///     Creates a tool definition with a custom JSON schema.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="inputSchema">JSON schema defining the tool's input parameters.</param>
    /// <param name="handler">Async function that executes the tool logic.</param>
    /// <returns>A tool definition that can be registered with an MCP server.</returns>
    public static ToolDefinition Tool(
        string name,
        string description,
        JsonObject inputSchema,
        Func<JsonElement, CancellationToken, Task<ToolResult>> handler)
    {
        return new ToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
            Handler = handler
        };
    }

    /// <summary>
    ///     Creates an MCP server instance that runs in the same process as your application.
    /// </summary>
    /// <param name="name">The name of the MCP server.</param>
    /// <param name="version">Optional version string.</param>
    /// <param name="tools">Array of tool definitions created with <see cref="Tool{TInput}" />.</param>
    /// <returns>An MCP server configuration that can be used with <see cref="ClaudeAgentOptions.McpServers" />.</returns>
    /// <example>
    ///     <code>
    /// var server = ToolHelpers.CreateSdkMcpServer(
    ///     name: "my-tools",
    ///     tools: [
    ///         ToolHelpers.Tool&lt;WeatherInput&gt;("get-weather", "Gets weather", HandleWeather),
    ///         ToolHelpers.Tool&lt;TimeInput&gt;("get-time", "Gets time", HandleTime)
    ///     ]
    /// );
    /// 
    /// var options = new ClaudeAgentOptions
    /// {
    ///     McpServers = new Dictionary&lt;string, McpServerConfig&gt;
    ///     {
    ///         ["my-tools"] = server
    ///     }
    /// };
    /// </code>
    /// </example>
    public static McpSdkServerConfig CreateSdkMcpServer(
        string name,
        string? version = null,
        IEnumerable<ToolDefinition>? tools = null)
    {
        var server = new McpToolServer(name, version ?? "1.0.0");

        if (tools is not null)
        {
            foreach (var tool in tools)
            {
                server.RegisterTool(tool);
            }
        }

        return new McpSdkServerConfig
        {
            Name = name,
            Instance = server
        };
    }

    /// <summary>
    ///     Creates an MCP server with tools defined via a configuration action.
    /// </summary>
    /// <param name="name">The name of the MCP server.</param>
    /// <param name="configure">Action to configure and register tools on the server.</param>
    /// <returns>An MCP server configuration.</returns>
    /// <example>
    ///     <code>
    /// var server = ToolHelpers.CreateSdkMcpServer("my-tools", server =>
    /// {
    ///     server.RegisterTool&lt;WeatherInput&gt;("get-weather", "Gets weather", HandleWeather);
    ///     server.RegisterToolsFrom(new MyToolsClass());
    /// });
    /// </code>
    /// </example>
    public static McpSdkServerConfig CreateSdkMcpServer(
        string name,
        Action<McpToolServer> configure)
    {
        var server = new McpToolServer(name);
        configure(server);

        return new McpSdkServerConfig
        {
            Name = name,
            Instance = server
        };
    }

    /// <summary>
    ///     Creates an MCP server from a type containing methods marked with <see cref="ClaudeToolAttribute" />.
    /// </summary>
    /// <typeparam name="T">The type containing tool methods.</typeparam>
    /// <param name="name">The name of the MCP server.</param>
    /// <param name="version">Optional version string (default: "1.0.0").</param>
    /// <returns>An MCP server configuration ready for use.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type has no methods marked with <see cref="ClaudeToolAttribute" />.
    /// </exception>
    /// <example>
    ///     <code>
    /// public class Calculator
    /// {
    ///     [ClaudeTool("add", "Add two numbers")]
    ///     public string Add(AddArgs args) => $"Result: {args.A + args.B}";
    /// }
    /// 
    /// var server = ToolHelpers.FromType&lt;Calculator&gt;("calculator");
    /// </code>
    /// </example>
    public static McpSdkServerConfig FromType<T>(string name, string? version = null)
        where T : new()
    {
        return FromInstance(new T(), name, version);
    }

    /// <summary>
    ///     Creates an MCP server from an instance containing methods marked with <see cref="ClaudeToolAttribute" />.
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="instance">The instance containing tool methods.</param>
    /// <param name="name">The name of the MCP server.</param>
    /// <param name="version">Optional version string (default: "1.0.0").</param>
    /// <returns>An MCP server configuration ready for use.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the instance has no methods marked with <see cref="ClaudeToolAttribute" />.
    /// </exception>
    /// <example>
    ///     <code>
    /// var calculator = new Calculator();
    /// var server = ToolHelpers.FromInstance(calculator, "calculator");
    /// </code>
    /// </example>
    public static McpSdkServerConfig FromInstance<T>(T instance, string name, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var server = new McpToolServer(name, version ?? "1.0.0");
        var toolCount = server.RegisterToolsFrom(instance);

        if (toolCount == 0)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' has no methods marked with [ClaudeTool] attribute.");
        }

        return new McpSdkServerConfig
        {
            Name = name,
            Instance = server
        };
    }
}
