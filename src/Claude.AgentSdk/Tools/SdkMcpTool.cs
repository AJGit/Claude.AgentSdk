using System.Text.Json.Nodes;

namespace Claude.AgentSdk.Tools;

/// <summary>
///     Factory for creating MCP tool definitions.
/// </summary>
/// <remarks>
///     <para>
///         Use this class to create individual tool definitions that can be registered
///         with an <see cref="McpToolServer" /> or passed to ToolHelpers.CreateSdkMcpServer.
///     </para>
///     <para>
///         For creating complete MCP servers from types with <c>[ClaudeTool]</c> attributes,
///         use <see cref="ToolHelpers.FromType{T}" /> instead.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Create a type-safe tool
/// var addTool = SdkMcpTool.Create&lt;AddArgs&gt;(
///     "add",
///     "Add two numbers together",
///     async (args, ct) => ToolResult.Text($"Result: {args.A + args.B}"));
/// 
/// // Create a tool with custom schema
/// var echoTool = SdkMcpTool.Create(
///     "echo",
///     "Echo a message",
///     new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["message"] = new JsonObject { ["type"] = "string" } } },
///     async (input, ct) => ToolResult.Text(input.GetProperty("message").GetString()!));
/// 
/// // Register with a server
/// var server = ToolHelpers.CreateSdkMcpServer("my-tools", tools: [addTool, echoTool]);
/// </code>
/// </example>
public static class SdkMcpTool
{
    /// <summary>
    ///     Create a type-safe tool definition with automatic schema generation.
    /// </summary>
    /// <typeparam name="TInput">The type representing the tool's input parameters.</typeparam>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="handler">Async function that executes the tool logic.</param>
    /// <returns>A tool definition ready for registration.</returns>
    /// <example>
    ///     <code>
    /// public record WeatherArgs(string Location, string? Units);
    /// 
    /// var tool = SdkMcpTool.Create&lt;WeatherArgs&gt;(
    ///     "get-weather",
    ///     "Get the current weather for a location",
    ///     async (args, ct) =>
    ///     {
    ///         var weather = await GetWeatherAsync(args.Location, args.Units ?? "celsius", ct);
    ///         return ToolResult.Text($"Weather in {args.Location}: {weather}");
    ///     });
    /// </code>
    /// </example>
    public static ToolDefinition Create<TInput>(
        string name,
        string description,
        Func<TInput, CancellationToken, Task<ToolResult>> handler)
        where TInput : class
    {
        return ToolHelpers.Tool(name, description, handler);
    }

    /// <summary>
    ///     Create a tool definition with a custom JSON schema.
    /// </summary>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="inputSchema">JSON schema defining the tool's input parameters.</param>
    /// <param name="handler">Async function that executes the tool logic.</param>
    /// <returns>A tool definition ready for registration.</returns>
    /// <example>
    ///     <code>
    /// var schema = new JsonObject
    /// {
    ///     ["type"] = "object",
    ///     ["properties"] = new JsonObject
    ///     {
    ///         ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query" }
    ///     },
    ///     ["required"] = new JsonArray { "query" }
    /// };
    /// 
    /// var tool = SdkMcpTool.Create(
    ///     "search",
    ///     "Search for information",
    ///     schema,
    ///     async (input, ct) =>
    ///     {
    ///         var query = input.GetProperty("query").GetString()!;
    ///         var results = await SearchAsync(query, ct);
    ///         return ToolResult.Text(results);
    ///     });
    /// </code>
    /// </example>
    public static ToolDefinition Create(
        string name,
        string description,
        JsonObject inputSchema,
        Func<JsonElement, CancellationToken, Task<ToolResult>> handler)
    {
        return ToolHelpers.Tool(name, description, inputSchema, handler);
    }

    /// <summary>
    ///     Create a simple tool that returns text.
    /// </summary>
    /// <typeparam name="TInput">The type representing the tool's input parameters.</typeparam>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="handler">Function that returns the text result.</param>
    /// <returns>A tool definition ready for registration.</returns>
    /// <example>
    ///     <code>
    /// var tool = SdkMcpTool.CreateSimple&lt;GreetArgs&gt;(
    ///     "greet",
    ///     "Greet a person",
    ///     args => $"Hello, {args.Name}!");
    /// </code>
    /// </example>
    public static ToolDefinition CreateSimple<TInput>(
        string name,
        string description,
        Func<TInput, string> handler)
        where TInput : class
    {
        return ToolHelpers.Tool<TInput>(
            name,
            description,
            (input, _) => Task.FromResult(ToolResult.Text(handler(input))));
    }

    /// <summary>
    ///     Create a simple async tool that returns text.
    /// </summary>
    /// <typeparam name="TInput">The type representing the tool's input parameters.</typeparam>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="handler">Async function that returns the text result.</param>
    /// <returns>A tool definition ready for registration.</returns>
    public static ToolDefinition CreateSimple<TInput>(
        string name,
        string description,
        Func<TInput, Task<string>> handler)
        where TInput : class
    {
        return ToolHelpers.Tool<TInput>(
            name,
            description,
            async (input, _) => ToolResult.Text(await handler(input).ConfigureAwait(false)));
    }
}
