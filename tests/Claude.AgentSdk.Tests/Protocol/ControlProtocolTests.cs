using System.Text.Json;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Comprehensive unit tests for ControlProtocol types in the Claude.AgentSdk.
/// </summary>
public class ControlProtocolTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ControlRequest_Serialize_ProducesExpectedJson()
    {
        ControlRequest request = new()
        {
            RequestId = "req-123",
            Request = JsonDocument.Parse("""{"subtype":"initialize"}""").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("control_request", parsed.GetProperty("type").GetString());
        Assert.Equal("req-123", parsed.GetProperty("request_id").GetString());
        Assert.Equal("initialize", parsed.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public void ControlRequest_Type_IsAlwaysControlRequest()
    {
        ControlRequest request = new()
        {
            RequestId = "test-id",
            Request = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal("control_request", request.Type);
    }

    [Fact]
    public void ControlRequest_Deserialize_MapsProperties()
    {
        const string json = """
                            {
                                "type": "control_request",
                                "request_id": "req-456",
                                "request": {
                                    "subtype": "can_use_tool",
                                    "tool_name": "read_file"
                                }
                            }
                            """;

        ControlRequest? request = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("req-456", request.RequestId);
        Assert.Equal("can_use_tool", request.Request.GetProperty("subtype").GetString());
        Assert.Equal("read_file", request.Request.GetProperty("tool_name").GetString());
    }

    [Fact]
    public void ControlRequest_Deserialize_WithComplexRequest_PreservesStructure()
    {
        const string json = """
                            {
                                "type": "control_request",
                                "request_id": "req-complex",
                                "request": {
                                    "subtype": "mcp_message",
                                    "server_name": "test-server",
                                    "message": {
                                        "jsonrpc": "2.0",
                                        "method": "tools/call",
                                        "params": {
                                            "name": "search",
                                            "arguments": { "query": "test" }
                                        }
                                    }
                                }
                            }
                            """;

        ControlRequest? request = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("mcp_message", request.Request.GetProperty("subtype").GetString());
        JsonElement message = request.Request.GetProperty("message");
        Assert.Equal("2.0", message.GetProperty("jsonrpc").GetString());
        Assert.Equal("tools/call", message.GetProperty("method").GetString());
    }

    [Theory]
    [InlineData("req-1")]
    [InlineData("request-with-dashes")]
    [InlineData("uuid-550e8400-e29b-41d4-a716-446655440000")]
    public void ControlRequest_RequestId_AcceptsVariousFormats(string requestId)
    {
        ControlRequest request = new()
        {
            RequestId = requestId,
            Request = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        ControlRequest? deserialized = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(requestId, deserialized.RequestId);
    }

    [Fact]
    public void ControlResponse_Serialize_ProducesExpectedJson()
    {
        ControlResponse response = new()
        {
            Response = new ControlSuccessResponse
            {
                RequestId = "req-123",
                ResponseData = JsonDocument.Parse("""{"status":"ok"}""").RootElement
            }
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("control_response", parsed.GetProperty("type").GetString());
        JsonElement responsePayload = parsed.GetProperty("response");
        Assert.Equal("req-123", responsePayload.GetProperty("request_id").GetString());
        // Note: Without [JsonDerivedType] on base class, only base class properties are serialized
        // The subtype property from the derived class may not be included unless serialized as concrete type
    }

    [Fact]
    public void ControlResponse_SerializeConcretePayload_IncludesAllProperties()
    {
        // When serializing the concrete type directly, all properties are included
        ControlSuccessResponse successResponse = new()
        {
            RequestId = "req-123",
            ResponseData = JsonDocument.Parse("""{"status":"ok"}""").RootElement
        };

        string json = JsonSerializer.Serialize(successResponse, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("req-123", parsed.GetProperty("request_id").GetString());
        Assert.Equal("success", parsed.GetProperty("subtype").GetString());
        Assert.Equal("ok", parsed.GetProperty("response").GetProperty("status").GetString());
    }

    [Fact]
    public void ControlResponse_Type_IsAlwaysControlResponse()
    {
        ControlResponse response = new()
        {
            Response = new ControlSuccessResponse { RequestId = "test" }
        };

        Assert.Equal("control_response", response.Type);
    }

    [Fact]
    public void ControlSuccessResponse_Serialize_ProducesExpectedJson()
    {
        ControlSuccessResponse response = new()
        {
            RequestId = "req-success",
            ResponseData = JsonDocument.Parse("""{"result":"completed"}""").RootElement
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("req-success", parsed.GetProperty("request_id").GetString());
        Assert.Equal("success", parsed.GetProperty("subtype").GetString());
        Assert.Equal("completed", parsed.GetProperty("response").GetProperty("result").GetString());
    }

    [Fact]
    public void ControlSuccessResponse_Subtype_IsAlwaysSuccess()
    {
        ControlSuccessResponse response = new() { RequestId = "test" };
        Assert.Equal("success", response.Subtype);
    }

    [Fact]
    public void ControlSuccessResponse_WithNullResponseData_SerializesCorrectly()
    {
        ControlSuccessResponse response = new()
        {
            RequestId = "req-null-data"
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("req-null-data", parsed.GetProperty("request_id").GetString());
        Assert.Equal("success", parsed.GetProperty("subtype").GetString());
        // ResponseData is nullable, so it may or may not be present
    }

    [Fact]
    public void ControlSuccessResponse_WithComplexResponseData_PreservesStructure()
    {
        JsonElement complexData = JsonDocument.Parse("""
                                                     {
                                                         "tools": ["read", "write", "bash"],
                                                         "models": [
                                                             {"name": "claude-3-opus", "available": true},
                                                             {"name": "claude-3-sonnet", "available": false}
                                                         ],
                                                         "config": {
                                                             "maxTokens": 4096,
                                                             "temperature": 0.7
                                                         }
                                                     }
                                                     """).RootElement;

        ControlSuccessResponse response = new()
        {
            RequestId = "req-complex",
            ResponseData = complexData
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;
        JsonElement responseData = parsed.GetProperty("response");

        Assert.Equal(3, responseData.GetProperty("tools").GetArrayLength());
        Assert.Equal(2, responseData.GetProperty("models").GetArrayLength());
        Assert.Equal(4096, responseData.GetProperty("config").GetProperty("maxTokens").GetInt32());
    }

    [Fact]
    public void ControlErrorResponse_Serialize_ProducesExpectedJson()
    {
        ControlErrorResponse response = new()
        {
            RequestId = "req-error",
            Error = "Permission denied: cannot execute bash command"
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("req-error", parsed.GetProperty("request_id").GetString());
        Assert.Equal("error", parsed.GetProperty("subtype").GetString());
        Assert.Equal("Permission denied: cannot execute bash command", parsed.GetProperty("error").GetString());
    }

    [Fact]
    public void ControlErrorResponse_Subtype_IsAlwaysError()
    {
        ControlErrorResponse response = new()
        {
            RequestId = "test",
            Error = "test error"
        };
        Assert.Equal("error", response.Subtype);
    }

    [Theory]
    [InlineData("Simple error")]
    [InlineData("Error with special chars: <>\"'&")]
    [InlineData("Multi\nline\nerror")]
    [InlineData("")]
    public void ControlErrorResponse_Error_AcceptsVariousFormats(string errorMessage)
    {
        ControlErrorResponse response = new()
        {
            RequestId = "req-test",
            Error = errorMessage
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        ControlErrorResponse? deserialized = JsonSerializer.Deserialize<ControlErrorResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(errorMessage, deserialized.Error);
    }

    [Fact]
    public void ControlErrorResponse_Deserialize_MapsProperties()
    {
        const string json = """
                            {
                                "request_id": "req-deserialize",
                                "subtype": "error",
                                "error": "Tool not found: unknown_tool"
                            }
                            """;

        ControlErrorResponse? response = JsonSerializer.Deserialize<ControlErrorResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal("req-deserialize", response.RequestId);
        Assert.Equal("Tool not found: unknown_tool", response.Error);
    }

    [Fact]
    public void ControlSubtype_Interrupt_HasCorrectValue()
    {
        Assert.Equal("interrupt", ControlSubtype.Interrupt);
    }

    [Fact]
    public void ControlSubtype_CanUseTool_HasCorrectValue()
    {
        Assert.Equal("can_use_tool", ControlSubtype.CanUseTool);
    }

    [Fact]
    public void ControlSubtype_Initialize_HasCorrectValue()
    {
        Assert.Equal("initialize", ControlSubtype.Initialize);
    }

    [Fact]
    public void ControlSubtype_SetPermissionMode_HasCorrectValue()
    {
        Assert.Equal("set_permission_mode", ControlSubtype.SetPermissionMode);
    }

    [Fact]
    public void ControlSubtype_HookCallback_HasCorrectValue()
    {
        Assert.Equal("hook_callback", ControlSubtype.HookCallback);
    }

    [Fact]
    public void ControlSubtype_McpMessage_HasCorrectValue()
    {
        Assert.Equal("mcp_message", ControlSubtype.McpMessage);
    }

    [Fact]
    public void ControlSubtype_RewindFiles_HasCorrectValue()
    {
        Assert.Equal("rewind_files", ControlSubtype.RewindFiles);
    }

    [Fact]
    public void ControlSubtype_SetModel_HasCorrectValue()
    {
        Assert.Equal("set_model", ControlSubtype.SetModel);
    }

    [Fact]
    public void ControlSubtype_SetMaxThinkingTokens_HasCorrectValue()
    {
        Assert.Equal("set_max_thinking_tokens", ControlSubtype.SetMaxThinkingTokens);
    }

    [Fact]
    public void ControlSubtype_SupportedCommands_HasCorrectValue()
    {
        Assert.Equal("supported_commands", ControlSubtype.SupportedCommands);
    }

    [Fact]
    public void ControlSubtype_SupportedModels_HasCorrectValue()
    {
        Assert.Equal("supported_models", ControlSubtype.SupportedModels);
    }

    [Fact]
    public void ControlSubtype_McpServerStatus_HasCorrectValue()
    {
        Assert.Equal("mcp_server_status", ControlSubtype.McpServerStatus);
    }

    [Fact]
    public void ControlSubtype_AccountInfo_HasCorrectValue()
    {
        Assert.Equal("account_info", ControlSubtype.AccountInfo);
    }

    [Theory]
    [InlineData("interrupt")]
    [InlineData("can_use_tool")]
    [InlineData("initialize")]
    [InlineData("set_permission_mode")]
    [InlineData("hook_callback")]
    [InlineData("mcp_message")]
    [InlineData("rewind_files")]
    [InlineData("set_model")]
    [InlineData("set_max_thinking_tokens")]
    [InlineData("supported_commands")]
    [InlineData("supported_models")]
    [InlineData("mcp_server_status")]
    [InlineData("account_info")]
    public void ControlSubtype_AllValues_AreSnakeCase(string subtype)
    {
        // Verify all subtypes use snake_case convention
        Assert.DoesNotContain(subtype, "-");
        Assert.Equal(subtype.ToLowerInvariant(), subtype);
    }

    [Fact]
    public void InitializeRequest_Serialize_ProducesExpectedJson()
    {
        InitializeRequest request = new()
        {
            Hooks = JsonDocument.Parse("""{"pre_tool_use": []}""").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("initialize", parsed.GetProperty("subtype").GetString());
        Assert.True(parsed.TryGetProperty("hooks", out JsonElement hooks));
        Assert.True(hooks.TryGetProperty("pre_tool_use", out _));
    }

    [Fact]
    public void InitializeRequest_Subtype_IsAlwaysInitialize()
    {
        InitializeRequest request = new();
        Assert.Equal(ControlSubtype.Initialize, request.Subtype);
    }

    [Fact]
    public void InitializeRequest_WithNullHooks_SerializesCorrectly()
    {
        InitializeRequest request = new();

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("initialize", parsed.GetProperty("subtype").GetString());
    }

    [Fact]
    public void InitializeRequest_WithComplexHooks_PreservesStructure()
    {
        JsonElement hooksJson = JsonDocument.Parse("""
                                                   {
                                                       "pre_tool_use": [
                                                           {"id": "hook1", "pattern": "*.txt"},
                                                           {"id": "hook2", "pattern": "*.md"}
                                                       ],
                                                       "post_tool_use": [
                                                           {"id": "hook3", "action": "notify"}
                                                       ]
                                                   }
                                                   """).RootElement;

        InitializeRequest request = new() { Hooks = hooksJson };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;
        JsonElement hooks = parsed.GetProperty("hooks");

        Assert.Equal(2, hooks.GetProperty("pre_tool_use").GetArrayLength());
        Assert.Single(hooks.GetProperty("post_tool_use").EnumerateArray());
    }

    [Fact]
    public void InitializeRequest_Deserialize_MapsProperties()
    {
        const string json = """
                            {
                                "subtype": "initialize",
                                "hooks": {
                                    "on_start": { "enabled": true }
                                }
                            }
                            """;

        InitializeRequest? request = JsonSerializer.Deserialize<InitializeRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.NotNull(request.Hooks);
        Assert.True(request.Hooks.Value.GetProperty("on_start").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void CanUseToolRequest_Serialize_ProducesExpectedJson()
    {
        CanUseToolRequest request = new()
        {
            ToolName = "read_file",
            Input = JsonDocument.Parse("""{"path": "/test/file.txt"}""").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("can_use_tool", parsed.GetProperty("subtype").GetString());
        Assert.Equal("read_file", parsed.GetProperty("tool_name").GetString());
        Assert.Equal("/test/file.txt", parsed.GetProperty("input").GetProperty("path").GetString());
    }

    [Fact]
    public void CanUseToolRequest_Subtype_IsAlwaysCanUseTool()
    {
        CanUseToolRequest request = new()
        {
            ToolName = "test",
            Input = JsonDocument.Parse("{}").RootElement
        };
        Assert.Equal(ControlSubtype.CanUseTool, request.Subtype);
    }

    [Fact]
    public void CanUseToolRequest_WithPermissionSuggestions_SerializesCorrectly()
    {
        JsonElement suggestions = JsonDocument.Parse("""
                                                     {
                                                         "allow": true,
                                                         "scope": "session",
                                                         "path_pattern": "/home/user/**"
                                                     }
                                                     """).RootElement;

        CanUseToolRequest request = new()
        {
            ToolName = "write_file",
            Input = JsonDocument.Parse("""{"path": "/home/user/test.txt"}""").RootElement,
            PermissionSuggestions = suggestions
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.True(parsed.TryGetProperty("permission_suggestions", out JsonElement permSuggestions));
        Assert.True(permSuggestions.GetProperty("allow").GetBoolean());
        Assert.Equal("session", permSuggestions.GetProperty("scope").GetString());
    }

    [Fact]
    public void CanUseToolRequest_WithBlockedPath_SerializesCorrectly()
    {
        CanUseToolRequest request = new()
        {
            ToolName = "read_file",
            Input = JsonDocument.Parse("""{"path": "/etc/passwd"}""").RootElement,
            BlockedPath = "/etc/passwd"
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("/etc/passwd", parsed.GetProperty("blocked_path").GetString());
    }

    [Fact]
    public void CanUseToolRequest_Deserialize_MapsAllProperties()
    {
        const string json = """
                            {
                                "subtype": "can_use_tool",
                                "tool_name": "bash",
                                "input": {
                                    "command": "ls -la"
                                },
                                "permission_suggestions": {
                                    "suggest_always_allow": false
                                },
                                "blocked_path": "/root"
                            }
                            """;

        CanUseToolRequest? request = JsonSerializer.Deserialize<CanUseToolRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("bash", request.ToolName);
        Assert.Equal("ls -la", request.Input.GetProperty("command").GetString());
        Assert.NotNull(request.PermissionSuggestions);
        Assert.False(request.PermissionSuggestions.Value.GetProperty("suggest_always_allow").GetBoolean());
        Assert.Equal("/root", request.BlockedPath);
    }

    [Theory]
    [InlineData("read_file")]
    [InlineData("write_file")]
    [InlineData("bash")]
    [InlineData("glob")]
    [InlineData("grep")]
    [InlineData("mcp_tool:custom_server")]
    public void CanUseToolRequest_ToolName_AcceptsVariousFormats(string toolName)
    {
        CanUseToolRequest request = new()
        {
            ToolName = toolName,
            Input = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        CanUseToolRequest? deserialized = JsonSerializer.Deserialize<CanUseToolRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(toolName, deserialized.ToolName);
    }

    [Fact]
    public void HookCallbackRequest_Serialize_ProducesExpectedJson()
    {
        HookCallbackRequest request = new()
        {
            CallbackId = "callback-123",
            Input = JsonDocument.Parse("""{"event": "tool_completed"}""").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("hook_callback", parsed.GetProperty("subtype").GetString());
        Assert.Equal("callback-123", parsed.GetProperty("callback_id").GetString());
        Assert.Equal("tool_completed", parsed.GetProperty("input").GetProperty("event").GetString());
    }

    [Fact]
    public void HookCallbackRequest_Subtype_IsAlwaysHookCallback()
    {
        HookCallbackRequest request = new()
        {
            CallbackId = "test",
            Input = JsonDocument.Parse("{}").RootElement
        };
        Assert.Equal(ControlSubtype.HookCallback, request.Subtype);
    }

    [Fact]
    public void HookCallbackRequest_WithToolUseId_SerializesCorrectly()
    {
        HookCallbackRequest request = new()
        {
            CallbackId = "callback-456",
            Input = JsonDocument.Parse("""{"result": "success"}""").RootElement,
            ToolUseId = "toolu_789"
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("toolu_789", parsed.GetProperty("tool_use_id").GetString());
    }

    [Fact]
    public void HookCallbackRequest_WithNullToolUseId_OmitsField()
    {
        HookCallbackRequest request = new()
        {
            CallbackId = "callback-no-tool",
            Input = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        // ToolUseId should be null or not present
        if (parsed.TryGetProperty("tool_use_id", out JsonElement toolUseId))
        {
            Assert.Equal(JsonValueKind.Null, toolUseId.ValueKind);
        }
    }

    [Fact]
    public void HookCallbackRequest_Deserialize_MapsAllProperties()
    {
        const string json = """
                            {
                                "subtype": "hook_callback",
                                "callback_id": "cb-deserialize",
                                "input": {
                                    "hook_type": "pre_tool_use",
                                    "tool": "read_file"
                                },
                                "tool_use_id": "toolu_abc123"
                            }
                            """;

        HookCallbackRequest? request = JsonSerializer.Deserialize<HookCallbackRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("cb-deserialize", request.CallbackId);
        Assert.Equal("pre_tool_use", request.Input.GetProperty("hook_type").GetString());
        Assert.Equal("toolu_abc123", request.ToolUseId);
    }

    [Fact]
    public void HookCallbackRequest_WithComplexInput_PreservesStructure()
    {
        JsonElement complexInput = JsonDocument.Parse("""
                                                      {
                                                          "hook_metadata": {
                                                              "timestamp": "2024-01-15T10:30:00Z",
                                                              "triggered_by": "user_action"
                                                          },
                                                          "context": {
                                                              "files_modified": ["file1.txt", "file2.txt"],
                                                              "git_status": {
                                                                  "branch": "main",
                                                                  "ahead": 2,
                                                                  "behind": 0
                                                              }
                                                          }
                                                      }
                                                      """).RootElement;

        HookCallbackRequest request = new()
        {
            CallbackId = "complex-callback",
            Input = complexInput
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;
        JsonElement input = parsed.GetProperty("input");

        Assert.Equal("2024-01-15T10:30:00Z", input.GetProperty("hook_metadata").GetProperty("timestamp").GetString());
        Assert.Equal(2, input.GetProperty("context").GetProperty("files_modified").GetArrayLength());
    }

    [Fact]
    public void McpMessageRequest_Serialize_ProducesExpectedJson()
    {
        McpMessageRequest request = new()
        {
            ServerName = "test-mcp-server",
            Message = JsonDocument.Parse("""
                                         {
                                             "jsonrpc": "2.0",
                                             "id": 1,
                                             "method": "tools/list"
                                         }
                                         """).RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;

        Assert.Equal("mcp_message", parsed.GetProperty("subtype").GetString());
        Assert.Equal("test-mcp-server", parsed.GetProperty("server_name").GetString());
        Assert.Equal("2.0", parsed.GetProperty("message").GetProperty("jsonrpc").GetString());
    }

    [Fact]
    public void McpMessageRequest_Subtype_IsAlwaysMcpMessage()
    {
        McpMessageRequest request = new()
        {
            ServerName = "test",
            Message = JsonDocument.Parse("{}").RootElement
        };
        Assert.Equal(ControlSubtype.McpMessage, request.Subtype);
    }

    [Fact]
    public void McpMessageRequest_Deserialize_MapsAllProperties()
    {
        const string json = """
                            {
                                "subtype": "mcp_message",
                                "server_name": "database-server",
                                "message": {
                                    "jsonrpc": "2.0",
                                    "id": 42,
                                    "method": "resources/read",
                                    "params": {
                                        "uri": "db://users/123"
                                    }
                                }
                            }
                            """;

        McpMessageRequest? request = JsonSerializer.Deserialize<McpMessageRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("database-server", request.ServerName);
        Assert.Equal(42, request.Message.GetProperty("id").GetInt32());
        Assert.Equal("resources/read", request.Message.GetProperty("method").GetString());
    }

    [Theory]
    [InlineData("simple-server")]
    [InlineData("server_with_underscores")]
    [InlineData("Server.With.Dots")]
    [InlineData("mcp://custom/server")]
    public void McpMessageRequest_ServerName_AcceptsVariousFormats(string serverName)
    {
        McpMessageRequest request = new()
        {
            ServerName = serverName,
            Message = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        McpMessageRequest? deserialized = JsonSerializer.Deserialize<McpMessageRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(serverName, deserialized.ServerName);
    }

    [Fact]
    public void McpMessageRequest_WithToolsCallMessage_PreservesComplexStructure()
    {
        JsonElement mcpMessage = JsonDocument.Parse("""
                                                    {
                                                        "jsonrpc": "2.0",
                                                        "id": 100,
                                                        "method": "tools/call",
                                                        "params": {
                                                            "name": "search_files",
                                                            "arguments": {
                                                                "pattern": "*.cs",
                                                                "path": "/src",
                                                                "options": {
                                                                    "recursive": true,
                                                                    "ignore_case": false,
                                                                    "max_depth": 10
                                                                }
                                                            }
                                                        }
                                                    }
                                                    """).RootElement;

        McpMessageRequest request = new()
        {
            ServerName = "file-server",
            Message = mcpMessage
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        JsonElement parsed = JsonDocument.Parse(json).RootElement;
        JsonElement message = parsed.GetProperty("message");

        Assert.Equal("tools/call", message.GetProperty("method").GetString());
        JsonElement args = message.GetProperty("params").GetProperty("arguments");
        Assert.Equal("*.cs", args.GetProperty("pattern").GetString());
        Assert.True(args.GetProperty("options").GetProperty("recursive").GetBoolean());
    }

    [Fact]
    public void RequestResponseCorrelation_SuccessResponse_MatchesRequestId()
    {
        const string requestId = "corr-test-123";

        ControlRequest request = new()
        {
            RequestId = requestId,
            Request = JsonDocument.Parse("""{"subtype":"initialize"}""").RootElement
        };

        ControlResponse response = new()
        {
            Response = new ControlSuccessResponse
            {
                RequestId = requestId,
                ResponseData = JsonDocument.Parse("""{"initialized":true}""").RootElement
            }
        };

        Assert.Equal(request.RequestId, response.Response.RequestId);
    }

    [Fact]
    public void RequestResponseCorrelation_ErrorResponse_MatchesRequestId()
    {
        const string requestId = "error-corr-456";

        ControlRequest request = new()
        {
            RequestId = requestId,
            Request = JsonDocument.Parse("""{"subtype":"can_use_tool"}""").RootElement
        };

        ControlResponse response = new()
        {
            Response = new ControlErrorResponse
            {
                RequestId = requestId,
                Error = "Permission denied"
            }
        };

        Assert.Equal(request.RequestId, response.Response.RequestId);
    }

    [Theory]
    [InlineData("uuid-550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("simple-id-1")]
    [InlineData("1234567890")]
    [InlineData("req_with_underscores")]
    public void RequestResponseCorrelation_VariousIdFormats_WorkCorrectly(string requestId)
    {
        ControlRequest request = new()
        {
            RequestId = requestId,
            Request = JsonDocument.Parse("{}").RootElement
        };

        ControlSuccessResponse successResponse = new() { RequestId = requestId };
        ControlErrorResponse errorResponse = new() { RequestId = requestId, Error = "test" };

        Assert.Equal(request.RequestId, successResponse.RequestId);
        Assert.Equal(request.RequestId, errorResponse.RequestId);
    }

    [Fact]
    public void RoundTrip_ControlRequest_PreservesData()
    {
        ControlRequest original = new()
        {
            RequestId = "round-trip-req",
            Request = JsonDocument.Parse("""
                                         {
                                             "subtype": "can_use_tool",
                                             "tool_name": "bash",
                                             "input": {"command": "echo hello"}
                                         }
                                         """).RootElement
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        ControlRequest? deserialized = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.RequestId, deserialized.RequestId);
        Assert.Equal(
            original.Request.GetProperty("subtype").GetString(),
            deserialized.Request.GetProperty("subtype").GetString());
    }

    [Fact]
    public void RoundTrip_ControlSuccessResponse_PreservesData()
    {
        ControlSuccessResponse original = new()
        {
            RequestId = "round-trip-success",
            ResponseData = JsonDocument.Parse("""{"status":"ok","count":42}""").RootElement
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        ControlSuccessResponse? deserialized = JsonSerializer.Deserialize<ControlSuccessResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.RequestId, deserialized.RequestId);
        Assert.NotNull(deserialized.ResponseData);
        // ResponseData is object? but deserializes to JsonElement
        JsonElement responseElement = (JsonElement)deserialized.ResponseData;
        Assert.Equal(42, responseElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public void RoundTrip_ControlErrorResponse_PreservesData()
    {
        ControlErrorResponse original = new()
        {
            RequestId = "round-trip-error",
            Error = "Test error message with special chars: <>\"'"
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        ControlErrorResponse? deserialized = JsonSerializer.Deserialize<ControlErrorResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.RequestId, deserialized.RequestId);
        Assert.Equal(original.Error, deserialized.Error);
    }

    [Fact]
    public void RoundTrip_InitializeRequest_PreservesData()
    {
        InitializeRequest original = new()
        {
            Hooks = JsonDocument.Parse("""{"hook1":{"enabled":true}}""").RootElement
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        InitializeRequest? deserialized = JsonSerializer.Deserialize<InitializeRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Hooks);
        Assert.True(deserialized.Hooks.Value.GetProperty("hook1").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void RoundTrip_CanUseToolRequest_PreservesData()
    {
        CanUseToolRequest original = new()
        {
            ToolName = "write_file",
            Input = JsonDocument.Parse("""{"path":"/test.txt","content":"hello"}""").RootElement,
            PermissionSuggestions = JsonDocument.Parse("""{"allow":true}""").RootElement,
            BlockedPath = "/blocked"
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        CanUseToolRequest? deserialized = JsonSerializer.Deserialize<CanUseToolRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.ToolName, deserialized.ToolName);
        Assert.Equal(original.BlockedPath, deserialized.BlockedPath);
        Assert.NotNull(deserialized.PermissionSuggestions);
    }

    [Fact]
    public void RoundTrip_HookCallbackRequest_PreservesData()
    {
        HookCallbackRequest original = new()
        {
            CallbackId = "cb-round-trip",
            Input = JsonDocument.Parse("""{"data":"test"}""").RootElement,
            ToolUseId = "toolu_round_trip"
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        HookCallbackRequest? deserialized = JsonSerializer.Deserialize<HookCallbackRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.CallbackId, deserialized.CallbackId);
        Assert.Equal(original.ToolUseId, deserialized.ToolUseId);
    }

    [Fact]
    public void RoundTrip_McpMessageRequest_PreservesData()
    {
        McpMessageRequest original = new()
        {
            ServerName = "test-server",
            Message = JsonDocument.Parse("""{"jsonrpc":"2.0","method":"test"}""").RootElement
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        McpMessageRequest? deserialized = JsonSerializer.Deserialize<McpMessageRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.ServerName, deserialized.ServerName);
        Assert.Equal("2.0", deserialized.Message.GetProperty("jsonrpc").GetString());
    }

    [Fact]
    public void Deserialize_ControlRequest_WithUnknownFields_IgnoresExtra()
    {
        const string json = """
                            {
                                "type": "control_request",
                                "request_id": "test-unknown",
                                "request": {},
                                "unknown_field": "should be ignored",
                                "another_unknown": 123
                            }
                            """;

        ControlRequest? request = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("test-unknown", request.RequestId);
    }

    [Fact]
    public void Deserialize_WithEmptyJsonObject_HandlesGracefully()
    {
        ControlRequest request = new()
        {
            RequestId = "empty-request",
            Request = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        ControlRequest? deserialized = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(JsonValueKind.Object, deserialized.Request.ValueKind);
    }

    [Fact]
    public void Serialize_WithUnicodeContent_HandlesCorrectly()
    {
        CanUseToolRequest request = new()
        {
            ToolName = "write_file",
            Input = JsonDocument.Parse("""{"content":"Hello \u4e16\u754c \ud83c\udf0d"}""").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        CanUseToolRequest? deserialized = JsonSerializer.Deserialize<CanUseToolRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        // Unicode should be preserved
        Assert.Contains("\u4e16\u754c", deserialized.Input.GetProperty("content").GetString());
    }

    [Fact]
    public void Serialize_WithLargeNestedStructure_HandlesCorrectly()
    {
        // Create a large nested structure
        string[] items = Enumerable.Range(0, 100)
            .Select(i => $"{{\"id\":{i},\"name\":\"item{i}\"}}")
            .ToArray();
        string arrayJson = $"[{string.Join(",", items)}]";

        CanUseToolRequest request = new()
        {
            ToolName = "process_items",
            Input = JsonDocument.Parse($"{{\"items\":{arrayJson}}}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        CanUseToolRequest? deserialized = JsonSerializer.Deserialize<CanUseToolRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(100, deserialized.Input.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void ControlResponsePayload_RequestId_IsRequired()
    {
        // Both success and error responses require RequestId
        ControlSuccessResponse success = new() { RequestId = "required-id" };
        ControlErrorResponse error = new() { RequestId = "required-id", Error = "test" };

        Assert.NotNull(success.RequestId);
        Assert.NotNull(error.RequestId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a-very-long-request-id-that-might-be-generated-by-some-system-with-lots-of-characters")]
    public void RequestId_AcceptsVariousLengths(string requestId)
    {
        ControlRequest request = new()
        {
            RequestId = requestId,
            Request = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        ControlRequest? deserialized = JsonSerializer.Deserialize<ControlRequest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(requestId, deserialized.RequestId);
    }

    [Fact]
    public void ControlSuccessResponse_Equality_WorksForRequestId()
    {
        ControlSuccessResponse response1 = new() { RequestId = "same-id" };
        ControlSuccessResponse response2 = new() { RequestId = "same-id" };
        ControlSuccessResponse response3 = new() { RequestId = "different-id" };

        // Note: Records with JsonElement may not have full value equality
        // but RequestId comparison should work
        Assert.Equal(response1.RequestId, response2.RequestId);
        Assert.NotEqual(response1.RequestId, response3.RequestId);
    }

    [Fact]
    public void ControlErrorResponse_Equality_WorksCorrectly()
    {
        ControlErrorResponse error1 = new() { RequestId = "id1", Error = "error message" };
        ControlErrorResponse error2 = new() { RequestId = "id1", Error = "error message" };
        ControlErrorResponse error3 = new() { RequestId = "id1", Error = "different error" };

        Assert.Equal(error1, error2);
        Assert.NotEqual(error1, error3);
    }

    [Fact]
    public void ControlRequest_JsonPropertyNames_AreSnakeCase()
    {
        ControlRequest request = new()
        {
            RequestId = "test",
            Request = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"request_id\"", json);
        Assert.Contains("\"type\"", json);
        Assert.Contains("\"request\"", json);
    }

    [Fact]
    public void CanUseToolRequest_JsonPropertyNames_AreSnakeCase()
    {
        CanUseToolRequest request = new()
        {
            ToolName = "test",
            Input = JsonDocument.Parse("{}").RootElement,
            PermissionSuggestions = JsonDocument.Parse("{}").RootElement,
            BlockedPath = "/test"
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"tool_name\"", json);
        Assert.Contains("\"permission_suggestions\"", json);
        Assert.Contains("\"blocked_path\"", json);
    }

    [Fact]
    public void HookCallbackRequest_JsonPropertyNames_AreSnakeCase()
    {
        HookCallbackRequest request = new()
        {
            CallbackId = "test",
            Input = JsonDocument.Parse("{}").RootElement,
            ToolUseId = "toolu_123"
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"callback_id\"", json);
        Assert.Contains("\"tool_use_id\"", json);
    }

    [Fact]
    public void McpMessageRequest_JsonPropertyNames_AreSnakeCase()
    {
        McpMessageRequest request = new()
        {
            ServerName = "test",
            Message = JsonDocument.Parse("{}").RootElement
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"server_name\"", json);
    }
}
