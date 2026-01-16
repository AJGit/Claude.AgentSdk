using Claude.AgentSdk.Exceptions;
using Xunit;

namespace Claude.AgentSdk.Tests.Exceptions;

/// <summary>
/// Comprehensive tests for Claude Agent SDK exception types.
/// </summary>
public class ExceptionTests
{
    #region ClaudeAgentException Tests

    [Fact]
    public void ClaudeAgentException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Test error message";

        var exception = new ClaudeAgentException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ClaudeAgentException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Outer error message";
        var innerException = new InvalidOperationException("Inner error");

        var exception = new ClaudeAgentException(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void ClaudeAgentException_InheritsFromException()
    {
        var exception = new ClaudeAgentException("Test message");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple error")]
    [InlineData("Error with special characters: !@#$%^&*()")]
    [InlineData("Multi\nline\nerror\nmessage")]
    public void ClaudeAgentException_VariousMessages_PreservesMessage(string message)
    {
        var exception = new ClaudeAgentException(message);

        Assert.Equal(message, exception.Message);
    }

    #endregion

    #region TransportException Tests

    [Fact]
    public void TransportException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Transport layer failed";

        var exception = new TransportException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void TransportException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Transport error occurred";
        var innerException = new IOException("Network error");

        var exception = new TransportException(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TransportException_InheritsFromClaudeAgentException()
    {
        var exception = new TransportException("Test message");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void TransportException_InheritsFromException()
    {
        var exception = new TransportException("Test message");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Connection refused")]
    [InlineData("Timeout waiting for response")]
    [InlineData("Stream closed unexpectedly")]
    public void TransportException_TransportRelatedMessages_PreservesMessage(string message)
    {
        var exception = new TransportException(message);

        Assert.Equal(message, exception.Message);
    }

    #endregion

    #region CliNotFoundException Tests

    [Fact]
    public void CliNotFoundException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Claude CLI not found at /usr/bin/claude";

        var exception = new CliNotFoundException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromTransportException()
    {
        var exception = new CliNotFoundException("CLI not found");

        Assert.IsAssignableFrom<TransportException>(exception);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromClaudeAgentException()
    {
        var exception = new CliNotFoundException("CLI not found");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromException()
    {
        var exception = new CliNotFoundException("CLI not found");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Could not find 'claude' executable")]
    [InlineData("Claude CLI not found in PATH")]
    [InlineData("Executable 'claude.exe' does not exist")]
    public void CliNotFoundException_VariousMessages_PreservesMessage(string message)
    {
        var exception = new CliNotFoundException(message);

        Assert.Equal(message, exception.Message);
    }

    #endregion

    #region CliProcessException Tests

    [Fact]
    public void CliProcessException_ConstructedWithMessageOnly_HasCorrectMessageAndNullExitCode()
    {
        const string expectedMessage = "CLI process failed";

        var exception = new CliProcessException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_ConstructedWithMessageAndExitCode_HasBoth()
    {
        const string expectedMessage = "CLI process exited with error";
        const int expectedExitCode = 1;

        var exception = new CliProcessException(expectedMessage, expectedExitCode);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedExitCode, exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_WithExitCodeNull_ExitCodeIsNull()
    {
        var exception = new CliProcessException("Process failed", null);

        Assert.Null(exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_InheritsFromTransportException()
    {
        var exception = new CliProcessException("Process failed");

        Assert.IsAssignableFrom<TransportException>(exception);
    }

    [Fact]
    public void CliProcessException_InheritsFromClaudeAgentException()
    {
        var exception = new CliProcessException("Process failed");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void CliProcessException_InheritsFromException()
    {
        var exception = new CliProcessException("Process failed");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(127)]
    [InlineData(255)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void CliProcessException_VariousExitCodes_PreservesExitCode(int exitCode)
    {
        var exception = new CliProcessException("Process exited", exitCode);

        Assert.Equal(exitCode, exception.ExitCode);
    }

    [Theory]
    [InlineData("Process terminated unexpectedly", 1)]
    [InlineData("Process killed by signal", 137)]
    [InlineData("Process exited with success but no output", 0)]
    public void CliProcessException_VariousMessagesAndExitCodes_PreservesBoth(string message, int exitCode)
    {
        var exception = new CliProcessException(message, exitCode);

        Assert.Equal(message, exception.Message);
        Assert.Equal(exitCode, exception.ExitCode);
    }

    #endregion

    #region ProtocolException Tests

    [Fact]
    public void ProtocolException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Protocol error occurred";

        var exception = new ProtocolException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ProtocolException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Protocol handshake failed";
        var innerException = new FormatException("Invalid format");

        var exception = new ProtocolException(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void ProtocolException_InheritsFromClaudeAgentException()
    {
        var exception = new ProtocolException("Protocol error");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void ProtocolException_InheritsFromException()
    {
        var exception = new ProtocolException("Protocol error");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Invalid JSON-RPC request")]
    [InlineData("Unexpected message type")]
    [InlineData("Protocol version mismatch")]
    public void ProtocolException_ProtocolRelatedMessages_PreservesMessage(string message)
    {
        var exception = new ProtocolException(message);

        Assert.Equal(message, exception.Message);
    }

    #endregion

    #region ControlTimeoutException Tests

    [Fact]
    public void ControlTimeoutException_ConstructedWithRequestIdAndMessage_HasBoth()
    {
        const string expectedRequestId = "req-12345";
        const string expectedMessage = "Request timed out after 30 seconds";

        var exception = new ControlTimeoutException(expectedRequestId, expectedMessage);

        Assert.Equal(expectedRequestId, exception.RequestId);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromProtocolException()
    {
        var exception = new ControlTimeoutException("req-1", "Timeout");

        Assert.IsAssignableFrom<ProtocolException>(exception);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromClaudeAgentException()
    {
        var exception = new ControlTimeoutException("req-1", "Timeout");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromException()
    {
        var exception = new ControlTimeoutException("req-1", "Timeout");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("request-001", "Timeout waiting for tool response")]
    [InlineData("req-abc-123", "Control request timed out")]
    [InlineData("", "Timeout with empty request ID")]
    [InlineData("00000000-0000-0000-0000-000000000000", "UUID-style request ID")]
    public void ControlTimeoutException_VariousRequestIds_PreservesRequestId(string requestId, string message)
    {
        var exception = new ControlTimeoutException(requestId, message);

        Assert.Equal(requestId, exception.RequestId);
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void ControlTimeoutException_RequestIdProperty_IsReadOnly()
    {
        var exception = new ControlTimeoutException("req-1", "Timeout");
        var property = typeof(ControlTimeoutException).GetProperty("RequestId");

        Assert.NotNull(property);
        Assert.False(property!.CanWrite, "RequestId should be read-only");
    }

    #endregion

    #region MessageParseException Tests

    [Fact]
    public void MessageParseException_ConstructedWithMessageOnly_HasCorrectMessageAndNullRawMessage()
    {
        const string expectedMessage = "Failed to parse message";

        var exception = new MessageParseException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.RawMessage);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndRawMessage_HasBoth()
    {
        const string expectedMessage = "Invalid JSON structure";
        const string expectedRawMessage = "{invalid json}";

        var exception = new MessageParseException(expectedMessage, expectedRawMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedRawMessage, exception.RawMessage);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndInnerExceptionAndRawMessage_HasAll()
    {
        const string expectedMessage = "JSON parsing failed";
        var innerException = new JsonException("Unexpected token");
        const string expectedRawMessage = "{\"broken\": }";

        var exception = new MessageParseException(expectedMessage, innerException, expectedRawMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(expectedRawMessage, exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndInnerExceptionAndNullRawMessage_HasCorrectValues()
    {
        const string expectedMessage = "Parse error";
        var innerException = new FormatException("Bad format");

        var exception = new MessageParseException(expectedMessage, innerException, null);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Null(exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_InheritsFromClaudeAgentException()
    {
        var exception = new MessageParseException("Parse error");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void MessageParseException_InheritsFromException()
    {
        var exception = new MessageParseException("Parse error");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Missing required field", null)]
    [InlineData("Invalid JSON", "not json at all")]
    [InlineData("Unexpected end of stream", "")]
    [InlineData("Schema validation failed", "{\"type\":\"unknown\"}")]
    public void MessageParseException_VariousMessagesAndRawMessages_PreservesBoth(string message, string? rawMessage)
    {
        var exception = new MessageParseException(message, rawMessage);

        Assert.Equal(message, exception.Message);
        Assert.Equal(rawMessage, exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_RawMessageProperty_IsReadOnly()
    {
        var exception = new MessageParseException("Parse error");
        var property = typeof(MessageParseException).GetProperty("RawMessage");

        Assert.NotNull(property);
        Assert.False(property!.CanWrite, "RawMessage should be read-only");
    }

    [Fact]
    public void MessageParseException_WithLongRawMessage_PreservesEntireMessage()
    {
        var longRawMessage = new string('x', 10000);
        var exception = new MessageParseException("Parse error", longRawMessage);

        Assert.Equal(10000, exception.RawMessage!.Length);
        Assert.Equal(longRawMessage, exception.RawMessage);
    }

    #endregion

    #region Exception Hierarchy Tests

    [Fact]
    public void ExceptionHierarchy_TransportException_IsClaudeAgentException()
    {
        Assert.True(typeof(ClaudeAgentException).IsAssignableFrom(typeof(TransportException)));
    }

    [Fact]
    public void ExceptionHierarchy_CliNotFoundException_IsTransportException()
    {
        Assert.True(typeof(TransportException).IsAssignableFrom(typeof(CliNotFoundException)));
    }

    [Fact]
    public void ExceptionHierarchy_CliProcessException_IsTransportException()
    {
        Assert.True(typeof(TransportException).IsAssignableFrom(typeof(CliProcessException)));
    }

    [Fact]
    public void ExceptionHierarchy_ProtocolException_IsClaudeAgentException()
    {
        Assert.True(typeof(ClaudeAgentException).IsAssignableFrom(typeof(ProtocolException)));
    }

    [Fact]
    public void ExceptionHierarchy_ControlTimeoutException_IsProtocolException()
    {
        Assert.True(typeof(ProtocolException).IsAssignableFrom(typeof(ControlTimeoutException)));
    }

    [Fact]
    public void ExceptionHierarchy_MessageParseException_IsClaudeAgentException()
    {
        Assert.True(typeof(ClaudeAgentException).IsAssignableFrom(typeof(MessageParseException)));
    }

    [Fact]
    public void ExceptionHierarchy_AllCustomExceptions_InheritFromClaudeAgentException()
    {
        var customExceptionTypes = new[]
        {
            typeof(TransportException),
            typeof(CliNotFoundException),
            typeof(CliProcessException),
            typeof(ProtocolException),
            typeof(ControlTimeoutException),
            typeof(MessageParseException)
        };

        foreach (var exceptionType in customExceptionTypes)
        {
            Assert.True(
                typeof(ClaudeAgentException).IsAssignableFrom(exceptionType),
                $"{exceptionType.Name} should inherit from ClaudeAgentException");
        }
    }

    #endregion

    #region Exception Catching Tests

    [Fact]
    public void CatchingClaudeAgentException_CatchesTransportException()
    {
        ClaudeAgentException? caught = null;

        try
        {
            throw new TransportException("Transport error");
        }
        catch (ClaudeAgentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<TransportException>(caught);
    }

    [Fact]
    public void CatchingTransportException_CatchesCliNotFoundException()
    {
        TransportException? caught = null;

        try
        {
            throw new CliNotFoundException("CLI not found");
        }
        catch (TransportException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<CliNotFoundException>(caught);
    }

    [Fact]
    public void CatchingTransportException_CatchesCliProcessException()
    {
        TransportException? caught = null;

        try
        {
            throw new CliProcessException("Process failed", 1);
        }
        catch (TransportException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<CliProcessException>(caught);
    }

    [Fact]
    public void CatchingProtocolException_CatchesControlTimeoutException()
    {
        ProtocolException? caught = null;

        try
        {
            throw new ControlTimeoutException("req-1", "Timeout");
        }
        catch (ProtocolException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<ControlTimeoutException>(caught);
    }

    [Fact]
    public void CatchingClaudeAgentException_CatchesAllCustomExceptions()
    {
        var exceptions = new Exception[]
        {
            new ClaudeAgentException("Base exception"),
            new TransportException("Transport error"),
            new CliNotFoundException("CLI not found"),
            new CliProcessException("Process failed", 1),
            new ProtocolException("Protocol error"),
            new ControlTimeoutException("req-1", "Timeout"),
            new MessageParseException("Parse error")
        };

        foreach (var exception in exceptions)
        {
            ClaudeAgentException? caught = null;

            try
            {
                throw exception;
            }
            catch (ClaudeAgentException ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Equal(exception.GetType(), caught!.GetType());
        }
    }

    #endregion

    #region Exception ToString Tests

    [Fact]
    public void ClaudeAgentException_ToString_ContainsMessage()
    {
        const string message = "Test exception message";
        var exception = new ClaudeAgentException(message);

        var result = exception.ToString();

        Assert.Contains(message, result);
        Assert.Contains("ClaudeAgentException", result);
    }

    [Fact]
    public void TransportException_ToString_ContainsTypeAndMessage()
    {
        const string message = "Transport failed";
        var exception = new TransportException(message);

        var result = exception.ToString();

        Assert.Contains(message, result);
        Assert.Contains("TransportException", result);
    }

    [Fact]
    public void ExceptionWithInnerException_ToString_ContainsInnerExceptionInfo()
    {
        var innerException = new InvalidOperationException("Inner error");
        var exception = new ClaudeAgentException("Outer error", innerException);

        var result = exception.ToString();

        Assert.Contains("Outer error", result);
        Assert.Contains("Inner error", result);
        Assert.Contains("InvalidOperationException", result);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ClaudeAgentException_WithNullInnerException_InnerExceptionIsNull()
    {
        var exception = new ClaudeAgentException("Test", null!);

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void CliProcessException_WithZeroExitCode_ExitCodeIsZero()
    {
        var exception = new CliProcessException("Success but error", 0);

        Assert.Equal(0, exception.ExitCode);
    }

    [Fact]
    public void ControlTimeoutException_WithEmptyRequestId_RequestIdIsEmpty()
    {
        var exception = new ControlTimeoutException(string.Empty, "Timeout");

        Assert.Equal(string.Empty, exception.RequestId);
    }

    [Fact]
    public void MessageParseException_WithEmptyRawMessage_RawMessageIsEmpty()
    {
        var exception = new MessageParseException("Parse error", string.Empty);

        Assert.Equal(string.Empty, exception.RawMessage);
    }

    [Fact]
    public void NestedInnerExceptions_PreservesChain()
    {
        var innermost = new IOException("IO error");
        var middle = new TransportException("Transport error", innermost);
        var outer = new ClaudeAgentException("Agent error", middle);

        Assert.Same(middle, outer.InnerException);
        Assert.Same(innermost, outer.InnerException!.InnerException);
    }

    #endregion
}

// Helper class for testing - System.Text.Json exception
#pragma warning disable CA2201 // Exception type System.Exception is not sufficiently specific - intentional test mock
file sealed class JsonException : Exception
{
    public JsonException(string message) : base(message) { }
}
