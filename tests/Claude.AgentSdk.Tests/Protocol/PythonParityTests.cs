using System.Text.Json;
using System.Text.Json.Nodes;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Extensions;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Tools;
using Claude.AgentSdk.Transport;
using SdkMockTransport = Claude.AgentSdk.Transport.MockTransport;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Tests for behavior ported from the Python SDK (v0.1.33 → v0.2.160).
/// </summary>
public class PythonParityMessageParsingTests
{
    private static async Task<List<Message>> ParseAsync(params string[] lines)
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions());
        await handler.StartAsync();

        foreach (var line in lines)
        {
            transport.EnqueueMessage(line);
        }

        transport.CompleteMessages();

        var messages = new List<Message>();
        await foreach (var message in handler.ReceiveMessagesAsync())
        {
            messages.Add(message);
        }

        return messages;
    }

    [Fact]
    public async Task AssistantMessage_ReadsTopLevelFieldsAndServerToolBlocks()
    {
        var messages = await ParseAsync("""
                                        {"type":"assistant","uuid":"u-1","session_id":"s-1","parent_tool_use_id":"toolu_parent","error":"rate_limit",
                                         "message":{"id":"msg_1","model":"claude-opus-5","stop_reason":"end_turn",
                                           "usage":{"input_tokens":10,"output_tokens":5},
                                           "content":[
                                             {"type":"text","text":"hi"},
                                             {"type":"redacted_thinking","data":"xyz"},
                                             {"type":"server_tool_use","id":"srv_1","name":"web_search","input":{"q":"x"}},
                                             {"type":"advisor_tool_result","tool_use_id":"srv_1","content":{"type":"advice","text":"ok"}}
                                           ]}}
                                        """);

        var assistant = Assert.IsType<AssistantMessage>(Assert.Single(messages));
        Assert.Equal("u-1", assistant.Uuid);
        Assert.Equal("s-1", assistant.SessionId);
        Assert.Equal("toolu_parent", assistant.ParentToolUseId);
        Assert.Equal("rate_limit", assistant.Error);
        Assert.Equal("msg_1", assistant.MessageContent.Id);
        Assert.Equal("end_turn", assistant.MessageContent.StopReason);
        Assert.Equal(10, assistant.MessageContent.Usage!.InputTokens);

        // Unknown block types are skipped without dropping the message
        Assert.Equal(3, assistant.MessageContent.Content.Count);
        var serverTool = Assert.IsType<ServerToolUseBlock>(assistant.MessageContent.Content[1]);
        Assert.Equal("web_search", serverTool.Name);
        var serverResult = Assert.IsType<ServerToolResultBlock>(assistant.MessageContent.Content[2]);
        Assert.Equal("advice", serverResult.Content.GetProperty("type").GetString());

        Assert.Equal(JsonValueKind.Object, assistant.RawJson.ValueKind);
    }

    [Fact]
    public async Task UserMessage_ReadsTopLevelFieldsAndOrigin()
    {
        var messages = await ParseAsync(
            """
            {"type":"user","uuid":"u-2","parent_tool_use_id":"toolu_p","tool_use_result":{"stdout":"ok"},
             "origin":{"kind":"task-notification","subkind":"scheduled-trigger","futureField":42},
             "message":{"role":"user","content":"hello"}}
            """,
            """{"type":"user","origin":"not-an-object","message":{"role":"user","content":"x"}}""");

        var user = Assert.IsType<UserMessage>(messages[0]);
        Assert.Equal("u-2", user.Uuid);
        Assert.Equal("toolu_p", user.ParentToolUseId);
        Assert.Equal("ok", user.ToolUseResult!.Value.GetProperty("stdout").GetString());
        Assert.Equal("task-notification", user.Origin!.Kind);
        Assert.Equal("scheduled-trigger", user.Origin.Subkind);
        Assert.False(user.Origin.IsHuman);
        Assert.Equal(42, user.Origin.AdditionalProperties!["futureField"].GetInt32());

        // A malformed origin is treated as absent rather than failing the message
        var malformed = Assert.IsType<UserMessage>(messages[1]);
        Assert.Null(malformed.Origin);
    }

    [Fact]
    public async Task ResultMessage_ReadsNewFields()
    {
        var messages = await ParseAsync("""
                                        {"type":"result","subtype":"error_max_turns","duration_ms":1,"duration_api_ms":1,"is_error":true,
                                         "num_turns":3,"session_id":"s","uuid":"r-1","terminal_reason":"max_turns","api_error_status":529,
                                         "errors":["Reached maximum number of turns"],
                                         "permission_denials":[{"tool_name":"Bash"}],
                                         "deferred_tool_use":{"id":"toolu_d","name":"Bash","input":{"command":"ls"}},
                                         "origin":{"kind":"human"},
                                         "modelUsage":{"claude-opus-5":{"inputTokens":10,"outputTokens":2,"cacheReadInputTokens":0,
                                           "cacheCreationInputTokens":0,"webSearchRequests":1,"costUSD":0.5,"contextWindow":200000,
                                           "maxOutputTokens":32000,"canonicalModel":"claude-opus-5","provider":"firstParty"}}}
                                        """);

        var result = Assert.IsType<ResultMessage>(Assert.Single(messages));
        Assert.Equal("r-1", result.Uuid);
        Assert.Equal("max_turns", result.TerminalReason);
        Assert.Equal(529, result.ApiErrorStatus);
        Assert.Equal("Reached maximum number of turns", Assert.Single(result.Errors!));
        Assert.Equal("toolu_d", result.DeferredToolUse!.Id);
        Assert.Equal("ls", result.DeferredToolUse.Input.GetProperty("command").GetString());
        Assert.True(result.Origin!.IsHuman);
        Assert.Equal(JsonValueKind.Array, result.PermissionDenials!.Value.ValueKind);

        var usage = result.ModelUsage!["claude-opus-5"];
        Assert.Equal(0.5, usage.CostUsd);
        Assert.Equal(200000, usage.ContextWindow);
        Assert.Equal("firstParty", usage.Provider);
    }

    [Fact]
    public async Task RateLimitAndConversationReset_AreParsed_UnknownTypesSkipped()
    {
        var messages = await ParseAsync(
            """
            {"type":"rate_limit_event","uuid":"u","session_id":"s",
             "rate_limit_info":{"status":"allowed_warning","resetsAt":1700000000,"rateLimitType":"five_hour","utilization":0.9,"extra":true}}
            """,
            """{"type":"brand_new_message_type","foo":1}""",
            """{"type":"conversation_reset","new_conversation_id":"c2","uuid":"u2","session_id":"s"}""");

        Assert.Equal(2, messages.Count);
        var rateLimit = Assert.IsType<RateLimitEvent>(messages[0]);
        Assert.Equal("allowed_warning", rateLimit.RateLimitInfo.Status);
        Assert.Equal(1700000000, rateLimit.RateLimitInfo.ResetsAt);
        Assert.Equal("five_hour", rateLimit.RateLimitInfo.RateLimitType);
        Assert.True(rateLimit.RateLimitInfo.AdditionalProperties!["extra"].GetBoolean());

        var reset = Assert.IsType<ConversationResetMessage>(messages[1]);
        Assert.Equal("c2", reset.NewConversationId);
    }

    [Fact]
    public async Task SystemMessage_GetData_ReadsTopLevelTaskFields()
    {
        var messages = await ParseAsync(
            """{"type":"system","subtype":"task_started","task_id":"t1","description":"d","uuid":"u","session_id":"s","task_type":"local_agent","tool_use_id":"toolu_1"}""",
            """{"type":"system","subtype":"task_progress","task_id":"t1","description":"d","uuid":"u","session_id":"s","usage":{"total_tokens":5,"tool_uses":1,"duration_ms":10},"last_tool_name":"Read"}""",
            """{"type":"system","subtype":"task_updated","task_id":"t1","patch":{"status":"killed"}}""");

        var started = ((SystemMessage)messages[0]).GetData<TaskStartedData>()!;
        Assert.Equal("t1", started.TaskId);
        Assert.Equal("local_agent", started.TaskType);
        Assert.Equal(SystemMessageSubtypeValue("task_started"), ((SystemMessage)messages[0]).SubtypeEnum);

        var progress = ((SystemMessage)messages[1]).GetData<TaskProgressData>()!;
        Assert.Equal(5, progress.Usage!.TotalTokens);
        Assert.Equal("Read", progress.LastToolName);

        var updated = ((SystemMessage)messages[2]).GetData<TaskUpdatedData>()!;
        Assert.Equal("killed", updated.Status);
        Assert.True(TaskStatuses.IsTerminal(updated.Status));
        Assert.False(TaskStatuses.IsTerminal("running"));
    }

    [Fact]
    public async Task SessionStateChanged_SdkHostOnlyFramesAreNotYielded()
    {
        var messages = await ParseAsync(
            """{"type":"system","subtype":"session_state_changed","state":"running","sdk_host_only":true}""",
            """{"type":"system","subtype":"session_state_changed","state":"idle"}""");

        var visible = Assert.IsType<SystemMessage>(Assert.Single(messages));
        Assert.Equal("idle", visible.GetData<SessionStateChangedData>()!.State);
    }

    private static Claude.AgentSdk.Types.SystemMessageSubtype SystemMessageSubtypeValue(string value)
    {
        return Claude.AgentSdk.Types.EnumStringMappings.ParseSystemMessageSubtype(value);
    }
}

public class PythonParityProtocolTests
{
    private static async Task InitializeAsync(QueryHandler handler, SdkMockTransport transport,
        string responseJson = "{}")
    {
        var initTask = handler.InitializeAsync();
        var request = await WaitForWrittenAsync(transport, m =>
            m.TryGetProperty("request", out var r) && r.GetProperty("subtype").GetString() == "initialize");
        transport.EnqueueMessage(
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" +
            request.GetProperty("request_id").GetString() + "\",\"response\":" + responseJson + "}}");
        await initTask;
    }

    private static async Task<JsonElement> WaitForWrittenAsync(SdkMockTransport transport,
        Func<JsonElement, bool> predicate)
    {
        for (var i = 0; i < 300; i++)
        {
            foreach (var message in transport.WrittenMessages)
            {
                if (predicate(message))
                {
                    return message;
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Expected message was not written");
    }

    [Fact]
    public async Task Initialize_SendsAgentsSkillsAndSessionFlags()
    {
        var transport = new SdkMockTransport();
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["reviewer"] = new()
                {
                    Description = "Reviews code",
                    Prompt = "You review code",
                    DisallowedTools = ["Bash"],
                    MaxTurns = 4
                }
            },
            Skills = SkillsConfig.Only("pdf"),
            ForwardSubagentText = true,
            SystemPrompt = new PresetSystemPrompt
                { Preset = "claude_code", ExcludeDynamicSections = true, Snapshot = false }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();
        await InitializeAsync(handler, transport);

        var request = transport.WrittenMessages[0].GetProperty("request");
        Assert.Equal(JsonValueKind.Null, request.GetProperty("hooks").ValueKind);
        var agent = request.GetProperty("agents").GetProperty("reviewer");
        Assert.Equal("You review code", agent.GetProperty("prompt").GetString());
        Assert.Equal("Bash", agent.GetProperty("disallowedTools")[0].GetString());
        Assert.Equal(4, agent.GetProperty("maxTurns").GetInt32());
        Assert.Equal("pdf", request.GetProperty("skills")[0].GetString());
        Assert.True(request.GetProperty("forwardSubagentText").GetBoolean());
        Assert.True(request.GetProperty("excludeDynamicSections").GetBoolean());
        Assert.False(request.GetProperty("systemPromptSnapshot").GetBoolean());
    }

    [Fact]
    public async Task Initialize_WithAllSkills_OmitsSkillsField()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions { Skills = SkillsConfig.All });
        await handler.StartAsync();
        await InitializeAsync(handler, transport);

        var request = transport.WrittenMessages[0].GetProperty("request");
        Assert.False(request.TryGetProperty("skills", out _));
        Assert.False(request.TryGetProperty("agents", out _));
        Assert.False(request.TryGetProperty("forwardSubagentText", out _));
    }

    [Fact]
    public async Task InitializationResult_ServesCommandsWithoutAnotherRequest()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions());
        await handler.StartAsync();
        await InitializeAsync(handler, transport,
            """{"commands":[{"name":"help","description":"Show help","argumentHint":""}],"models":[]}""");

        var commands = await handler.GetSupportedCommandsAsync();

        Assert.Equal("help", commands[0].GetProperty("name").GetString());
        Assert.Single(transport.WrittenMessages); // only the initialize request
        Assert.Equal(JsonValueKind.Object, handler.InitializationResult!.Value.ValueKind);
    }

    [Fact]
    public async Task ControlErrorResponse_ThrowsControlRequestException()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions());
        await handler.StartAsync();

        var task = handler.GetContextUsageAsync();
        var request = await WaitForWrittenAsync(transport, m => m.TryGetProperty("request_id", out _));
        Assert.Equal("get_context_usage", request.GetProperty("request").GetProperty("subtype").GetString());

        transport.EnqueueMessage(
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"error\",\"request_id\":\"" +
            request.GetProperty("request_id").GetString() + "\",\"error\":\"not supported\"}}");

        var ex = await Assert.ThrowsAsync<ControlRequestException>(() => task);
        Assert.Equal("not supported", ex.Message);
        Assert.Equal("get_context_usage", ex.Subtype);
    }

    [Fact]
    public async Task ControlRequests_UseCorrectWireShapes()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions
        {
            ControlRequestTimeout = TimeSpan.FromMilliseconds(200)
        });
        await handler.StartAsync();

        await Assert.ThrowsAsync<ControlTimeoutException>(() => handler.ReconnectMcpServerAsync("srv"));
        await Assert.ThrowsAsync<ControlTimeoutException>(() => handler.ToggleMcpServerAsync("srv", false));
        await Assert.ThrowsAsync<ControlTimeoutException>(() => handler.StopTaskAsync("task-1"));
        await Assert.ThrowsAsync<ControlTimeoutException>(() => handler.SetModelAsync(null));
        await Assert.ThrowsAsync<ControlTimeoutException>(() =>
            handler.SetPermissionModeAsync(PermissionMode.DontAsk));

        var requests = transport.WrittenMessages.Select(m => m.GetProperty("request")).ToList();
        Assert.Equal("mcp_reconnect", requests[0].GetProperty("subtype").GetString());
        Assert.Equal("srv", requests[0].GetProperty("serverName").GetString());
        Assert.Equal("mcp_toggle", requests[1].GetProperty("subtype").GetString());
        Assert.False(requests[1].GetProperty("enabled").GetBoolean());
        Assert.Equal("stop_task", requests[2].GetProperty("subtype").GetString());
        Assert.Equal("task-1", requests[2].GetProperty("task_id").GetString());
        Assert.Equal(JsonValueKind.Null, requests[3].GetProperty("model").ValueKind);
        Assert.Equal("dontAsk", requests[4].GetProperty("mode").GetString());
    }

    [Fact]
    public async Task ControlCancelRequest_CancelsInFlightHookWithoutResponding()
    {
        var hookStarted = new TaskCompletionSource();
        var hookCancelled = new TaskCompletionSource();
        HookCallback callback = async (_, _, _, ct) =>
        {
            hookStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                hookCancelled.TrySetResult();
                throw;
            }

            return new SyncHookOutput();
        };

        var transport = new SdkMockTransport();
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = [new HookMatcher { Hooks = [callback] }]
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();
        await InitializeAsync(handler, transport);

        transport.EnqueueMessage("""
                                 {"type":"control_request","request_id":"slow","request":{"subtype":"hook_callback","callback_id":"hook_0",
                                  "input":{"hook_event_name":"PreToolUse","session_id":"s","transcript_path":"t","cwd":"c","tool_name":"Bash","tool_input":{}}}}
                                 """);
        await hookStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The read loop is not blocked by the slow hook: a cancel request is processed immediately
        transport.EnqueueMessage("""{"type":"control_cancel_request","request_id":"slow"}""");
        await hookCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        Assert.DoesNotContain(transport.WrittenMessages, m =>
            m.TryGetProperty("response", out var r) && r.TryGetProperty("request_id", out var id) &&
            id.GetString() == "slow");
    }

    [Fact]
    public async Task VerbatimPrompts_StampsClientComposed()
    {
        var transport = new SdkMockTransport();
        await using (var handler = new QueryHandler(transport, new ClaudeAgentOptions { VerbatimPrompts = true }))
        {
            await handler.StartAsync();
            await handler.SendUserMessageAsync("@/etc/passwd please");
        }

        var plain = new SdkMockTransport();
        await using (var handler = new QueryHandler(plain, new ClaudeAgentOptions()))
        {
            await handler.StartAsync();
            await handler.SendUserMessageAsync("hello");
        }

        Assert.True(transport.WrittenMessages[0].GetProperty("client_composed").GetBoolean());
        Assert.False(plain.WrittenMessages[0].TryGetProperty("client_composed", out _));
    }

    [Fact]
    public async Task McpNotification_IsAcknowledgedWithEmptyResult()
    {
        var server = new McpToolServer("tools");
        var transport = new SdkMockTransport();
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["tools"] = new McpSdkServerConfig { Name = "tools", Instance = server }
            }
        };

        await using var handler = new QueryHandler(transport, options);
        await handler.StartAsync();

        transport.EnqueueMessage("""
                                 {"type":"control_request","request_id":"n1","request":{"subtype":"mcp_message","server_name":"tools",
                                  "message":{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":3}}}}
                                 """);

        var response = await WaitForWrittenAsync(transport, m =>
            m.TryGetProperty("response", out var r) && r.GetProperty("request_id").GetString() == "n1");
        var mcpResponse = response.GetProperty("response").GetProperty("response").GetProperty("mcp_response");
        Assert.Equal(JsonValueKind.Object, mcpResponse.GetProperty("result").ValueKind);
        Assert.False(mcpResponse.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task McpToolsList_IncludesMaxResultSizeCharsMeta()
    {
        var server = new McpToolServer("tools");
        server.RegisterTool(new ToolDefinition
        {
            Name = "big",
            Description = "Returns a lot",
            InputSchema = new JsonObject { ["type"] = "object" },
            Handler = (_, _) => Task.FromResult(ToolResult.Image("AAAA", "image/png")),
            Annotations = new ToolAnnotations { ReadOnlyHint = true, MaxResultSizeChars = 500_000 }
        });

        var list = await server.HandleRequestAsync(
            JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""").RootElement);
        var listJson = JsonSerializer.SerializeToElement(list);
        var tool = listJson.GetProperty("result").GetProperty("tools")[0];
        Assert.Equal(500_000, tool.GetProperty("_meta").GetProperty("anthropic/maxResultSizeChars").GetInt32());

        var call = await server.HandleRequestAsync(JsonDocument.Parse(
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"big","arguments":{}}}""").RootElement);
        var content = JsonSerializer.SerializeToElement(call).GetProperty("result").GetProperty("content")[0];
        Assert.Equal("image", content.GetProperty("type").GetString());
        Assert.Equal("image/png", content.GetProperty("mimeType").GetString());
    }

    [Fact]
    public async Task OneShot_WithoutBidirectionalNeeds_CompletesAtResult()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions()) { CompleteOnResult = true };
        await handler.StartAsync();

        transport.EnqueueMessage(
            """{"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"s"}""");

        var messages = new List<Message>();
        await Task.Run(async () =>
        {
            await foreach (var message in handler.ReceiveMessagesAsync())
            {
                messages.Add(message);
            }
        }).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<ResultMessage>(Assert.Single(messages));
        await transport.WaitForInputEndedAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OneShot_WithHooks_WaitsForIdleBeforeCompleting()
    {
        HookCallback callback = (_, _, _, _) => Task.FromResult<HookOutput>(new SyncHookOutput());
        var transport = new SdkMockTransport();
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = [new HookMatcher { Hooks = [callback] }]
            }
        };

        await using var handler = new QueryHandler(transport, options) { CompleteOnResult = true };
        await handler.StartAsync();

        var received = new List<Message>();
        var consumer = Task.Run(async () =>
        {
            await foreach (var message in handler.ReceiveMessagesAsync())
            {
                received.Add(message);
            }
        });

        transport.EnqueueMessage(
            """{"type":"system","subtype":"session_state_changed","state":"running","sdk_host_only":true}""");
        transport.EnqueueMessage(
            """{"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"s"}""");

        // A background agent finished just before the result, so the CLI owes a follow-up turn.
        await Task.Delay(200);
        Assert.False(consumer.IsCompleted);
        Assert.False(transport.InputEnded);

        transport.EnqueueMessage(
            """{"type":"assistant","message":{"model":"m","content":[{"type":"text","text":"follow-up"}]}}""");
        transport.EnqueueMessage(
            """{"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":2,"session_id":"s"}""");
        transport.EnqueueMessage(
            """{"type":"system","subtype":"session_state_changed","state":"idle","sdk_host_only":true}""");

        await consumer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, received.Count);
        Assert.IsType<AssistantMessage>(received[1]);
        await transport.WaitForInputEndedAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessError_AfterErrorResult_BecomesResultException()
    {
        var transport = new SdkMockTransport();
        await using var handler = new QueryHandler(transport, new ClaudeAgentOptions());
        await handler.StartAsync();

        transport.EnqueueMessage("""
                                 {"type":"result","subtype":"error_max_turns","duration_ms":1,"duration_api_ms":1,"is_error":true,
                                  "num_turns":2,"session_id":"sess-9","errors":["Reached maximum number of turns (2)"]}
                                 """);
        transport.CompleteMessagesWithError(new CliProcessException("Command failed with exit code 1", 1));

        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in handler.ReceiveMessagesAsync())
            {
            }
        });

        var resultException = Assert.IsType<ResultException>(ex);
        Assert.Equal("error_max_turns", resultException.Subtype);
        Assert.Equal("sess-9", resultException.SessionId);
        Assert.Equal(1, resultException.ExitCode);
        Assert.Contains("Reached maximum number of turns (2)", resultException.Message);
        Assert.Same(resultException, handler.TerminalException);
    }

    [Fact]
    public void ResultException_DescribeErrorResult_PrefersMostInformativeText()
    {
        static string Describe(string json) => ResultException.DescribeErrorResult(JsonDocument.Parse(json).RootElement);

        Assert.Equal("a; b", Describe("""{"errors":["a","b"],"result":"r"}"""));
        Assert.Equal("API Error: overloaded", Describe("""{"subtype":"success","errors":[],"result":" API Error: overloaded "}"""));
        Assert.Equal("error_during_execution", Describe("""{"subtype":"error_during_execution"}"""));
        Assert.Equal("API error (HTTP 529)", Describe("""{"subtype":"success","api_error_status":529}"""));
        Assert.Equal("unknown error", Describe("""{"subtype":"success"}"""));
    }

    [Fact]
    public void PermissionUpdate_WireFormat_RoundTrips()
    {
        var update = new PermissionUpdate
        {
            Type = PermissionUpdateType.AddDirectories,
            Directories = ["/tmp/a"],
            Destination = PermissionUpdateDestination.LocalSettings
        };

        var wire = JsonSerializer.SerializeToElement(update.ToWireFormat());
        Assert.Equal("addDirectories", wire.GetProperty("type").GetString());
        Assert.Equal("localSettings", wire.GetProperty("destination").GetString());

        var parsed = PermissionUpdate.FromWireFormat(wire)!;
        Assert.Equal(update.Type, parsed.Type);
        Assert.Equal(update.Destination, parsed.Destination);
        Assert.Equal("/tmp/a", Assert.Single(parsed.Directories!));
        Assert.Null(PermissionUpdate.FromWireFormat(JsonDocument.Parse("""{"type":"bogus"}""").RootElement));
    }

    [Fact]
    public void HookSpecificOutput_SerializesWithEventNameAndDecision()
    {
        var output = new PreToolUseHookSpecificOutput
        {
            PermissionDecision = "defer",
            PermissionDecisionReason = "needs human review"
        }.ToJsonElement();

        Assert.Equal("PreToolUse", output.GetProperty("hookEventName").GetString());
        Assert.Equal("defer", output.GetProperty("permissionDecision").GetString());
        Assert.False(output.TryGetProperty("updatedInput", out _));

        var post = new PostToolUseHookSpecificOutput
        {
            UpdatedToolOutput = JsonDocument.Parse("""{"stdout":"redacted"}""").RootElement
        }.ToJsonElement();
        Assert.Equal("redacted", post.GetProperty("updatedToolOutput").GetProperty("stdout").GetString());
    }

    [Fact]
    public void RunEndCeiling_ReadsOptionsEnvironmentFirst()
    {
        Assert.Equal(1234, QueryHandler.ResolveRunEndCeilingMs(
            new Dictionary<string, string> { [QueryHandler.RunEndCeilingEnv] = "1234" }));
        Assert.Equal(0, QueryHandler.ResolveRunEndCeilingMs(
            new Dictionary<string, string> { [QueryHandler.RunEndCeilingEnv] = "0" }));
        Assert.Equal(QueryHandler.DefaultRunEndCeilingMs, QueryHandler.ResolveRunEndCeilingMs(
            new Dictionary<string, string> { [QueryHandler.RunEndCeilingEnv] = "-5" }));
        Assert.Equal(QueryHandler.DefaultRunEndCeilingMs, QueryHandler.ResolveRunEndCeilingMs(
            new Dictionary<string, string> { [QueryHandler.RunEndCeilingEnv] = "soon" }));
    }
}

public class PythonParityOptionsTests
{
    [Fact]
    public async Task CanUseToolWithPermissionPromptToolName_Throws()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
            PermissionPromptToolName = "mcp__perm__prompt"
        };
        await using var client = ClaudeAgentClient.CreateForTesting(options, new SdkMockTransport());

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateSessionAsync());
    }

    [Theory]
    [InlineData("Read", "Read")]
    [InlineData("Read()", "Read")]
    [InlineData("Read(*)", "Read")]
    [InlineData("Bash(ls:*)", null)]
    [InlineData("(Read)", null)]
    [InlineData("  ", null)]
    public void WholeToolAllowed_MatchesCliRuleParser(string entry, string? expected)
    {
        Assert.Equal(expected, ClaudeAgentOptionsValidation.WholeToolAllowed(entry));
    }

    [Fact]
    public void ShadowWarning_NamesShadowedTools()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
            AllowedTools = ["Read", "Read()", "Bash(git:*)"],
            Skills = SkillsConfig.All
        };

        var warning = ClaudeAgentOptionsValidation.GetCanUseToolShadowedWarning(options);

        Assert.NotNull(warning);
        Assert.Contains("Read, Skill", warning);
        Assert.DoesNotContain("Bash", warning);

        Assert.Contains("bypassPermissions", ClaudeAgentOptionsValidation.GetCanUseToolShadowedWarning(
            options with { PermissionMode = PermissionMode.BypassPermissions, AllowedTools = [] })!);
        Assert.Null(ClaudeAgentOptionsValidation.GetCanUseToolShadowedWarning(
            options with { AllowedTools = ["Bash(git:*)"], Skills = null }));
    }

    [Fact]
    public void ProcessStartInfo_SetsSdkEnvironment()
    {
        var startInfo = ProcessStartInfoBuilder.Create("/bin/claude", [])
            .WithEnvironment(new Dictionary<string, string>
            {
                ["CLAUDE_CODE_ENTRYPOINT"] = "my-app",
                ["CLAUDE_AGENT_SDK_VERSION"] = "spoofed"
            })
            .Build();

        Assert.Equal("my-app", startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"]);
        Assert.NotEqual("spoofed", startInfo.Environment["CLAUDE_AGENT_SDK_VERSION"]);
        Assert.Equal("1", startInfo.Environment[ProcessStartInfoBuilder.SdkReadsSessionStateEnv]);
        Assert.False(startInfo.Environment.ContainsKey("CLAUDECODE"));
    }

    [Theory]
    [InlineData(@"C:\npm\claude.cmd", true)]
    [InlineData(@"C:\npm\CLAUDE.BAT", true)]
    [InlineData(@"C:\npm\claude.cmd. ", true)]
    [InlineData(@"C:\x.cmd\..\claude.exe", true)]
    [InlineData(@"C:\npm\claude.cmd:stream", true)]
    [InlineData(@"C:\Users\me\.local\bin\claude.exe", false)]
    public void IsWindowsBatchPath_ClassifiesEveryComponent(string path, bool expectedOnWindows)
    {
        Assert.Equal(OperatingSystem.IsWindows() && expectedOnWindows, ProcessStartInfoBuilder.IsWindowsBatchPath(path));
    }
}
