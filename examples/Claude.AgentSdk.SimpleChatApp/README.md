# Simple Chat App Example

A console-based chat application demonstrating interactive multi-turn conversations with Claude.

## What This Example Demonstrates

- **Interactive bidirectional mode** using `CreateSessionAsync`
- **Multi-turn conversations** with context persistence
- **Streaming responses** with `SendAsync`/`ReceiveResponseAsync`
- **Session management** with clear/restart capability

## Features

- Real-time streaming of Claude's responses
- Tool usage visualization (WebSearch, file operations, etc.)
- Thinking block display for extended reasoning
- Cost and duration tracking per message
- `/clear` command to start fresh conversations
- `/exit` command to quit

## Running the Example

```bash
cd examples/Claude.AgentSdk.SimpleChatApp
dotnet run
```

## Example Session

```
==============================================
   Simple Chat App - Claude Agent SDK
==============================================

Commands:
  /clear  - Start a new conversation
  /exit   - Exit the application

--------------------------------------------------

[Connected - new conversation started]

You: What's the weather like in San Francisco?

Claude: I'll search for the current weather in San Francisco.
[Using WebSearch: "San Francisco weather today"]

Based on my search, San Francisco is currently experiencing...

[2.3s | $0.0045]

You: How does that compare to New York?

Claude: Let me check the weather in New York for comparison.
[Using WebSearch: "New York weather today"]

Comparing the two cities...

[1.8s | $0.0038]

You: /clear
[Starting new conversation...]

You: /exit
Goodbye!
```

## C#-Centric Features

### Fluent Options Builder

```csharp
using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptionsBuilder()
    .WithModel(ModelIdentifier.Sonnet)
    .WithFallbackModel(ModelIdentifier.Haiku)
    .WithSystemPrompt("You are a helpful AI assistant...")
    .WithMaxTurns(100)
    .WithPermissionMode(PermissionMode.AcceptEdits)
    .AllowTools(ToolName.Bash, ToolName.Read, ToolName.Write, ToolName.Edit,
                ToolName.Glob, ToolName.Grep, ToolName.WebSearch, ToolName.WebFetch,
                ToolName.TodoWrite)
    .Build();

await using var client = new ClaudeAgentClient(options);
await using var session = await client.CreateSessionAsync();
```

### ModelIdentifier for Type-Safe Model Selection

```csharp
using Claude.AgentSdk.Types;

var options = new ClaudeAgentOptions
{
    ModelId = ModelIdentifier.Sonnet,  // Type-safe model selection with IntelliSense
    FallbackModelId = ModelIdentifier.Haiku,  // Fallback option
    // Or use specific versions:
    // ModelId = ModelIdentifier.ClaudeSonnet4,
    MaxTurns = 100
};
```

### Message Extensions for Simplified Processing

```csharp
using Claude.AgentSdk.Extensions;

await foreach (var message in session.ReceiveResponseAsync())
{
    if (message is AssistantMessage assistant)
    {
        // Get all text content at once
        Console.Write(assistant.GetText());

        // Check for tool usage
        if (assistant.HasToolUse(ToolName.WebSearch))
            Console.Write(" [searching...]");

        // Iterate tool uses
        foreach (var tool in assistant.GetToolUses())
            Console.Write($" [{tool.Name}]");
    }
}
```

### Functional Match Patterns

Replace verbose switch statements with exhaustive pattern matching:

```csharp
using Claude.AgentSdk.Messages;

private static MessageResult ProcessMessage(Message message)
{
    return message.Match(
        assistantMessage: a => {
            foreach (var block in a.MessageContent.Content)
            {
                block.Match(
                    textBlock: t => Console.Write(t.Text),
                    toolUseBlock: t => Console.Write($"[Using {t.Name}]"),
                    thinkingBlock: _ => Console.Write("[thinking...]"),
                    toolResultBlock: _ => { }
                );
            }
            return MessageResult.MoreMessages;
        },
        resultMessage: r => {
            Console.WriteLine($"[{r.DurationMs/1000.0:F1}s | ${r.TotalCostUsd:F4}]");
            return MessageResult.Completed;
        },
        systemMessage: _ => MessageResult.NoMessage,
        userMessage: _ => MessageResult.NoMessage,
        streamEvent: _ => MessageResult.NoMessage
    );
}
```

### Generated Enum String Mappings

```csharp
using Claude.AgentSdk.Types;

// Strongly-typed enum handling
case ResultMessage result:
    var statusStr = result.SubtypeEnum.ToJsonString();  // "success", "error", or "partial"
    Console.WriteLine($"Status: {statusStr}");
    break;

// Safe parsing from JSON strings
if (EnumStringMappings.TryParseResultMessageSubtype(jsonValue, out var subtype))
{
    if (subtype == ResultMessageSubtype.Success)
        Console.WriteLine("Completed successfully!");
}
```

## Key Code Patterns

### Bidirectional Communication

```csharp
await using var client = new ClaudeAgentClient(options);

// Create a session for bidirectional communication
await using var session = await client.CreateSessionAsync();

while (true)
{
    var input = Console.ReadLine();

    // Send message to session
    await session.SendAsync(input);

    // Stream responses until completion
    await foreach (var message in session.ReceiveResponseAsync())
    {
        var result = ProcessMessage(message);
        if (result == MessageResult.Completed)
            break;
    }
}
```

### Handling Different Content Blocks

```csharp
private enum MessageResult { MoreMessages, NoMessage, Completed }

private static MessageResult ProcessMessage(Message message)
{
    switch (message)
    {
        case AssistantMessage assistant:
            foreach (var block in assistant.MessageContent.Content)
            {
                switch (block)
                {
                    case TextBlock text:
                        Console.Write(text.Text);
                        break;

                    case ToolUseBlock toolUse:
                        var summary = GetToolInputSummary(toolUse.Name, toolUse.Input);
                        Console.Write($"[Using {toolUse.Name}: {summary}]");
                        break;

                    case ThinkingBlock:
                        Console.Write("[thinking...]");
                        break;
                }
            }
            return MessageResult.MoreMessages;

        case ResultMessage result:
            Console.Write($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4}]");
            return MessageResult.Completed;
    }

    return MessageResult.NoMessage;
}
```

## Available Tools

The chat assistant has access to:
- `Bash` - Execute shell commands
- `Read` / `Write` / `Edit` - File operations
- `Glob` / `Grep` - File search
- `WebSearch` / `WebFetch` - Web access
- `TodoWrite` - Task management

## Project Structure

```
Claude.AgentSdk.SimpleChatApp/
├── Program.cs              # Main entry point with chat loop
└── Claude.AgentSdk.SimpleChatApp.csproj
```

## Configuration

The example uses these default settings:

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = "You are a helpful AI assistant...",
    Model = "sonnet",
    MaxTurns = 100,
    PermissionMode = PermissionMode.AcceptEdits,
    AllowedTools = ["Bash", "Read", "Write", "Edit", "Glob", "Grep",
                    "WebSearch", "WebFetch", "TodoWrite"]
};
```

## Ported From

This example is ported from the official TypeScript demo:
`claude-agent-sdk-demos/simple-chatapp/`
