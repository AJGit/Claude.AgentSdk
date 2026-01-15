using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Claude.AgentSdk.Transport;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
/// A mock implementation of ITransport for testing QueryHandler message processing.
/// Allows configuring messages to return, tracks writes, and supports cancellation.
/// </summary>
public class MockTransport : ITransport
{
    private readonly Channel<JsonDocument> _messageChannel;
    private readonly List<object> _writtenMessages = [];
    private readonly object _lock = new();
    private bool _disposed;
    private bool _connected;
    private TaskCompletionSource? _inputEndedTcs;

    public MockTransport()
    {
        _messageChannel = Channel.CreateUnbounded<JsonDocument>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Whether the transport is connected and ready.
    /// </summary>
    public bool IsReady => _connected && !_disposed;

    /// <summary>
    /// Gets all messages that were written to this transport.
    /// </summary>
    public IReadOnlyList<object> WrittenMessages
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
    /// Gets whether input has been ended.
    /// </summary>
    public bool InputEnded { get; private set; }

    /// <summary>
    /// Gets whether the transport has been closed.
    /// </summary>
    public bool Closed { get; private set; }

    /// <summary>
    /// Connect the transport.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _connected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Write a JSON message to the transport.
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
    /// Write a JSON message using a pre-serialized object.
    /// </summary>
    public virtual Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotConnected();
        lock (_lock)
        {
            // Store a serialized copy
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            _writtenMessages.Add(JsonDocument.Parse(json).RootElement.Clone());
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Read messages from the transport.
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
    /// Signal end of input (close stdin).
    /// </summary>
    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        InputEnded = true;
        _inputEndedTcs?.TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Close the transport.
    /// </summary>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Closed = true;
        _messageChannel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose the transport asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _messageChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Queue a message to be returned from ReadMessagesAsync.
    /// </summary>
    public void EnqueueMessage(JsonDocument message)
    {
        _messageChannel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Queue a message from a JSON string.
    /// </summary>
    public void EnqueueMessage(string json)
    {
        EnqueueMessage(JsonDocument.Parse(json));
    }

    /// <summary>
    /// Inject a message asynchronously.
    /// </summary>
    public async Task InjectMessageAsync(JsonDocument message)
    {
        await _messageChannel.Writer.WriteAsync(message);
    }

    /// <summary>
    /// Complete the message channel (signals end of messages).
    /// </summary>
    public void CompleteMessages()
    {
        _messageChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Complete the message channel with an error.
    /// </summary>
    public void CompleteMessagesWithError(Exception ex)
    {
        _messageChannel.Writer.TryComplete(ex);
    }

    /// <summary>
    /// Wait for input to be ended.
    /// </summary>
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
    /// Get the last written message as a JsonElement.
    /// </summary>
    public JsonElement? GetLastWrittenMessage()
    {
        lock (_lock)
        {
            return _writtenMessages.Count > 0 ? (JsonElement)_writtenMessages[^1] : null;
        }
    }

    /// <summary>
    /// Get all written messages as JsonElements.
    /// </summary>
    public IReadOnlyList<JsonElement> GetAllWrittenMessagesAsJson()
    {
        lock (_lock)
        {
            return _writtenMessages.Cast<JsonElement>().ToList();
        }
    }

    /// <summary>
    /// Clear all written messages.
    /// </summary>
    public void ClearWrittenMessages()
    {
        lock (_lock)
        {
            _writtenMessages.Clear();
        }
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MockTransport));
    }

    protected void ThrowIfNotConnected()
    {
        if (!_connected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }
    }
}
