using System.Text.Json;
using Claude.AgentSdk.Protocol;

namespace Claude.AgentSdk.Tests.Protocol;

/// <summary>
///     Comprehensive unit tests for Hook types in the Claude.AgentSdk.
/// </summary>
public class HookTypesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Theory]
    [InlineData(HookEvent.PreToolUse, 0)]
    [InlineData(HookEvent.PostToolUse, 1)]
    [InlineData(HookEvent.PostToolUseFailure, 2)]
    [InlineData(HookEvent.UserPromptSubmit, 3)]
    [InlineData(HookEvent.Stop, 4)]
    [InlineData(HookEvent.SubagentStart, 5)]
    [InlineData(HookEvent.SubagentStop, 6)]
    [InlineData(HookEvent.PreCompact, 7)]
    [InlineData(HookEvent.PermissionRequest, 8)]
    [InlineData(HookEvent.SessionStart, 9)]
    [InlineData(HookEvent.SessionEnd, 10)]
    [InlineData(HookEvent.Notification, 11)]
    public void HookEvent_HasCorrectValues(HookEvent hookEvent, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)hookEvent);
    }

    [Fact]
    public void HookEvent_HasAllExpectedValues()
    {
        HookEvent[] values = Enum.GetValues<HookEvent>();
        Assert.Equal(15, values.Length);
    }

    [Fact]
    public void HookEvent_CanBeConvertedToString()
    {
        Assert.Equal("PreToolUse", HookEvent.PreToolUse.ToString());
        Assert.Equal("PostToolUse", HookEvent.PostToolUse.ToString());
        Assert.Equal("PostToolUseFailure", HookEvent.PostToolUseFailure.ToString());
        Assert.Equal("UserPromptSubmit", HookEvent.UserPromptSubmit.ToString());
        Assert.Equal("Stop", HookEvent.Stop.ToString());
        Assert.Equal("SubagentStart", HookEvent.SubagentStart.ToString());
        Assert.Equal("SubagentStop", HookEvent.SubagentStop.ToString());
        Assert.Equal("PreCompact", HookEvent.PreCompact.ToString());
        Assert.Equal("PermissionRequest", HookEvent.PermissionRequest.ToString());
        Assert.Equal("SessionStart", HookEvent.SessionStart.ToString());
        Assert.Equal("SessionEnd", HookEvent.SessionEnd.ToString());
        Assert.Equal("Notification", HookEvent.Notification.ToString());
    }

    [Theory]
    [InlineData("PreToolUse", HookEvent.PreToolUse)]
    [InlineData("PostToolUse", HookEvent.PostToolUse)]
    [InlineData("Stop", HookEvent.Stop)]
    [InlineData("Notification", HookEvent.Notification)]
    public void HookEvent_CanBeParsedFromString(string name, HookEvent expected)
    {
        HookEvent parsed = Enum.Parse<HookEvent>(name);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void HookMatcher_RequiredHooksProperty_MustBeSet()
    {
        List<HookCallback> hooks = new();
        HookMatcher matcher = new() { Hooks = hooks };

        Assert.NotNull(matcher.Hooks);
        Assert.Empty(matcher.Hooks);
    }

    [Fact]
    public void HookMatcher_OptionalProperties_AreNullByDefault()
    {
        HookMatcher matcher = new() { Hooks = Array.Empty<HookCallback>() };

        Assert.Null(matcher.Matcher);
        Assert.Null(matcher.Timeout);
    }

    [Fact]
    public void HookMatcher_WithAllProperties_MapsCorrectly()
    {
        HookCallback callback = (input, toolUseId, context, ct) => Task.FromResult<HookOutput>(new SyncHookOutput());
        HookMatcher matcher = new()
        {
            Matcher = "Bash|Write",
            Hooks = new List<HookCallback> { callback },
            Timeout = 30.0
        };

        Assert.Equal("Bash|Write", matcher.Matcher);
        Assert.Single(matcher.Hooks);
        Assert.Equal(30.0, matcher.Timeout);
    }

    [Theory]
    [InlineData("Bash")]
    [InlineData("Write|Edit")]
    [InlineData("Read|Grep|Glob")]
    [InlineData(".*")]
    [InlineData(null)]
    public void HookMatcher_MatcherPatterns_AcceptsVariousPatterns(string? pattern)
    {
        HookMatcher matcher = new()
        {
            Matcher = pattern,
            Hooks = Array.Empty<HookCallback>()
        };

        Assert.Equal(pattern, matcher.Matcher);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(30.0)]
    [InlineData(120.5)]
    public void HookMatcher_TimeoutValues_AcceptsVariousValues(double timeout)
    {
        HookMatcher matcher = new()
        {
            Hooks = Array.Empty<HookCallback>(),
            Timeout = timeout
        };

        Assert.Equal(timeout, matcher.Timeout);
    }

    [Fact]
    public void HookMatcher_Record_SupportsEquality()
    {
        HookMatcher matcher1 = new()
        {
            Matcher = "Bash",
            Hooks = Array.Empty<HookCallback>(),
            Timeout = 30.0
        };

        HookMatcher matcher2 = new()
        {
            Matcher = "Bash",
            Hooks = Array.Empty<HookCallback>(),
            Timeout = 30.0
        };

        // Records with same reference for Hooks will be equal
        HookCallback[] sameHooks = Array.Empty<HookCallback>();
        HookMatcher matcher3 = matcher1 with { Hooks = sameHooks };
        HookMatcher matcher4 = matcher1 with { Hooks = sameHooks };

        Assert.Equal(matcher3, matcher4);
    }

    [Fact]
    public void HookMatcher_WithExpression_CreatesNewInstance()
    {
        HookMatcher original = new()
        {
            Matcher = "Bash",
            Hooks = Array.Empty<HookCallback>(),
            Timeout = 30.0
        };

        HookMatcher modified = original with { Timeout = 60.0 };

        Assert.Equal(30.0, original.Timeout);
        Assert.Equal(60.0, modified.Timeout);
        Assert.Equal("Bash", modified.Matcher);
    }

    [Fact]
    public void HookContext_CanBeCreated()
    {
        HookContext context = new();
        Assert.NotNull(context);
    }

    [Fact]
    public void HookContext_Record_SupportsEquality()
    {
        HookContext context1 = new();
        HookContext context2 = new();

        Assert.Equal(context1, context2);
    }

    [Fact]
    public void PreToolUseHookInput_HookEventName_ReturnsPreToolUse()
    {
        PreToolUseHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            ToolName = "Bash",
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal("PreToolUse", input.HookEventName);
    }

    [Fact]
    public void Deserialize_PreToolUseHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-abc123",
                                "transcript_path": "/home/user/.claude/transcripts/session.json",
                                "cwd": "/home/user/project",
                                "permission_mode": "default",
                                "tool_name": "Bash",
                                "tool_input": { "command": "ls -la" }
                            }
                            """;

        PreToolUseHookInput? input = JsonSerializer.Deserialize<PreToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("session-abc123", input.SessionId);
        Assert.Equal("/home/user/.claude/transcripts/session.json", input.TranscriptPath);
        Assert.Equal("/home/user/project", input.Cwd);
        Assert.Equal("default", input.PermissionMode);
        Assert.Equal("Bash", input.ToolName);
        Assert.Equal("ls -la", input.ToolInput.GetProperty("command").GetString());
    }

    [Fact]
    public void Deserialize_PreToolUseHookInput_WithComplexToolInput()
    {
        const string json = """
                            {
                                "session_id": "session-123",
                                "transcript_path": "/path/to/transcript",
                                "cwd": "/cwd",
                                "tool_name": "Write",
                                "tool_input": {
                                    "file_path": "/src/file.cs",
                                    "content": "public class Test { }",
                                    "options": {
                                        "create_if_missing": true,
                                        "backup": false
                                    }
                                }
                            }
                            """;

        PreToolUseHookInput? input = JsonSerializer.Deserialize<PreToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("Write", input.ToolName);
        Assert.Equal("/src/file.cs", input.ToolInput.GetProperty("file_path").GetString());
        Assert.True(input.ToolInput.GetProperty("options").GetProperty("create_if_missing").GetBoolean());
    }

    [Theory]
    [InlineData("Bash")]
    [InlineData("Write")]
    [InlineData("Edit")]
    [InlineData("Read")]
    [InlineData("Grep")]
    [InlineData("Glob")]
    [InlineData("WebFetch")]
    [InlineData("mcp__file-server__read")]
    public void PreToolUseHookInput_AcceptsVariousToolNames(string toolName)
    {
        PreToolUseHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            ToolName = toolName,
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal(toolName, input.ToolName);
    }

    [Fact]
    public void PostToolUseHookInput_HookEventName_ReturnsPostToolUse()
    {
        PostToolUseHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            ToolName = "Bash",
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal("PostToolUse", input.HookEventName);
    }

    [Fact]
    public void Deserialize_PostToolUseHookInput_WithToolResponse()
    {
        const string json = """
                            {
                                "session_id": "session-456",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "tool_name": "Read",
                                "tool_input": { "file_path": "/src/test.cs" },
                                "tool_response": {
                                    "content": "file contents here",
                                    "lines": 50
                                }
                            }
                            """;

        PostToolUseHookInput? input = JsonSerializer.Deserialize<PostToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("PostToolUse", input.HookEventName);
        Assert.Equal("Read", input.ToolName);
        Assert.NotNull(input.ToolResponse);
        Assert.Equal("file contents here", input.ToolResponse.Value.GetProperty("content").GetString());
        Assert.Equal(50, input.ToolResponse.Value.GetProperty("lines").GetInt32());
    }

    [Fact]
    public void Deserialize_PostToolUseHookInput_WithNullToolResponse()
    {
        const string json = """
                            {
                                "session_id": "session-789",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "tool_name": "Bash",
                                "tool_input": { "command": "echo test" },
                                "tool_response": null
                            }
                            """;

        PostToolUseHookInput? input = JsonSerializer.Deserialize<PostToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Null(input.ToolResponse);
    }

    [Fact]
    public void Deserialize_PostToolUseHookInput_WithoutToolResponse()
    {
        const string json = """
                            {
                                "session_id": "session-101",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "tool_name": "Write",
                                "tool_input": { "file_path": "/test.txt", "content": "hello" }
                            }
                            """;

        PostToolUseHookInput? input = JsonSerializer.Deserialize<PostToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Null(input.ToolResponse);
    }

    [Fact]
    public void PostToolUseFailureHookInput_HookEventName_ReturnsPostToolUseFailure()
    {
        PostToolUseFailureHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            ToolName = "Bash",
            ToolInput = JsonDocument.Parse("{}").RootElement,
            Error = "Command failed"
        };

        Assert.Equal("PostToolUseFailure", input.HookEventName);
    }

    [Fact]
    public void Deserialize_PostToolUseFailureHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-error-123",
                                "transcript_path": "/transcripts/error-session.json",
                                "cwd": "/project",
                                "permission_mode": "strict",
                                "tool_name": "Bash",
                                "tool_input": { "command": "rm -rf /" },
                                "error": "Permission denied: Cannot execute destructive command",
                                "is_interrupt": true
                            }
                            """;

        PostToolUseFailureHookInput? input = JsonSerializer.Deserialize<PostToolUseFailureHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("PostToolUseFailure", input.HookEventName);
        Assert.Equal("Bash", input.ToolName);
        Assert.Equal("Permission denied: Cannot execute destructive command", input.Error);
        Assert.True(input.IsInterrupt);
    }

    [Fact]
    public void Deserialize_PostToolUseFailureHookInput_IsInterruptDefaultsFalse()
    {
        const string json = """
                            {
                                "session_id": "session-fail-456",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "tool_name": "Read",
                                "tool_input": { "file_path": "/nonexistent/file.txt" },
                                "error": "File not found"
                            }
                            """;

        PostToolUseFailureHookInput? input = JsonSerializer.Deserialize<PostToolUseFailureHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.False(input.IsInterrupt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PostToolUseFailureHookInput_IsInterrupt_AcceptsBothValues(bool isInterrupt)
    {
        PostToolUseFailureHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            ToolName = "Bash",
            ToolInput = JsonDocument.Parse("{}").RootElement,
            Error = "Error",
            IsInterrupt = isInterrupt
        };

        Assert.Equal(isInterrupt, input.IsInterrupt);
    }

    [Fact]
    public void UserPromptSubmitHookInput_HookEventName_ReturnsUserPromptSubmit()
    {
        UserPromptSubmitHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            Prompt = "Hello, Claude!"
        };

        Assert.Equal("UserPromptSubmit", input.HookEventName);
    }

    [Fact]
    public void Deserialize_UserPromptSubmitHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-prompt-123",
                                "transcript_path": "/transcripts/prompt-session.json",
                                "cwd": "/home/user/project",
                                "permission_mode": "auto",
                                "prompt": "Please review this code and suggest improvements."
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("UserPromptSubmit", input.HookEventName);
        Assert.Equal("session-prompt-123", input.SessionId);
        Assert.Equal("auto", input.PermissionMode);
        Assert.Equal("Please review this code and suggest improvements.", input.Prompt);
    }

    [Fact]
    public void Deserialize_UserPromptSubmitHookInput_WithEmptyPrompt()
    {
        const string json = """
                            {
                                "session_id": "session-empty",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "prompt": ""
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("", input.Prompt);
    }

    [Fact]
    public void Deserialize_UserPromptSubmitHookInput_WithUnicodePrompt()
    {
        const string json = """
                            {
                                "session_id": "session-unicode",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "prompt": "\u4f60\u597d\uff01 \ud83d\ude00 Please help."
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Contains("\u4f60\u597d", input.Prompt);
    }

    [Fact]
    public void StopHookInput_HookEventName_ReturnsStop()
    {
        StopHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            StopHookActive = true
        };

        Assert.Equal("Stop", input.HookEventName);
    }

    [Fact]
    public void Deserialize_StopHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-stop-123",
                                "transcript_path": "/transcripts/stop-session.json",
                                "cwd": "/project",
                                "stop_hook_active": true
                            }
                            """;

        StopHookInput? input = JsonSerializer.Deserialize<StopHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("Stop", input.HookEventName);
        Assert.Equal("session-stop-123", input.SessionId);
        Assert.True(input.StopHookActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deserialize_StopHookInput_WithVariousStopHookActiveValues(bool stopHookActive)
    {
        string json = $$"""
                        {
                            "session_id": "session-stop",
                            "transcript_path": "/transcripts/session.json",
                            "cwd": "/project",
                            "stop_hook_active": {{stopHookActive.ToString().ToLower()}}
                        }
                        """;

        StopHookInput? input = JsonSerializer.Deserialize<StopHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal(stopHookActive, input.StopHookActive);
    }

    [Fact]
    public void SubagentStartHookInput_HookEventName_ReturnsSubagentStart()
    {
        SubagentStartHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            AgentId = "agent-456",
            AgentType = "task"
        };

        Assert.Equal("SubagentStart", input.HookEventName);
    }

    [Fact]
    public void Deserialize_SubagentStartHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "parent-session-123",
                                "transcript_path": "/transcripts/parent.json",
                                "cwd": "/project",
                                "agent_id": "subagent-abc123",
                                "agent_type": "worker"
                            }
                            """;

        SubagentStartHookInput? input = JsonSerializer.Deserialize<SubagentStartHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("SubagentStart", input.HookEventName);
        Assert.Equal("parent-session-123", input.SessionId);
        Assert.Equal("subagent-abc123", input.AgentId);
        Assert.Equal("worker", input.AgentType);
    }

    [Theory]
    [InlineData("task")]
    [InlineData("worker")]
    [InlineData("assistant")]
    [InlineData("custom")]
    public void SubagentStartHookInput_AcceptsVariousAgentTypes(string agentType)
    {
        SubagentStartHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            AgentId = "agent-1",
            AgentType = agentType
        };

        Assert.Equal(agentType, input.AgentType);
    }

    [Fact]
    public void SubagentStopHookInput_HookEventName_ReturnsSubagentStop()
    {
        SubagentStopHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            StopHookActive = true
        };

        Assert.Equal("SubagentStop", input.HookEventName);
    }

    [Fact]
    public void Deserialize_SubagentStopHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "parent-session-456",
                                "transcript_path": "/transcripts/parent.json",
                                "cwd": "/project",
                                "stop_hook_active": true,
                                "agent_id": "subagent-xyz789",
                                "agent_transcript_path": "/transcripts/subagent-xyz789.json"
                            }
                            """;

        SubagentStopHookInput? input = JsonSerializer.Deserialize<SubagentStopHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("SubagentStop", input.HookEventName);
        Assert.True(input.StopHookActive);
        Assert.Equal("subagent-xyz789", input.AgentId);
        Assert.Equal("/transcripts/subagent-xyz789.json", input.AgentTranscriptPath);
    }

    [Fact]
    public void Deserialize_SubagentStopHookInput_WithNullOptionalProperties()
    {
        const string json = """
                            {
                                "session_id": "session-789",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "stop_hook_active": false
                            }
                            """;

        SubagentStopHookInput? input = JsonSerializer.Deserialize<SubagentStopHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.False(input.StopHookActive);
        Assert.Null(input.AgentId);
        Assert.Null(input.AgentTranscriptPath);
    }

    [Fact]
    public void PreCompactHookInput_HookEventName_ReturnsPreCompact()
    {
        PreCompactHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            Trigger = "token_limit"
        };

        Assert.Equal("PreCompact", input.HookEventName);
    }

    [Fact]
    public void Deserialize_PreCompactHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-compact-123",
                                "transcript_path": "/transcripts/compact.json",
                                "cwd": "/project",
                                "trigger": "manual",
                                "custom_instructions": "Preserve all code changes and key decisions"
                            }
                            """;

        PreCompactHookInput? input = JsonSerializer.Deserialize<PreCompactHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("PreCompact", input.HookEventName);
        Assert.Equal("manual", input.Trigger);
        Assert.Equal("Preserve all code changes and key decisions", input.CustomInstructions);
    }

    [Fact]
    public void Deserialize_PreCompactHookInput_WithNullCustomInstructions()
    {
        const string json = """
                            {
                                "session_id": "session-456",
                                "transcript_path": "/transcripts/session.json",
                                "cwd": "/project",
                                "trigger": "token_limit"
                            }
                            """;

        PreCompactHookInput? input = JsonSerializer.Deserialize<PreCompactHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("token_limit", input.Trigger);
        Assert.Null(input.CustomInstructions);
    }

    [Theory]
    [InlineData("token_limit")]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("time_based")]
    public void PreCompactHookInput_AcceptsVariousTriggers(string trigger)
    {
        PreCompactHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Trigger = trigger
        };

        Assert.Equal(trigger, input.Trigger);
    }

    [Fact]
    public void PermissionRequestHookInput_HookEventName_ReturnsPermissionRequest()
    {
        PermissionRequestHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            ToolName = "Bash",
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal("PermissionRequest", input.HookEventName);
    }

    [Fact]
    public void Deserialize_PermissionRequestHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "session-perm-123",
                                "transcript_path": "/transcripts/perm.json",
                                "cwd": "/project",
                                "permission_mode": "strict",
                                "tool_name": "Write",
                                "tool_input": { "file_path": "/etc/config", "content": "data" },
                                "permission_suggestions": {
                                    "allow_once": true,
                                    "allow_always": false,
                                    "reason": "Modifying system configuration"
                                }
                            }
                            """;

        PermissionRequestHookInput? input = JsonSerializer.Deserialize<PermissionRequestHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("PermissionRequest", input.HookEventName);
        Assert.Equal("Write", input.ToolName);
        Assert.Equal("/etc/config", input.ToolInput.GetProperty("file_path").GetString());
        Assert.NotNull(input.PermissionSuggestions);
        Assert.True(input.PermissionSuggestions.Value.GetProperty("allow_once").GetBoolean());
    }

    [Fact]
    public void Deserialize_PermissionRequestHookInput_WithNullPermissionSuggestions()
    {
        const string json = """
                            {
                                "session_id": "session-perm-456",
                                "transcript_path": "/transcripts/perm.json",
                                "cwd": "/project",
                                "tool_name": "Bash",
                                "tool_input": { "command": "ls" }
                            }
                            """;

        PermissionRequestHookInput? input = JsonSerializer.Deserialize<PermissionRequestHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Null(input.PermissionSuggestions);
    }

    [Fact]
    public void SessionStartHookInput_HookEventName_ReturnsSessionStart()
    {
        SessionStartHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            Source = "startup"
        };

        Assert.Equal("SessionStart", input.HookEventName);
    }

    [Fact]
    public void Deserialize_SessionStartHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "new-session-123",
                                "transcript_path": "/transcripts/new.json",
                                "cwd": "/home/user/project",
                                "permission_mode": "default",
                                "source": "startup"
                            }
                            """;

        SessionStartHookInput? input = JsonSerializer.Deserialize<SessionStartHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("SessionStart", input.HookEventName);
        Assert.Equal("new-session-123", input.SessionId);
        Assert.Equal("startup", input.Source);
    }

    [Theory]
    [InlineData("startup")]
    [InlineData("resume")]
    [InlineData("clear")]
    [InlineData("compact")]
    public void SessionStartHookInput_AcceptsVariousSources(string source)
    {
        SessionStartHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Source = source
        };

        Assert.Equal(source, input.Source);
    }

    [Fact]
    public void SessionEndHookInput_HookEventName_ReturnsSessionEnd()
    {
        SessionEndHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            Reason = "clear"
        };

        Assert.Equal("SessionEnd", input.HookEventName);
    }

    [Fact]
    public void Deserialize_SessionEndHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "ending-session-123",
                                "transcript_path": "/transcripts/ending.json",
                                "cwd": "/home/user/project",
                                "reason": "logout"
                            }
                            """;

        SessionEndHookInput? input = JsonSerializer.Deserialize<SessionEndHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("SessionEnd", input.HookEventName);
        Assert.Equal("ending-session-123", input.SessionId);
        Assert.Equal("logout", input.Reason);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("logout")]
    [InlineData("prompt_input_exit")]
    [InlineData("bypass_permissions_disabled")]
    [InlineData("other")]
    public void SessionEndHookInput_AcceptsVariousReasons(string reason)
    {
        SessionEndHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Reason = reason
        };

        Assert.Equal(reason, input.Reason);
    }

    [Fact]
    public void NotificationHookInput_HookEventName_ReturnsNotification()
    {
        NotificationHookInput input = new()
        {
            SessionId = "session-123",
            TranscriptPath = "/path/to/transcript",
            Cwd = "/working/dir",
            Message = "Test notification",
            NotificationType = "permission_prompt"
        };

        Assert.Equal("Notification", input.HookEventName);
    }

    [Fact]
    public void Deserialize_NotificationHookInput_MapsAllProperties()
    {
        const string json = """
                            {
                                "session_id": "notif-session-123",
                                "transcript_path": "/transcripts/notif.json",
                                "cwd": "/project",
                                "message": "Waiting for user input",
                                "notification_type": "idle_prompt",
                                "title": "Input Required"
                            }
                            """;

        NotificationHookInput? input = JsonSerializer.Deserialize<NotificationHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("Notification", input.HookEventName);
        Assert.Equal("Waiting for user input", input.Message);
        Assert.Equal("idle_prompt", input.NotificationType);
        Assert.Equal("Input Required", input.Title);
    }

    [Fact]
    public void Deserialize_NotificationHookInput_WithNullTitle()
    {
        const string json = """
                            {
                                "session_id": "notif-session-456",
                                "transcript_path": "/transcripts/notif.json",
                                "cwd": "/project",
                                "message": "Authentication successful",
                                "notification_type": "auth_success"
                            }
                            """;

        NotificationHookInput? input = JsonSerializer.Deserialize<NotificationHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("auth_success", input.NotificationType);
        Assert.Null(input.Title);
    }

    [Theory]
    [InlineData("permission_prompt")]
    [InlineData("idle_prompt")]
    [InlineData("auth_success")]
    [InlineData("elicitation_dialog")]
    public void NotificationHookInput_AcceptsVariousNotificationTypes(string notificationType)
    {
        NotificationHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Message = "Test",
            NotificationType = notificationType
        };

        Assert.Equal(notificationType, input.NotificationType);
    }

    [Fact]
    public void SyncHookOutput_AllPropertiesAreOptional()
    {
        SyncHookOutput output = new();

        Assert.Null(output.Continue);
        Assert.Null(output.SuppressOutput);
        Assert.Null(output.StopReason);
        Assert.Null(output.Decision);
        Assert.Null(output.SystemMessage);
        Assert.Null(output.Reason);
        Assert.Null(output.HookSpecificOutput);
    }

    [Fact]
    public void SyncHookOutput_AllowContinue_SetsContinueTrue()
    {
        SyncHookOutput output = new() { Continue = true };

        Assert.True(output.Continue);
    }

    [Fact]
    public void SyncHookOutput_DenyContinue_SetsContinueFalseWithReason()
    {
        SyncHookOutput output = new()
        {
            Continue = false,
            StopReason = "Operation blocked by security policy",
            Decision = "block"
        };

        Assert.False(output.Continue);
        Assert.Equal("Operation blocked by security policy", output.StopReason);
        Assert.Equal("block", output.Decision);
    }

    [Fact]
    public void SyncHookOutput_WithSystemMessage_MapsCorrectly()
    {
        SyncHookOutput output = new()
        {
            Continue = true,
            SystemMessage = "Warning: This operation may take a long time"
        };

        Assert.Equal("Warning: This operation may take a long time", output.SystemMessage);
    }

    [Fact]
    public void SyncHookOutput_WithReason_MapsCorrectly()
    {
        SyncHookOutput output = new()
        {
            Continue = true,
            Reason = "Operation approved by hook"
        };

        Assert.Equal("Operation approved by hook", output.Reason);
    }

    [Fact]
    public void SyncHookOutput_WithSuppressOutput_MapsCorrectly()
    {
        SyncHookOutput output = new()
        {
            Continue = true,
            SuppressOutput = true
        };

        Assert.True(output.SuppressOutput);
    }

    [Fact]
    public void SyncHookOutput_WithHookSpecificOutput_MapsJsonElement()
    {
        JsonElement specificOutput =
            JsonDocument.Parse("""{ "modified_input": { "command": "safe-command" } }""").RootElement;
        SyncHookOutput output = new()
        {
            Continue = true,
            HookSpecificOutput = specificOutput
        };

        Assert.NotNull(output.HookSpecificOutput);
        Assert.Equal("safe-command",
            output.HookSpecificOutput.Value.GetProperty("modified_input").GetProperty("command").GetString());
    }

    [Fact]
    public void Serialize_SyncHookOutput_ProducesValidJson()
    {
        SyncHookOutput output = new()
        {
            Continue = true,
            Decision = "allow",
            Reason = "Approved"
        };

        string json = JsonSerializer.Serialize(output, JsonOptions);

        Assert.Contains("continue", json);
        Assert.Contains("decision", json);
        Assert.Contains("reason", json);
    }

    [Fact]
    public void AsyncHookOutput_DefaultTimeout_IsNull()
    {
        AsyncHookOutput output = new();

        Assert.Null(output.AsyncTimeout);
    }

    [Fact]
    public void AsyncHookOutput_WithTimeout_MapsCorrectly()
    {
        AsyncHookOutput output = new() { AsyncTimeout = 5000 };

        Assert.Equal(5000, output.AsyncTimeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(600000)]
    public void AsyncHookOutput_AcceptsVariousTimeouts(int timeout)
    {
        AsyncHookOutput output = new() { AsyncTimeout = timeout };

        Assert.Equal(timeout, output.AsyncTimeout);
    }

    [Fact]
    public void Serialize_AsyncHookOutput_ProducesValidJson()
    {
        AsyncHookOutput output = new() { AsyncTimeout = 10000 };

        string json = JsonSerializer.Serialize(output, JsonOptions);

        // Note: asyncTimeout property name is specified with camelCase in JsonPropertyName attribute
        Assert.Contains("asyncTimeout", json);
        Assert.Contains("10000", json);
    }

    [Fact]
    public void HookOutput_Inheritance_SyncHookOutputIsHookOutput()
    {
        HookOutput output = new SyncHookOutput { Continue = true };

        Assert.IsType<SyncHookOutput>(output);
    }

    [Fact]
    public void HookOutput_Inheritance_AsyncHookOutputIsHookOutput()
    {
        HookOutput output = new AsyncHookOutput { AsyncTimeout = 5000 };

        Assert.IsType<AsyncHookOutput>(output);
    }

    [Theory]
    [InlineData(typeof(PreToolUseHookInput), "PreToolUse")]
    [InlineData(typeof(PostToolUseHookInput), "PostToolUse")]
    [InlineData(typeof(PostToolUseFailureHookInput), "PostToolUseFailure")]
    [InlineData(typeof(UserPromptSubmitHookInput), "UserPromptSubmit")]
    [InlineData(typeof(StopHookInput), "Stop")]
    [InlineData(typeof(SubagentStartHookInput), "SubagentStart")]
    [InlineData(typeof(SubagentStopHookInput), "SubagentStop")]
    [InlineData(typeof(PreCompactHookInput), "PreCompact")]
    [InlineData(typeof(PermissionRequestHookInput), "PermissionRequest")]
    [InlineData(typeof(SessionStartHookInput), "SessionStart")]
    [InlineData(typeof(SessionEndHookInput), "SessionEnd")]
    [InlineData(typeof(NotificationHookInput), "Notification")]
    public void AllHookInputTypes_ReturnCorrectHookEventName(Type hookInputType, string expectedEventName)
    {
        HookInput input = hookInputType.Name switch
        {
            nameof(PreToolUseHookInput) => new PreToolUseHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                ToolName = "T",
                ToolInput = JsonDocument.Parse("{}").RootElement
            },
            nameof(PostToolUseHookInput) => new PostToolUseHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                ToolName = "T",
                ToolInput = JsonDocument.Parse("{}").RootElement
            },
            nameof(PostToolUseFailureHookInput) => new PostToolUseFailureHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                ToolName = "T",
                ToolInput = JsonDocument.Parse("{}").RootElement,
                Error = "e"
            },
            nameof(UserPromptSubmitHookInput) => new UserPromptSubmitHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                Prompt = "p"
            },
            nameof(StopHookInput) => new StopHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                StopHookActive = true
            },
            nameof(SubagentStartHookInput) => new SubagentStartHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                AgentId = "a",
                AgentType = "t"
            },
            nameof(SubagentStopHookInput) => new SubagentStopHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                StopHookActive = true
            },
            nameof(PreCompactHookInput) => new PreCompactHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                Trigger = "manual"
            },
            nameof(PermissionRequestHookInput) => new PermissionRequestHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                ToolName = "T",
                ToolInput = JsonDocument.Parse("{}").RootElement
            },
            nameof(SessionStartHookInput) => new SessionStartHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                Source = "startup"
            },
            nameof(SessionEndHookInput) => new SessionEndHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                Reason = "clear"
            },
            nameof(NotificationHookInput) => new NotificationHookInput
            {
                SessionId = "s",
                TranscriptPath = "/t",
                Cwd = "/c",
                Message = "m",
                NotificationType = "permission_prompt"
            },
            _ => throw new ArgumentException($"Unknown type: {hookInputType}")
        };

        Assert.Equal(expectedEventName, input.HookEventName);
    }

    [Fact]
    public void HookInput_BaseProperties_AreRequiredOnAllDerivedTypes()
    {
        PreToolUseHookInput input = new()
        {
            SessionId = "test-session",
            TranscriptPath = "/test/transcript.json",
            Cwd = "/test/cwd",
            ToolName = "Test",
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        Assert.Equal("test-session", input.SessionId);
        Assert.Equal("/test/transcript.json", input.TranscriptPath);
        Assert.Equal("/test/cwd", input.Cwd);
    }

    [Fact]
    public void HookInput_PermissionMode_IsOptional()
    {
        UserPromptSubmitHookInput input = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Prompt = "test"
        };

        Assert.Null(input.PermissionMode);

        UserPromptSubmitHookInput inputWithMode = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Prompt = "test",
            PermissionMode = "strict"
        };

        Assert.Equal("strict", inputWithMode.PermissionMode);
    }

    [Fact]
    public void Deserialize_HookInput_WithExtraProperties_IgnoresUnknown()
    {
        const string json = """
                            {
                                "session_id": "session-extra",
                                "transcript_path": "/path",
                                "cwd": "/cwd",
                                "unknown_property": "should be ignored",
                                "another_unknown": { "nested": true },
                                "prompt": "Hello"
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal("session-extra", input.SessionId);
        Assert.Equal("Hello", input.Prompt);
    }

    [Fact]
    public void Deserialize_HookInput_WithNullOptionalProperties_AllowsNull()
    {
        const string json = """
                            {
                                "session_id": "session-null",
                                "transcript_path": "/path",
                                "cwd": "/cwd",
                                "permission_mode": null,
                                "prompt": "Hello"
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Null(input.PermissionMode);
    }

    [Fact]
    public void Deserialize_ToolInput_WithEmptyObject_MapsCorrectly()
    {
        const string json = """
                            {
                                "session_id": "session",
                                "transcript_path": "/path",
                                "cwd": "/cwd",
                                "tool_name": "Test",
                                "tool_input": {}
                            }
                            """;

        PreToolUseHookInput? input = JsonSerializer.Deserialize<PreToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal(JsonValueKind.Object, input.ToolInput.ValueKind);
    }

    [Fact]
    public void Deserialize_ToolInput_WithArrayValue_MapsCorrectly()
    {
        const string json = """
                            {
                                "session_id": "session",
                                "transcript_path": "/path",
                                "cwd": "/cwd",
                                "tool_name": "Test",
                                "tool_input": { "files": ["a.txt", "b.txt", "c.txt"] }
                            }
                            """;

        PreToolUseHookInput? input = JsonSerializer.Deserialize<PreToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal(3, input.ToolInput.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void Deserialize_ToolInput_WithNestedObjects_MapsCorrectly()
    {
        const string json = """
                            {
                                "session_id": "session",
                                "transcript_path": "/path",
                                "cwd": "/cwd",
                                "tool_name": "ComplexTool",
                                "tool_input": {
                                    "level1": {
                                        "level2": {
                                            "level3": {
                                                "value": 42
                                            }
                                        }
                                    }
                                }
                            }
                            """;

        PreToolUseHookInput? input = JsonSerializer.Deserialize<PreToolUseHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Equal(42, input.ToolInput
            .GetProperty("level1")
            .GetProperty("level2")
            .GetProperty("level3")
            .GetProperty("value")
            .GetInt32());
    }

    [Fact]
    public void Deserialize_WithUnicodePaths_MapsCorrectly()
    {
        const string json = """
                            {
                                "session_id": "session-unicode",
                                "transcript_path": "/home/\u7528\u6237/transcripts/session.json",
                                "cwd": "/home/\u7528\u6237/\u9879\u76ee",
                                "prompt": "Test"
                            }
                            """;

        UserPromptSubmitHookInput? input = JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json, JsonOptions);

        Assert.NotNull(input);
        Assert.Contains("\u7528\u6237", input.TranscriptPath);
        Assert.Contains("\u9879\u76ee", input.Cwd);
    }

    [Fact]
    public void HookMatcher_WithExpression_SupportsImmutableModification()
    {
        HookCallback[] hooks = Array.Empty<HookCallback>();
        HookMatcher original = new()
        {
            Matcher = "Bash",
            Hooks = hooks,
            Timeout = 30.0
        };

        HookMatcher modified = original with { Matcher = "Write" };

        Assert.Equal("Bash", original.Matcher);
        Assert.Equal("Write", modified.Matcher);
        Assert.Same(hooks, modified.Hooks);
    }

    [Fact]
    public void SyncHookOutput_WithExpression_SupportsImmutableModification()
    {
        SyncHookOutput original = new()
        {
            Continue = true,
            Decision = "allow"
        };

        SyncHookOutput modified = original with { Decision = "deny", Continue = false };

        Assert.True(original.Continue);
        Assert.Equal("allow", original.Decision);
        Assert.False(modified.Continue);
        Assert.Equal("deny", modified.Decision);
    }

    [Fact]
    public void PreToolUseHookInput_RecordEquality_WorksCorrectly()
    {
        JsonElement toolInput = JsonDocument.Parse("{}").RootElement;
        PreToolUseHookInput input1 = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            ToolName = "Bash",
            ToolInput = toolInput
        };

        PreToolUseHookInput input2 = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            ToolName = "Bash",
            ToolInput = toolInput
        };

        // Same reference for JsonElement
        Assert.Equal(input1, input2);
    }

    [Fact]
    public async Task HookCallback_CanBeInvoked()
    {
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
        };

        PreToolUseHookInput hookInput = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            ToolName = "Test",
            ToolInput = JsonDocument.Parse("{}").RootElement
        };

        HookOutput result = await callback(hookInput, "tool-use-123", new HookContext());

        Assert.NotNull(result);
        SyncHookOutput syncResult = Assert.IsType<SyncHookOutput>(result);
        Assert.True(syncResult.Continue);
    }

    [Fact]
    public async Task HookCallback_ReceivesAllParameters()
    {
        string? receivedToolUseId = null;
        HookInput? receivedInput = null;
        HookContext? receivedContext = null;

        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedInput = input;
            receivedToolUseId = toolUseId;
            receivedContext = context;
            return Task.FromResult<HookOutput>(new SyncHookOutput());
        };

        UserPromptSubmitHookInput hookInput = new()
        {
            SessionId = "test-session",
            TranscriptPath = "/test/path",
            Cwd = "/test/cwd",
            Prompt = "Test prompt"
        };

        HookContext hookContext = new();

        await callback(hookInput, "test-tool-use-id", hookContext);

        Assert.Same(hookInput, receivedInput);
        Assert.Equal("test-tool-use-id", receivedToolUseId);
        Assert.Same(hookContext, receivedContext);
    }

    [Fact]
    public async Task HookCallback_CanReturnAsyncHookOutput()
    {
        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            return Task.FromResult<HookOutput>(new AsyncHookOutput { AsyncTimeout = 10000 });
        };

        StopHookInput hookInput = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            StopHookActive = true
        };

        HookOutput result = await callback(hookInput, null, new HookContext());

        Assert.IsType<AsyncHookOutput>(result);
        Assert.Equal(10000, ((AsyncHookOutput)result).AsyncTimeout);
    }

    [Fact]
    public async Task HookCallback_SupportsCancellation()
    {
        CancellationTokenSource cts = new();
        bool wasCancellationTokenPassed = false;

        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            wasCancellationTokenPassed = ct == cts.Token;
            return Task.FromResult<HookOutput>(new SyncHookOutput());
        };

        NotificationHookInput hookInput = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Message = "test",
            NotificationType = "test"
        };

        await callback(hookInput, null, new HookContext(), cts.Token);

        Assert.True(wasCancellationTokenPassed);
    }

    [Fact]
    public async Task HookCallback_HandlesNullToolUseId()
    {
        string? receivedToolUseId = "not-null";

        HookCallback callback = (input, toolUseId, context, ct) =>
        {
            receivedToolUseId = toolUseId;
            return Task.FromResult<HookOutput>(new SyncHookOutput());
        };

        SessionStartHookInput hookInput = new()
        {
            SessionId = "session",
            TranscriptPath = "/path",
            Cwd = "/cwd",
            Source = "startup"
        };

        await callback(hookInput, null, new HookContext());

        Assert.Null(receivedToolUseId);
    }

    [Fact]
    public void RoundTrip_SyncHookOutput_PreservesData()
    {
        SyncHookOutput original = new()
        {
            Continue = true,
            SuppressOutput = false,
            Decision = "allow",
            Reason = "Test reason",
            SystemMessage = "Test message"
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        SyncHookOutput? deserialized = JsonSerializer.Deserialize<SyncHookOutput>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Continue, deserialized.Continue);
        Assert.Equal(original.SuppressOutput, deserialized.SuppressOutput);
        Assert.Equal(original.Decision, deserialized.Decision);
        Assert.Equal(original.Reason, deserialized.Reason);
        Assert.Equal(original.SystemMessage, deserialized.SystemMessage);
    }

    [Fact]
    public void RoundTrip_AsyncHookOutput_PreservesData()
    {
        AsyncHookOutput original = new() { AsyncTimeout = 30000 };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        AsyncHookOutput? deserialized = JsonSerializer.Deserialize<AsyncHookOutput>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.AsyncTimeout, deserialized.AsyncTimeout);
    }

    [Fact]
    public void RoundTrip_HookMatcher_PreservesData()
    {
        HookMatcher original = new()
        {
            Matcher = "Bash|Write",
            Hooks = Array.Empty<HookCallback>(),
            Timeout = 45.5
        };

        // Note: Hooks (delegates) cannot be serialized, so we only test serializable properties
        string json = JsonSerializer.Serialize(new { original.Matcher, original.Timeout }, JsonOptions);

        Assert.Contains("\"matcher\":", json);
        Assert.Contains("\"timeout\":", json);
    }
}
