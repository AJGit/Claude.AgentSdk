# Functional Chat App

An interactive chat application demonstrating functional programming patterns with the Claude Agent SDK.

## What This Example Demonstrates

- **Functional types**: `Result<T>`, `Option<T>`, and `Pipeline<TIn, TOut>`
- **Railway-oriented programming**: Composing operations with automatic error handling
- **Immutable state**: Using records for configuration and state
- **Pattern matching**: Type-safe handling of discriminated unions
- **Functional collection operations**: `Choose`, `Map`, `Bind`, `Where`

## Functional Programming Features Used

### Option&lt;T&gt; for Null Safety

```csharp
using Claude.AgentSdk.Functional;

// Safe user input handling - no null checks needed
private static Option<string> GetUserInput()
{
    return Option.FromNullable(Console.ReadLine())
        .Map(s => s.Trim())
        .Where(s => !string.IsNullOrEmpty(s));
}

// Chain operations safely
var command = GetUserInput()
    .Bind(ParseCommand);

// Pattern match on the result
await command.Match(
    some: async cmd => await HandleCommandAsync(cmd, session),
    none: () => Task.FromResult((true, false))
);
```

### Result&lt;T&gt; for Error Handling

```csharp
using Claude.AgentSdk.Functional;

// Wrap operations that can fail
private static async Task<Result<ChatState>> RunSessionAsync(ChatConfig config)
{
    return await Result.TryAsync(async () =>
    {
        var options = CreateOptions(config);
        await using var client = new ClaudeAgentClient(options);
        await using var session = await client.CreateSessionAsync();
        return await ProcessChatMessagesAsync(session);
    });
}

// Handle success and failure explicitly
sessionResult.Match(
    success: state =>
    {
        if (!state.ShouldContinue)
        {
            Console.WriteLine("Goodbye!");
            Environment.Exit(0);
        }
    },
    failure: error =>
    {
        Console.WriteLine($"Error: {error}");
    }
);
```

### Pipeline&lt;TIn, TOut&gt; for Composable Processing

```csharp
using Claude.AgentSdk.Functional;

// Create a processing pipeline
private static Pipeline<Message, ProcessingResult> CreateMessageProcessingPipeline() =>
    Pipeline.StartWith<Message, ProcessingResult>(ProcessMessage);

// Use the pipeline
await foreach (var msg in session.ReceiveResponseAsync())
{
    var result = processingPipeline.Run(msg);

    if (result.Match(r => r == ProcessingResult.Completed, _ => false))
    {
        break;
    }
}
```

### Discriminated Unions with Records

```csharp
// Define a command union
private abstract record Command;
private sealed record QuitCommand : Command;
private sealed record ClearCommand : Command;
private sealed record ChatCommand(string Message) : Command;

// Parse input into commands
private static Option<Command> ParseCommand(string input) =>
    input.ToLowerInvariant() switch
    {
        "/exit" or "/quit" => Option.Some<Command>(new QuitCommand()),
        "/clear" => Option.Some<Command>(new ClearCommand()),
        _ when input.StartsWith('/') => Option.NoneOf<Command>(),
        _ => Option.Some<Command>(new ChatCommand(input))
    };

// Handle commands with pattern matching
return command switch
{
    QuitCommand => (false, false),
    ClearCommand => HandleClear(),
    ChatCommand chat => await HandleChatAsync(chat.Message, session),
    _ => (true, false)
};
```

### Functional Collection Extensions

```csharp
using Claude.AgentSdk.Functional;

// Choose: Filter and transform in one operation
blocks
    .Choose(block => block switch
    {
        TextBlock text => Option.Some<Action>(() => Console.Write(text.Text)),
        ToolUseBlock toolUse => Option.Some<Action>(() => PrintToolUse(toolUse)),
        ThinkingBlock => Option.Some<Action>(PrintThinking),
        _ => Option.NoneOf<Action>()
    })
    .ToList()
    .ForEach(action => action());
```

### Immutable Configuration

```csharp
// Configuration as an immutable record
private sealed record ChatConfig(
    string SystemPrompt,
    string Model,
    int MaxTurns,
    PermissionMode PermissionMode,
    IReadOnlyList<string> AllowedTools
);

// State as an immutable record
private sealed record ChatState(
    bool ShouldContinue,
    bool ShouldRestart,
    Option<string> LastError
);
```

## Running the Example

```bash
cd examples/Claude.AgentSdk.FunctionalChatApp
dotnet run
```

## Commands

| Command | Description |
|---------|-------------|
| `/clear` | Start a new conversation |
| `/exit` | Exit the application |

## Key Functional Patterns

### Railway-Oriented Programming

Operations are chained together, and any failure automatically short-circuits the rest of the pipeline:

```csharp
var result = await Result.TryAsync(async () =>
{
    // Step 1: Create client
    var client = new ClaudeAgentClient(options);

    // Step 2: Create session (automatically rolled back if this fails)
    var session = await client.CreateSessionAsync();

    // Step 3: Process messages
    return await ProcessChatMessagesAsync(session);
});

// Only handle the final result
result.Match(
    success: state => HandleSuccess(state),
    failure: error => HandleError(error)
);
```

### Option Chaining

Chain operations on optional values without null checks:

```csharp
private static Option<string> GetToolInputSummary(string toolName, JsonElement? input) =>
    input.HasValue
        ? ExtractToolSummary(toolName, input.Value)
        : Option.NoneOf<string>();

// Use Do for side effects without breaking the chain
GetToolInputSummary(toolUse.Name, toolUse.Input)
    .Do(summary => Console.Write($": {summary}"));
```

## Available Functional Types

| Type | Purpose |
|------|---------|
| `Option<T>` | Represents a value that may or may not exist |
| `Result<T>` | Represents success with a value or failure with an error |
| `Result<T, TError>` | Result with a custom error type |
| `Pipeline<TIn, TOut>` | Composable processing pipeline |
| `Validation<T>` | Accumulates multiple errors (not shown in this example) |
| `Either<TLeft, TRight>` | Represents one of two possible values |
| `Unit` | Represents no value (void equivalent for functional APIs) |

## Comparison: Imperative vs Functional

### Imperative Style
```csharp
string input = Console.ReadLine();
if (input == null || string.IsNullOrWhiteSpace(input))
{
    return;
}
input = input.Trim();
Command command = ParseCommand(input);
if (command == null)
{
    return;
}
await HandleCommand(command);
```

### Functional Style
```csharp
await Option.FromNullable(Console.ReadLine())
    .Map(s => s.Trim())
    .Where(s => !string.IsNullOrEmpty(s))
    .Bind(ParseCommand)
    .Match(
        some: async cmd => await HandleCommandAsync(cmd),
        none: () => Task.CompletedTask
    );
```

## Benefits of Functional Patterns

1. **Explicit error handling**: Errors are part of the type system, not exceptions
2. **No null reference exceptions**: `Option<T>` makes absence explicit
3. **Composability**: Small functions combine into complex pipelines
4. **Immutability**: State changes are explicit and traceable
5. **Testability**: Pure functions are easy to test in isolation
