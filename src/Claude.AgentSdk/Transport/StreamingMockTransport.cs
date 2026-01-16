namespace Claude.AgentSdk.Transport;

/// <summary>
///     A mock transport that simulates streaming message delivery with configurable delays.
/// </summary>
/// <remarks>
///     <para>
///         This transport extends <see cref="MockTransport" /> to add artificial delays
///         between operations, useful for testing streaming scenarios and timeout handling.
///     </para>
///     <para>
///         Use this when you need to test how your application handles real-time streaming
///         responses from Claude.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Create a streaming transport with 100ms delay between writes
/// var transport = new StreamingMockTransport(TimeSpan.FromMilliseconds(100));
/// transport.EnqueueMessage("""{"type":"system","subtype":"init"}""");
/// transport.EnqueueMessage("""{"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"claude-3"}}""");
/// transport.EnqueueMessage("""{"type":"result","subtype":"success","is_error":false,"session_id":"test"}""");
/// </code>
/// </example>
internal class StreamingMockTransport : MockTransport
{
    private readonly TimeSpan _messageDelay;
    private readonly TimeSpan _readDelay;

    /// <summary>
    ///     Creates a new streaming mock transport.
    /// </summary>
    /// <param name="messageDelay">Delay before each write operation (default: 50ms).</param>
    /// <param name="readDelay">Delay before each read operation (default: 0ms).</param>
    public StreamingMockTransport(TimeSpan? messageDelay = null, TimeSpan? readDelay = null)
    {
        _messageDelay = messageDelay ?? TimeSpan.FromMilliseconds(50);
        _readDelay = readDelay ?? TimeSpan.Zero;
    }

    /// <summary>
    ///     Write a JSON message with simulated delay.
    /// </summary>
    public override async Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        if (_messageDelay > TimeSpan.Zero)
        {
            await Task.Delay(_messageDelay, cancellationToken).ConfigureAwait(false);
        }

        await base.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Enqueue a message with a delay before it becomes available for reading.
    /// </summary>
    /// <param name="json">The JSON string to enqueue.</param>
    /// <param name="delay">The delay before the message is available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnqueueMessageWithDelayAsync(
        string json,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        EnqueueMessage(json);
    }

    /// <summary>
    ///     Enqueue multiple messages with delays between each.
    /// </summary>
    /// <param name="messages">The JSON strings to enqueue.</param>
    /// <param name="delayBetween">The delay between each message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnqueueMessagesWithDelaysAsync(
        IEnumerable<string> messages,
        TimeSpan delayBetween,
        CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            EnqueueMessage(message);
            await Task.Delay(delayBetween, cancellationToken).ConfigureAwait(false);
        }
    }
}
