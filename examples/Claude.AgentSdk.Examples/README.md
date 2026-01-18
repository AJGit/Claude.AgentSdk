# SDK Examples Collection

A comprehensive collection of examples demonstrating the core features of the Claude Agent SDK for C#.

## What This Project Contains

This project provides a menu-driven console application with 13 standalone examples, each demonstrating a specific SDK feature. Run the project to see an interactive menu and choose which example to explore.

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

| #   | Example                   | Description                                                             |
| --- | ------------------------- | ----------------------------------------------------------------------- |
| 1   | **Basic Query**           | Simple one-shot query with streaming response                           |
| 2   | **Streaming**             | Detailed streaming with partial message updates                         |
| 3   | **Interactive Session**   | Bidirectional mode with `CreateSessionAsync`/`SendAsync`/`ReceiveAsync` |
| 4   | **Custom Tools (MCP)**    | Create tools using `[ClaudeTool]` with compile-time registration        |
| 5   | **Hooks**                 | Intercept tool execution with hooks and enum accessors                  |
| 6   | **Subagents**             | Define specialized subagents using `AgentDefinition`                    |
| 7   | **Structured Output**     | Get typed responses using JSON schema                                   |
| 8   | **Permission Handler**    | Control tool permissions with `CanUseTool` callback                     |
| 9   | **System Prompt**         | Configure system prompts (string, preset, or append)                    |
| 10  | **Settings Sources**      | Load CLAUDE.md files from project/user directories                      |
| 11  | **MCP Servers**           | Configure MCP servers with compile-time registration                    |
| 12  | **Sandbox**               | Configure sandbox settings for secure execution                         |
| 13  | **Functional Patterns**   | Use `Result`, `Option`, `Pipeline` for functional programming           |

## C#-Centric Features Demonstrated

### Fluent Options Builder

```csharp
using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptionsBuilder()
    .WithModel(ModelIdentifier.Sonnet)
    .WithFallbackModel(ModelIdentifier.Haiku)
    .WithMaxTurns(50)
    .WithSystemPrompt("You are a helpful assistant.")
    .AllowTools(ToolName.Read, ToolName.Write, ToolName.Bash, ToolName.Task)
    .WithHooks(new HookConfigurationBuilder()
        .OnPreToolUse(handler, matcher: "Bash")
        .Build())
    .AddAgent("reviewer", new AgentDefinitionBuilder()
        .WithDescription("Code reviewer")
        .WithPrompt("Review code for quality.")
        .AsReadOnlyAnalyzer()
        .Build())
    .Build();
```

### ModelIdentifier for Type-Safe Models

```csharp
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptions
{
    // Strongly-typed model selection
    ModelId = ModelIdentifier.Sonnet,
    FallbackModelId = ModelIdentifier.Haiku,

    // Specific versions available
    // ModelId = ModelIdentifier.ClaudeOpus45,  // claude-opus-4-5-20251101

    MaxTurns = 10
};
```

### Strongly-Typed Tool Names

```csharp
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptions
{
    AllowedTools = [ToolName.Read, ToolName.Write, ToolName.Bash, ToolName.Grep],
    DisallowedTools = [ToolName.WebSearch]
};

// MCP tools
var server = McpServerName.Sdk("my-tools");
var tools = [server.Tool("search"), server.Tool("read")];
```

### MCP Server Builder (Fluent Configuration)

```csharp
using Claude.AgentSdk.Builders;

var servers = new McpServerBuilder()
    .AddStdio("file-tools", "python", "file_tools.py")
        .WithEnvironment("DEBUG", "true")
    .AddSse("remote-api", "https://api.example.com/mcp")
        .WithHeaders("Authorization", "Bearer token")
    .AddSdk("custom-tools", myToolServer)
    .Build();

var options = new ClaudeAgentOptions { McpServers = servers };
```

### Functional Match Patterns

```csharp
using Claude.AgentSdk.Messages;

// Exhaustive pattern matching
var description = message.Match(
    userMessage: u => $"User: {u.MessageContent.Content}",
    assistantMessage: a => $"Claude: {a.MessageContent.Content.Count} blocks",
    systemMessage: s => $"System: {s.Subtype}",
    resultMessage: r => {
        var ctx = r.Usage is not null ? $"{r.Usage.TotalContextTokens / 1000.0:F0}k" : "?";
        return $"[{r.DurationMs/1000.0:F1}s | ${r.TotalCostUsd:F4} | {ctx}]";
    },
    streamEvent: e => $"Event: {e.Uuid}"
);

// Partial matching with default
var isFromClaude = message.Match(
    assistantMessage: _ => true,
    defaultCase: () => false
);
```

### Message Processing Extensions

```csharp
using Claude.AgentSdk.Extensions;

if (message is AssistantMessage assistant)
{
    // Get combined text content
    Console.WriteLine(assistant.GetText());

    // Check for tool usage
    if (assistant.HasToolUse(ToolName.Bash))
        Console.WriteLine("Bash command executed");

    // Iterate tool uses with typed input
    foreach (var tool in assistant.GetToolUses())
    {
        var input = tool.GetInput<MyInputType>();
        Console.WriteLine($"Tool: {tool.Name}");
    }
}
```

### Content Block Extensions

```csharp
using Claude.AgentSdk.Extensions;

foreach (var block in assistant.MessageContent.Content)
{
    if (block.IsText())
        Console.Write(block.AsText());

    if (block.IsToolUse())
        Console.WriteLine($"[{block.AsToolUse()!.Name}]");
}
```

### Compile-Time Tool Registration (#4, #11)

```csharp
using Claude.AgentSdk.Attributes;

[GenerateToolRegistration]  // Enables RegisterToolsCompiled() extension
public class DemoTools
{
    [ClaudeTool("calculate", "Perform arithmetic calculations",
        Categories = ["math"])]
    public string Calculate(
        [ToolParameter(Description = "Operation: add, subtract, multiply, divide",
                       AllowedValues = ["add", "subtract", "multiply", "divide"])]
        string operation,
        [ToolParameter(Description = "First numeric operand")] double a,
        [ToolParameter(Description = "Second numeric operand")] double b)
    {
        // Implementation
    }

    [ClaudeTool("get_weather", "Get weather for a city (mock data)",
        Categories = ["weather"],
        TimeoutSeconds = 5)]
    public string GetWeather(
        [ToolParameter(Description = "City name", Example = "Tokyo")] string city,
        [ToolParameter(Description = "Temperature unit",
                       AllowedValues = ["celsius", "fahrenheit"])] string unit = "celsius")
    {
        // Implementation
    }
}

// No reflection - uses generated code
var tools = new DemoTools();
toolServer.RegisterToolsCompiled(tools);
```

### Declarative Hook Registration (#5)

```csharp
using Claude.AgentSdk.Attributes;

[GenerateHookRegistration]  // Generates GetHooksCompiled() extension
public class SecurityHooks
{
    [HookHandler(HookEvent.PreToolUse, Matcher = "Bash")]
    public Task<HookOutput> ValidateBash(HookInput input, string? toolUseId,
        HookContext ctx, CancellationToken ct)
    {
        // Validation logic
        return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
    }

    [HookHandler(HookEvent.PreToolUse, Matcher = "Write|Edit")]
    public Task<HookOutput> ValidateFiles(HookInput input, string? toolUseId,
        HookContext ctx, CancellationToken ct)
    {
        // Block .env file modifications
        return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
    }
}

// Use generated extension
var hooks = new SecurityHooks();
var options = new ClaudeAgentOptions { Hooks = hooks.GetHooksCompiled() };
```

### Parameter Validation in Tools

```csharp
[GenerateToolRegistration]
public class ValidatedTools
{
    [ClaudeTool("search", "Search with validation")]
    public string Search(
        [ToolParameter(MinLength = 1, MaxLength = 100)] string query,
        [ToolParameter(MinValue = 1, MaxValue = 50)] int limit = 10,
        [ToolParameter(Pattern = @"^[a-z]+$")] string? filter = null)
    {
        // Validation runs before this - invalid inputs return ToolResult.Error()
        return $"Searching: {query}";
    }
}
```

### Declarative Agent Registration (#6)

```csharp
using Claude.AgentSdk.Attributes;

[GenerateAgentRegistration]  // Generates GetAgentsCompiled() extension
public class MyAgents
{
    [ClaudeAgent("code-reviewer", Description = "Code review specialist")]
    [AgentTools("Read", "Grep", "Glob")]
    public static string CodeReviewerPrompt => "You are a code review specialist...";

    [ClaudeAgent("test-runner", Description = "Test execution", Model = "haiku")]
    [AgentTools("Bash", "Read")]
    public static string TestRunnerPrompt => "You run and analyze tests...";
}

// Use generated extension
var agents = new MyAgents();
var options = new ClaudeAgentOptions { Agents = agents.GetAgentsCompiled() };
```

### Strongly-Typed Enum Accessors (#5 Hooks)

```csharp
using Claude.AgentSdk.Types;

// Hook example with enum accessors
[HookEvent.SessionStart] = new List<HookMatcher>
{
    new()
    {
        Hooks = [
            async (input, toolUseId, context, ct) =>
            {
                if (input is SessionStartHookInput sessionStart)
                {
                    // Use SourceEnum for type-safe checking
                    var description = sessionStart.SourceEnum switch
                    {
                        SessionStartSource.Startup => "Fresh startup",
                        SessionStartSource.Resume => "Resumed session",
                        SessionStartSource.Clear => "Session cleared",
                        SessionStartSource.Compact => "Session compacted",
                        _ => "Unknown"
                    };
                    Console.WriteLine($"Session started: {description}");
                }
                return new SyncHookOutput { Continue = true };
            }
        ]
    }
}

// Result message with enum accessor
case ResultMessage result:
    var status = result.SubtypeEnum switch
    {
        ResultMessageSubtype.Success => "Completed",
        ResultMessageSubtype.Error => "Failed",
        ResultMessageSubtype.Partial => "Partial",
        _ => "Unknown"
    };
    var ctx = result.Usage is not null ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k" : "?";
    Console.WriteLine($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {ctx}]");
    break;
```

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

### 4. Custom Tools (MCP) - With Compile-Time Registration
Create tools using the `[ClaudeTool]` attribute with enhanced properties:

```csharp
[GenerateToolRegistration]
public class DemoTools
{
    [ClaudeTool("calculate", "Perform arithmetic calculations",
        Categories = ["math"])]
    public string Calculate(
        [ToolParameter(Description = "Operation to perform",
                       AllowedValues = ["add", "subtract", "multiply", "divide"])]
        string operation,
        [ToolParameter(Description = "First operand")] double a,
        [ToolParameter(Description = "Second operand")] double b)
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
}

// Register tools using generated extension (no reflection)
var toolServer = new McpToolServer("demo-tools");
toolServer.RegisterToolsCompiled(new DemoTools());
```

### 5. Hooks with Enum Accessors
Intercept and control tool execution with strongly-typed enums:

```csharp
Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
{
    [HookEvent.PreToolUse] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [
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
    },
    [HookEvent.Notification] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [
                async (input, toolUseId, context, ct) =>
                {
                    if (input is NotificationHookInput notification)
                    {
                        // Use NotificationTypeEnum for type-safe handling
                        var icon = notification.NotificationTypeEnum switch
                        {
                            NotificationType.PermissionPrompt => "Permission Required",
                            NotificationType.IdlePrompt => "Idle",
                            NotificationType.AuthSuccess => "Authenticated",
                            _ => "Unknown"
                        };
                        Console.WriteLine($"[{icon}] {notification.Message}");
                    }
                    return new SyncHookOutput { Continue = true };
                }
            ]
        }
    }
}
```

### 11. MCP Servers with Compile-Time Registration
Four types of MCP server configurations:

```csharp
// Time tools with compile-time registration
[GenerateToolRegistration]
public class TimeTools
{
    [ClaudeTool("current_time", "Get the current date and time",
        Categories = ["time"],
        TimeoutSeconds = 3)]
    public string GetCurrentTime(
        [ToolParameter(Description = "IANA timezone identifier like 'America/New_York'",
                       Example = "America/New_York")]
        string? timezone = null)
    {
        // Implementation
    }

    [ClaudeTool("time_difference", "Calculate difference between two dates",
        Categories = ["time"],
        TimeoutSeconds = 3)]
    public string GetTimeDifference(
        [ToolParameter(Description = "Start date in ISO 8601 format", Example = "2024-01-01")]
        string startDate,
        [ToolParameter(Description = "End date in ISO 8601 format", Example = "2024-12-31")]
        string endDate)
    {
        // Implementation
    }
}

// Configuration
McpServers = new Dictionary<string, McpServerConfig>
{
    // In-process C# tools with compile-time registration
    ["time-tools"] = new McpSdkServerConfig
    {
        Name = "time-tools",
        Instance = toolServer  // Uses RegisterToolsCompiled()
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
│   ├── CustomToolsExample.cs     # #4 - MCP tools with compile-time registration
│   ├── HooksExample.cs           # #5 - Hook system with enum accessors
│   ├── SubagentsExample.cs       # #6 - Agent definitions
│   ├── StructuredOutputExample.cs    # #7 - JSON schema outputs
│   ├── PermissionHandlerExample.cs   # #8 - Permission callbacks
│   ├── SystemPromptExample.cs    # #9 - System prompt config
│   ├── SettingsSourcesExample.cs # #10 - CLAUDE.md loading
│   ├── McpServersExample.cs      # #11 - MCP server types with compile-time registration
│   ├── SandboxExample.cs         # #12 - Sandbox configuration
│   └── FunctionalPatternsExample.cs  # #13 - Functional programming patterns
└── Claude.AgentSdk.Examples.csproj
```

## Source Generator Reference

To use compile-time tool registration, the project references the source generator:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Claude.AgentSdk\Claude.AgentSdk.csproj" />
  <ProjectReference Include="..\..\src\Claude.AgentSdk.Generators\Claude.AgentSdk.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
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
