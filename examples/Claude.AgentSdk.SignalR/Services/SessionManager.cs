using System.Collections.Concurrent;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.SignalR.Hubs;

namespace Claude.AgentSdk.SignalR.Services;

/// <summary>
/// Manages active Claude Agent sessions for SignalR connections.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Create a new session for a connection.
    /// </summary>
    Task<string> CreateSessionAsync(
        string connectionId,
        SessionOptionsDto? options,
        Func<string, Message, Task> messageCallback);

    /// <summary>
    /// Get an existing session by connection ID.
    /// </summary>
    AgentSession? GetSession(string connectionId);

    /// <summary>
    /// Remove a session.
    /// </summary>
    Task RemoveSessionAsync(string connectionId);
}

/// <summary>
/// Represents an active agent session.
/// </summary>
public class AgentSession : IAsyncDisposable
{
    public required string SessionId { get; init; }
    public required string ConnectionId { get; init; }
    public required ClaudeAgentClient Client { get; init; }
    public required ClaudeAgentSession Session { get; init; }
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource.Cancel();
        await Session.DisposeAsync();
        await Client.DisposeAsync();
        CancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Default implementation of session manager using in-memory storage.
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateSessionAsync(
        string connectionId,
        SessionOptionsDto? options,
        Func<string, Message, Task> messageCallback)
    {
        // Remove existing session if any
        await RemoveSessionAsync(connectionId);

        var sessionId = Guid.NewGuid().ToString("N")[..12];

        var agentOptions = new ClaudeAgentOptions
        {
            SystemPrompt = options?.SystemPrompt ?? string.Empty,
            MaxTurns = options?.MaxTurns ?? 10,
            Model = options?.Model,
            WorkingDirectory = options?.WorkingDirectory,
            PermissionMode = PermissionMode.AcceptEdits
        };

        var client = new ClaudeAgentClient(agentOptions);

        // Create the session for bidirectional communication
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

        _logger.LogInformation(
            "Created session {SessionId} for connection {ConnectionId}",
            sessionId,
            connectionId);

        return sessionId;
    }

    public AgentSession? GetSession(string connectionId)
    {
        _sessions.TryGetValue(connectionId, out var session);
        return session;
    }

    public async Task RemoveSessionAsync(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var session))
        {
            _logger.LogInformation(
                "Removing session {SessionId} for connection {ConnectionId}",
                session.SessionId,
                connectionId);

            await session.DisposeAsync();
        }
    }

    private async Task RelayMessagesAsync(
        AgentSession session,
        Func<string, Message, Task> messageCallback)
    {
        try
        {
            await foreach (var message in session.Session.ReceiveAsync(session.CancellationTokenSource.Token))
            {
                try
                {
                    await messageCallback(session.ConnectionId, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message to client {ConnectionId}", session.ConnectionId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal termination
            _logger.LogDebug("Message relay cancelled for session {SessionId}", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message relay for session {SessionId}", session.SessionId);
        }
    }
}
