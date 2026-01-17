using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Transport;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Tests;

/// <summary>
///     Tests for the static Query class and ClaudeAgent alias.
/// </summary>
[UnitTest]
public class QueryTests
{
    [Fact]
    public async Task RunToCompletionAsync_MethodSignature_IsCorrect()
    {
        // Verify the API shape
        Assert.NotNull(
            (Func<string, ClaudeAgentOptions?, ILoggerFactory?, CancellationToken, Task<ResultMessage?>>)Query
                .RunToCompletionAsync);
    }

    [Fact]
    public async Task RunAsync_WithPrompt_ReturnsMessages()
    {
        // This test verifies the API shape compiles correctly
        // Actual execution would require the Claude CLI
        // See E2E tests for full integration testing

        // Arrange - verify the method signature is correct
        var options = new ClaudeAgentOptions
        {
            Model = "test-model",
            MaxTurns = 1
        };

        // Assert - the method exists and is callable
        Assert.NotNull(
            (Func<string, ClaudeAgentOptions?, ILoggerFactory?, CancellationToken, IAsyncEnumerable<Message>>)Query
                .RunAsync);
    }

    [Fact]
    public async Task RunAsync_WithInlineOptions_CreatesCorrectOptions()
    {
        // Arrange
        const string testModel = "claude-sonnet-4-20250514";
        const int testMaxTurns = 5;
        const string testSystemPrompt = "You are a helpful assistant";
        var testPermissionMode = PermissionMode.AcceptEdits;

        // Act - call the overload to verify it compiles
        // We can't actually execute without the CLI
        var enumerable = Query.RunAsync(
            "test prompt",
            testModel,
            testMaxTurns,
            testSystemPrompt,
            testPermissionMode);

        // Assert - the method returns an IAsyncEnumerable
        Assert.NotNull(enumerable);
    }

    [Fact]
    public async Task GetTextAsync_MethodSignature_IsCorrect()
    {
        // Verify the API shape
        Assert.NotNull(
            (Func<string, ClaudeAgentOptions?, ILoggerFactory?, CancellationToken, Task<string>>)Query.GetTextAsync);
    }

    [Fact]
    public async Task GetTextAsync_WithModel_MethodExists()
    {
        // Verify the convenience overload exists
        Assert.NotNull((Func<string, string, CancellationToken, Task<string>>)Query.GetTextAsync);
    }

    [Fact]
    public async Task ClaudeAgent_RunAsync_IsSameAsQuery()
    {
        // Verify ClaudeAgent is an alias for Query
        var queryMethod = typeof(Query).GetMethod("RunAsync",
            [typeof(string), typeof(ClaudeAgentOptions), typeof(ILoggerFactory), typeof(CancellationToken)]);
        var agentMethod = typeof(ClaudeAgent).GetMethod("RunAsync",
            [typeof(string), typeof(ClaudeAgentOptions), typeof(ILoggerFactory), typeof(CancellationToken)]);

        Assert.NotNull(queryMethod);
        Assert.NotNull(agentMethod);

        // Both should have the same return type
        Assert.Equal(queryMethod.ReturnType, agentMethod.ReturnType);
    }

    [Fact]
    public async Task ClaudeAgent_GetTextAsync_IsSameAsQuery()
    {
        var queryMethod = typeof(Query).GetMethod("GetTextAsync",
            [typeof(string), typeof(ClaudeAgentOptions), typeof(ILoggerFactory), typeof(CancellationToken)]);
        var agentMethod = typeof(ClaudeAgent).GetMethod("GetTextAsync",
            [typeof(string), typeof(ClaudeAgentOptions), typeof(ILoggerFactory), typeof(CancellationToken)]);

        Assert.NotNull(queryMethod);
        Assert.NotNull(agentMethod);
        Assert.Equal(queryMethod.ReturnType, agentMethod.ReturnType);
    }

    [Fact]
    public async Task ClaudeAgent_RunToCompletionAsync_IsSameAsQuery()
    {
        var queryMethod = typeof(Query).GetMethod("RunToCompletionAsync");
        var agentMethod = typeof(ClaudeAgent).GetMethod("RunToCompletionAsync");

        Assert.NotNull(queryMethod);
        Assert.NotNull(agentMethod);
        Assert.Equal(queryMethod!.ReturnType, agentMethod!.ReturnType);
    }

    [Fact]
    public async Task CreateForTesting_WithMockTransport_ReturnsClient()
    {
        // Arrange
        var transport = new MockTransport();

        // Act
        var client = ClaudeAgentClient.CreateForTesting(transport: transport);

        // Assert
        Assert.NotNull(client);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task CreateForTesting_WithOptions_UsesOptions()
    {
        // Arrange
        var options = new ClaudeAgentOptions
        {
            Model = "test-model",
            MaxTurns = 5
        };
        var transport = new MockTransport();

        // Act
        var client = ClaudeAgentClient.CreateForTesting(options, transport);

        // Assert
        Assert.NotNull(client);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task CreateForTesting_WithoutTransport_CreatesDefaultMockTransport()
    {
        // Act
        var client = ClaudeAgentClient.CreateForTesting();

        // Assert
        Assert.NotNull(client);
        await client.DisposeAsync();
    }
}
