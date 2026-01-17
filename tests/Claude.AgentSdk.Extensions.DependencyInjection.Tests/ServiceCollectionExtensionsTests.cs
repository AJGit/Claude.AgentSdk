using Claude.AgentSdk.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for ServiceCollectionExtensions.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    #region AddClaudeAgent Basic Tests

    [Fact]
    public void AddClaudeAgent_WithConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
            options.MaxTurns = 10;
        });

        // Assert
        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);
        Assert.Null(builder.Name);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddClaudeAgent_WithoutConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddClaudeAgent();

        // Assert
        Assert.NotNull(builder);
        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddClaudeAgent_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddClaudeAgent((Action<ClaudeAgentConfiguration>)null!));
    }

    #endregion

    #region Named Agent Tests

    [Fact]
    public void AddClaudeAgent_WithName_CreatesNamedBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddClaudeAgent("analyzer", options =>
        {
            options.Model = "opus";
        });

        // Assert
        Assert.NotNull(builder);
        Assert.Equal("analyzer", builder.Name);
    }

    [Fact]
    public void AddClaudeAgent_MultipleNamed_RegistersAll()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddClaudeAgent("analyzer", options => options.Model = "sonnet");
        services.AddClaudeAgent("generator", options => options.Model = "opus");

        // Assert
        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ClaudeAgentConfiguration>>();

        var analyzerConfig = optionsMonitor.Get("analyzer");
        var generatorConfig = optionsMonitor.Get("generator");

        Assert.Equal("sonnet", analyzerConfig.Model);
        Assert.Equal("opus", generatorConfig.Model);
    }

    [Fact]
    public void AddClaudeAgent_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddClaudeAgent(null!, options => { }));
    }

    #endregion

    #region IOptions Configuration Tests

    [Fact]
    public void AddClaudeAgent_ConfigurationIsAccessibleViaIOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
            options.MaxTurns = 15;
            options.MaxBudgetUsd = 5.0;
            options.WorkingDirectory = "C:/Test";
            options.SystemPrompt = "You are helpful.";
        });

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

        // Assert
        Assert.Equal("sonnet", options.Value.Model);
        Assert.Equal(15, options.Value.MaxTurns);
        Assert.Equal(5.0, options.Value.MaxBudgetUsd);
        Assert.Equal("C:/Test", options.Value.WorkingDirectory);
        Assert.Equal("You are helpful.", options.Value.SystemPrompt);
    }

    [Fact]
    public void AddClaudeAgent_AllowedTools_AreConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddClaudeAgent(options =>
        {
            options.AllowedTools = ["Read", "Write", "Bash"];
        });

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

        // Assert
        Assert.NotNull(options.Value.AllowedTools);
        Assert.Equal(3, options.Value.AllowedTools.Count);
        Assert.Contains("Read", options.Value.AllowedTools);
        Assert.Contains("Write", options.Value.AllowedTools);
        Assert.Contains("Bash", options.Value.AllowedTools);
    }

    [Fact]
    public void AddClaudeAgent_DisallowedTools_AreConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddClaudeAgent(options =>
        {
            options.DisallowedTools = ["WebSearch"];
        });

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

        // Assert
        Assert.NotNull(options.Value.DisallowedTools);
        Assert.Single(options.Value.DisallowedTools);
        Assert.Contains("WebSearch", options.Value.DisallowedTools);
    }

    #endregion

    #region Builder Chaining Tests

    [Fact]
    public void IClaudeAgentBuilder_Configure_ChainsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddClaudeAgent();

        // Act
        var result = builder
            .Configure(o => o.Model = "sonnet")
            .Configure(o => o.MaxTurns = 10);

        // Assert
        Assert.Same(builder, result);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();
        Assert.Equal("sonnet", options.Value.Model);
        Assert.Equal(10, options.Value.MaxTurns);
    }

    [Fact]
    public void IClaudeAgentBuilder_PostConfigure_ChainsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddClaudeAgent(options =>
        {
            options.MaxTurns = 5;
        });

        // Act
        builder.PostConfigure(o => o.MaxTurns *= 2);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();
        Assert.Equal(10, options.Value.MaxTurns);
    }

    [Fact]
    public void IClaudeAgentBuilder_Configure_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddClaudeAgent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.Configure(null!));
    }

    [Fact]
    public void IClaudeAgentBuilder_PostConfigure_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddClaudeAgent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.PostConfigure(null!));
    }

    #endregion

    #region Factory Tests

    [Fact]
    public void Factory_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddClaudeAgent();

        // Act
        var provider = services.BuildServiceProvider();
        var factory1 = provider.GetRequiredService<IClaudeAgentClientFactory>();
        var factory2 = provider.GetRequiredService<IClaudeAgentClientFactory>();

        // Assert
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void Factory_OnlyRegisteredOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddClaudeAgent();
        services.AddClaudeAgent("named", o => { });

        // Assert - Should not throw despite multiple registrations
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }

    #endregion

    #region ClaudeAgentClient Registration Tests

    [Fact]
    public async Task AddClaudeAgent_RegistersClaudeAgentClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
        });

        // Act
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var client = scope.ServiceProvider.GetService<ClaudeAgentClient>();

        // Assert
        Assert.NotNull(client);
    }

    #endregion
}
