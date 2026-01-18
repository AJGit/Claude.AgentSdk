using System.Runtime.CompilerServices;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.SignalR.Services;
using Microsoft.AspNetCore.SignalR;

namespace Claude.AgentSdk.SignalR.Hubs;

/// <summary>
///     SignalR Hub that wraps the Claude Agent SDK.
///     Demonstrates how to expose Claude Agent functionality over SignalR
///     without modifying the SDK source code.
/// </summary>
public class ClaudeAgentHub(ISessionManager sessionManager, ILogger<ClaudeAgentHub> logger)
    : Hub
{
    private readonly ILogger<ClaudeAgentHub> _logger = logger;
    private readonly ISessionManager _sessionManager = sessionManager;

    /// <summary>
    ///     One-shot query with streaming response.
    ///     Messages are streamed back to the client as they arrive.
    /// </summary>
    public async IAsyncEnumerable<MessageDto> Query(
        string prompt,
        QueryOptionsDto? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Query from {ConnectionId}: {Prompt}", Context.ConnectionId, prompt);

        ClaudeAgentOptions agentOptions = new()
        {
            SystemPrompt = options?.SystemPrompt ?? string.Empty,
            MaxTurns = options?.MaxTurns ?? 10,
            PermissionMode = PermissionMode.AcceptEdits
        };

        await using ClaudeAgentClient client = new(agentOptions);

        await foreach (Message message in client.QueryAsync(prompt, null, cancellationToken))
        {
            MessageDto? dto = MapToDto(message);
            if (dto != null)
            {
                yield return dto;
            }
        }
    }

    /// <summary>
    ///     Start a bidirectional session.
    ///     The session remains open for multiple messages.
    /// </summary>
    public async Task<SessionInfoDto> StartSession(SessionOptionsDto? options = null)
    {
        _logger.LogInformation("Starting session for {ConnectionId}", Context.ConnectionId);

        string sessionId = await _sessionManager.CreateSessionAsync(
            Context.ConnectionId,
            options,
            SendMessageToClient);

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        return new SessionInfoDto
        {
            SessionId = sessionId,
            Status = "connected"
        };
    }

    /// <summary>
    ///     Send a message in an active session.
    /// </summary>
    public async Task SendMessage(string content, string? sessionId = null)
    {
        _logger.LogInformation("SendMessage from {ConnectionId}: {Content}", Context.ConnectionId, content);

        AgentSession? session = _sessionManager.GetSession(Context.ConnectionId);
        if (session == null)
        {
            throw new HubException("No active session. Call StartSession first.");
        }

        await session.Session.SendAsync(content, sessionId);
    }

    /// <summary>
    ///     Interrupt the current operation.
    /// </summary>
    public async Task Interrupt()
    {
        _logger.LogInformation("Interrupt from {ConnectionId}", Context.ConnectionId);

        AgentSession? session = _sessionManager.GetSession(Context.ConnectionId);
        if (session == null)
        {
            throw new HubException("No active session.");
        }

        await session.Session.InterruptAsync();
    }

    /// <summary>
    ///     End the current session.
    /// </summary>
    public async Task EndSession()
    {
        _logger.LogInformation("EndSession from {ConnectionId}", Context.ConnectionId);

        AgentSession? session = _sessionManager.GetSession(Context.ConnectionId);
        if (session != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, session.SessionId);
            await _sessionManager.RemoveSessionAsync(Context.ConnectionId);
        }
    }

    /// <summary>
    ///     Set the model for the current session.
    /// </summary>
    public async Task SetModel(string model)
    {
        AgentSession? session = _sessionManager.GetSession(Context.ConnectionId);
        if (session == null)
        {
            throw new HubException("No active session.");
        }

        await session.Session.SetModelAsync(model);
    }

    /// <summary>
    ///     Set the permission mode for the current session.
    /// </summary>
    public async Task SetPermissionMode(string mode)
    {
        AgentSession? session = _sessionManager.GetSession(Context.ConnectionId);
        if (session == null)
        {
            throw new HubException("No active session.");
        }

        PermissionMode permissionMode = mode.ToLower() switch
        {
            "default" => PermissionMode.Default,
            "acceptedits" => PermissionMode.AcceptEdits,
            "plan" => PermissionMode.Plan,
            "bypasspermissions" => PermissionMode.BypassPermissions,
            _ => PermissionMode.Default
        };

        await session.Session.SetPermissionModeAsync(permissionMode);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await _sessionManager.RemoveSessionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendMessageToClient(string connectionId, Message message)
    {
        MessageDto? dto = MapToDto(message);
        if (dto != null)
        {
            await Clients.Client(connectionId).SendAsync("ReceiveMessage", dto);
        }
    }

    private static MessageDto? MapToDto(Message message)
    {
        return message switch
        {
            AssistantMessage assistant => new MessageDto
            {
                Type = "assistant",
                Content = assistant.MessageContent.Content.Select(MapContentBlock).ToList(),
                Model = assistant.MessageContent.Model
            },
            SystemMessage system => new MessageDto
            {
                Type = "system",
                Subtype = system.Subtype,
                SessionId = system.SessionId,
                Model = system.Model,
                IsInit = system.IsInit
            },
            ResultMessage result => new MessageDto
            {
                Type = "result",
                SessionId = result.SessionId,
                TotalCostUsd = result.TotalCostUsd,
                DurationMs = result.DurationMs,
                NumTurns = result.NumTurns,
                IsError = result.IsError,
                Result = result.Result,
                ContextTokens = result.Usage?.TotalContextTokens
            },
            UserMessage user => new MessageDto
            {
                Type = "user",
                Content = [new ContentBlockDto { Type = "text", Text = user.MessageContent.Content.ToString() }]
            },
            _ => null
        };
    }

    private static ContentBlockDto MapContentBlock(ContentBlock block)
    {
        return block switch
        {
            TextBlock text => new ContentBlockDto
            {
                Type = "text",
                Text = text.Text
            },
            ToolUseBlock toolUse => new ContentBlockDto
            {
                Type = "tool_use",
                ToolName = toolUse.Name,
                ToolId = toolUse.Id,
                Input = toolUse.Input.ToString()
            },
            ToolResultBlock toolResult => new ContentBlockDto
            {
                Type = "tool_result",
                ToolId = toolResult.ToolUseId,
                Content = toolResult.Content?.ToString(),
                IsError = toolResult.IsError
            },
            ThinkingBlock thinking => new ContentBlockDto
            {
                Type = "thinking",
                Text = thinking.Thinking
            },
            _ => new ContentBlockDto { Type = "unknown" }
        };
    }
}

/// <summary>
///     Options for one-shot queries.
/// </summary>
public class QueryOptionsDto
{
    public string? SystemPrompt { get; set; }
    public int? MaxTurns { get; set; }
    public string? Model { get; set; }
}

/// <summary>
///     Options for creating a session.
/// </summary>
public class SessionOptionsDto
{
    public string? SystemPrompt { get; set; }
    public int? MaxTurns { get; set; }
    public string? Model { get; set; }
    public string? WorkingDirectory { get; set; }
}

/// <summary>
///     Session information returned after starting a session.
/// </summary>
public class SessionInfoDto
{
    public required string SessionId { get; set; }
    public required string Status { get; set; }
}

/// <summary>
///     Message DTO for SignalR transmission.
/// </summary>
public class MessageDto
{
    public required string Type { get; set; }
    public string? Subtype { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public List<ContentBlockDto>? Content { get; set; }
    public double? TotalCostUsd { get; set; }
    public int? DurationMs { get; set; }
    public int? NumTurns { get; set; }
    public bool? IsError { get; set; }
    public bool? IsInit { get; set; }
    public string? Result { get; set; }
    public int? ContextTokens { get; set; }
}

/// <summary>
///     Content block DTO for SignalR transmission.
/// </summary>
public class ContentBlockDto
{
    public required string Type { get; set; }
    public string? Text { get; set; }
    public string? ToolName { get; set; }
    public string? ToolId { get; set; }
    public string? Input { get; set; }
    public string? Content { get; set; }
    public bool? IsError { get; set; }
}
