using System.Text.Json;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Tests.Tools;

/// <summary>
///     Comprehensive tests for McpToolServer and related tool types.
/// </summary>
public class McpToolServerTests
{
    [Fact]
    public void RegisterTool_WithHandlerParameters_RegistersSuccessfully()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["message"] = new JsonObject { ["type"] = "string" }
            }
        };

        // Act
        server.RegisterTool(
            "echo",
            "Echoes a message",
            schema,
            (input, _) => Task.FromResult(ToolResult.Text(input.GetProperty("message").GetString()!)));

        // Assert
        Assert.Single(server.Tools);
        var tool = server.Tools.First();
        Assert.Equal("echo", tool.Name);
        Assert.Equal("Echoes a message", tool.Description);
    }

    public class WeatherInput
    {
        public required string Location { get; init; }
        public string? Units { get; init; }
    }

    public class CalculatorInput
    {
        public required int A { get; init; }
        public required int B { get; init; }
        public string Operation { get; init; } = "add";
    }

    public class ComplexInput
    {
        public required string Name { get; init; }
        public required double Value { get; init; }
        public bool? Optional { get; init; }
        public string[] Tags { get; init; } = [];
    }

    public class SampleToolProvider
    {
        [ClaudeTool("greet", "Greets a person by name")]
        public string Greet(string name)
        {
            return $"Hello, {name}!";
        }

        [ClaudeTool("add-numbers", "Adds two numbers together")]
        public int Add(int a, int b)
        {
            return a + b;
        }

        [ClaudeTool("get-info", "Gets information with optional detail level")]
        public string GetInfo(string topic, int detailLevel = 1)
        {
            return $"Info about {topic} at level {detailLevel}";
        }

        [ClaudeTool("async-operation", "Performs an async operation")]
        public async Task<ToolResult> AsyncOperation(string input)
        {
            await Task.Delay(1); // Simulate async work
            return ToolResult.Text($"Processed: {input}");
        }

        [ClaudeTool("async-string", "Returns async string")]
        public async Task<string> AsyncString(string message)
        {
            await Task.Delay(1);
            return $"Async result: {message}";
        }

        [ClaudeTool("returns-tool-result", "Returns a ToolResult directly")]
        public ToolResult ReturnsToolResult(string data)
        {
            return ToolResult.Text($"Direct result: {data}");
        }

        public string NotATool(string input)
        {
            return "This should not be registered";
        }
    }

    public class ErrorProneToolProvider
    {
        [ClaudeTool("throw-error", "Throws an error")]
        public string ThrowError(string message)
        {
            throw new InvalidOperationException(message);
        }
    }

    [Fact]
    public void Constructor_SetsNameAndVersion()
    {
        // Arrange & Act
        var server = new McpToolServer("test-server", "2.0.0");

        // Assert
        Assert.Equal("test-server", server.Name);
    }

    [Fact]
    public void Constructor_DefaultVersion()
    {
        // Arrange & Act
        var server = new McpToolServer("test-server");

        // Assert
        Assert.Equal("test-server", server.Name);
    }

    [Fact]
    public void Tools_InitiallyEmpty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Assert
        Assert.Empty(server.Tools);
    }

    [Fact]
    public void RegisterTool_WithToolDefinition_RegistersSuccessfully()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var tool = new ToolDefinition
        {
            Name = "test-tool",
            Description = "A test tool",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("result"))
        };

        // Act
        var result = server.RegisterTool(tool);

        // Assert
        Assert.Same(server, result); // Fluent API returns server
        Assert.Single(server.Tools);
        Assert.Contains(server.Tools, t => t.Name == "test-tool");
    }

    [Fact]
    public void RegisterTool_WithSameName_OverwritesPreviousTool()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var tool1 = new ToolDefinition
        {
            Name = "duplicate-tool",
            Description = "First tool",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("first"))
        };
        var tool2 = new ToolDefinition
        {
            Name = "duplicate-tool",
            Description = "Second tool",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("second"))
        };

        // Act
        server.RegisterTool(tool1);
        server.RegisterTool(tool2);

        // Assert
        Assert.Single(server.Tools);
        Assert.Contains(server.Tools, t => t.Description == "Second tool");
    }

    [Fact]
    public void RegisterTool_FluentChaining_RegistersMultipleTools()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server
            .RegisterTool(new ToolDefinition
            {
                Name = "tool1",
                Description = "Tool 1",
                InputSchema = new JsonObject { ["type"] = "object" },
                Handler = (_, _) => Task.FromResult(ToolResult.Text("1"))
            })
            .RegisterTool(new ToolDefinition
            {
                Name = "tool2",
                Description = "Tool 2",
                InputSchema = new JsonObject { ["type"] = "object" },
                Handler = (_, _) => Task.FromResult(ToolResult.Text("2"))
            });

        // Assert
        Assert.Equal(2, server.Tools.Count);
    }

    [Fact]
    public void RegisterTool_Generic_RegistersWithGeneratedSchema()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<WeatherInput>(
            "get-weather",
            "Gets weather for a location",
            (input, _) => Task.FromResult(ToolResult.Text($"Weather in {input.Location}")));

        // Assert
        Assert.Single(server.Tools);
        var tool = server.Tools.First();
        Assert.Equal("get-weather", tool.Name);
        Assert.NotNull(tool.InputSchema);
        Assert.Equal("object", tool.InputSchema["type"]?.ToString());
    }

    [Fact]
    public async Task RegisterTool_Generic_DeserializesInputCorrectly()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        WeatherInput? capturedInput = null;

        server.RegisterTool<WeatherInput>(
            "get-weather",
            "Gets weather",
            (input, _) =>
            {
                capturedInput = input;
                return Task.FromResult(ToolResult.Text("OK"));
            });

        var request = CreateToolCallRequest("get-weather", new { location = "Seattle", units = "metric" });

        // Act
        await server.HandleRequestAsync(request);

        // Assert
        Assert.NotNull(capturedInput);
        Assert.Equal("Seattle", capturedInput.Location);
        Assert.Equal("metric", capturedInput.Units);
    }

    [Fact]
    public async Task RegisterTool_Generic_HandlesNullDeserialization()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        server.RegisterTool<WeatherInput>(
            "get-weather",
            "Gets weather",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Create a request with null/invalid JSON that can't deserialize
        var requestJson = """
                          {
                              "jsonrpc": "2.0",
                              "id": 1,
                              "method": "tools/call",
                              "params": {
                                  "name": "get-weather",
                                  "arguments": null
                              }
                          }
                          """;
        var request = JsonDocument.Parse(requestJson).RootElement;

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        Assert.NotNull(result);
        Assert.True((bool)result["isError"]);
    }

    [Fact]
    public void RegisterToolsFrom_DiscoversAttributedMethods()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();

        // Act
        server.RegisterToolsFrom(provider);

        // Assert
        Assert.Equal(6, server.Tools.Count); // 6 methods with [ClaudeTool]
        Assert.Contains(server.Tools, t => t.Name == "greet");
        Assert.Contains(server.Tools, t => t.Name == "add-numbers");
        Assert.Contains(server.Tools, t => t.Name == "get-info");
        Assert.Contains(server.Tools, t => t.Name == "async-operation");
        Assert.Contains(server.Tools, t => t.Name == "async-string");
        Assert.Contains(server.Tools, t => t.Name == "returns-tool-result");
    }

    [Fact]
    public void RegisterToolsFrom_IgnoresNonAttributedMethods()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();

        // Act
        server.RegisterToolsFrom(provider);

        // Assert
        Assert.DoesNotContain(server.Tools, t => t.Name == "NotATool");
    }

    [Fact]
    public void RegisterToolsFrom_CapturesToolDescription()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();

        // Act
        server.RegisterToolsFrom(provider);

        // Assert
        var greetTool = server.Tools.First(t => t.Name == "greet");
        Assert.Equal("Greets a person by name", greetTool.Description);
    }

    [Fact]
    public void RegisterToolsFrom_GeneratesParameterSchema()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();

        // Act
        server.RegisterToolsFrom(provider);

        // Assert
        var addTool = server.Tools.First(t => t.Name == "add-numbers");
        var schema = addTool.InputSchema;

        Assert.Equal("object", schema["type"]?.ToString());
        Assert.NotNull(schema["properties"]);

        var properties = schema["properties"]!.AsObject();
        Assert.Contains("a", properties.Select(p => p.Key));
        Assert.Contains("b", properties.Select(p => p.Key));
    }

    [Fact]
    public void RegisterToolsFrom_HandlesOptionalParameters()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();

        // Act
        server.RegisterToolsFrom(provider);

        // Assert
        var getInfoTool = server.Tools.First(t => t.Name == "get-info");
        var required = getInfoTool.InputSchema["required"]?.AsArray();

        Assert.NotNull(required);
        Assert.Contains("topic", required.Select(r => r?.ToString()));
        Assert.DoesNotContain("detailLevel", required.Select(r => r?.ToString()));
    }

    [Fact]
    public async Task RegisterToolsFrom_ExecutesStringReturningMethod()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("greet", new { name = "World" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        Assert.NotNull(result);
        Assert.False((bool)result["isError"]);

        var content = result["content"] as List<object>;
        Assert.NotNull(content);
        var textContent = content[0] as Dictionary<string, object>;
        Assert.NotNull(textContent);
        Assert.Equal("Hello, World!", textContent["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_ExecutesIntReturningMethod()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("add-numbers", new { a = 5, b = 3 });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("8", textContent!["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_ExecutesAsyncToolResultMethod()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("async-operation", new { input = "test-data" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("Processed: test-data", textContent!["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_ExecutesAsyncStringMethod()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("async-string", new { message = "hello" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("Async result: hello", textContent!["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_ExecutesToolResultReturningMethod()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("returns-tool-result", new { data = "info" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("Direct result: info", textContent!["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_HandlesDefaultParameterValues()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new SampleToolProvider();
        server.RegisterToolsFrom(provider);

        // Only provide required parameter, omit optional one
        var request = CreateToolCallRequest("get-info", new { topic = "AI" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("Info about AI at level 1", textContent!["text"]);
    }

    [Fact]
    public async Task RegisterToolsFrom_HandlesMethodExceptions()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new ErrorProneToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("throw-error", new { message = "Something went wrong" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        Assert.NotNull(result);
        Assert.True((bool)result["isError"]);

        var content = result["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        // The exception message may be wrapped in TargetInvocationException
        var errorText = textContent!["text"]?.ToString();
        Assert.NotNull(errorText);
        // Verify it contains an error message (either the direct message or wrapped)
        Assert.True(
            errorText.Contains("Something went wrong") ||
            errorText.Contains("Exception has been thrown by the target"),
            $"Error message should contain expected text but was: {errorText}");
    }

    [Fact]
    public async Task HandleRequestAsync_Initialize_ReturnsServerInfo()
    {
        // Arrange
        var server = new McpToolServer("my-server", "2.5.0");
        var request = CreateJsonRpcRequest("initialize", 1);

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        Assert.Equal("2.0", response["jsonrpc"]);
        Assert.Equal(1L, response["id"]);

        var result = response["result"] as Dictionary<string, object>;
        Assert.NotNull(result);
        Assert.Equal("2024-11-05", result["protocolVersion"]);

        var serverInfo = result["serverInfo"] as Dictionary<string, object>;
        Assert.NotNull(serverInfo);
        Assert.Equal("my-server", serverInfo["name"]);
        Assert.Equal("2.5.0", serverInfo["version"]);
    }

    [Fact]
    public async Task HandleRequestAsync_ToolsList_ReturnsRegisteredTools()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        server.RegisterTool(new ToolDefinition
        {
            Name = "tool1",
            Description = "First tool",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("1"))
        });
        server.RegisterTool(new ToolDefinition
        {
            Name = "tool2",
            Description = "Second tool",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["arg"] = new JsonObject { ["type"] = "string" } }
            },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("2"))
        });

        var request = CreateJsonRpcRequest("tools/list", 1);

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var tools = result!["tools"] as List<Dictionary<string, object?>>;
        Assert.NotNull(tools);
        Assert.Equal(2, tools.Count);

        var tool1 = tools.First(t => t["name"]?.ToString() == "tool1");
        Assert.Equal("First tool", tool1["description"]);
    }

    [Fact]
    public async Task HandleRequestAsync_ToolsCall_ExecutesTool()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        server.RegisterTool(
            "echo",
            "Echoes input",
            new JsonObject { ["type"] = "object" },
            (input, _) => Task.FromResult(ToolResult.Text($"Echo: {input.GetProperty("message").GetString()}")));

        var request = CreateToolCallRequest("echo", new { message = "Hello!" });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("Echo: Hello!", textContent!["text"]);
        Assert.False((bool)result["isError"]);
    }

    [Fact]
    public async Task HandleRequestAsync_UnknownMethod_ReturnsError()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var request = CreateJsonRpcRequest("unknown/method", 1);

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var error = response["error"] as Dictionary<string, object>;
        Assert.NotNull(error);
        Assert.Equal(-32603, error["code"]);
        Assert.Contains("Unknown method", error["message"]?.ToString());
    }

    [Fact]
    public async Task HandleRequestAsync_UnknownTool_ThrowsAndReturnsError()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var request = CreateToolCallRequest("nonexistent-tool", new { });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var error = response["error"] as Dictionary<string, object>;
        Assert.NotNull(error);
        Assert.Contains("Unknown tool", error["message"]?.ToString());
    }

    [Fact]
    public async Task HandleRequestAsync_NotificationsInitialized_ReturnsEmptyAck()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var request = CreateJsonRpcRequest("notifications/initialized", null);

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        Assert.Equal("2.0", response["jsonrpc"]);
        var result = response["result"] as Dictionary<string, object>;
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData("abc-123")]
    public async Task HandleRequestAsync_PreservesRequestId(object id)
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var requestJson = $@"{{
            ""jsonrpc"": ""2.0"",
            ""id"": {(id is string ? $"\"{id}\"" : id)},
            ""method"": ""initialize""
        }}";
        var request = JsonDocument.Parse(requestJson).RootElement;

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        if (id is string strId)
        {
            Assert.Equal(strId, response["id"]);
        }
        else
        {
            Assert.Equal((long)(int)id, response["id"]);
        }
    }

    [Fact]
    public void ToolResult_Text_CreatesTextContent()
    {
        // Act
        var result = ToolResult.Text("Hello, World!");

        // Assert
        Assert.Single(result.Content);
        Assert.False(result.IsError);

        var content = result.Content[0] as ToolResultTextContent;
        Assert.NotNull(content);
        Assert.Equal("text", content.Type);
        Assert.Equal("Hello, World!", content.Text);
    }

    [Fact]
    public void ToolResult_Error_CreatesErrorResult()
    {
        // Act
        var result = ToolResult.Error("Something went wrong");

        // Assert
        Assert.Single(result.Content);
        Assert.True(result.IsError);

        var content = result.Content[0] as ToolResultTextContent;
        Assert.NotNull(content);
        Assert.Equal("Something went wrong", content.Text);
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesStringProperty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<WeatherInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var properties = tool.InputSchema["properties"]!.AsObject();
        Assert.Equal("string", properties["location"]?["type"]?.ToString());
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesIntegerProperty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<CalculatorInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var properties = tool.InputSchema["properties"]!.AsObject();
        Assert.Equal("integer", properties["a"]?["type"]?.ToString());
        Assert.Equal("integer", properties["b"]?["type"]?.ToString());
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesNumberProperty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<ComplexInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var properties = tool.InputSchema["properties"]!.AsObject();
        Assert.Equal("number", properties["value"]?["type"]?.ToString());
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesArrayProperty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<ComplexInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var properties = tool.InputSchema["properties"]!.AsObject();
        Assert.Equal("array", properties["tags"]?["type"]?.ToString());
        Assert.Equal("string", properties["tags"]?["items"]?["type"]?.ToString());
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesBooleanProperty()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<ComplexInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var properties = tool.InputSchema["properties"]!.AsObject();
        Assert.Equal("boolean", properties["optional"]?["type"]?.ToString());
    }

    [Fact]
    public void RegisterTool_Generic_GeneratesRequiredProperties()
    {
        // Arrange
        var server = new McpToolServer("test-server");

        // Act
        server.RegisterTool<WeatherInput>(
            "test",
            "Test",
            (_, _) => Task.FromResult(ToolResult.Text("OK")));

        // Assert
        var tool = server.Tools.First();
        var required = tool.InputSchema["required"]!.AsArray();
        Assert.Contains("location", required.Select(r => r?.ToString()));
    }

    [Fact]
    public async Task HandleRequestAsync_PassesCancellationToken()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        CancellationToken capturedToken = CancellationToken.None;

        server.RegisterTool(
            "capture-token",
            "Captures the cancellation token",
            new JsonObject { ["type"] = "object" },
            (_, ct) =>
            {
                capturedToken = ct;
                return Task.FromResult(ToolResult.Text("OK"));
            });

        var cts = new CancellationTokenSource();
        var request = CreateToolCallRequest("capture-token", new { });

        // Act
        await server.HandleRequestAsync(request, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task RegisterToolsFrom_HandlesCancellationTokenParameter()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var toolProvider = new CancellationTokenToolProvider();
        server.RegisterToolsFrom(toolProvider);

        var request = CreateToolCallRequest("with-ct", new { message = "test" });

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        Assert.NotNull(response);
    }

    public class CancellationTokenToolProvider
    {
        [ClaudeTool("with-ct", "Has a CancellationToken parameter")]
        public string WithCancellationToken(string message, CancellationToken cancellationToken)
        {
            return $"Got: {message}, Token valid: {!cancellationToken.IsCancellationRequested}";
        }
    }

    [Fact]
    public void ToolHelpers_Tool_CreatesToolDefinition()
    {
        // Act
        var tool = ToolHelpers.Tool<WeatherInput>(
            "get-weather",
            "Gets weather",
            (input, _) => Task.FromResult(ToolResult.Text($"Weather in {input.Location}")));

        // Assert
        Assert.Equal("get-weather", tool.Name);
        Assert.Equal("Gets weather", tool.Description);
        Assert.NotNull(tool.InputSchema);
        Assert.NotNull(tool.Handler);
    }

    [Fact]
    public void ToolHelpers_Tool_WithJsonSchema_CreatesToolDefinition()
    {
        // Arrange
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject { ["type"] = "string" }
            }
        };

        // Act
        var tool = ToolHelpers.Tool(
            "search",
            "Searches for data",
            schema,
            (input, _) => Task.FromResult(ToolResult.Text("results")));

        // Assert
        Assert.Equal("search", tool.Name);
        Assert.Equal("Searches for data", tool.Description);
        Assert.Same(schema, tool.InputSchema);
    }

    [Fact]
    public async Task ToolHelpers_Tool_HandlerDeserializesCorrectly()
    {
        // Arrange
        WeatherInput? captured = null;
        var tool = ToolHelpers.Tool<WeatherInput>(
            "get-weather",
            "Gets weather",
            (input, _) =>
            {
                captured = input;
                return Task.FromResult(ToolResult.Text("OK"));
            });

        var inputJson = JsonDocument.Parse("""{"location": "Paris", "units": "celsius"}""").RootElement;

        // Act
        await tool.Handler(inputJson, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("Paris", captured.Location);
        Assert.Equal("celsius", captured.Units);
    }

    [Fact]
    public void ToolHelpers_CreateSdkMcpServer_WithTools_RegistersAllTools()
    {
        // Arrange & Act
        var config = ToolHelpers.CreateSdkMcpServer(
            "my-server",
            "1.0.0",
            [
                ToolHelpers.Tool<WeatherInput>("weather", "Gets weather",
                    (_, _) => Task.FromResult(ToolResult.Text("sunny"))),
                ToolHelpers.Tool<CalculatorInput>("calc", "Calculates",
                    (_, _) => Task.FromResult(ToolResult.Text("42")))
            ]);

        // Assert
        Assert.Equal("my-server", config.Name);
        Assert.NotNull(config.Instance);
        var serverInstance = config.Instance as McpToolServer;
        Assert.NotNull(serverInstance);
        Assert.Equal(2, serverInstance.Tools.Count);
    }

    [Fact]
    public void ToolHelpers_CreateSdkMcpServer_WithConfigure_AllowsCustomRegistration()
    {
        // Arrange & Act
        var config = ToolHelpers.CreateSdkMcpServer("my-server", server =>
        {
            server.RegisterTool<WeatherInput>("weather", "Gets weather",
                (_, _) => Task.FromResult(ToolResult.Text("cloudy")));
            server.RegisterToolsFrom(new SampleToolProvider());
        });

        // Assert
        Assert.Equal("my-server", config.Name);
        Assert.NotNull(config.Instance);
        var serverInstance = config.Instance as McpToolServer;
        Assert.NotNull(serverInstance);
        Assert.Equal(7, serverInstance.Tools.Count); // 1 + 6 from SampleToolProvider
    }

    [Fact]
    public async Task HandleRequestAsync_EmptyArguments_HandledGracefully()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        server.RegisterTool(
            "no-args",
            "Needs no arguments",
            new JsonObject { ["type"] = "object" },
            (_, _) => Task.FromResult(ToolResult.Text("No args needed")));

        var requestJson = """
                          {
                              "jsonrpc": "2.0",
                              "id": 1,
                              "method": "tools/call",
                              "params": {
                                  "name": "no-args"
                              }
                          }
                          """;
        var request = JsonDocument.Parse(requestJson).RootElement;

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        Assert.False((bool)result!["isError"]);
    }

    [Fact]
    public async Task HandleRequestAsync_NullResult_HandledGracefully()
    {
        // Arrange
        var server = new McpToolServer("test-server");
        var provider = new NullReturningToolProvider();
        server.RegisterToolsFrom(provider);

        var request = CreateToolCallRequest("returns-null", new { });

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        var result = response["result"] as Dictionary<string, object>;
        var content = result!["content"] as List<object>;
        var textContent = content![0] as Dictionary<string, object>;
        Assert.Equal("", textContent!["text"]);
    }

    public class NullReturningToolProvider
    {
        [ClaudeTool("returns-null", "Returns null")]
        public object? ReturnsNull()
        {
            return null;
        }
    }

    [Fact]
    public void ClaudeToolAttribute_StoresNameAndDescription()
    {
        // Arrange & Act
        var attr = new ClaudeToolAttribute("my-tool", "My tool description");

        // Assert
        Assert.Equal("my-tool", attr.Name);
        Assert.Equal("My tool description", attr.Description);
    }

    [Fact]
    public void ToolDefinition_RequiredPropertiesSet()
    {
        // Arrange & Act
        var tool = new ToolDefinition
        {
            Name = "test",
            Description = "Test description",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Text("OK"))
        };

        // Assert
        Assert.Equal("test", tool.Name);
        Assert.Equal("Test description", tool.Description);
        Assert.NotNull(tool.InputSchema);
        Assert.NotNull(tool.Handler);
    }

    [Fact]
    public void McpToolServer_ImplementsIMcpToolServer()
    {
        // Arrange & Act
        var server = new McpToolServer("test-server");

        // Assert
        Assert.IsAssignableFrom<IMcpToolServer>(server);
    }

    [Fact]
    public async Task IMcpToolServer_HandleRequestAsync_WorksThroughInterface()
    {
        // Arrange
        McpToolServer server = new("test-server");
        var request = CreateJsonRpcRequest("initialize", 1);

        // Act
        var response = await server.HandleRequestAsync(request) as Dictionary<string, object?>;

        // Assert
        Assert.NotNull(response);
        Assert.Equal("test-server", server.Name);
    }

    private static JsonElement CreateJsonRpcRequest(string method, object? id)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };

        if (id is not null)
        {
            if (id is int intId)
            {
                request["id"] = intId;
            }
            else if (id is long longId)
            {
                request["id"] = longId;
            }
            else
            {
                request["id"] = id.ToString();
            }
        }

        return JsonDocument.Parse(request.ToJsonString()).RootElement;
    }

    private static JsonElement CreateToolCallRequest(string toolName, object arguments)
    {
        var argsJson = JsonSerializer.Serialize(arguments);
        var requestJson = $@"{{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""method"": ""tools/call"",
            ""params"": {{
                ""name"": ""{toolName}"",
                ""arguments"": {argsJson}
            }}
        }}";
        return JsonDocument.Parse(requestJson).RootElement;
    }
}
