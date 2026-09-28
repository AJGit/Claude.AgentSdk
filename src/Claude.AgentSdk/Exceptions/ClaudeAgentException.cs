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
///     Exception thrown when the CLI exits after reporting a terminal error result (e.g. error_max_turns,
///     error_during_execution, or an API failure). Carries the structured payload so callers can branch on
///     the failure reason without string matching.
/// </summary>
public class ResultException : CliProcessException
{
    public ResultException(string message, JsonElement data, int? exitCode = null) : base(message, exitCode)
    {
        ResultData = data;
        Subtype = GetString(data, "subtype");
        Result = GetString(data, "result");
        TerminalReason = GetString(data, "terminal_reason");
        SessionId = GetString(data, "session_id");
        ApiErrorStatus = data.ValueKind == JsonValueKind.Object &&
                         data.TryGetProperty("api_error_status", out var status) &&
                         status.ValueKind == JsonValueKind.Number &&
                         status.TryGetInt32(out var code)
            ? code
            : null;
        Errors = NormalizeErrors(data);
    }

    /// <summary>
    ///     The raw result message payload.
    /// </summary>
    public JsonElement ResultData { get; }

    /// <summary>
    ///     The result subtype (e.g. "error_max_turns", or "success" for API failures).
    /// </summary>
    public string? Subtype { get; }

    /// <summary>
    ///     Error messages reported by the CLI.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    ///     The result text, if any.
    /// </summary>
    public string? Result { get; }

    /// <summary>
    ///     HTTP status of the failing API call, when applicable.
    /// </summary>
    public int? ApiErrorStatus { get; }

    /// <summary>
    ///     Why the query loop terminated.
    /// </summary>
    public string? TerminalReason { get; }

    /// <summary>
    ///     The session id of the failed run.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    ///     Picks the most informative text from an error result: errors[], then result, then a non-success
    ///     subtype, then the HTTP status.
    /// </summary>
    internal static string DescribeErrorResult(JsonElement data)
    {
        var errors = NormalizeErrors(data);
        if (errors.Count > 0)
        {
            return string.Join("; ", errors);
        }

        var result = GetString(data, "result");
        if (!string.IsNullOrWhiteSpace(result))
        {
            return result.Trim();
        }

        var subtype = GetString(data, "subtype");
        if (!string.IsNullOrEmpty(subtype) && subtype != "success")
        {
            return subtype;
        }

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("api_error_status", out var status) &&
            status.ValueKind == JsonValueKind.Number)
        {
            return $"API error (HTTP {status.GetRawText()})";
        }

        return "unknown error";
    }

    private static List<string> NormalizeErrors(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return errors.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!)
            .ToList();
    }

    private static string? GetString(JsonElement data, string property)
    {
        return data.ValueKind == JsonValueKind.Object &&
               data.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
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
///     Exception thrown when the CLI answers a control request with an error response.
/// </summary>
public class ControlRequestException(string subtype, string message) : ProtocolException(message)
{
    /// <summary>
    ///     The control request subtype that failed (e.g. "mcp_status").
    /// </summary>
    public string Subtype { get; } = subtype;
}

/// <summary>
///     Exception thrown when a <see cref="Sessions.ISessionStore" /> call fails or times out while materializing a
///     store-backed resume, or when the store lacks a capability the operation needs.
/// </summary>
public class SessionStoreException : ClaudeAgentException
{
    public SessionStoreException(string message) : base(message)
    {
    }

    public SessionStoreException(string message, Exception innerException) : base(message, innerException)
    {
    }
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
