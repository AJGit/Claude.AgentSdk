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
