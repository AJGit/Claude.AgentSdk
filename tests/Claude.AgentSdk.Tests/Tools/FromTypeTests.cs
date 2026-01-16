using System.Text.Json;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Tests.Tools;

/// <summary>
///     Tests for ToolHelpers.FromType and FromInstance factory methods.
/// </summary>
[UnitTest]
public class FromTypeTests
{
    #region Test Tool Classes

    public class CalculatorTools
    {
        [ClaudeTool("add", "Add two numbers together")]
        public string Add(AddArgs args) => $"Result: {args.A + args.B}";

        [ClaudeTool("subtract", "Subtract two numbers")]
        public string Subtract(SubtractArgs args) => $"Result: {args.A - args.B}";

        [ClaudeTool("multiply", "Multiply two numbers")]
        public Task<string> MultiplyAsync(MultiplyArgs args) => Task.FromResult($"Result: {args.A * args.B}");
    }

    public record AddArgs(int A, int B);
    public record SubtractArgs(int A, int B);
    public record MultiplyArgs(int A, int B);

    public class NoToolsClass
    {
        public string NotATool() => "This is not a tool";
    }

    public class ToolWithDependency
    {
        private readonly string _prefix;

        public ToolWithDependency(string prefix)
        {
            _prefix = prefix;
        }

        [ClaudeTool("greet", "Greet someone")]
        public string Greet(GreetArgs args) => $"{_prefix} {args.Name}!";
    }

    public record GreetArgs(string Name);

    #endregion

    #region FromType Tests

    [Fact]
    public void FromType_WithValidClass_CreatesServerConfig()
    {
        // Act
        var config = ToolHelpers.FromType<CalculatorTools>("calculator");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("calculator", config.Name);
        Assert.NotNull(config.Instance);
    }

    [Fact]
    public void FromType_WithVersion_SetsVersion()
    {
        // Act
        var config = ToolHelpers.FromType<CalculatorTools>("calculator", "2.0.0");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("calculator", config.Name);
    }

    [Fact]
    public void FromType_WithNoToolMethods_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            ToolHelpers.FromType<NoToolsClass>("no-tools"));
    }

    #endregion

    #region FromInstance Tests

    [Fact]
    public void FromInstance_WithValidInstance_CreatesServerConfig()
    {
        // Arrange
        var calculator = new CalculatorTools();

        // Act
        var config = ToolHelpers.FromInstance(calculator, "calculator");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("calculator", config.Name);
        Assert.NotNull(config.Instance);
    }

    [Fact]
    public void FromInstance_WithDependencies_UsesProvidedInstance()
    {
        // Arrange
        var greeter = new ToolWithDependency("Hello");

        // Act
        var config = ToolHelpers.FromInstance(greeter, "greeter");

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.Instance);
    }

    [Fact]
    public void FromInstance_WithNullInstance_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ToolHelpers.FromInstance<CalculatorTools>(null!, "test"));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FromType_ToolsCanBeInvoked()
    {
        // Arrange
        var config = ToolHelpers.FromType<CalculatorTools>("calculator");
        var server = config.Instance as McpToolServer;
        Assert.NotNull(server);

        // Create a tools/call request
        var request = JsonDocument.Parse("""
        {
            "jsonrpc": "2.0",
            "id": "1",
            "method": "tools/call",
            "params": {
                "name": "add",
                "arguments": { "a": 5, "b": 3 }
            }
        }
        """);

        // Act
        var result = await server.HandleRequestAsync(request.RootElement);

        // Assert
        Assert.NotNull(result);
        var resultJson = JsonSerializer.Serialize(result);
        Assert.Contains("Result: 8", resultJson);
    }

    [Fact]
    public async Task FromType_ListsAllTools()
    {
        // Arrange
        var config = ToolHelpers.FromType<CalculatorTools>("calculator");
        var server = config.Instance as McpToolServer;
        Assert.NotNull(server);

        // Create a tools/list request
        var request = JsonDocument.Parse("""
        {
            "jsonrpc": "2.0",
            "id": "1",
            "method": "tools/list"
        }
        """);

        // Act
        var result = await server.HandleRequestAsync(request.RootElement);

        // Assert
        Assert.NotNull(result);
        var resultJson = JsonSerializer.Serialize(result);
        Assert.Contains("add", resultJson);
        Assert.Contains("subtract", resultJson);
        Assert.Contains("multiply", resultJson);
    }

    #endregion
}
