# SignalR Integration Example

An ASP.NET Core application demonstrating how to expose the Claude Agent SDK over SignalR for real-time web communication.

## What This Example Demonstrates

- **SignalR Hub wrapper** for the Claude Agent SDK
- **Real-time streaming** of Claude responses to web clients
- **Session management** for multi-turn conversations
- **Bidirectional communication** pattern
- **DTO mapping** from SDK types to SignalR-friendly objects

## Architecture

```
┌─────────────────┐     SignalR      ┌─────────────────┐     SDK      ┌─────────────────┐
│   Web Client    │◄───WebSocket───►│  ClaudeAgentHub │◄───────────►│ ClaudeAgentClient│
│  (JavaScript)   │                  │   (ASP.NET)     │              │     (SDK)       │
└─────────────────┘                  └─────────────────┘              └─────────────────┘
                                             │
                                             ▼
                                     ┌─────────────────┐
                                     │ SessionManager  │
                                     │ (manages client │
                                     │   sessions)     │
                                     └─────────────────┘
```

## Running the Example

```bash
cd examples/Claude.AgentSdk.SignalR
dotnet run
```

The server starts at `http://localhost:5000` with these endpoints:

| Endpoint      | Description                    |
| ------------- | ------------------------------ |
| `/claude`     | SignalR Hub endpoint           |
| `/index.html` | Demo web client (if available) |
| `/health`     | Health check endpoint          |

## Hub Methods

### One-Shot Query
Stream a response for a single prompt:

```javascript
// JavaScript client
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/claude")
    .build();

await connection.start();

// Stream query results
for await (const message of connection.stream("Query", prompt, options)) {
    console.log(message);
}
```

### Bidirectional Session
Start a persistent session for multi-turn conversations:

```javascript
// Start session
const session = await connection.invoke("StartSession", {
    systemPrompt: "You are a helpful assistant",
    maxTurns: 10
});

// Listen for messages
connection.on("ReceiveMessage", (message) => {
    console.log("Received:", message);
});

// Send messages
await connection.invoke("SendMessage", "Hello Claude!");
await connection.invoke("SendMessage", "What's 2 + 2?");

// Control session
await connection.invoke("SetModel", "sonnet");
await connection.invoke("Interrupt");
await connection.invoke("EndSession");
```

## Key Components

### ClaudeAgentHub

The SignalR Hub that wraps SDK functionality:

```csharp
public class ClaudeAgentHub : Hub
{
    // One-shot streaming query
    public async IAsyncEnumerable<MessageDto> Query(
        string prompt,
        QueryOptionsDto? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var client = new ClaudeAgentClient(agentOptions);

        await foreach (var message in client.QueryAsync(prompt, null, cancellationToken))
        {
            yield return MapToDto(message);
        }
    }

    // Start bidirectional session
    public async Task<SessionInfoDto> StartSession(SessionOptionsDto? options = null)
    {
        var sessionId = await _sessionManager.CreateSessionAsync(
            Context.ConnectionId,
            options,
            SendMessageToClient);

        return new SessionInfoDto { SessionId = sessionId, Status = "connected" };
    }

    // Send message in active session
    public async Task SendMessage(string content, string? sessionId = null)
    {
        var session = _sessionManager.GetSession(Context.ConnectionId);
        await session.Session.SendAsync(content, sessionId);  // Note: SendAsync is on Session, not Client
    }

    // Additional methods: Interrupt, EndSession, SetModel, SetPermissionMode
}
```

### SessionManager

Manages active Claude Agent sessions:

```csharp
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public async Task<string> CreateSessionAsync(
        string connectionId,
        SessionOptionsDto? options,
        Func<string, Message, Task> messageCallback)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..12];

        var agentOptions = new ClaudeAgentOptions
        {
            SystemPrompt = options?.SystemPrompt ?? string.Empty,
            MaxTurns = options?.MaxTurns ?? 10,
            Model = options?.Model,
            PermissionMode = PermissionMode.AcceptEdits
        };

        var client = new ClaudeAgentClient(agentOptions);

        // Create session for bidirectional communication
        var claudeSession = await client.CreateSessionAsync();

        var session = new AgentSession
        {
            SessionId = sessionId,
            ConnectionId = connectionId,
            Client = client,
            Session = claudeSession
        };

        _sessions[connectionId] = session;

        // Start background message relay
        _ = RelayMessagesAsync(session, messageCallback);

        return sessionId;
    }
}
```

### DTO Types

SignalR-friendly data transfer objects:

```csharp
public class MessageDto
{
    public required string Type { get; set; }      // "user", "assistant", "system", "result"
    public string? Subtype { get; set; }           // e.g., "init", "compact_boundary"
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public List<ContentBlockDto>? Content { get; set; }
    public double? TotalCostUsd { get; set; }
    public int? DurationMs { get; set; }
    public int? NumTurns { get; set; }
    public bool? IsError { get; set; }
    public bool? IsInit { get; set; }
}

public class ContentBlockDto
{
    public required string Type { get; set; }      // "text", "tool_use", "tool_result", "thinking"
    public string? Text { get; set; }
    public string? ToolName { get; set; }
    public string? ToolId { get; set; }
    public string? Input { get; set; }
    public string? Content { get; set; }           // For tool results
    public bool? IsError { get; set; }             // For tool results
}
```

## Project Structure

```
Claude.AgentSdk.SignalR/
├── Program.cs                  # ASP.NET Core host setup
├── Hubs/
│   └── ClaudeAgentHub.cs       # SignalR Hub with DTO definitions
├── Services/
│   └── SessionManager.cs       # Session lifecycle management
├── wwwroot/                    # Static files for demo client
│   └── index.html              # (optional) Demo web client
└── Claude.AgentSdk.SignalR.csproj
```

## Configuration

The example configures SignalR with extended timeouts for long-running Claude operations:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

## Using with a Web Frontend

### JavaScript/TypeScript Client

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/claude")
    .withAutomaticReconnect()
    .build();

// Handle incoming messages
connection.on("ReceiveMessage", (message: MessageDto) => {
    if (message.type === "assistant" && message.content) {
        for (const block of message.content) {
            if (block.type === "text") {
                console.log(block.text);
            }
        }
    }
});

await connection.start();

// Start a session
const session = await connection.invoke("StartSession", {
    systemPrompt: "You are a helpful coding assistant"
});

// Send messages
await connection.invoke("SendMessage", "Help me write a function");
```

### React Example

```tsx
function ClaudeChat() {
    const [messages, setMessages] = useState<MessageDto[]>([]);
    const connectionRef = useRef<signalR.HubConnection>();

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/claude")
            .build();

        connection.on("ReceiveMessage", (msg) => {
            setMessages(prev => [...prev, msg]);
        });

        connection.start().then(() => {
            connection.invoke("StartSession");
        });

        connectionRef.current = connection;
        return () => { connection.stop(); };
    }, []);

    const sendMessage = (text: string) => {
        connectionRef.current?.invoke("SendMessage", text);
    };

    return (/* render messages and input */);
}
```

## Production Considerations

1. **Authentication**: Add authentication/authorization to the Hub
2. **Rate Limiting**: Implement rate limiting per connection
3. **Session Cleanup**: Configure session timeout and cleanup policies
4. **Scaling**: Use Azure SignalR Service or Redis backplane for multiple servers
5. **Error Handling**: Add comprehensive error handling and logging
6. **CORS**: Configure appropriate CORS policies for production

## Dependencies

- ASP.NET Core 10.0
- Microsoft.AspNetCore.SignalR
- Claude.AgentSdk
