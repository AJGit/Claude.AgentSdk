using System.Text.Json;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates creating custom tools using the MCP (Model Context Protocol) interface.
/// </summary>
public class CustomToolsExample : IExample
{
    public string Name => "Custom Tools (MCP)";
    public string Description => "Create and use custom tools via MCP protocol";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates creating custom tools using MCP.");
        Console.WriteLine("We'll create a calculator tool and a weather lookup tool.\n");

        // Create an MCP tool server with custom tools
        McpToolServer toolServer = new("demo-tools");

        // Register tools using compile-time generated registration (no reflection)
        DemoTools tools = new();
        toolServer.RegisterToolsCompiled(tools);

        ClaudeAgentOptions options = new()
        {
            SystemPrompt = "You are a helpful assistant with access to calculator and weather tools.",
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["demo-tools"] = new McpSdkServerConfig
                {
                    Name = "demo-tools",
                    Instance = toolServer
                }
            },
            // Allow the custom tools
            AllowedTools = ["mcp__demo-tools__calculate", "mcp__demo-tools__get_weather"],
            MaxTurns = 5,
            PermissionMode = PermissionMode.AcceptEdits
        };

        await using ClaudeAgentClient client = new(options);

        string prompt = "What is 15 * 7? Also, what's the weather like in Tokyo?";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");
        Console.WriteLine("---------");

        await foreach (Message message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (ContentBlock block in assistant.MessageContent.Content)
                    {
                        switch (block)
                        {
                            case TextBlock text:
                                Console.WriteLine(text.Text);
                                break;

                            case ToolUseBlock toolUse:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"\n[Tool Call: {toolUse.Name}]");
                                Console.WriteLine($"  Input: {toolUse.Input}");
                                Console.ResetColor();
                                break;

                            case ToolResultBlock toolResult:
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[Tool Result: {toolResult.Content}]");
                                Console.ResetColor();
                                break;
                        }
                    }

                    break;

                case ResultMessage result:
                    Console.WriteLine($"\n[Completed - Cost: ${result.TotalCostUsd:F4}]");
                    break;
            }
        }
    }
}

/// <summary>
///     Class containing demo tool methods.
///     Methods marked with [ClaudeTool] will be registered as MCP tools.
///     Uses [GenerateToolRegistration] for compile-time tool registration.
/// </summary>
[GenerateToolRegistration]
public class DemoTools
{
    /// <summary>
    ///     Perform basic arithmetic calculations.
    /// </summary>
    [ClaudeTool("calculate", "Perform basic arithmetic calculations (add, subtract, multiply, divide)",
        Categories = ["math"])]
    public string Calculate(
        [ToolParameter(Description = "The operation to perform: add, subtract, multiply, or divide",
            AllowedValues = ["add", "subtract", "multiply", "divide"])]
        string operation,
        [ToolParameter(Description = "The first numeric operand")]
        double a,
        [ToolParameter(Description = "The second numeric operand")]
        double b)
    {
        double result = operation.ToLower() switch
        {
            "add" or "+" => a + b,
            "subtract" or "-" => a - b,
            "multiply" or "*" => a * b,
            "divide" or "/" when b != 0 => a / b,
            "divide" or "/" => throw new InvalidOperationException("Cannot divide by zero"),
            _ => throw new InvalidOperationException($"Unknown operation: {operation}")
        };

        return JsonSerializer.Serialize(new { result, expression = $"{a} {operation} {b} = {result}" });
    }

    /// <summary>
    ///     Get current weather for a city.
    /// </summary>
    [ClaudeTool("get_weather", "Get current weather for a city (mock data for demonstration)",
        Categories = ["weather"],
        TimeoutSeconds = 5)]
    public string GetWeather(
        [ToolParameter(Description = "City name to get weather for", Example = "Tokyo")]
        string city,
        [ToolParameter(Description = "Temperature unit: celsius or fahrenheit",
            AllowedValues = ["celsius", "fahrenheit"])]
        string unit = "celsius")
    {
        // Mock weather data
        Random random = new(city.GetHashCode());
        int tempC = random.Next(-10, 35);
        int tempF = (tempC * 9 / 5) + 32;
        string[] conditions = ["sunny", "cloudy", "partly cloudy", "rainy", "snowy"];
        string condition = conditions[random.Next(conditions.Length)];

        return JsonSerializer.Serialize(new
        {
            city,
            temperature = unit.ToLower() == "fahrenheit" ? tempF : tempC,
            unit = unit.ToLower() == "fahrenheit" ? "F" : "C",
            condition,
            humidity = random.Next(30, 90)
        });
    }
}
