using System.Reflection;
using Claude.AgentSdk.Exceptions;

namespace Claude.AgentSdk.Tests.Exceptions;

/// <summary>
///     Comprehensive tests for Claude Agent SDK exception types.
/// </summary>
public class ExceptionTests
{
    [Fact]
    public void ClaudeAgentException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Test error message";

        ClaudeAgentException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ClaudeAgentException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Outer error message";
        InvalidOperationException innerException = new("Inner error");

        ClaudeAgentException exception = new(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void ClaudeAgentException_InheritsFromException()
    {
        ClaudeAgentException exception = new("Test message");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple error")]
    [InlineData("Error with special characters: !@#$%^&*()")]
    [InlineData("Multi\nline\nerror\nmessage")]
    public void ClaudeAgentException_VariousMessages_PreservesMessage(string message)
    {
        ClaudeAgentException exception = new(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void TransportException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Transport layer failed";

        TransportException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void TransportException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Transport error occurred";
        IOException innerException = new("Network error");

        TransportException exception = new(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TransportException_InheritsFromClaudeAgentException()
    {
        TransportException exception = new("Test message");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void TransportException_InheritsFromException()
    {
        TransportException exception = new("Test message");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Connection refused")]
    [InlineData("Timeout waiting for response")]
    [InlineData("Stream closed unexpectedly")]
    public void TransportException_TransportRelatedMessages_PreservesMessage(string message)
    {
        TransportException exception = new(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void CliNotFoundException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Claude CLI not found at /usr/bin/claude";

        CliNotFoundException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromTransportException()
    {
        CliNotFoundException exception = new("CLI not found");

        Assert.IsAssignableFrom<TransportException>(exception);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromClaudeAgentException()
    {
        CliNotFoundException exception = new("CLI not found");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void CliNotFoundException_InheritsFromException()
    {
        CliNotFoundException exception = new("CLI not found");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Could not find 'claude' executable")]
    [InlineData("Claude CLI not found in PATH")]
    [InlineData("Executable 'claude.exe' does not exist")]
    public void CliNotFoundException_VariousMessages_PreservesMessage(string message)
    {
        CliNotFoundException exception = new(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void CliProcessException_ConstructedWithMessageOnly_HasCorrectMessageAndNullExitCode()
    {
        const string expectedMessage = "CLI process failed";

        CliProcessException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_ConstructedWithMessageAndExitCode_HasBoth()
    {
        const string expectedMessage = "CLI process exited with error";
        const int expectedExitCode = 1;

        CliProcessException exception = new(expectedMessage, expectedExitCode);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedExitCode, exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_WithExitCodeNull_ExitCodeIsNull()
    {
        CliProcessException exception = new("Process failed");

        Assert.Null(exception.ExitCode);
    }

    [Fact]
    public void CliProcessException_InheritsFromTransportException()
    {
        CliProcessException exception = new("Process failed");

        Assert.IsAssignableFrom<TransportException>(exception);
    }

    [Fact]
    public void CliProcessException_InheritsFromClaudeAgentException()
    {
        CliProcessException exception = new("Process failed");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void CliProcessException_InheritsFromException()
    {
        CliProcessException exception = new("Process failed");

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
        CliProcessException exception = new("Process exited", exitCode);

        Assert.Equal(exitCode, exception.ExitCode);
    }

    [Theory]
    [InlineData("Process terminated unexpectedly", 1)]
    [InlineData("Process killed by signal", 137)]
    [InlineData("Process exited with success but no output", 0)]
    public void CliProcessException_VariousMessagesAndExitCodes_PreservesBoth(string message, int exitCode)
    {
        CliProcessException exception = new(message, exitCode);

        Assert.Equal(message, exception.Message);
        Assert.Equal(exitCode, exception.ExitCode);
    }

    [Fact]
    public void ProtocolException_ConstructedWithMessage_HasCorrectMessage()
    {
        const string expectedMessage = "Protocol error occurred";

        ProtocolException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ProtocolException_ConstructedWithMessageAndInnerException_HasBoth()
    {
        const string expectedMessage = "Protocol handshake failed";
        FormatException innerException = new("Invalid format");

        ProtocolException exception = new(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void ProtocolException_InheritsFromClaudeAgentException()
    {
        ProtocolException exception = new("Protocol error");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void ProtocolException_InheritsFromException()
    {
        ProtocolException exception = new("Protocol error");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Invalid JSON-RPC request")]
    [InlineData("Unexpected message type")]
    [InlineData("Protocol version mismatch")]
    public void ProtocolException_ProtocolRelatedMessages_PreservesMessage(string message)
    {
        ProtocolException exception = new(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void ControlTimeoutException_ConstructedWithRequestIdAndMessage_HasBoth()
    {
        const string expectedRequestId = "req-12345";
        const string expectedMessage = "Request timed out after 30 seconds";

        ControlTimeoutException exception = new(expectedRequestId, expectedMessage);

        Assert.Equal(expectedRequestId, exception.RequestId);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromProtocolException()
    {
        ControlTimeoutException exception = new("req-1", "Timeout");

        Assert.IsAssignableFrom<ProtocolException>(exception);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromClaudeAgentException()
    {
        ControlTimeoutException exception = new("req-1", "Timeout");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void ControlTimeoutException_InheritsFromException()
    {
        ControlTimeoutException exception = new("req-1", "Timeout");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("request-001", "Timeout waiting for tool response")]
    [InlineData("req-abc-123", "Control request timed out")]
    [InlineData("", "Timeout with empty request ID")]
    [InlineData("00000000-0000-0000-0000-000000000000", "UUID-style request ID")]
    public void ControlTimeoutException_VariousRequestIds_PreservesRequestId(string requestId, string message)
    {
        ControlTimeoutException exception = new(requestId, message);

        Assert.Equal(requestId, exception.RequestId);
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void ControlTimeoutException_RequestIdProperty_IsReadOnly()
    {
        ControlTimeoutException exception = new("req-1", "Timeout");
        PropertyInfo? property = typeof(ControlTimeoutException).GetProperty("RequestId");

        Assert.NotNull(property);
        Assert.False(property!.CanWrite, "RequestId should be read-only");
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageOnly_HasCorrectMessageAndNullRawMessage()
    {
        const string expectedMessage = "Failed to parse message";

        MessageParseException exception = new(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.RawMessage);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndRawMessage_HasBoth()
    {
        const string expectedMessage = "Invalid JSON structure";
        const string expectedRawMessage = "{invalid json}";

        MessageParseException exception = new(expectedMessage, expectedRawMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedRawMessage, exception.RawMessage);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndInnerExceptionAndRawMessage_HasAll()
    {
        const string expectedMessage = "JSON parsing failed";
        JsonException innerException = new("Unexpected token");
        const string expectedRawMessage = "{\"broken\": }";

        MessageParseException exception = new(expectedMessage, innerException, expectedRawMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(expectedRawMessage, exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_ConstructedWithMessageAndInnerExceptionAndNullRawMessage_HasCorrectValues()
    {
        const string expectedMessage = "Parse error";
        FormatException innerException = new("Bad format");

        MessageParseException exception = new(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Null(exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_InheritsFromClaudeAgentException()
    {
        MessageParseException exception = new("Parse error");

        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public void MessageParseException_InheritsFromException()
    {
        MessageParseException exception = new("Parse error");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Missing required field", null)]
    [InlineData("Invalid JSON", "not json at all")]
    [InlineData("Unexpected end of stream", "")]
    [InlineData("Schema validation failed", "{\"type\":\"unknown\"}")]
    public void MessageParseException_VariousMessagesAndRawMessages_PreservesBoth(string message, string? rawMessage)
    {
        MessageParseException exception = new(message, rawMessage);

        Assert.Equal(message, exception.Message);
        Assert.Equal(rawMessage, exception.RawMessage);
    }

    [Fact]
    public void MessageParseException_RawMessageProperty_IsReadOnly()
    {
        MessageParseException exception = new("Parse error");
        PropertyInfo? property = typeof(MessageParseException).GetProperty("RawMessage");

        Assert.NotNull(property);
        Assert.False(property!.CanWrite, "RawMessage should be read-only");
    }

    [Fact]
    public void MessageParseException_WithLongRawMessage_PreservesEntireMessage()
    {
        string longRawMessage = new('x', 10000);
        MessageParseException exception = new("Parse error", longRawMessage);

        Assert.Equal(10000, exception.RawMessage!.Length);
        Assert.Equal(longRawMessage, exception.RawMessage);
    }

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
        Type[] customExceptionTypes = new[]
        {
            typeof(TransportException),
            typeof(CliNotFoundException),
            typeof(CliProcessException),
            typeof(ProtocolException),
            typeof(ControlTimeoutException),
            typeof(MessageParseException)
        };

        foreach (Type exceptionType in customExceptionTypes)
        {
            Assert.True(
                typeof(ClaudeAgentException).IsAssignableFrom(exceptionType),
                $"{exceptionType.Name} should inherit from ClaudeAgentException");
        }
    }

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
        Exception[] exceptions = new Exception[]
        {
            new ClaudeAgentException("Base exception"),
            new TransportException("Transport error"),
            new CliNotFoundException("CLI not found"),
            new CliProcessException("Process failed", 1),
            new ProtocolException("Protocol error"),
            new ControlTimeoutException("req-1", "Timeout"),
            new MessageParseException("Parse error")
        };

        foreach (Exception exception in exceptions)
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

    [Fact]
    public void ClaudeAgentException_ToString_ContainsMessage()
    {
        const string message = "Test exception message";
        ClaudeAgentException exception = new(message);

        string result = exception.ToString();

        Assert.Contains(message, result);
        Assert.Contains("ClaudeAgentException", result);
    }

    [Fact]
    public void TransportException_ToString_ContainsTypeAndMessage()
    {
        const string message = "Transport failed";
        TransportException exception = new(message);

        string result = exception.ToString();

        Assert.Contains(message, result);
        Assert.Contains("TransportException", result);
    }

    [Fact]
    public void ExceptionWithInnerException_ToString_ContainsInnerExceptionInfo()
    {
        InvalidOperationException innerException = new("Inner error");
        ClaudeAgentException exception = new("Outer error", innerException);

        string result = exception.ToString();

        Assert.Contains("Outer error", result);
        Assert.Contains("Inner error", result);
        Assert.Contains("InvalidOperationException", result);
    }

    [Fact]
    public void ClaudeAgentException_WithNullInnerException_InnerExceptionIsNull()
    {
        ClaudeAgentException exception = new("Test", null!);

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void CliProcessException_WithZeroExitCode_ExitCodeIsZero()
    {
        CliProcessException exception = new("Success but error", 0);

        Assert.Equal(0, exception.ExitCode);
    }

    [Fact]
    public void ControlTimeoutException_WithEmptyRequestId_RequestIdIsEmpty()
    {
        ControlTimeoutException exception = new(string.Empty, "Timeout");

        Assert.Equal(string.Empty, exception.RequestId);
    }

    [Fact]
    public void MessageParseException_WithEmptyRawMessage_RawMessageIsEmpty()
    {
        MessageParseException exception = new("Parse error", string.Empty);

        Assert.Equal(string.Empty, exception.RawMessage);
    }

    [Fact]
    public void NestedInnerExceptions_PreservesChain()
    {
        IOException innermost = new("IO error");
        TransportException middle = new("Transport error", innermost);
        ClaudeAgentException outer = new("Agent error", middle);

        Assert.Same(middle, outer.InnerException);
        Assert.Same(innermost, outer.InnerException!.InnerException);
    }
}

// Helper class for testing - System.Text.Json exception
#pragma warning disable CA2201 // Exception type System.Exception is not sufficiently specific - intentional test mock
sealed file class JsonException : Exception
{
    public JsonException(string message) : base(message) { }
}
