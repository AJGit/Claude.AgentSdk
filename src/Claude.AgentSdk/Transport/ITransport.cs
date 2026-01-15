namespace Claude.AgentSdk.Transport;

/// <summary>
///     Interface for communication transport with the Claude CLI.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    ///     Whether the transport is connected and ready.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    ///     Connect the transport.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Write a JSON message to the transport.
    /// </summary>
    Task WriteAsync(JsonDocument message, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Write a JSON message using a pre-serialized object.
    /// </summary>
    Task WriteAsync<T>(T message, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Read messages from the transport.
    /// </summary>
    IAsyncEnumerable<JsonDocument> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Signal end of input (close stdin).
    /// </summary>
    Task EndInputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Close the transport.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
