using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Claude.AgentSdk.Transport;

/// <summary>
///     A mock implementation of <see cref="ITransport" /> for testing without the Claude CLI.
/// </summary>
/// <remarks>
///     <para>
///         This transport allows you to write unit tests for code that uses the Claude Agent SDK
///         without requiring a real Claude CLI installation or API access.
///     </para>
///     <para>
///         Use <see cref="EnqueueMessage(string)" /> to set up expected responses, and check
///         <see cref="WrittenMessages" /> to verify what was sent.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// var transport = new MockTransport();
/// transport.EnqueueMessage("""{"type":"system","subtype":"init"}""");
/// transport.EnqueueMessage("""{"type":"result","subtype":"success","is_error":false,"session_id":"test"}""");
///
/// // Use with ClaudeAgentClient.CreateForTesting(transport: transport)
/// </code>
/// </example>
internal class MockTransport : ITransport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly Channel<JsonDocument> _messageChannel;
    private readonly List<JsonElement> _writtenMessages = [];
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private bool _disposed;
    private bool _connected;
    private TaskCompletionSource? _inputEndedTcs;

    /// <summary>
    ///     Creates a new mock transport instance.
    /// </summary>
    public MockTransport()
    {
        _messageChannel = Channel.CreateUnbounded<JsonDocument>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    ///     Whether the transport is connected and ready.
    /// </summary>
    public bool IsReady => _connected && !_disposed;

    /// <summary>
    ///     Gets all messages that were written to this transport.
    /// </summary>
    public IReadOnlyList<JsonElement> WrittenMessages
    {
        get
        {
            lock (_lock)
            {
                return _writtenMessages.ToList();
            }
        }
    }

    /// <summary>
    ///     Gets whether input has been ended (stdin closed).
    /// </summary>
    public bool InputEnded { get; private set; }

    /// <summary>
    ///     Gets whether the transport has been closed.
    /// </summary>
    public bool Closed { get; private set; }

    /// <summary>
    ///     Connect the transport.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _connected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Write a JSON message to the transport.
    /// </summary>
    public Task WriteAsync(JsonDocument message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotConnected();
        lock (_lock)
        {
            _writtenMessages.Add(message.RootElement.Clone());
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Write a JSON message using a pre-serialized object.
    /// </summary>
    public virtual Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotConnected();
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            _writtenMessages.Add(JsonDocument.Parse(json).RootElement.Clone());
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Read messages from the transport.
    /// </summary>
    public async IAsyncEnumerable<JsonDocument> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotConnected();

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    ///     Signal end of input (close stdin).
    /// </summary>
    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        InputEnded = true;
        _inputEndedTcs?.TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Close the transport.
    /// </summary>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Closed = true;
        _messageChannel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Dispose the transport asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _messageChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Queue a message to be returned from <see cref="ReadMessagesAsync" />.
    /// </summary>
    /// <param name="message">The JSON document to enqueue.</param>
    public void EnqueueMessage(JsonDocument message)
    {
        _messageChannel.Writer.TryWrite(message);
    }

    /// <summary>
    ///     Queue a message from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to parse and enqueue.</param>
    public void EnqueueMessage(string json)
    {
        EnqueueMessage(JsonDocument.Parse(json));
    }

    /// <summary>
    ///     Inject a message asynchronously (waits for space in channel).
    /// </summary>
    /// <param name="message">The JSON document to inject.</param>
    public async Task InjectMessageAsync(JsonDocument message)
    {
        await _messageChannel.Writer.WriteAsync(message);
    }

    /// <summary>
    ///     Complete the message channel (signals end of messages).
    /// </summary>
    public void CompleteMessages()
    {
        _messageChannel.Writer.TryComplete();
    }

    /// <summary>
    ///     Complete the message channel with an error.
    /// </summary>
    /// <param name="ex">The exception to complete with.</param>
    public void CompleteMessagesWithError(Exception ex)
    {
        _messageChannel.Writer.TryComplete(ex);
    }

    /// <summary>
    ///     Wait for input to be ended.
    /// </summary>
    /// <returns>A task that completes when <see cref="EndInputAsync" /> is called.</returns>
    public Task WaitForInputEndedAsync()
    {
        _inputEndedTcs ??= new TaskCompletionSource();
        if (InputEnded)
        {
            _inputEndedTcs.TrySetResult();
        }

        return _inputEndedTcs.Task;
    }

    /// <summary>
    ///     Get the last written message.
    /// </summary>
    /// <returns>The last message written, or null if none.</returns>
    public JsonElement? GetLastWrittenMessage()
    {
        lock (_lock)
        {
            return _writtenMessages.Count > 0 ? _writtenMessages[^1] : null;
        }
    }

    /// <summary>
    ///     Clear all written messages.
    /// </summary>
    public void ClearWrittenMessages()
    {
        lock (_lock)
        {
            _writtenMessages.Clear();
        }
    }

    /// <summary>
    ///     Throws if the transport has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    ///     Throws if the transport is not connected.
    /// </summary>
    protected void ThrowIfNotConnected()
    {
        if (!_connected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }
    }
}
