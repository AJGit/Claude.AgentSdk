using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Logging;

/// <summary>
///     Centralized logging using source-generated LoggerMessage for high-performance logging.
/// </summary>
/// <remarks>
///     Event ID ranges:
///     - 1000-1099: Transport events (SubprocessTransport)
///     - 2000-2099: Protocol events (QueryHandler)
///     - 3000-3099: Client events (ClaudeAgentClient)
/// </remarks>
internal static partial class Log
{
    // ============================================================================
    // Transport Events (1000-1099)
    // ============================================================================

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Starting CLI: {CliPath} {Args}")]
    public static partial void CliStarting(ILogger logger, string cliPath, string args);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "CLI process started with PID {Pid}")]
    public static partial void CliProcessStarted(ILogger logger, int pid);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "CLI stdout closed")]
    public static partial void CliStdoutClosed(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "CLI stderr: {Data}")]
    public static partial void CliStderr(ILogger logger, string data);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "Closing stdin for one-shot mode")]
    public static partial void ClosingStdinOneShot(ILogger logger);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Closed stdin")]
    public static partial void StdinClosed(ILogger logger);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Warning,
        Message = "CLI did not exit gracefully, killing process")]
    public static partial void CliKillingProcess(ILogger logger);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Warning,
        Message = "Error closing CLI process")]
    public static partial void CliCloseError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "CLI process exited with code {ExitCode} (killed={WasKilled})")]
    public static partial void CliProcessExited(ILogger logger, int exitCode, bool wasKilled);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Trace,
        Message = "Sent: {Message}")]
    public static partial void MessageSent(ILogger logger, string message);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Trace,
        Message = "Received: {Line}")]
    public static partial void MessageReceived(ILogger logger, string line);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Warning,
        Message = "Failed to parse JSON line: {Line}")]
    public static partial void JsonParseError(ILogger logger, Exception exception, string line);

    // ============================================================================
    // Protocol Events (2000-2099)
    // ============================================================================

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Control protocol initialized")]
    public static partial void ControlProtocolInitialized(ILogger logger);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Message reader cancelled")]
    public static partial void MessageReaderCancelled(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Error in message reader loop")]
    public static partial void MessageReaderError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Received control_cancel_request")]
    public static partial void ControlCancelRequestReceived(ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "Final ResultMessage received (subtype={Subtype}) - completing message channel")]
    public static partial void ResultMessageReceived(ILogger logger, string? subtype);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Message missing 'type' property")]
    public static partial void MessageMissingType(ILogger logger);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Warning,
        Message = "Control response missing 'response' property")]
    public static partial void ControlResponseMissingResponse(ILogger logger);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Warning,
        Message = "Control response missing 'request_id'")]
    public static partial void ControlResponseMissingRequestId(ILogger logger);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Warning,
        Message = "Received response for unknown request: {RequestId}")]
    public static partial void UnknownRequestResponse(ILogger logger, string requestId);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Error,
        Message = "Error handling control request {Subtype}")]
    public static partial void ControlRequestError(ILogger logger, Exception exception, string subtype);

    [LoggerMessage(
        EventId = 2020,
        Level = LogLevel.Warning,
        Message = "No hook callback found for ID: {CallbackId}")]
    public static partial void HookCallbackNotFound(ILogger logger, string callbackId);

    [LoggerMessage(
        EventId = 2021,
        Level = LogLevel.Error,
        Message = "Hook callback failed for {CallbackId}")]
    public static partial void HookCallbackError(ILogger logger, Exception exception, string callbackId);

    [LoggerMessage(
        EventId = 2022,
        Level = LogLevel.Warning,
        Message = "Failed to parse hook input for {EventName}")]
    public static partial void HookInputParseError(ILogger logger, Exception exception, string eventName);

    [LoggerMessage(
        EventId = 2030,
        Level = LogLevel.Warning,
        Message = "Failed to parse message of type {Type}")]
    public static partial void MessageParseError(ILogger logger, Exception exception, string? type);

    // ============================================================================
    // Client Events (3000-3099)
    // ============================================================================

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Connected to Claude CLI in bidirectional mode")]
    public static partial void ConnectedBidirectional(ILogger logger);

    // ============================================================================
    // Session Events (4000-4099)
    // ============================================================================

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "Session created with ID {SessionId}")]
    public static partial void SessionCreated(ILogger logger, string sessionId);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Session disposing")]
    public static partial void SessionDisposing(ILogger logger);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Session cancelled")]
    public static partial void SessionCancelled(ILogger logger);
}
