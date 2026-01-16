using System.Text.Json;
using Claude.AgentSdk.Transport;

namespace Claude.AgentSdk.Tests.Transport;

/// <summary>
///     Tests for MockTransport and StreamingMockTransport.
/// </summary>
[UnitTest]
public class MockTransportTests
{
    #region MockTransport Basic Tests

    [Fact]
    public async Task MockTransport_InitialState_IsNotReady()
    {
        // Arrange
        var transport = new MockTransport();

        // Assert
        Assert.False(transport.IsReady);
        Assert.False(transport.InputEnded);
        Assert.False(transport.Closed);

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_AfterConnect_IsReady()
    {
        // Arrange
        var transport = new MockTransport();

        // Act
        await transport.ConnectAsync();

        // Assert
        Assert.True(transport.IsReady);

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_AfterDispose_IsNotReady()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();
        Assert.True(transport.IsReady);

        // Act
        await transport.DisposeAsync();

        // Assert
        Assert.False(transport.IsReady);
    }

    #endregion

    #region Message Enqueueing Tests

    [Fact]
    public async Task MockTransport_EnqueueMessage_CanBeRead()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        var message = """{"type":"test","value":123}""";
        transport.EnqueueMessage(message);
        transport.CompleteMessages();

        // Act
        var messages = new List<JsonDocument>();
        await foreach (var doc in transport.ReadMessagesAsync())
        {
            messages.Add(doc);
        }

        // Assert
        Assert.Single(messages);
        Assert.Equal("test", messages[0].RootElement.GetProperty("type").GetString());

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_EnqueueMultipleMessages_ReturnsInOrder()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        transport.EnqueueMessage("""{"order":1}""");
        transport.EnqueueMessage("""{"order":2}""");
        transport.EnqueueMessage("""{"order":3}""");
        transport.CompleteMessages();

        // Act
        var orders = new List<int>();
        await foreach (var doc in transport.ReadMessagesAsync())
        {
            orders.Add(doc.RootElement.GetProperty("order").GetInt32());
        }

        // Assert
        Assert.Equal([1, 2, 3], orders);

        await transport.DisposeAsync();
    }

    #endregion

    #region Write Tests

    [Fact]
    public async Task MockTransport_WriteAsync_TracksMessages()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        var message = new { Type = "test", Value = 42 };

        // Act
        await transport.WriteAsync(message);

        // Assert
        Assert.Single(transport.WrittenMessages);

        var written = transport.WrittenMessages[0];
        Assert.Equal("test", written.GetProperty("type").GetString());
        Assert.Equal(42, written.GetProperty("value").GetInt32());

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_ClearWrittenMessages_ClearsHistory()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        await transport.WriteAsync(new { Type = "test1" });
        await transport.WriteAsync(new { Type = "test2" });
        Assert.Equal(2, transport.WrittenMessages.Count);

        // Act
        transport.ClearWrittenMessages();

        // Assert
        Assert.Empty(transport.WrittenMessages);

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_GetLastWrittenMessage_ReturnsLast()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        await transport.WriteAsync(new { Type = "first" });
        await transport.WriteAsync(new { Type = "second" });
        await transport.WriteAsync(new { Type = "third" });

        // Act
        var last = transport.GetLastWrittenMessage();

        // Assert
        Assert.NotNull(last);
        Assert.Equal("third", last.Value.GetProperty("type").GetString());

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_GetLastWrittenMessage_WhenEmpty_ReturnsNull()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        var last = transport.GetLastWrittenMessage();

        // Assert
        Assert.Null(last);

        await transport.DisposeAsync();
    }

    #endregion

    #region EndInput and Close Tests

    [Fact]
    public async Task MockTransport_EndInputAsync_SetsInputEnded()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        await transport.EndInputAsync();

        // Assert
        Assert.True(transport.InputEnded);

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_CloseAsync_SetsClosed()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        await transport.CloseAsync();

        // Assert
        Assert.True(transport.Closed);

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_WaitForInputEndedAsync_CompletesWhenEnded()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        // Act
        var waitTask = transport.WaitForInputEndedAsync();
        Assert.False(waitTask.IsCompleted);

        await transport.EndInputAsync();

        // Assert
        await waitTask; // Should complete without timing out
        Assert.True(transport.InputEnded);

        await transport.DisposeAsync();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task MockTransport_WriteBeforeConnect_ThrowsException()
    {
        // Arrange
        var transport = new MockTransport();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.WriteAsync(new { Type = "test" }));

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_ReadBeforeConnect_ThrowsException()
    {
        // Arrange
        var transport = new MockTransport();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
            }
        });

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task MockTransport_CompleteMessagesWithError_PropagatesException()
    {
        // Arrange
        var transport = new MockTransport();
        await transport.ConnectAsync();

        transport.EnqueueMessage("""{"type":"first"}""");
        transport.CompleteMessagesWithError(new InvalidOperationException("Test error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
            }
        });

        Assert.Equal("Test error", exception.Message);

        await transport.DisposeAsync();
    }

    #endregion

    #region StreamingMockTransport Tests

    [Fact]
    public async Task StreamingMockTransport_WriteAsync_HasDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        var transport = new StreamingMockTransport(messageDelay: delay);
        await transport.ConnectAsync();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await transport.WriteAsync(new { Type = "test" });
        stopwatch.Stop();

        // Assert - should take at least the delay time
        Assert.True(stopwatch.ElapsedMilliseconds >= 80); // Allow some tolerance

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task StreamingMockTransport_EnqueueMessagesWithDelays_HasDelaysBetween()
    {
        // Arrange
        var transport = new StreamingMockTransport();
        await transport.ConnectAsync();

        var messages = new[] { """{"order":1}""", """{"order":2}""", """{"order":3}""" };
        var delay = TimeSpan.FromMilliseconds(50);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await transport.EnqueueMessagesWithDelaysAsync(messages, delay);
        stopwatch.Stop();

        // Assert - should take at least 2 delays (between 3 messages)
        Assert.True(stopwatch.ElapsedMilliseconds >= 80); // 2 * 50ms with tolerance

        await transport.DisposeAsync();
    }

    #endregion
}
