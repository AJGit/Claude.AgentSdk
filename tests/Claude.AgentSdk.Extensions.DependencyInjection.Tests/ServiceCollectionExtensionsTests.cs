using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Claude.AgentSdk.Extensions.DependencyInjection.Tests;

/// <summary>
///     Tests for ServiceCollectionExtensions.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddClaudeAgent_RegistersClaudeAgentClient()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
        });

        // Act
        ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ClaudeAgentClient? client = scope.ServiceProvider.GetService<ClaudeAgentClient>();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void AddClaudeAgent_WithConfigure_RegistersServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IClaudeAgentBuilder builder = services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
            options.MaxTurns = 10;
        });

        // Assert
        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);
        Assert.Null(builder.Name);

        ServiceProvider provider = services.BuildServiceProvider();
        IClaudeAgentClientFactory? factory = provider.GetService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddClaudeAgent_WithoutConfigure_RegistersServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IClaudeAgentBuilder builder = services.AddClaudeAgent();

        // Assert
        Assert.NotNull(builder);
        ServiceProvider provider = services.BuildServiceProvider();
        IClaudeAgentClientFactory? factory = provider.GetService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddClaudeAgent_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddClaudeAgent((Action<ClaudeAgentConfiguration>)null!));
    }

    [Fact]
    public void AddClaudeAgent_WithName_CreatesNamedBuilder()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IClaudeAgentBuilder builder = services.AddClaudeAgent("analyzer", options =>
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
        ServiceCollection services = new();

        // Act
        services.AddClaudeAgent("analyzer", options => options.Model = "sonnet");
        services.AddClaudeAgent("generator", options => options.Model = "opus");

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<ClaudeAgentConfiguration> optionsMonitor =
            provider.GetRequiredService<IOptionsMonitor<ClaudeAgentConfiguration>>();

        ClaudeAgentConfiguration analyzerConfig = optionsMonitor.Get("analyzer");
        ClaudeAgentConfiguration generatorConfig = optionsMonitor.Get("generator");

        Assert.Equal("sonnet", analyzerConfig.Model);
        Assert.Equal("opus", generatorConfig.Model);
    }

    [Fact]
    public void AddClaudeAgent_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddClaudeAgent(null!, options => { }));
    }

    [Fact]
    public void AddClaudeAgent_ConfigurationIsAccessibleViaIOptions()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddClaudeAgent(options =>
        {
            options.Model = "sonnet";
            options.MaxTurns = 15;
            options.MaxBudgetUsd = 5.0;
            options.WorkingDirectory = "C:/Test";
            options.SystemPrompt = "You are helpful.";
        });

        // Act
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ClaudeAgentConfiguration> options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

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
        ServiceCollection services = new();
        services.AddClaudeAgent(options =>
        {
            options.AllowedTools = ["Read", "Write", "Bash"];
        });

        // Act
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ClaudeAgentConfiguration> options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

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
        ServiceCollection services = new();
        services.AddClaudeAgent(options =>
        {
            options.DisallowedTools = ["WebSearch"];
        });

        // Act
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ClaudeAgentConfiguration> options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();

        // Assert
        Assert.NotNull(options.Value.DisallowedTools);
        Assert.Single(options.Value.DisallowedTools);
        Assert.Contains("WebSearch", options.Value.DisallowedTools);
    }

    [Fact]
    public void IClaudeAgentBuilder_Configure_ChainsCorrectly()
    {
        // Arrange
        ServiceCollection services = new();
        IClaudeAgentBuilder builder = services.AddClaudeAgent();

        // Act
        IClaudeAgentBuilder result = builder
            .Configure(o => o.Model = "sonnet")
            .Configure(o => o.MaxTurns = 10);

        // Assert
        Assert.Same(builder, result);

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ClaudeAgentConfiguration> options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();
        Assert.Equal("sonnet", options.Value.Model);
        Assert.Equal(10, options.Value.MaxTurns);
    }

    [Fact]
    public void IClaudeAgentBuilder_PostConfigure_ChainsCorrectly()
    {
        // Arrange
        ServiceCollection services = new();
        IClaudeAgentBuilder builder = services.AddClaudeAgent(options =>
        {
            options.MaxTurns = 5;
        });

        // Act
        builder.PostConfigure(o => o.MaxTurns *= 2);

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ClaudeAgentConfiguration> options = provider.GetRequiredService<IOptions<ClaudeAgentConfiguration>>();
        Assert.Equal(10, options.Value.MaxTurns);
    }

    [Fact]
    public void IClaudeAgentBuilder_Configure_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();
        IClaudeAgentBuilder builder = services.AddClaudeAgent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.Configure(null!));
    }

    [Fact]
    public void IClaudeAgentBuilder_PostConfigure_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();
        IClaudeAgentBuilder builder = services.AddClaudeAgent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.PostConfigure(null!));
    }

    [Fact]
    public void Factory_IsSingleton()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddClaudeAgent();

        // Act
        ServiceProvider provider = services.BuildServiceProvider();
        IClaudeAgentClientFactory factory1 = provider.GetRequiredService<IClaudeAgentClientFactory>();
        IClaudeAgentClientFactory factory2 = provider.GetRequiredService<IClaudeAgentClientFactory>();

        // Assert
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void Factory_OnlyRegisteredOnce()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddClaudeAgent();
        services.AddClaudeAgent("named", o => { });

        // Assert - Should not throw despite multiple registrations
        ServiceProvider provider = services.BuildServiceProvider();
        IClaudeAgentClientFactory factory = provider.GetRequiredService<IClaudeAgentClientFactory>();
        Assert.NotNull(factory);
    }
}
