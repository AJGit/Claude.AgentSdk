using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Claude.AgentSdk.Tools;

/// <summary>
///     Result from a tool execution.
/// </summary>
public sealed record ToolResult
{
    /// <summary>
    ///     Content blocks returned by the tool.
    /// </summary>
    public required IReadOnlyList<ToolResultContent> Content { get; init; }

    /// <summary>
    ///     Whether the result is an error.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    ///     Create a text result.
    /// </summary>
    public static ToolResult Text(string text)
    {
        return new ToolResult
        {
            Content = [new ToolResultTextContent { Text = text }]
        };
    }

    /// <summary>
    ///     Create an error result.
    /// </summary>
    public static ToolResult Error(string message)
    {
        return new ToolResult
        {
            Content = [new ToolResultTextContent { Text = message }],
            IsError = true
        };
    }
}

/// <summary>
///     Base class for tool result content.
/// </summary>
public abstract record ToolResultContent
{
    public abstract string Type { get; }
}

/// <summary>
///     Text content in a tool result.
/// </summary>
public sealed record ToolResultTextContent : ToolResultContent
{
    public override string Type => "text";
    public required string Text { get; init; }
}

/// <summary>
///     Tool definition for registration.
/// </summary>
public sealed class ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject InputSchema { get; init; }
    public required Func<JsonElement, CancellationToken, Task<ToolResult>> Handler { get; init; }
}

/// <summary>
///     Attribute to mark a method as a Claude tool.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ClaudeToolAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}

/// <summary>
///     In-process MCP tool server for defining tools in C#.
/// </summary>
public sealed class McpToolServer(string name, string version = "1.0.0") : IMcpToolServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, ToolDefinition> _tools = new();
    private readonly string _version = version;

    /// <summary>
    ///     Gets the registered tools.
    /// </summary>
    public IReadOnlyCollection<ToolDefinition> Tools => _tools.Values;

    public string Name { get; } = name;

    public async Task<object?> HandleRequestAsync(JsonElement request, CancellationToken cancellationToken = default)
    {
        var method = request.GetProperty("method").GetString()!;

        // Handle notifications - return empty acknowledgment
        if (method == "notifications/initialized")
        {
            return BuildJsonRpcResponse(null, new Dictionary<string, object>());
        }

        var id = ExtractJsonRpcId(request);

        try
        {
            var result = await DispatchMethodAsync(method, request, cancellationToken).ConfigureAwait(false);
            return BuildJsonRpcResponse(id, result);
        }
        catch (Exception ex)
        {
            return BuildJsonRpcError(id, -32603, ex.Message);
        }
    }

    /// <summary>
    ///     Register a tool with an explicit definition.
    /// </summary>
    public McpToolServer RegisterTool(ToolDefinition tool)
    {
        _tools[tool.Name] = tool;
        return this;
    }

    /// <summary>
    ///     Register a tool with a simple handler.
    /// </summary>
    public McpToolServer RegisterTool(
        string name,
        string description,
        JsonObject inputSchema,
        Func<JsonElement, CancellationToken, Task<ToolResult>> handler)
    {
        _tools[name] = new ToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
            Handler = handler
        };
        return this;
    }

    /// <summary>
    ///     Register a tool with a typed input.
    /// </summary>
    public McpToolServer RegisterTool<TInput>(
        string name,
        string description,
        Func<TInput, CancellationToken, Task<ToolResult>> handler)
        where TInput : class
    {
        var schema = GenerateSchema<TInput>();

        _tools[name] = new ToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = schema,
            Handler = async (input, ct) =>
            {
                var typed = JsonSerializer.Deserialize<TInput>(input.GetRawText(), JsonOptions);
                if (typed is null)
                {
                    return ToolResult.Error("Failed to deserialize input");
                }

                return await handler(typed, ct).ConfigureAwait(false);
            }
        };

        return this;
    }

    /// <summary>
    ///     Register tools from an object with [ClaudeTool] attributes.
    /// </summary>
    public McpToolServer RegisterToolsFrom(object instance)
    {
        var type = instance.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<ClaudeToolAttribute>();
            if (attr is null)
            {
                continue;
            }

            var parameters = method.GetParameters();
            var schema = GenerateSchemaFromParameters(parameters);

            _tools[attr.Name] = new ToolDefinition
            {
                Name = attr.Name,
                Description = attr.Description,
                InputSchema = schema,
                Handler = async (input, ct) =>
                {
                    try
                    {
                        var args = DeserializeParameters(input, parameters);
                        var result = method.Invoke(instance, args);

                        if (result is Task<ToolResult> taskResult)
                        {
                            return await taskResult.ConfigureAwait(false);
                        }

                        if (result is Task<string> taskString)
                        {
                            return ToolResult.Text(await taskString.ConfigureAwait(false));
                        }

                        if (result is ToolResult toolResult)
                        {
                            return toolResult;
                        }

                        if (result is string str)
                        {
                            return ToolResult.Text(str);
                        }

                        return ToolResult.Text(result?.ToString() ?? "");
                    }
                    catch (Exception ex)
                    {
                        return ToolResult.Error(ex.Message);
                    }
                }
            };
        }

        return this;
    }

    private async Task<object?> DispatchMethodAsync(string method, JsonElement request,
        CancellationToken cancellationToken)
    {
        return method switch
        {
            "initialize" => HandleInitialize(),
            "tools/list" => HandleToolsList(),
            "tools/call" => await HandleToolsCallAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Unknown method: {method}")
        };
    }

    private static object? ExtractJsonRpcId(JsonElement request)
    {
        if (!request.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        return idElement.ValueKind switch
        {
            JsonValueKind.Number => idElement.GetInt64(),
            JsonValueKind.String => idElement.GetString(),
            _ => null
        };
    }

    private static Dictionary<string, object?> BuildJsonRpcResponse(object? id, object? result)
    {
        var response = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = id };
        if (result is not null)
        {
            response["result"] = result;
        }

        return response;
    }

    private static Dictionary<string, object?> BuildJsonRpcError(object? id, int code, string message)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object> { ["code"] = code, ["message"] = message }
        };
    }

    private Dictionary<string, object> HandleInitialize()
    {
        // Use Dictionary to preserve exact property names (avoid snake_case conversion)
        return new Dictionary<string, object>
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new Dictionary<string, object>
            {
                ["tools"] = new Dictionary<string, object>()
            },
            ["serverInfo"] = new Dictionary<string, object>
            {
                ["name"] = Name,
                ["version"] = _version
            }
        };
    }

    private Dictionary<string, object> HandleToolsList()
    {
        // Use Dictionary to preserve exact property names (avoid snake_case conversion)
        var tools = _tools.Values.Select(t => new Dictionary<string, object?>
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = t.InputSchema
        }).ToList();

        return new Dictionary<string, object> { ["tools"] = tools };
    }

    private async Task<object> HandleToolsCallAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var @params = request.GetProperty("params");
        var toolName = @params.GetProperty("name").GetString()!;
        var arguments = @params.TryGetProperty("arguments", out var args) ? args : default;

        if (!_tools.TryGetValue(toolName, out var tool))
        {
            throw new InvalidOperationException($"Unknown tool: {toolName}");
        }

        var result = await tool.Handler(arguments, cancellationToken).ConfigureAwait(false);

        // Use Dictionary to preserve exact property names (avoid snake_case conversion)
        var content = result.Content.Select(c => c switch
        {
            ToolResultTextContent text => (object)new Dictionary<string, object>
                { ["type"] = "text", ["text"] = text.Text },
            _ => new Dictionary<string, object> { ["type"] = "text", ["text"] = c.ToString()! }
        }).ToList();

        return new Dictionary<string, object>
        {
            ["content"] = content,
            ["isError"] = result.IsError
        };
    }

    private static JsonObject GenerateSchema<T>()
    {
        var type = typeof(T);
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var prop in type.GetProperties())
        {
            var propSchema = GetPropertySchema(prop.PropertyType);
            properties[ToCamelCase(prop.Name)] = propSchema;

            // Check if nullable
            var nullableAttr = prop.GetCustomAttribute<NullableAttribute>();
            if (nullableAttr is null || (nullableAttr.NullableFlags.Length > 0 && nullableAttr.NullableFlags[0] == 1))
            {
                required.Add(ToCamelCase(prop.Name));
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static JsonObject GenerateSchemaFromParameters(ParameterInfo[] parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var param in parameters)
        {
            if (param.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            var propSchema = GetPropertySchema(param.ParameterType);
            properties[param.Name!] = propSchema;

            if (!param.HasDefaultValue)
            {
                required.Add(param.Name!);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    /// <remarks>
    ///     Complexity is due to mapping multiple primitive types - each is a simple type check with no nested logic.
    /// </remarks>
    private static JsonObject GetPropertySchema(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying switch
        {
            _ when underlying == typeof(string) => new JsonObject { ["type"] = "string" },
            _ when underlying == typeof(int) || underlying == typeof(long) => new JsonObject { ["type"] = "integer" },
            _ when underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal) =>
                new JsonObject { ["type"] = "number" },
            _ when underlying == typeof(bool) => new JsonObject { ["type"] = "boolean" },
            _ when underlying.IsArray => new JsonObject
            {
                ["type"] = "array",
                ["items"] = GetPropertySchema(underlying.GetElementType()!)
            },
            _ => new JsonObject { ["type"] = "object" }
        };
    }

    private static object?[] DeserializeParameters(JsonElement input, ParameterInfo[] parameters)
    {
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            if (input.TryGetProperty(param.Name!, out var value))
            {
                args[i] = JsonSerializer.Deserialize(value.GetRawText(), param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
