namespace Claude.AgentSdk.Exceptions;

/// <summary>
///     Base exception for Claude Agent SDK errors.
/// </summary>
public class ClaudeAgentException : Exception
{
    public ClaudeAgentException(string message) : base(message)
    {
    }

    public ClaudeAgentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
///     Exception thrown when the transport fails.
/// </summary>
public class TransportException : ClaudeAgentException
{
    public TransportException(string message) : base(message)
    {
    }

    public TransportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
///     Exception thrown when the CLI cannot be found.
/// </summary>
public class CliNotFoundException(string message) : TransportException(message);

/// <summary>
///     Exception thrown when the CLI process exits unexpectedly.
/// </summary>
public class CliProcessException(string message, int? exitCode = null) : TransportException(message)
{
    public int? ExitCode { get; } = exitCode;
}

/// <summary>
///     Exception thrown when the control protocol fails.
/// </summary>
public class ProtocolException : ClaudeAgentException
{
    public ProtocolException(string message) : base(message)
    {
    }

    public ProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
///     Exception thrown when a control request times out.
/// </summary>
public class ControlTimeoutException(string requestId, string message) : ProtocolException(message)
{
    public string RequestId { get; } = requestId;
}

/// <summary>
///     Exception thrown when message parsing fails.
/// </summary>
public class MessageParseException : ClaudeAgentException
{
    public MessageParseException(string message, string? rawMessage = null) : base(message)
    {
        RawMessage = rawMessage;
    }

    public MessageParseException(string message, Exception innerException, string? rawMessage = null)
        : base(message, innerException)
    {
        RawMessage = rawMessage;
    }

    public string? RawMessage { get; }
}
