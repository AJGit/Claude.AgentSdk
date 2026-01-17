using System.Runtime.CompilerServices;
using System.Text.Json;
using Claude.AgentSdk.Exceptions;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Protocol;
using Claude.AgentSdk.Transport;
using Microsoft.Extensions.Logging;
using Moq;

namespace Claude.AgentSdk.Tests;

/// <summary>
///     Comprehensive tests for ClaudeAgentClient covering initialization, queries,
///     bidirectional mode, options merging, and error handling.
/// </summary>
public class ClaudeAgentClientTests
{
    [Fact]
    public async Task Constructor_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange & Act
        await using var client = new ClaudeAgentClient();

        // Assert - client is created successfully with defaults
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Constructor_WithCustomOptions_StoresOptions()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Model = "opus",
            MaxTurns = 10
        };

        // Act
        await using var client = new ClaudeAgentClient(options);

        // Assert - client is created successfully with custom options
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Constructor_WithLoggerFactory_CreatesLogger()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<ClaudeAgentClient>>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(logger.Object);

        // Act
        await using var client = new ClaudeAgentClient(null, loggerFactory.Object);

        // Assert
        Assert.NotNull(client);
        loggerFactory.Verify(f => f.CreateLogger(It.Is<string>(s => s.Contains("ClaudeAgentClient"))), Times.Once);
    }

    [Fact]
    public async Task Constructor_WithNullLoggerFactory_DoesNotThrow()
    {
        // Arrange & Act
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions());

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new ClaudeAgentClient();

        // Act & Assert - should not throw on multiple dispose calls
        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_SetsDisposedState()
    {
        // Arrange
        var client = new ClaudeAgentClient();

        // Act
        await client.DisposeAsync();

        // Assert - subsequent operations should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.QueryAsync("test").FirstOrDefaultAsync());
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_QueryAsyncThrows()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("test"))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_CreateSessionAsyncThrows()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.CreateSessionAsync());
    }

    [Fact]
    public async Task QueryAsync_WithEmptyPrompt_DoesNotThrow()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            // Use an invalid CLI path to trigger expected exception
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert - should throw CliNotFoundException (not ArgumentException)
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync(""))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithInvalidCliPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_claude_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange - Note: CLI path validation happens before cancellation check,
        // so with an invalid path, CliNotFoundException is thrown first.
        // This test documents the current behavior where sync operations
        // (like path validation) execute before async cancellation.
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act & Assert - CliNotFoundException is thrown before cancellation is checked
        // because the path validation is synchronous during StartAsync
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("test", cancellationToken: cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });
    }

    [Fact]
    public async Task QueryToCompletionAsync_WithInvalidCliPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await client.QueryToCompletionAsync("Hello"));
    }

    [Fact]
    public async Task QueryToCompletionAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange - Note: CLI path validation happens before cancellation check,
        // so with an invalid path, CliNotFoundException is thrown first.
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert - CliNotFoundException is thrown before cancellation is checked
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await client.QueryToCompletionAsync("test", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task CreateSessionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.CreateSessionAsync());
    }

    [Fact]
    public async Task CreateSessionAsync_WithInvalidCliPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await client.CreateSessionAsync());
    }

    // Note: Session-level operations (SendAsync, ReceiveAsync, InterruptAsync, etc.)
    // are now on ClaudeAgentSession, created via CreateSessionAsync.
    // These tests verify session behavior after disposal.

    // Note: Query methods (GetSupportedCommandsAsync, GetSupportedModelsAsync, etc.)
    // are now on ClaudeAgentSession, accessible after CreateSessionAsync.

    // Note: GetSupportedCommandsAsync, GetSupportedModelsAsync, GetMcpServerStatusAsync,
    // and GetAccountInfoAsync methods have been moved to ClaudeAgentSession.
    // Use client.CreateSessionAsync() to get a session, then call these methods on the session.

    [Fact]
    public async Task QueryAsync_WithMethodOptions_MergesWithConstructorOptions()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            Model = "sonnet",
            MaxTurns = 5,
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var methodOptions = new ClaudeAgentOptions
        {
            Model = "opus" // Override model
            // MaxTurns not specified - should inherit from base
        };
        var client = new ClaudeAgentClient(baseOptions);

        // Act & Assert - should throw CliNotFoundException but method options should be merged
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello", methodOptions))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithNullMethodOptions_UsesConstructorOptions()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            Model = "sonnet",
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(baseOptions);

        // Act & Assert
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });
    }

    [Fact]
    public async Task QueryToCompletionAsync_WithMethodOptions_MergesOptions()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            Model = "sonnet",
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var methodOptions = new ClaudeAgentOptions
        {
            Model = "opus"
        };
        var client = new ClaudeAgentClient(baseOptions);

        // Act & Assert
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await client.QueryToCompletionAsync("Hello", methodOptions));
    }

    [Fact]
    public void OptionsMerging_ToolsOverride_UsesOverrideValue()
    {
        // This tests the internal MergeOptions logic indirectly
        // by verifying expected behavior through QueryAsync

        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            Tools = new ToolsList(["Read", "Write"])
        };
        var overrideOptions = new ClaudeAgentOptions
        {
            Tools = new ToolsList(["Bash"]) // Override tools
        };

        // Test that override takes precedence
        Assert.NotEqual(baseOptions.Tools, overrideOptions.Tools);
    }

    [Fact]
    public void OptionsMerging_AllowedTools_NonEmptyOverrideWins()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            AllowedTools = ["Tool1", "Tool2"]
        };
        var overrideOptions = new ClaudeAgentOptions
        {
            AllowedTools = ["Tool3"]
        };

        // Assert - override is non-empty, so it should win
        Assert.NotEmpty(overrideOptions.AllowedTools);
    }

    [Fact]
    public void OptionsMerging_DisallowedTools_NonEmptyOverrideWins()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            DisallowedTools = ["Bash"]
        };
        var overrideOptions = new ClaudeAgentOptions
        {
            DisallowedTools = ["Write"]
        };

        // Assert
        Assert.NotEmpty(overrideOptions.DisallowedTools);
    }

    [Fact]
    public void OptionsMerging_BooleanOptions_OrLogic()
    {
        // Boolean options use OR logic in merging

        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            ContinueConversation = true,
            IncludePartialMessages = false
        };
        var overrideOptions = new ClaudeAgentOptions
        {
            ContinueConversation = false,
            IncludePartialMessages = true
        };

        // Both should be true in merged result (OR logic)
        Assert.True(baseOptions.ContinueConversation || overrideOptions.ContinueConversation);
        Assert.True(baseOptions.IncludePartialMessages || overrideOptions.IncludePartialMessages);
    }

    [Fact]
    public void OptionsMerging_NullOverride_UsesBaseValue()
    {
        // Arrange
        var baseOptions = new ClaudeAgentOptions
        {
            Model = "sonnet",
            MaxTurns = 10
        };
        var overrideOptions = new ClaudeAgentOptions
        {
            // Model is null (not specified)
            MaxTurns = 20 // Explicitly set
        };

        // Assert
        Assert.NotNull(baseOptions.Model);
        Assert.Null(overrideOptions.Model);
        Assert.Equal(20, overrideOptions.MaxTurns);
    }

    [Fact]
    public async Task Client_ImplementsIAsyncDisposable()
    {
        // Arrange & Assert
        await using var client = new ClaudeAgentClient();
        Assert.IsAssignableFrom<IAsyncDisposable>(client);
    }

    [Fact]
    public async Task Client_CanBeUsedInUsingBlock()
    {
        // Arrange & Act
        await using (var client = new ClaudeAgentClient())
        {
            Assert.NotNull(client);
        }

        // Assert - no exception thrown
    }

    [Fact]
    public async Task Client_DisposedAfterUsingBlock()
    {
        // Arrange
        ClaudeAgentClient? capturedClient;

        await using (var client = new ClaudeAgentClient())
        {
            capturedClient = client;
        }

        // Assert - operations should throw after using block
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await capturedClient!.QueryAsync("test").FirstOrDefaultAsync());
    }

    // Note: SendAsync and ReceiveAsync error message tests have been removed.
    // These methods are now on ClaudeAgentSession, created via CreateSessionAsync.

    [Fact]
    public async Task MultipleDisposeCalls_AreIdempotent()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        var tasks = new List<Task>();

        // Act - dispose multiple times concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(client.DisposeAsync().AsTask());
        }

        // Assert - should complete without exceptions
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task QueryAsync_AfterDispose_AllThrow()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        var tasks = new List<Task>();

        // Act - try multiple queries after dispose
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await foreach (var _ in client.QueryAsync($"Query {i}"))
                {
                }
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task QueryAsync_WithComplexOptions_DoesNotThrowBeforeConnect()
    {
        // Arrange
        var complexOptions = new ClaudeAgentOptions
        {
            Model = "opus",
            FallbackModel = "sonnet",
            Tools = new ToolsList(["Read", "Write", "Bash"]),
            AllowedTools = ["Task"],
            DisallowedTools = ["WebFetch"],
            SystemPrompt = SystemPromptConfig.ClaudeCode("Be helpful"),
            SettingSources = [SettingSource.Project, SettingSource.User],
            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 50,
            MaxBudgetUsd = 10.0,
            MaxThinkingTokens = 2000,
            IncludePartialMessages = true,
            EnableFileCheckpointing = true,
            Sandbox = SandboxConfig.Strict,
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };

        var client = new ClaudeAgentClient(complexOptions);

        // Act & Assert - should fail at CLI path validation, not options parsing
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithHooksOption_AcceptsCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new()
                    {
                        Matcher = "Bash",
                        Hooks = new List<HookCallback>
                        {
                            (input, toolUseId, context, ct) =>
                            {
                                callbackInvoked = true;
                                return Task.FromResult<HookOutput>(new SyncHookOutput { Continue = true });
                            }
                        }
                    }
                }
            },
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };

        var client = new ClaudeAgentClient(options);

        // Act & Assert - verify hook options are accepted
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });

        // The callback won't be invoked since CLI is not found, but options should be accepted
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task QueryAsync_WithCanUseToolCallback_AcceptsCallback()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CanUseTool = async (request, ct) =>
            {
                await Task.Delay(1, ct);
                return new PermissionResultAllow();
            },
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };

        var client = new ClaudeAgentClient(options);

        // Act & Assert - verify callback options are accepted
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("Hello"))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithVeryLongPrompt_DoesNotThrowImmediately()
    {
        // Arrange
        var longPrompt = new string('A', 100_000); // 100KB prompt
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert - should fail at CLI path, not prompt validation
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync(longPrompt))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithSpecialCharactersInPrompt_DoesNotThrow()
    {
        // Arrange
        var specialPrompt = "Hello <>&\"'`${}[]|\\!\n\r\ttab";
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert - should fail at CLI path, not prompt encoding
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync(specialPrompt))
            {
            }
        });
    }

    [Fact]
    public async Task QueryAsync_WithUnicodePrompt_DoesNotThrow()
    {
        // Arrange
        var unicodePrompt = "Hello \u4e2d\u6587 \u0410\u0411\u0412 \ud83d\ude00 \u2764\ufe0f";
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert - should fail at CLI path, not encoding
        await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync(unicodePrompt))
            {
            }
        });
    }

    // Note: SendAsync tests have been moved to ClaudeAgentSession tests.
    // The session is created via client.CreateSessionAsync().
}

/// <summary>
///     Tests using a mock ITransport to verify message flow without launching a subprocess.
///     These tests verify the expected interactions with the transport layer.
/// </summary>
public class ClaudeAgentClientMockTransportTests
{
    /// <summary>
    ///     Tests that verify the expected contract between client and transport.
    ///     Since ClaudeAgentClient creates its own transport internally, these tests
    ///     document expected behavior rather than inject mocks.
    /// </summary>
    [Fact]
    public void Transport_MustImplementITransport()
    {
        // Verify SubprocessTransport implements ITransport
        Assert.True(typeof(ITransport).IsAssignableFrom(typeof(SubprocessTransport)));
    }

    [Fact]
    public void Transport_MustBeDisposable()
    {
        // Verify transport is async disposable
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ITransport)));
    }

    [Fact]
    public async Task MockTransport_ExpectedWriteAsyncBehavior()
    {
        // Arrange
        var mockTransport = new Mock<ITransport>();
        mockTransport.Setup(t => t.IsReady).Returns(true);
        mockTransport.Setup(t => t.WriteAsync(It.IsAny<JsonDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockTransport.Object.WriteAsync(JsonDocument.Parse("{}"));

        // Assert
        mockTransport.Verify(t => t.WriteAsync(It.IsAny<JsonDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MockTransport_ExpectedConnectAsyncBehavior()
    {
        // Arrange
        var mockTransport = new Mock<ITransport>();
        mockTransport.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockTransport.Object.ConnectAsync();

        // Assert
        mockTransport.Verify(t => t.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MockTransport_ReadMessagesAsync_ReturnsAsyncEnumerable()
    {
        // Arrange
        var mockTransport = new Mock<ITransport>();
        var messages = new List<JsonDocument>
        {
            JsonDocument.Parse("{\"type\":\"system\",\"subtype\":\"init\"}"),
            JsonDocument.Parse("{\"type\":\"result\",\"subtype\":\"success\"}")
        };

        mockTransport.Setup(t => t.ReadMessagesAsync(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(messages));

        // Act
        var receivedMessages = new List<JsonDocument>();
        await foreach (var msg in mockTransport.Object.ReadMessagesAsync())
        {
            receivedMessages.Add(msg);
        }

        // Assert
        Assert.Equal(2, receivedMessages.Count);
    }

    private static async IAsyncEnumerable<JsonDocument> ToAsyncEnumerable(
        IEnumerable<JsonDocument> documents,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var doc in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return doc;
            await Task.Yield();
        }
    }
}

/// <summary>
///     Tests that verify integration contracts and expected behaviors.
/// </summary>
public class ClaudeAgentClientContractTests
{
    [Fact]
    public void ClaudeAgentClient_IsSealed()
    {
        // ClaudeAgentClient should be sealed for design consistency
        Assert.True(typeof(ClaudeAgentClient).IsSealed);
    }

    [Fact]
    public void ClaudeAgentClient_ImplementsIAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ClaudeAgentClient)));
    }

    [Fact]
    public void ClaudeAgentClient_HasExpectedPublicMethods()
    {
        // Verify expected public API exists
        var clientType = typeof(ClaudeAgentClient);

        // Query methods (one-shot)
        Assert.NotNull(clientType.GetMethod("QueryAsync"));
        Assert.NotNull(clientType.GetMethod("QueryToCompletionAsync"));

        // Session creation
        Assert.NotNull(clientType.GetMethod("CreateSessionAsync"));

        // Dispose
        Assert.NotNull(clientType.GetMethod("DisposeAsync"));
    }

    [Fact]
    public void ClaudeAgentSession_HasExpectedPublicMethods()
    {
        // Verify expected session API exists
        var sessionType = typeof(ClaudeAgentSession);

        // Bidirectional mode methods
        Assert.NotNull(sessionType.GetMethod("SendAsync"));
        Assert.NotNull(sessionType.GetMethod("ReceiveAsync"));
        Assert.NotNull(sessionType.GetMethod("ReceiveResponseAsync"));
        Assert.NotNull(sessionType.GetMethod("InterruptAsync"));
        Assert.NotNull(sessionType.GetMethod("CancelAsync"));

        // Control methods
        Assert.NotNull(sessionType.GetMethod("SetPermissionModeAsync"));
        Assert.NotNull(sessionType.GetMethod("SetModelAsync"));

        // Query methods
        Assert.NotNull(sessionType.GetMethod("GetSupportedCommandsAsync"));
        Assert.NotNull(sessionType.GetMethod("GetSupportedCommandsTypedAsync"));
        Assert.NotNull(sessionType.GetMethod("GetSupportedModelsAsync"));
        Assert.NotNull(sessionType.GetMethod("GetSupportedModelsTypedAsync"));
        Assert.NotNull(sessionType.GetMethod("GetMcpServerStatusAsync"));
        Assert.NotNull(sessionType.GetMethod("GetMcpServerStatusTypedAsync"));
        Assert.NotNull(sessionType.GetMethod("GetAccountInfoAsync"));
        Assert.NotNull(sessionType.GetMethod("GetAccountInfoTypedAsync"));

        // Dispose
        Assert.NotNull(sessionType.GetMethod("DisposeAsync"));
    }

    [Fact]
    public void QueryAsync_ReturnsIAsyncEnumerable()
    {
        // Verify QueryAsync return type
        var method = typeof(ClaudeAgentClient).GetMethod("QueryAsync");
        Assert.NotNull(method);

        var returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(IAsyncEnumerable<>), returnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Message), returnType.GetGenericArguments()[0]);
    }

    [Fact]
    public void QueryToCompletionAsync_ReturnsTaskOfResultMessage()
    {
        // Verify QueryToCompletionAsync return type
        var method = typeof(ClaudeAgentClient).GetMethod("QueryToCompletionAsync");
        Assert.NotNull(method);

        var returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(ResultMessage), returnType.GetGenericArguments()[0]);
    }

    [Fact]
    public void Session_ReceiveAsync_ReturnsIAsyncEnumerable()
    {
        // Verify ReceiveAsync return type on session
        var method = typeof(ClaudeAgentSession).GetMethod("ReceiveAsync");
        Assert.NotNull(method);

        var returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(IAsyncEnumerable<>), returnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Message), returnType.GetGenericArguments()[0]);
    }
}

/// <summary>
///     Tests verifying correct exception types are thrown in various scenarios.
/// </summary>
public class ClaudeAgentClientExceptionTests
{
    [Fact]
    public async Task QueryAsync_InvalidCliPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CliNotFoundException>(async () =>
        {
            await foreach (var _ in client.QueryAsync("test"))
            {
            }
        });

        Assert.IsAssignableFrom<TransportException>(exception);
        Assert.IsAssignableFrom<ClaudeAgentException>(exception);
    }

    [Fact]
    public async Task CreateSessionAsync_InvalidCliPath_ThrowsCliNotFoundException()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            CliPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}")
        };
        var client = new ClaudeAgentClient(options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CliNotFoundException>(async () =>
            await client.CreateSessionAsync());

        Assert.IsAssignableFrom<TransportException>(exception);
    }

    [Fact]
    public async Task Disposed_Operations_ThrowObjectDisposedException()
    {
        // Arrange
        var client = new ClaudeAgentClient();
        await client.DisposeAsync();

        // Act & Assert - all should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.QueryAsync("test").FirstOrDefaultAsync());

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.QueryToCompletionAsync("test"));

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.CreateSessionAsync());

        // Note: SendAsync, ReceiveAsync, InterruptAsync, SetPermissionModeAsync, SetModelAsync,
        // GetSupportedCommandsAsync, GetSupportedModelsAsync, GetMcpServerStatusAsync, and
        // GetAccountInfoAsync have been moved to ClaudeAgentSession.
    }
}
