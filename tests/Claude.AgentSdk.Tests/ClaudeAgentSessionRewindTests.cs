namespace Claude.AgentSdk.Tests;

/// <summary>
///     Tests for ClaudeAgentSession.RewindFilesAsync functionality.
/// </summary>
[UnitTest]
public class ClaudeAgentSessionRewindTests
{
    [Fact]
    public void RewindFilesAsync_OnSession_IsExposed()
    {
        // Verify the method exists on ClaudeAgentSession
        var method = typeof(ClaudeAgentSession).GetMethod("RewindFilesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<Claude.AgentSdk.Protocol.RewindFilesResult>), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("userMessageId", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public void RewindFilesAsync_IsPublicMethod()
    {
        // Verify the method is public
        var method = typeof(ClaudeAgentSession).GetMethod("RewindFilesAsync");
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void RewindFilesAsync_HasCorrectSignature()
    {
        // Verify method signature matches the expected pattern
        var method = typeof(ClaudeAgentSession).GetMethod("RewindFilesAsync");
        Assert.NotNull(method);

        // Should return Task<RewindFilesResult>
        Assert.Equal(typeof(Task<Claude.AgentSdk.Protocol.RewindFilesResult>), method.ReturnType);

        // Should have userMessageId and optional cancellationToken parameters
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        // First parameter: userMessageId (string, required)
        Assert.Equal("userMessageId", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].HasDefaultValue);

        // Second parameter: cancellationToken (CancellationToken, optional with default)
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void EnableFileCheckpointing_ExistsOnOptions()
    {
        // Verify the option exists
        var property = typeof(ClaudeAgentOptions).GetProperty("EnableFileCheckpointing");
        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property.PropertyType);
    }

    [Fact]
    public void EnableFileCheckpointing_DefaultsToFalse()
    {
        var options = new ClaudeAgentOptions();
        Assert.False(options.EnableFileCheckpointing);
    }

    [Fact]
    public void EnableFileCheckpointing_CanBeEnabled()
    {
        var options = new ClaudeAgentOptions { EnableFileCheckpointing = true };
        Assert.True(options.EnableFileCheckpointing);
    }
}
