using System.Text.Json;
using Claude.AgentSdk.Messages;
using Xunit;

namespace Claude.AgentSdk.Tests.Messages;

/// <summary>
///     Comprehensive unit tests for Message types in the Claude.AgentSdk.
/// </summary>
public class MessageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #region Polymorphic Type Discrimination Tests

    [Fact]
    public void Deserialize_UserMessage_ReturnsCorrectType()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": "Hello, Claude!",
                    "uuid": "test-uuid-123"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType<UserMessage>(message);
    }

    [Fact]
    public void Deserialize_AssistantMessage_ReturnsCorrectType()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        { "type": "text", "text": "Hello!" }
                    ],
                    "model": "claude-3-opus"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType<AssistantMessage>(message);
    }

    [Fact]
    public void Deserialize_SystemMessage_ReturnsCorrectType()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "session_id": "session-123"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType<SystemMessage>(message);
    }

    [Fact]
    public void Deserialize_ResultMessage_ReturnsCorrectType()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 1000,
                "duration_api_ms": 500,
                "is_error": false,
                "num_turns": 1,
                "session_id": "session-123"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType<ResultMessage>(message);
    }

    [Fact]
    public void Deserialize_StreamEvent_ReturnsCorrectType()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "event-uuid-123",
                "session_id": "session-123",
                "event": { "type": "content_block_delta" }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType<StreamEvent>(message);
    }

    [Theory]
    [InlineData("user", typeof(UserMessage))]
    [InlineData("assistant", typeof(AssistantMessage))]
    [InlineData("system", typeof(SystemMessage))]
    [InlineData("result", typeof(ResultMessage))]
    [InlineData("stream_event", typeof(StreamEvent))]
    public void Deserialize_AllMessageTypes_ReturnsExpectedTypes(string typeValue, Type expectedType)
    {
        var json = typeValue switch
        {
            "user" => """{"type":"user","message":{"content":"test"}}""",
            "assistant" => """{"type":"assistant","message":{"content":[],"model":"claude-3"}}""",
            "system" => """{"type":"system","subtype":"init"}""",
            "result" => """{"type":"result","subtype":"done","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s"}""",
            "stream_event" => """{"type":"stream_event","uuid":"u","session_id":"s","event":{}}""",
            _ => throw new ArgumentException($"Unknown type: {typeValue}")
        };

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.IsType(expectedType, message);
    }

    #endregion

    #region UserMessage Tests

    [Fact]
    public void Deserialize_UserMessage_WithStringContent_MapsProperties()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": "Hello, Claude!",
                    "uuid": "test-uuid-123",
                    "parent_tool_use_id": "tool-use-456"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as UserMessage;

        Assert.NotNull(message);
        Assert.Equal("test-uuid-123", message.MessageContent.Uuid);
        Assert.Equal("tool-use-456", message.MessageContent.ParentToolUseId);
        Assert.Equal(JsonValueKind.String, message.MessageContent.Content.ValueKind);
        Assert.Equal("Hello, Claude!", message.MessageContent.Content.GetString());
    }

    [Fact]
    public void Deserialize_UserMessage_WithArrayContent_MapsProperties()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": [
                        { "type": "text", "text": "Part 1" },
                        { "type": "text", "text": "Part 2" }
                    ],
                    "uuid": "array-uuid"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as UserMessage;

        Assert.NotNull(message);
        Assert.Equal("array-uuid", message.MessageContent.Uuid);
        Assert.Equal(JsonValueKind.Array, message.MessageContent.Content.ValueKind);
        Assert.Equal(2, message.MessageContent.Content.GetArrayLength());
    }

    [Fact]
    public void Deserialize_UserMessage_WithNullOptionalFields_AllowsNull()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": "Hello"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as UserMessage;

        Assert.NotNull(message);
        Assert.Null(message.MessageContent.Uuid);
        Assert.Null(message.MessageContent.ParentToolUseId);
    }

    #endregion

    #region AssistantMessage Tests

    [Fact]
    public void Deserialize_AssistantMessage_WithTextBlock_MapsProperties()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        { "type": "text", "text": "Hello, user!" }
                    ],
                    "model": "claude-3-opus-20240229"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Equal("claude-3-opus-20240229", message.MessageContent.Model);
        Assert.Single(message.MessageContent.Content);
        var textBlock = Assert.IsType<TextBlock>(message.MessageContent.Content[0]);
        Assert.Equal("Hello, user!", textBlock.Text);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithToolUseBlock_MapsProperties()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_use",
                            "id": "toolu_123",
                            "name": "read_file",
                            "input": { "path": "/test/file.txt" }
                        }
                    ],
                    "model": "claude-3-sonnet"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Single(message.MessageContent.Content);
        var toolUseBlock = Assert.IsType<ToolUseBlock>(message.MessageContent.Content[0]);
        Assert.Equal("toolu_123", toolUseBlock.Id);
        Assert.Equal("read_file", toolUseBlock.Name);
        Assert.Equal("/test/file.txt", toolUseBlock.Input.GetProperty("path").GetString());
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithThinkingBlock_MapsProperties()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "thinking",
                            "thinking": "Let me consider this carefully...",
                            "signature": "sig-abc123"
                        }
                    ],
                    "model": "claude-3-opus"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Single(message.MessageContent.Content);
        var thinkingBlock = Assert.IsType<ThinkingBlock>(message.MessageContent.Content[0]);
        Assert.Equal("Let me consider this carefully...", thinkingBlock.Thinking);
        Assert.Equal("sig-abc123", thinkingBlock.Signature);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithMixedContentBlocks_MapsAllBlocks()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        { "type": "thinking", "thinking": "I need to use a tool", "signature": "sig-1" },
                        { "type": "text", "text": "Let me check that file for you." },
                        { "type": "tool_use", "id": "toolu_1", "name": "read_file", "input": {} }
                    ],
                    "model": "claude-3-opus"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Equal(3, message.MessageContent.Content.Count);
        Assert.IsType<ThinkingBlock>(message.MessageContent.Content[0]);
        Assert.IsType<TextBlock>(message.MessageContent.Content[1]);
        Assert.IsType<ToolUseBlock>(message.MessageContent.Content[2]);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithError_MapsErrorProperty()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [],
                    "model": "claude-3-opus",
                    "error": "Rate limit exceeded"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Equal("Rate limit exceeded", message.MessageContent.Error);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithParentToolUseId_MapsProperty()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [{ "type": "text", "text": "Response" }],
                    "model": "claude-3",
                    "parent_tool_use_id": "parent-tool-123"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Equal("parent-tool-123", message.MessageContent.ParentToolUseId);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithEmptyContent_AllowsEmptyList()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Empty(message.MessageContent.Content);
    }

    #endregion

    #region SystemMessage Tests

    [Fact]
    public void Deserialize_SystemMessage_InitSubtype_MapsAllProperties()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "session_id": "session-abc123",
                "cwd": "/home/user/project",
                "slash_commands": ["/help", "/clear", "/exit"],
                "tools": ["read_file", "write_file", "bash"],
                "mcp_servers": [
                    { "name": "file-server", "status": "connected" },
                    { "name": "db-server", "status": "error", "error": "Connection refused" }
                ],
                "model": "claude-3-opus",
                "permission_mode": "auto"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Equal("init", message.Subtype);
        Assert.True(message.IsInit);
        Assert.False(message.IsCompactBoundary);
        Assert.Equal("session-abc123", message.SessionId);
        Assert.Equal("/home/user/project", message.Cwd);
        Assert.Equal(3, message.SlashCommands?.Count);
        Assert.Contains("/help", message.SlashCommands!);
        Assert.Equal(3, message.Tools?.Count);
        Assert.Contains("bash", message.Tools!);
        Assert.Equal(2, message.McpServers?.Count);
        Assert.Equal("claude-3-opus", message.Model);
        Assert.Equal("auto", message.PermissionMode);
    }

    [Fact]
    public void Deserialize_SystemMessage_CompactBoundarySubtype_MapsMetadata()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "compact_boundary",
                "compact_metadata": {
                    "pre_tokens": 50000,
                    "post_tokens": 10000,
                    "trigger": "token_limit"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Equal("compact_boundary", message.Subtype);
        Assert.True(message.IsCompactBoundary);
        Assert.False(message.IsInit);
        Assert.NotNull(message.CompactMetadata);
        Assert.Equal(50000, message.CompactMetadata.PreTokens);
        Assert.Equal(10000, message.CompactMetadata.PostTokens);
        Assert.Equal("token_limit", message.CompactMetadata.Trigger);
    }

    [Fact]
    public void Deserialize_SystemMessage_WithMinimalFields_AllowsNullOptional()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "status"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Equal("status", message.Subtype);
        Assert.Null(message.SessionId);
        Assert.Null(message.Cwd);
        Assert.Null(message.SlashCommands);
        Assert.Null(message.Tools);
        Assert.Null(message.McpServers);
        Assert.Null(message.Model);
        Assert.Null(message.PermissionMode);
        Assert.Null(message.CompactMetadata);
        Assert.Null(message.Data);
    }

    [Fact]
    public void Deserialize_SystemMessage_WithData_MapsJsonElement()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "custom",
                "data": {
                    "custom_field": "custom_value",
                    "nested": { "key": 123 }
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.NotNull(message.Data);
        Assert.Equal("custom_value", message.Data.Value.GetProperty("custom_field").GetString());
        Assert.Equal(123, message.Data.Value.GetProperty("nested").GetProperty("key").GetInt32());
    }

    [Fact]
    public void Deserialize_McpServerStatus_WithError_MapsAllFields()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "mcp_servers": [
                    { "name": "failing-server", "status": "error", "error": "Authentication failed" }
                ]
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.NotNull(message.McpServers);
        Assert.Single(message.McpServers);
        Assert.Equal("failing-server", message.McpServers[0].Name);
        Assert.Equal("error", message.McpServers[0].Status);
        Assert.Equal("Authentication failed", message.McpServers[0].Error);
    }

    [Theory]
    [InlineData("init", true, false)]
    [InlineData("compact_boundary", false, true)]
    [InlineData("status", false, false)]
    [InlineData("custom", false, false)]
    public void SystemMessage_IsInitAndIsCompactBoundary_ReturnsCorrectValues(
        string subtype, bool expectedIsInit, bool expectedIsCompactBoundary)
    {
        var json = $$"""
            {
                "type": "system",
                "subtype": "{{subtype}}"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Equal(expectedIsInit, message.IsInit);
        Assert.Equal(expectedIsCompactBoundary, message.IsCompactBoundary);
    }

    #endregion

    #region ResultMessage Tests

    [Fact]
    public void Deserialize_ResultMessage_SuccessfulResult_MapsAllProperties()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 15000,
                "duration_api_ms": 12500,
                "is_error": false,
                "num_turns": 5,
                "session_id": "session-result-123",
                "total_cost_usd": 0.0523,
                "result": "Task completed successfully"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.Equal("success", message.Subtype);
        Assert.Equal(15000, message.DurationMs);
        Assert.Equal(12500, message.DurationApiMs);
        Assert.False(message.IsError);
        Assert.Equal(5, message.NumTurns);
        Assert.Equal("session-result-123", message.SessionId);
        Assert.Equal(0.0523, message.TotalCostUsd);
        Assert.Equal("Task completed successfully", message.Result);
    }

    [Fact]
    public void Deserialize_ResultMessage_WithError_MapsIsErrorTrue()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "error",
                "duration_ms": 500,
                "duration_api_ms": 250,
                "is_error": true,
                "num_turns": 1,
                "session_id": "session-error-123",
                "result": "An unexpected error occurred"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.True(message.IsError);
        Assert.Equal("An unexpected error occurred", message.Result);
    }

    [Fact]
    public void Deserialize_ResultMessage_WithUsage_MapsJsonElement()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 1000,
                "duration_api_ms": 800,
                "is_error": false,
                "num_turns": 2,
                "session_id": "session-usage-123",
                "usage": {
                    "input_tokens": 1500,
                    "output_tokens": 500,
                    "cache_creation_input_tokens": 0,
                    "cache_read_input_tokens": 1000
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.NotNull(message.Usage);
        Assert.Equal(1500, message.Usage.Value.GetProperty("input_tokens").GetInt32());
        Assert.Equal(500, message.Usage.Value.GetProperty("output_tokens").GetInt32());
        Assert.Equal(1000, message.Usage.Value.GetProperty("cache_read_input_tokens").GetInt32());
    }

    [Fact]
    public void Deserialize_ResultMessage_WithStructuredOutput_MapsJsonElement()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 2000,
                "duration_api_ms": 1500,
                "is_error": false,
                "num_turns": 3,
                "session_id": "session-structured-123",
                "structured_output": {
                    "analysis": {
                        "score": 95,
                        "categories": ["performance", "security"],
                        "passed": true
                    }
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.NotNull(message.StructuredOutput);
        var analysis = message.StructuredOutput.Value.GetProperty("analysis");
        Assert.Equal(95, analysis.GetProperty("score").GetInt32());
        Assert.True(analysis.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void Deserialize_ResultMessage_WithNullOptionalFields_AllowsNull()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "done",
                "duration_ms": 100,
                "duration_api_ms": 50,
                "is_error": false,
                "num_turns": 1,
                "session_id": "session-minimal-123"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.Null(message.TotalCostUsd);
        Assert.Null(message.Usage);
        Assert.Null(message.Result);
        Assert.Null(message.StructuredOutput);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(1.5)]
    [InlineData(100.99)]
    public void Deserialize_ResultMessage_VariousCostValues_MapsPrecisely(double cost)
    {
        var json = $$"""
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 1000,
                "duration_api_ms": 500,
                "is_error": false,
                "num_turns": 1,
                "session_id": "session-cost-test",
                "total_cost_usd": {{cost.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.Equal(cost, message.TotalCostUsd);
    }

    #endregion

    #region StreamEvent Tests

    [Fact]
    public void Deserialize_StreamEvent_ContentBlockDelta_MapsAllProperties()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "event-uuid-abc123",
                "session_id": "session-stream-123",
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

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as StreamEvent;

        Assert.NotNull(message);
        Assert.Equal("event-uuid-abc123", message.Uuid);
        Assert.Equal("session-stream-123", message.SessionId);
        Assert.Equal("content_block_delta", message.Event.GetProperty("type").GetString());
        Assert.Equal(0, message.Event.GetProperty("index").GetInt32());
    }

    [Fact]
    public void Deserialize_StreamEvent_WithParentToolUseId_MapsProperty()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "event-uuid-456",
                "session_id": "session-tool-stream",
                "event": { "type": "message_start" },
                "parent_tool_use_id": "parent-toolu-789"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as StreamEvent;

        Assert.NotNull(message);
        Assert.Equal("parent-toolu-789", message.ParentToolUseId);
    }

    [Fact]
    public void Deserialize_StreamEvent_WithoutParentToolUseId_AllowsNull()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "event-uuid-null",
                "session_id": "session-no-parent",
                "event": { "type": "message_stop" }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as StreamEvent;

        Assert.NotNull(message);
        Assert.Null(message.ParentToolUseId);
    }

    [Theory]
    [InlineData("message_start")]
    [InlineData("content_block_start")]
    [InlineData("content_block_delta")]
    [InlineData("content_block_stop")]
    [InlineData("message_delta")]
    [InlineData("message_stop")]
    public void Deserialize_StreamEvent_VariousEventTypes_MapsEventProperty(string eventType)
    {
        var json = $$"""
            {
                "type": "stream_event",
                "uuid": "event-{{eventType}}",
                "session_id": "session-event-types",
                "event": { "type": "{{eventType}}" }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as StreamEvent;

        Assert.NotNull(message);
        Assert.Equal(eventType, message.Event.GetProperty("type").GetString());
    }

    #endregion

    #region ContentBlock Nested Tests

    [Fact]
    public void Deserialize_ToolResultBlock_WithContent_MapsAllFields()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_result",
                            "tool_use_id": "toolu_result_123",
                            "content": { "output": "File contents here", "lines": 50 },
                            "is_error": false
                        }
                    ],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        var toolResult = Assert.IsType<ToolResultBlock>(message.MessageContent.Content[0]);
        Assert.Equal("toolu_result_123", toolResult.ToolUseId);
        Assert.False(toolResult.IsError);
        Assert.NotNull(toolResult.Content);
        Assert.Equal("File contents here", toolResult.Content.Value.GetProperty("output").GetString());
    }

    [Fact]
    public void Deserialize_ToolResultBlock_WithError_MapsIsErrorTrue()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_result",
                            "tool_use_id": "toolu_error_456",
                            "content": "File not found",
                            "is_error": true
                        }
                    ],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        var toolResult = Assert.IsType<ToolResultBlock>(message.MessageContent.Content[0]);
        Assert.True(toolResult.IsError);
    }

    [Fact]
    public void Deserialize_ToolUseBlock_WithComplexInput_MapsNestedStructure()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_use",
                            "id": "toolu_complex_789",
                            "name": "search_files",
                            "input": {
                                "pattern": "*.cs",
                                "options": {
                                    "recursive": true,
                                    "ignore_case": false,
                                    "max_results": 100
                                },
                                "paths": ["/src", "/tests"]
                            }
                        }
                    ],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        var toolUse = Assert.IsType<ToolUseBlock>(message.MessageContent.Content[0]);
        Assert.Equal("*.cs", toolUse.Input.GetProperty("pattern").GetString());
        Assert.True(toolUse.Input.GetProperty("options").GetProperty("recursive").GetBoolean());
        Assert.Equal(2, toolUse.Input.GetProperty("paths").GetArrayLength());
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Deserialize_Message_WithUnknownFields_IgnoresExtra()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "unknown_field": "should be ignored",
                "another_unknown": { "nested": true }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Equal("init", message.Subtype);
    }

    [Fact]
    public void Deserialize_Message_WithNullJsonValues_HandlesGracefully()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "test",
                "session_id": null,
                "cwd": null,
                "model": null
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.Null(message.SessionId);
        Assert.Null(message.Cwd);
        Assert.Null(message.Model);
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithEmptyTextBlock_MapsEmptyString()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        { "type": "text", "text": "" }
                    ],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        var textBlock = Assert.IsType<TextBlock>(message.MessageContent.Content[0]);
        Assert.Equal("", textBlock.Text);
    }

    [Fact]
    public void Deserialize_ResultMessage_WithZeroDuration_MapsCorrectly()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "instant",
                "duration_ms": 0,
                "duration_api_ms": 0,
                "is_error": false,
                "num_turns": 0,
                "session_id": "session-zero"
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as ResultMessage;

        Assert.NotNull(message);
        Assert.Equal(0, message.DurationMs);
        Assert.Equal(0, message.DurationApiMs);
        Assert.Equal(0, message.NumTurns);
    }

    [Fact]
    public void Deserialize_SystemMessage_WithEmptyArrays_MapsEmptyCollections()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "slash_commands": [],
                "tools": [],
                "mcp_servers": []
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as SystemMessage;

        Assert.NotNull(message);
        Assert.NotNull(message.SlashCommands);
        Assert.Empty(message.SlashCommands);
        Assert.NotNull(message.Tools);
        Assert.Empty(message.Tools);
        Assert.NotNull(message.McpServers);
        Assert.Empty(message.McpServers);
    }

    [Fact]
    public void Deserialize_StreamEvent_WithEmptyEvent_MapsEmptyObject()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "event-empty",
                "session_id": "session-empty-event",
                "event": {}
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as StreamEvent;

        Assert.NotNull(message);
        Assert.Equal(JsonValueKind.Object, message.Event.ValueKind);
    }

    [Fact]
    public void Deserialize_UserMessage_WithUnicodeContent_MapsCorrectly()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": "Hello! \u4f60\u597d! \ud83d\ude00 \u00e9\u00e8\u00ea"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as UserMessage;

        Assert.NotNull(message);
        Assert.Contains("\u4f60\u597d", message.MessageContent.Content.GetString());
    }

    [Fact]
    public void Deserialize_AssistantMessage_WithLargeContentArray_MapsAllBlocks()
    {
        var blocks = string.Join(",\n", Enumerable.Range(0, 20)
            .Select(i => $"{{ \"type\": \"text\", \"text\": \"Block {i}\" }}"));
        var json = $$"""
            {
                "type": "assistant",
                "message": {
                    "content": [{{blocks}}],
                    "model": "claude-3"
                }
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions) as AssistantMessage;

        Assert.NotNull(message);
        Assert.Equal(20, message.MessageContent.Content.Count);
        for (var i = 0; i < 20; i++)
        {
            var textBlock = Assert.IsType<TextBlock>(message.MessageContent.Content[i]);
            Assert.Equal($"Block {i}", textBlock.Text);
        }
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void UserMessageContent_Equality_WorksCorrectly()
    {
        var content1 = new UserMessageContent
        {
            Content = JsonDocument.Parse("\"test\"").RootElement,
            Uuid = "uuid-1"
        };

        var content2 = new UserMessageContent
        {
            Content = JsonDocument.Parse("\"test\"").RootElement,
            Uuid = "uuid-1"
        };

        // Note: JsonElement doesn't implement value equality, so records with JsonElement
        // won't be equal even with same content. This is expected behavior.
        Assert.NotEqual(content1, content2);
    }

    [Fact]
    public void TextBlock_Equality_WorksCorrectly()
    {
        var block1 = new TextBlock { Text = "Hello" };
        var block2 = new TextBlock { Text = "Hello" };
        var block3 = new TextBlock { Text = "World" };

        Assert.Equal(block1, block2);
        Assert.NotEqual(block1, block3);
    }

    [Fact]
    public void McpServerStatus_Equality_WorksCorrectly()
    {
        var server1 = new McpServerStatus { Name = "server", Status = "connected" };
        var server2 = new McpServerStatus { Name = "server", Status = "connected" };
        var server3 = new McpServerStatus { Name = "server", Status = "error", Error = "Failed" };

        Assert.Equal(server1, server2);
        Assert.NotEqual(server1, server3);
    }

    [Fact]
    public void CompactMetadata_Equality_WorksCorrectly()
    {
        var meta1 = new CompactMetadata { PreTokens = 1000, PostTokens = 500, Trigger = "limit" };
        var meta2 = new CompactMetadata { PreTokens = 1000, PostTokens = 500, Trigger = "limit" };
        var meta3 = new CompactMetadata { PreTokens = 2000, PostTokens = 500, Trigger = "limit" };

        Assert.Equal(meta1, meta2);
        Assert.NotEqual(meta1, meta3);
    }

    #endregion

    #region Serialization Round-Trip Tests

    [Fact]
    public void RoundTrip_TextBlock_PreservesData()
    {
        var original = new TextBlock { Text = "Hello, World!" };
        var json = JsonSerializer.Serialize<ContentBlock>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        Assert.NotNull(deserialized);
        var textBlock = Assert.IsType<TextBlock>(deserialized);
        Assert.Equal(original.Text, textBlock.Text);
    }

    [Fact]
    public void RoundTrip_ThinkingBlock_PreservesData()
    {
        var original = new ThinkingBlock { Thinking = "Let me think...", Signature = "sig-123" };
        var json = JsonSerializer.Serialize<ContentBlock>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, JsonOptions);

        Assert.NotNull(deserialized);
        var thinkingBlock = Assert.IsType<ThinkingBlock>(deserialized);
        Assert.Equal(original.Thinking, thinkingBlock.Thinking);
        Assert.Equal(original.Signature, thinkingBlock.Signature);
    }

    [Fact]
    public void RoundTrip_McpServerStatus_PreservesData()
    {
        var original = new McpServerStatus { Name = "test-server", Status = "connected", Error = null };
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<McpServerStatus>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Status, deserialized.Status);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void RoundTrip_CompactMetadata_PreservesData()
    {
        var original = new CompactMetadata { PreTokens = 50000, PostTokens = 10000, Trigger = "auto" };
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CompactMetadata>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.PreTokens, deserialized.PreTokens);
        Assert.Equal(original.PostTokens, deserialized.PostTokens);
        Assert.Equal(original.Trigger, deserialized.Trigger);
    }

    #endregion
}
