# SDK Examples Collection

A comprehensive collection of examples demonstrating the core features of the Claude Agent SDK for C#.

## What This Project Contains

This project provides a menu-driven console application with 12 standalone examples, each demonstrating a specific SDK feature. Run the project to see an interactive menu and choose which example to explore.

## Running the Examples

```bash
cd examples/Claude.AgentSdk.Examples
dotnet run
```

Or run a specific example directly:

```bash
dotnet run -- 1   # Run example #1 (Basic Query)
dotnet run -- 4   # Run example #4 (Custom Tools)
```

## Available Examples

| #   | Example                 | Description                                                           |
| --- | ----------------------- | --------------------------------------------------------------------- |
| 1   | **Basic Query**         | Simple one-shot query with streaming response                         |
| 2   | **Streaming**           | Detailed streaming with partial message updates                       |
| 3   | **Interactive Session** | Bidirectional mode with `CreateSessionAsync`/`SendAsync`/`ReceiveAsync` |
| 4   | **Custom Tools (MCP)**  | Create tools using `[ClaudeTool]` attribute and `McpToolServer`       |
| 5   | **Hooks**               | Intercept tool execution with `PreToolUse`/`PostToolUse`/`Stop` hooks |
| 6   | **Subagents**           | Define specialized subagents using `AgentDefinition`                  |
| 7   | **Structured Output**   | Get typed responses using JSON schema                                 |
| 8   | **Permission Handler**  | Control tool permissions with `CanUseTool` callback                   |
| 9   | **System Prompt**       | Configure system prompts (string, preset, or append)                  |
| 10  | **Settings Sources**    | Load CLAUDE.md files from project/user directories                    |
| 11  | **MCP Servers**         | Configure MCP servers (stdio, SSE, HTTP, in-process)                  |
| 12  | **Sandbox**             | Configure sandbox settings for secure execution                       |

## Example Highlights

### 1. Basic Query
The simplest possible usage - send a prompt, stream the response:

```csharp
await using var client = new ClaudeAgentClient();

await foreach (var message in client.QueryAsync("What is the capital of France?"))
{
    if (message is AssistantMessage assistant)
    {
        foreach (var block in assistant.MessageContent.Content)
        {
            if (block is TextBlock text)
                Console.Write(text.Text);
        }
    }
}
```

### 4. Custom Tools (MCP)
Create tools using the `[ClaudeTool]` attribute:

```csharp
public class DemoTools
{
    [ClaudeTool("calculate", "Perform arithmetic calculations")]
    public string Calculate(string operation, double a, double b)
    {
        var result = operation switch
        {
            "add" or "+" => a + b,
            "subtract" or "-" => a - b,
            "multiply" or "*" => a * b,
            "divide" or "/" when b != 0 => a / b,
            "divide" or "/" => throw new InvalidOperationException("Cannot divide by zero"),
            _ => throw new InvalidOperationException($"Unknown operation: {operation}")
        };
        return JsonSerializer.Serialize(new { expression = $"{a} {operation} {b}", result });
    }

    [ClaudeTool("get_weather", "Get weather for a city")]
    public string GetWeather(string city, string unit = "celsius")
    {
        // Return mock weather data
        return JsonSerializer.Serialize(new { city, temperature = 22, unit });
    }
}

// Register tools
var toolServer = new McpToolServer("demo-tools");
toolServer.RegisterToolsFrom(new DemoTools());
```

### 5. Hooks
Intercept and control tool execution:

```csharp
Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
{
    [HookEvent.PreToolUse] = new List<HookMatcher>
    {
        new()
        {
            // Match all tools (Matcher is optional - omit for all tools)
            Hooks =
            [
                async (input, toolUseId, context, ct) =>
                {
                    if (input is PreToolUseHookInput preInput)
                    {
                        Console.WriteLine($"[Hook] Tool intercepted: {preInput.ToolName}");
                    }
                    return new SyncHookOutput { Continue = true };
                }
            ]
        }
    }
}
```

### 6. Subagents
Define specialized agents for task delegation:

```csharp
Agents = new Dictionary<string, AgentDefinition>
{
    ["code-reviewer"] = new AgentDefinition
    {
        Description = "Expert code reviewer for security and quality reviews",
        Prompt = "You are an expert code reviewer...",
        Tools = ["Read", "Grep", "Glob"],
        Model = "sonnet"
    }
}
```

### 11. MCP Servers
Four types of MCP server configurations:

```csharp
McpServers = new Dictionary<string, McpServerConfig>
{
    // External process (stdio)
    ["filesystem"] = new McpStdioServerConfig
    {
        Command = "npx",
        Args = ["-y", "@claude/mcp-server-filesystem", "./data"]
    },

    // Remote SSE server
    ["remote"] = new McpSseServerConfig
    {
        Url = "https://api.example.com/mcp/sse",
        Headers = new() { ["Authorization"] = "Bearer token" }
    },

    // Remote HTTP server
    ["api"] = new McpHttpServerConfig
    {
        Url = "https://api.example.com/mcp"
    },

    // In-process C# tools
    ["custom"] = new McpSdkServerConfig
    {
        Name = "custom",
        Instance = toolServer
    }
}
```

## Project Structure

```
Claude.AgentSdk.Examples/
├── Program.cs                    # Menu system and example runner
├── Examples/
│   ├── IExample.cs               # Common interface for all examples
│   ├── BasicQueryExample.cs      # #1 - Basic streaming query
│   ├── StreamingExample.cs       # #2 - Detailed streaming
│   ├── InteractiveSessionExample.cs  # #3 - Bidirectional sessions
│   ├── CustomToolsExample.cs     # #4 - MCP tools with [ClaudeTool]
│   ├── HooksExample.cs           # #5 - Hook system
│   ├── SubagentsExample.cs       # #6 - Agent definitions
│   ├── StructuredOutputExample.cs    # #7 - JSON schema outputs
│   ├── PermissionHandlerExample.cs   # #8 - Permission callbacks
│   ├── SystemPromptExample.cs    # #9 - System prompt config
│   ├── SettingsSourcesExample.cs # #10 - CLAUDE.md loading
│   ├── McpServersExample.cs      # #11 - MCP server types
│   └── SandboxExample.cs         # Sandbox configuration
└── Claude.AgentSdk.Examples.csproj
```

## Adding a New Example

1. Create a new class implementing `IExample`:

```csharp
public class MyNewExample : IExample
{
    public string Name => "My New Example";
    public string Description => "Demonstrates XYZ feature";

    public async Task RunAsync()
    {
        // Your example code here
    }
}
```

2. Add it to the `Examples` array in `Program.cs`:

```csharp
private static readonly IExample[] Examples =
[
    // ... existing examples
    new MyNewExample(),
];
```

## Prerequisites

- .NET 10.0 or later
- Claude Code CLI installed and authenticated
- ANTHROPIC_API_KEY environment variable set
