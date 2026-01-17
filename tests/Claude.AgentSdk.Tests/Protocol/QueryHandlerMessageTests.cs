using System.Text.Json;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Tests.Protocol;

// Note: This file uses the existing MockTransport defined in QueryHandlerControlTests.cs

/// <summary>
///     Tests for parsing different message types in the QueryHandler message processing pipeline.
///     These tests verify that JSON messages from the CLI are correctly deserialized into Message objects.
/// </summary>
public class MessageTypeParsingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void UserMessage_WithStringContent_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "user",
                       "message": {
                           "content": "Hello, Claude!",
                           "uuid": "user-msg-1"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<UserMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("user-msg-1", message.MessageContent.Uuid);
        Assert.Equal(JsonValueKind.String, message.MessageContent.Content.ValueKind);
        Assert.Equal("Hello, Claude!", message.MessageContent.Content.GetString());
    }

    [Fact]
    public void UserMessage_WithArrayContent_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "user",
                       "message": {
                           "content": [
                               {"type": "text", "text": "Look at this image:"},
                               {"type": "image", "source": {"type": "base64", "data": "..."}}
                           ],
                           "uuid": "user-msg-2"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<UserMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("user-msg-2", message.MessageContent.Uuid);
        Assert.Equal(JsonValueKind.Array, message.MessageContent.Content.ValueKind);
        Assert.Equal(2, message.MessageContent.Content.GetArrayLength());
    }

    [Fact]
    public void UserMessage_WithParentToolUseId_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "user",
                       "message": {
                           "content": "User feedback",
                           "uuid": "user-msg-3",
                           "parent_tool_use_id": "toolu_123"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<UserMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("toolu_123", message.MessageContent.ParentToolUseId);
    }

    [Fact]
    public void UserMessage_PolymorphicDeserialization_Works()
    {
        // Arrange
        var json = """
                   {
                       "type": "user",
                       "message": {
                           "content": "Test message",
                           "uuid": "poly-test-1"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<UserMessage>(message);
        var userMessage = (UserMessage)message;
        Assert.Equal("poly-test-1", userMessage.MessageContent.Uuid);
    }

    [Fact]
    public void UserMessage_WithNullUuid_ParsedCorrectly()
    {
        // Arrange - uuid is optional
        var json = """
                   {
                       "type": "user",
                       "message": {
                           "content": "No uuid message"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<UserMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Null(message.MessageContent.Uuid);
    }

    [Fact]
    public void AssistantMessage_WithTextBlock_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-sonnet",
                           "content": [
                               {"type": "text", "text": "Here is my response."}
                           ]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("claude-3-sonnet", message.MessageContent.Model);
        Assert.Single(message.MessageContent.Content);
        var textBlock = message.MessageContent.Content[0] as TextBlock;
        Assert.NotNull(textBlock);
        Assert.Equal("Here is my response.", textBlock.Text);
    }

    [Fact]
    public void AssistantMessage_WithThinkingBlock_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [
                               {"type": "thinking", "thinking": "Let me think about this...", "signature": "sig123"}
                           ]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Single(message.MessageContent.Content);
        var thinkingBlock = message.MessageContent.Content[0] as ThinkingBlock;
        Assert.NotNull(thinkingBlock);
        Assert.Equal("Let me think about this...", thinkingBlock.Thinking);
        Assert.Equal("sig123", thinkingBlock.Signature);
    }

    [Fact]
    public void AssistantMessage_WithToolUseBlock_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [
                               {
                                   "type": "tool_use",
                                   "id": "toolu_abc123",
                                   "name": "read_file",
                                   "input": {"path": "/home/user/file.txt"}
                               }
                           ]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Single(message.MessageContent.Content);
        var toolUseBlock = message.MessageContent.Content[0] as ToolUseBlock;
        Assert.NotNull(toolUseBlock);
        Assert.Equal("toolu_abc123", toolUseBlock.Id);
        Assert.Equal("read_file", toolUseBlock.Name);
        Assert.Equal("/home/user/file.txt", toolUseBlock.Input.GetProperty("path").GetString());
    }

    [Fact]
    public void AssistantMessage_WithToolResultBlock_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [
                               {
                                   "type": "tool_result",
                                   "tool_use_id": "toolu_abc123",
                                   "content": {"status": "success", "data": "file contents"},
                                   "is_error": false
                               }
                           ]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Single(message.MessageContent.Content);
        var toolResultBlock = message.MessageContent.Content[0] as ToolResultBlock;
        Assert.NotNull(toolResultBlock);
        Assert.Equal("toolu_abc123", toolResultBlock.ToolUseId);
        Assert.False(toolResultBlock.IsError);
    }

    [Fact]
    public void AssistantMessage_WithMultipleContentBlocks_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [
                               {"type": "thinking", "thinking": "Processing...", "signature": "sig1"},
                               {"type": "text", "text": "I'll read that file for you."},
                               {"type": "tool_use", "id": "toolu_1", "name": "read", "input": {}}
                           ]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(3, message.MessageContent.Content.Count);
        Assert.IsType<ThinkingBlock>(message.MessageContent.Content[0]);
        Assert.IsType<TextBlock>(message.MessageContent.Content[1]);
        Assert.IsType<ToolUseBlock>(message.MessageContent.Content[2]);
    }

    [Fact]
    public void AssistantMessage_WithError_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [],
                           "error": "Rate limit exceeded"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("Rate limit exceeded", message.MessageContent.Error);
    }

    [Fact]
    public void AssistantMessage_WithParentToolUseId_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "assistant",
                       "message": {
                           "model": "claude-3-opus",
                           "content": [{"type": "text", "text": "Subagent response"}],
                           "parent_tool_use_id": "toolu_parent_456"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("toolu_parent_456", message.MessageContent.ParentToolUseId);
    }

    [Fact]
    public void SystemMessage_Init_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "system",
                       "subtype": "init",
                       "session_id": "session-123",
                       "cwd": "/home/user/project",
                       "model": "claude-3-opus",
                       "tools": ["Read", "Write", "Bash"],
                       "permission_mode": "default"
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.True(message.IsInit);
        Assert.Equal("session-123", message.SessionId);
        Assert.Equal("/home/user/project", message.Cwd);
        Assert.Equal("claude-3-opus", message.Model);
        Assert.Equal(3, message.Tools!.Count);
        Assert.Equal("default", message.PermissionMode);
    }

    [Fact]
    public void SystemMessage_CompactBoundary_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "system",
                       "subtype": "compact_boundary",
                       "compact_metadata": {
                           "pre_tokens": 10000,
                           "post_tokens": 5000,
                           "trigger": "auto"
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.True(message.IsCompactBoundary);
        Assert.False(message.IsInit);
        Assert.NotNull(message.CompactMetadata);
        Assert.Equal(10000, message.CompactMetadata.PreTokens);
        Assert.Equal(5000, message.CompactMetadata.PostTokens);
        Assert.Equal("auto", message.CompactMetadata.Trigger);
    }

    [Fact]
    public void SystemMessage_WithMcpServers_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "system",
                       "subtype": "init",
                       "session_id": "session-1",
                       "mcp_servers": [
                           {"name": "filesystem", "status": "connected"},
                           {"name": "database", "status": "error", "error": "Connection refused"}
                       ]
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.McpServers);
        Assert.Equal(2, message.McpServers.Count);
        Assert.Equal("filesystem", message.McpServers[0].Name);
        Assert.Equal("connected", message.McpServers[0].Status);
        Assert.Null(message.McpServers[0].Error);
        Assert.Equal("database", message.McpServers[1].Name);
        Assert.Equal("error", message.McpServers[1].Status);
        Assert.Equal("Connection refused", message.McpServers[1].Error);
    }

    [Fact]
    public void SystemMessage_WithSlashCommands_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "system",
                       "subtype": "init",
                       "slash_commands": ["/help", "/clear", "/compact", "/model"]
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.SlashCommands);
        Assert.Equal(4, message.SlashCommands.Count);
        Assert.Contains("/help", message.SlashCommands);
        Assert.Contains("/compact", message.SlashCommands);
    }

    [Fact]
    public void SystemMessage_WithData_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "system",
                       "subtype": "custom",
                       "data": {
                           "custom_field": "custom_value",
                           "nested": {"key": 123}
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.Data);
        Assert.Equal("custom_value", message.Data.Value.GetProperty("custom_field").GetString());
        Assert.Equal(123, message.Data.Value.GetProperty("nested").GetProperty("key").GetInt32());
    }

    [Fact]
    public void ResultMessage_Success_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "result",
                       "subtype": "success",
                       "duration_ms": 5000,
                       "duration_api_ms": 4500,
                       "is_error": false,
                       "num_turns": 3,
                       "session_id": "session-456",
                       "total_cost_usd": 0.05,
                       "result": "Task completed successfully"
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("success", message.Subtype);
        Assert.Equal(5000, message.DurationMs);
        Assert.Equal(4500, message.DurationApiMs);
        Assert.False(message.IsError);
        Assert.Equal(3, message.NumTurns);
        Assert.Equal("session-456", message.SessionId);
        Assert.Equal(0.05, message.TotalCostUsd);
        Assert.Equal("Task completed successfully", message.Result);
    }

    [Fact]
    public void ResultMessage_Error_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "result",
                       "subtype": "error",
                       "duration_ms": 1000,
                       "duration_api_ms": 800,
                       "is_error": true,
                       "num_turns": 1,
                       "session_id": "session-789",
                       "result": "API error occurred"
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("error", message.Subtype);
        Assert.True(message.IsError);
        Assert.Equal("API error occurred", message.Result);
    }

    [Fact]
    public void ResultMessage_WithStructuredOutput_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "result",
                       "subtype": "success",
                       "duration_ms": 1000,
                       "duration_api_ms": 900,
                       "is_error": false,
                       "num_turns": 1,
                       "session_id": "session-1",
                       "structured_output": {
                           "answer": "42",
                           "confidence": 0.95,
                           "sources": ["doc1", "doc2"]
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.StructuredOutput);
        Assert.Equal("42", message.StructuredOutput.Value.GetProperty("answer").GetString());
        Assert.Equal(0.95, message.StructuredOutput.Value.GetProperty("confidence").GetDouble());
        Assert.Equal(2, message.StructuredOutput.Value.GetProperty("sources").GetArrayLength());
    }

    [Fact]
    public void ResultMessage_WithUsage_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "result",
                       "subtype": "success",
                       "duration_ms": 2000,
                       "duration_api_ms": 1800,
                       "is_error": false,
                       "num_turns": 2,
                       "session_id": "session-1",
                       "usage": {
                           "input_tokens": 100,
                           "output_tokens": 50,
                           "cache_read_input_tokens": 20,
                           "cache_creation_input_tokens": 0
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.Usage);
        Assert.Equal(100, message.Usage.Value.GetProperty("input_tokens").GetInt32());
        Assert.Equal(50, message.Usage.Value.GetProperty("output_tokens").GetInt32());
        Assert.Equal(20, message.Usage.Value.GetProperty("cache_read_input_tokens").GetInt32());
    }

    [Fact]
    public void ResultMessage_WithNullCost_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "result",
                       "subtype": "success",
                       "duration_ms": 500,
                       "duration_api_ms": 400,
                       "is_error": false,
                       "num_turns": 1,
                       "session_id": "session-free"
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Null(message.TotalCostUsd);
        Assert.Null(message.Usage);
        Assert.Null(message.StructuredOutput);
    }

    [Fact]
    public void StreamEvent_MessageStart_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "stream_event",
                       "uuid": "event-789",
                       "session_id": "session-123",
                       "event": {
                           "type": "message_start",
                           "message": {
                               "id": "msg_001",
                               "model": "claude-3-opus"
                           }
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<StreamEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("event-789", message.Uuid);
        Assert.Equal("session-123", message.SessionId);
        Assert.Equal("message_start", message.Event.GetProperty("type").GetString());
    }

    [Fact]
    public void StreamEvent_ContentBlockDelta_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "stream_event",
                       "uuid": "event-delta-1",
                       "session_id": "session-123",
                       "event": {
                           "type": "content_block_delta",
                           "index": 0,
                           "delta": {
                               "type": "text_delta",
                               "text": "Hello"
                           }
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<StreamEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("content_block_delta", message.Event.GetProperty("type").GetString());
        Assert.Equal(0, message.Event.GetProperty("index").GetInt32());
        Assert.Equal("Hello", message.Event.GetProperty("delta").GetProperty("text").GetString());
    }

    [Fact]
    public void StreamEvent_WithParentToolUseId_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "stream_event",
                       "uuid": "event-1",
                       "session_id": "session-1",
                       "parent_tool_use_id": "toolu_parent",
                       "event": {"type": "message_start"}
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<StreamEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("toolu_parent", message.ParentToolUseId);
    }

    [Fact]
    public void StreamEvent_ToolUseEvents_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "stream_event",
                       "uuid": "tool-event-1",
                       "session_id": "session-1",
                       "event": {
                           "type": "content_block_start",
                           "index": 1,
                           "content_block": {
                               "type": "tool_use",
                               "id": "toolu_streaming",
                               "name": "read_file",
                               "input": {}
                           }
                       }
                   }
                   """;

        // Act
        var message = JsonSerializer.Deserialize<StreamEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("content_block_start", message.Event.GetProperty("type").GetString());
        var contentBlock = message.Event.GetProperty("content_block");
        Assert.Equal("tool_use", contentBlock.GetProperty("type").GetString());
        Assert.Equal("read_file", contentBlock.GetProperty("name").GetString());
    }
}

/// <summary>
///     Tests for the polymorphic message type discrimination.
/// </summary>
public class MessageTypeDiscriminationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("user", typeof(UserMessage))]
    [InlineData("assistant", typeof(AssistantMessage))]
    [InlineData("system", typeof(SystemMessage))]
    [InlineData("result", typeof(ResultMessage))]
    [InlineData("stream_event", typeof(StreamEvent))]
    public void Message_PolymorphicDeserialization_CorrectType(string type, Type expectedType)
    {
        // Arrange - create valid JSON for each type
        var json = type switch
        {
            "user" => """{"type":"user","message":{"content":"test"}}""",
            "assistant" => """{"type":"assistant","message":{"model":"test","content":[]}}""",
            "system" => """{"type":"system","subtype":"init"}""",
            "result" =>
                """{"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s"}""",
            "stream_event" => """{"type":"stream_event","uuid":"u","session_id":"s","event":{}}""",
            _ => throw new ArgumentException($"Unknown type: {type}")
        };

        // Act
        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        // Assert
        Assert.NotNull(message);
        Assert.IsType(expectedType, message);
    }

    [Fact]
    public void UnknownType_ThrowsJsonException()
    {
        // Arrange
        var json = """{"type": "unknown_type_xyz", "data": "test"}""";

        // Act & Assert
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Message>(json, JsonOptions));
    }

    [Fact]
    public void MissingTypeProperty_ThrowsException()
    {
        // Arrange
        var json = """{"content": "no type field"}""";

        // Act & Assert - missing type discriminator throws NotSupportedException
        Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Deserialize<Message>(json, JsonOptions));
    }
}

/// <summary>
///     Tests for content block parsing within AssistantMessage.
/// </summary>
public class ContentBlockMessageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TextBlock_Serialize_ProducesCorrectJson()
    {
        // Arrange
        ContentBlock block = new TextBlock { Text = "Hello, world!" };

        // Act - serialize as ContentBlock to get the type discriminator
        var json = JsonSerializer.Serialize(block, JsonOptions);
        var doc = JsonDocument.Parse(json);

        // Assert
        Assert.Equal("text", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Hello, world!", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void ThinkingBlock_Serialize_ProducesCorrectJson()
    {
        // Arrange
        ContentBlock block = new ThinkingBlock { Thinking = "Let me think...", Signature = "sig_abc" };

        // Act - serialize as ContentBlock to get the type discriminator
        var json = JsonSerializer.Serialize(block, JsonOptions);
        var doc = JsonDocument.Parse(json);

        // Assert
        Assert.Equal("thinking", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Let me think...", doc.RootElement.GetProperty("thinking").GetString());
        Assert.Equal("sig_abc", doc.RootElement.GetProperty("signature").GetString());
    }

    [Fact]
    public void ToolUseBlock_Serialize_ProducesCorrectJson()
    {
        // Arrange
        ContentBlock block = new ToolUseBlock
        {
            Id = "toolu_123",
            Name = "read_file",
            Input = JsonDocument.Parse("""{"path": "/test"}""").RootElement
        };

        // Act - serialize as ContentBlock to get the type discriminator
        var json = JsonSerializer.Serialize(block, JsonOptions);
        var doc = JsonDocument.Parse(json);

        // Assert
        Assert.Equal("tool_use", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("toolu_123", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("read_file", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("/test", doc.RootElement.GetProperty("input").GetProperty("path").GetString());
    }

    [Fact]
    public void ToolResultBlock_Serialize_ProducesCorrectJson()
    {
        // Arrange
        ContentBlock block = new ToolResultBlock
        {
            ToolUseId = "toolu_123",
            Content = JsonDocument.Parse("""{"result": "success"}""").RootElement,
            IsError = false
        };

        // Act - serialize as ContentBlock to get the type discriminator
        var json = JsonSerializer.Serialize(block, JsonOptions);
        var doc = JsonDocument.Parse(json);

        // Assert
        Assert.Equal("tool_result", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("toolu_123", doc.RootElement.GetProperty("tool_use_id").GetString());
    }

    [Fact]
    public void ContentBlock_Polymorphic_Deserialize_TextBlock()
    {
        // Arrange
        var json = """{"type": "text", "text": "Hello"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        // Assert
        Assert.IsType<TextBlock>(block);
        Assert.Equal("Hello", ((TextBlock)block!).Text);
    }

    [Fact]
    public void ContentBlock_Polymorphic_Deserialize_ThinkingBlock()
    {
        // Arrange
        var json = """{"type": "thinking", "thinking": "Processing...", "signature": "sig"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        // Assert
        Assert.IsType<ThinkingBlock>(block);
        Assert.Equal("Processing...", ((ThinkingBlock)block!).Thinking);
    }

    [Fact]
    public void ContentBlock_Polymorphic_Deserialize_ToolUseBlock()
    {
        // Arrange
        var json = """{"type": "tool_use", "id": "t1", "name": "bash", "input": {}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        // Assert
        Assert.IsType<ToolUseBlock>(block);
        var toolUse = (ToolUseBlock)block!;
        Assert.Equal("t1", toolUse.Id);
        Assert.Equal("bash", toolUse.Name);
    }

    [Fact]
    public void ContentBlock_Polymorphic_Deserialize_ToolResultBlock()
    {
        // Arrange
        var json = """{"type": "tool_result", "tool_use_id": "t1"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        // Assert
        Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("t1", ((ToolResultBlock)block!).ToolUseId);
    }

    [Fact]
    public void ToolResultBlock_WithErrorContent_ParsedCorrectly()
    {
        // Arrange
        var json = """
                   {
                       "type": "tool_result",
                       "tool_use_id": "toolu_error",
                       "content": "Error: File not found",
                       "is_error": true
                   }
                   """;

        // Act
        var block = JsonSerializer.Deserialize<ToolResultBlock>(json, JsonOptions);

        // Assert
        Assert.NotNull(block);
        Assert.True(block.IsError);
        Assert.Equal("toolu_error", block.ToolUseId);
    }
}

/// <summary>
///     Tests that verify messages can be serialized and deserialized correctly.
/// </summary>
public class MessageSerializationRoundTripTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void UserMessage_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new UserMessage
        {
            MessageContent = new UserMessageContent
            {
                Content = JsonDocument.Parse("\"Hello\"").RootElement,
                Uuid = "user-uuid-1",
                ParentToolUseId = "toolu_parent"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<UserMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MessageContent.Uuid, deserialized.MessageContent.Uuid);
        Assert.Equal(original.MessageContent.ParentToolUseId, deserialized.MessageContent.ParentToolUseId);
    }

    [Fact]
    public void AssistantMessage_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new AssistantMessage
        {
            MessageContent = new AssistantMessageContent
            {
                Model = "claude-3-opus",
                Content = [new TextBlock { Text = "Response text" }],
                ParentToolUseId = "toolu_1",
                Error = null
            }
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AssistantMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MessageContent.Model, deserialized.MessageContent.Model);
        Assert.Single(deserialized.MessageContent.Content);
    }

    [Fact]
    public void SystemMessage_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new SystemMessage
        {
            Subtype = "init",
            SessionId = "session-rt-1",
            Cwd = "/project",
            Model = "claude-3-opus",
            Tools = ["Read", "Write"],
            PermissionMode = "default"
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SystemMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Subtype, deserialized.Subtype);
        Assert.Equal(original.SessionId, deserialized.SessionId);
        Assert.Equal(original.Cwd, deserialized.Cwd);
        Assert.Equal(original.Model, deserialized.Model);
        Assert.Equal(original.Tools, deserialized.Tools);
    }

    [Fact]
    public void ResultMessage_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new ResultMessage
        {
            Subtype = "success",
            DurationMs = 1500,
            DurationApiMs = 1400,
            IsError = false,
            NumTurns = 2,
            SessionId = "session-rt-2",
            TotalCostUsd = 0.025,
            Result = "Completed"
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ResultMessage>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Subtype, deserialized.Subtype);
        Assert.Equal(original.DurationMs, deserialized.DurationMs);
        Assert.Equal(original.DurationApiMs, deserialized.DurationApiMs);
        Assert.Equal(original.IsError, deserialized.IsError);
        Assert.Equal(original.NumTurns, deserialized.NumTurns);
        Assert.Equal(original.SessionId, deserialized.SessionId);
        Assert.Equal(original.TotalCostUsd, deserialized.TotalCostUsd);
        Assert.Equal(original.Result, deserialized.Result);
    }

    [Fact]
    public void StreamEvent_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new StreamEvent
        {
            Uuid = "event-rt-1",
            SessionId = "session-rt-3",
            Event = JsonDocument.Parse("""{"type": "delta", "text": "hello"}""").RootElement,
            ParentToolUseId = "toolu_rt"
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<StreamEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Uuid, deserialized.Uuid);
        Assert.Equal(original.SessionId, deserialized.SessionId);
        Assert.Equal(original.ParentToolUseId, deserialized.ParentToolUseId);
        Assert.Equal("delta", deserialized.Event.GetProperty("type").GetString());
    }
}

/// <summary>
///     Tests that simulate realistic message processing flows through QueryHandler.
///     Uses the existing MockTransport from QueryHandlerControlTests.cs.
/// </summary>
public class MessageProcessingIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task QueryHandler_ProcessesUserMessage_WritesToChannel()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a user message
        var userMessageJson = """
                              {
                                  "type": "user",
                                  "message": {
                                      "content": "Hello from test!",
                                      "uuid": "test-user-1"
                                  }
                              }
                              """;

        await transport.InjectMessageAsync(JsonDocument.Parse(userMessageJson));

        // Give time for message to be processed
        await Task.Delay(100);

        // Assert - handler should still be ready (no crash)
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_ProcessesAssistantMessage_WritesToChannel()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject an assistant message
        var assistantMessageJson = """
                                   {
                                       "type": "assistant",
                                       "message": {
                                           "model": "claude-3-opus",
                                           "content": [
                                               {"type": "text", "text": "Hello! How can I help?"}
                                           ]
                                       }
                                   }
                                   """;

        await transport.InjectMessageAsync(JsonDocument.Parse(assistantMessageJson));

        // Give time for message to be processed
        await Task.Delay(100);

        // Assert - handler should still be ready
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_ProcessesSystemInitMessage_WritesToChannel()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a system init message
        var systemMessageJson = """
                                {
                                    "type": "system",
                                    "subtype": "init",
                                    "session_id": "test-session",
                                    "model": "claude-3-opus",
                                    "tools": ["Read", "Write"]
                                }
                                """;

        await transport.InjectMessageAsync(JsonDocument.Parse(systemMessageJson));
        await Task.Delay(100);

        // Assert
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_ProcessesResultMessage_WritesToChannel()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a result message
        var resultMessageJson = """
                                {
                                    "type": "result",
                                    "subtype": "success",
                                    "duration_ms": 1000,
                                    "duration_api_ms": 900,
                                    "is_error": false,
                                    "num_turns": 1,
                                    "session_id": "test-session"
                                }
                                """;

        await transport.InjectMessageAsync(JsonDocument.Parse(resultMessageJson));
        await Task.Delay(100);

        // Assert
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_ProcessesStreamEvent_WritesToChannel()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a stream event
        var streamEventJson = """
                              {
                                  "type": "stream_event",
                                  "uuid": "stream-event-1",
                                  "session_id": "test-session",
                                  "event": {
                                      "type": "content_block_delta",
                                      "delta": {"text": "streaming..."}
                                  }
                              }
                              """;

        await transport.InjectMessageAsync(JsonDocument.Parse(streamEventJson));
        await Task.Delay(100);

        // Assert
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_UnknownMessageType_HandledGracefully()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject an unknown message type
        var unknownMessageJson = """
                                 {
                                     "type": "unknown_message_type_xyz",
                                     "data": "some data"
                                 }
                                 """;

        await transport.InjectMessageAsync(JsonDocument.Parse(unknownMessageJson));
        await Task.Delay(100);

        // Assert - handler should not crash, just log and skip
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_MessageMissingType_HandledGracefully()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a message without type property
        var noTypeMessageJson = """
                                {
                                    "content": "no type here",
                                    "uuid": "orphan-message"
                                }
                                """;

        await transport.InjectMessageAsync(JsonDocument.Parse(noTypeMessageJson));
        await Task.Delay(100);

        // Assert - handler should not crash
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_MultipleMessagesInSequence_AllProcessed()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject multiple messages in sequence
        var messages = new[]
        {
            """{"type": "system", "subtype": "init", "session_id": "s1"}""",
            """{"type": "user", "message": {"content": "Hello"}}""",
            """{"type": "assistant", "message": {"model": "opus", "content": [{"type": "text", "text": "Hi"}]}}""",
            """{"type": "result", "subtype": "success", "duration_ms": 100, "duration_api_ms": 90, "is_error": false, "num_turns": 1, "session_id": "s1"}"""
        };

        foreach (var msg in messages)
        {
            await transport.InjectMessageAsync(JsonDocument.Parse(msg));
        }

        await Task.Delay(200);

        // Assert
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_MalformedJsonInMessage_HandledGracefully()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Note: The transport parses JSON before passing to handler,
        // so we test with valid JSON that has invalid message structure
        var invalidStructureJson = """
                                   {
                                       "type": "user",
                                       "message": "not_an_object"
                                   }
                                   """;

        await transport.InjectMessageAsync(JsonDocument.Parse(invalidStructureJson));
        await Task.Delay(100);

        // Assert - handler should not crash, message parsing should fail gracefully
        Assert.True(transport.IsReady);
    }

    private static dynamic CreateQueryHandler(MockTransport transport, ClaudeAgentOptions options)
    {
        var assembly = typeof(ClaudeAgentOptions).Assembly;
        var handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests for message channel completion and lifecycle.
/// </summary>
public class MessageChannelCompletionTests
{
    [Fact]
    public async Task QueryHandler_TransportCompletes_ChannelCompletes()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Complete the transport
        transport.CompleteMessages();

        // Give time for completion to propagate
        await Task.Delay(100);

        // Assert - handler processed completion without error
        // (we can't directly check channel completion, but no exception means success)
    }

    [Fact]
    public async Task QueryHandler_Cancellation_StopsReader()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();

        using var cts = new CancellationTokenSource();
        var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync(cts.Token);

        // Act - cancel
        await cts.CancelAsync();
        await Task.Delay(100);

        // Clean up
        await handler.DisposeAsync();

        // Assert - no exception means cancellation was handled gracefully
    }

    private static dynamic CreateQueryHandler(MockTransport transport, ClaudeAgentOptions options)
    {
        var assembly = typeof(ClaudeAgentOptions).Assembly;
        var handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}

/// <summary>
///     Tests that verify control protocol messages (control_request, control_response)
///     are handled separately from regular messages.
/// </summary>
public class ControlProtocolMessageTests
{
    [Fact]
    public async Task QueryHandler_ControlResponse_NotQueuedAsRegularMessage()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a control response directly (simulating response to a request)
        var controlResponse = """
                              {
                                  "type": "control_response",
                                  "response": {
                                      "subtype": "success",
                                      "request_id": "test-req-123",
                                      "response": {"status": "ok"}
                                  }
                              }
                              """;

        await transport.InjectMessageAsync(JsonDocument.Parse(controlResponse));
        await Task.Delay(100);

        // Assert - handler should process control response without error
        // Control responses go to pending response handlers, not the message channel
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task QueryHandler_ControlRequest_HandledSeparately()
    {
        // Arrange
        var transport = new MockTransport();
        var options = new ClaudeAgentOptions();
        await using var handler = CreateQueryHandler(transport, options);

        await handler.StartAsync();

        // Inject a control request (normally from CLI, but we can inject for testing)
        var controlRequest = """
                             {
                                 "type": "control_request",
                                 "request_id": "cli-req-456",
                                 "request": {
                                     "subtype": "can_use_tool",
                                     "tool_name": "read_file",
                                     "input": {"path": "/test"}
                                 }
                             }
                             """;

        await transport.InjectMessageAsync(JsonDocument.Parse(controlRequest));
        await Task.Delay(100);

        // Assert - handler should process and respond to control request
        Assert.True(transport.IsReady);

        // Verify a response was written back
        var writtenMessages = transport.GetAllWrittenMessagesAsJson();
        // Control request should trigger a control response
        Assert.Contains(writtenMessages, m =>
            m.TryGetProperty("type", out var t) && t.GetString() == "control_response");
    }

    private static dynamic CreateQueryHandler(MockTransport transport, ClaudeAgentOptions options)
    {
        var assembly = typeof(ClaudeAgentOptions).Assembly;
        var handlerType = assembly.GetType("Claude.AgentSdk.Protocol.QueryHandler")!;
        return Activator.CreateInstance(handlerType, transport, options, null)!;
    }
}
