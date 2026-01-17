using Claude.AgentSdk.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for configuring Claude agent services with dependency injection.
/// </summary>
/// <remarks>
///     <para>
///         These extensions integrate the Claude Agent SDK with ASP.NET Core's dependency injection container,
///         enabling configuration through appsettings.json, IOptions pattern, and named instances.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     // Basic registration with fluent configuration
///     services.AddClaudeAgent(options => {
///         options.Model = "sonnet";
///         options.AllowedTools = ["Read", "Write"];
///     });
/// 
///     // Configuration binding from appsettings.json
///     services.AddClaudeAgent(configuration.GetSection("Claude"));
/// 
///     // Named instances for multiple agents
///     services.AddClaudeAgent("analyzer", options => options.Model = "sonnet");
///     services.AddClaudeAgent("generator", options => options.Model = "opus");
/// 
///     // With MCP tool servers
///     services.AddClaudeAgent(options => options.Model = "sonnet")
///         .AddMcpServer("tools", myToolServer);
///     </code>
///     </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Claude agent services with fluent configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the options.</param>
    /// <returns>A builder for further configuration.</returns>
    public static IClaudeAgentBuilder AddClaudeAgent(
        this IServiceCollection services,
        Action<ClaudeAgentConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        return services.AddClaudeAgentCore();
    }

    /// <summary>
    ///     Adds Claude agent services with configuration section binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <returns>A builder for further configuration.</returns>
    public static IClaudeAgentBuilder AddClaudeAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ClaudeAgentConfiguration>(configuration);
        return services.AddClaudeAgentCore();
    }

    /// <summary>
    ///     Adds Claude agent services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>A builder for further configuration.</returns>
    public static IClaudeAgentBuilder AddClaudeAgent(this IServiceCollection services)
    {
        return services.AddClaudeAgentCore();
    }

    /// <summary>
    ///     Adds a named Claude agent instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name for this agent instance.</param>
    /// <param name="configure">Action to configure the options.</param>
    /// <returns>A builder for further configuration.</returns>
    public static IClaudeAgentBuilder AddClaudeAgent(
        this IServiceCollection services,
        string name,
        Action<ClaudeAgentConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(name, configure);
        return services.AddClaudeAgentCore(name);
    }

    /// <summary>
    ///     Adds a named Claude agent instance with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name for this agent instance.</param>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <returns>A builder for further configuration.</returns>
    public static IClaudeAgentBuilder AddClaudeAgent(
        this IServiceCollection services,
        string name,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ClaudeAgentConfiguration>(name, configuration);
        return services.AddClaudeAgentCore(name);
    }

#pragma warning disable CA1859 // Return IClaudeAgentBuilder for public API consistency
    private static IClaudeAgentBuilder AddClaudeAgentCore(
        this IServiceCollection services,
        string? name = null)
    {
        // Register the factory
        services.TryAddSingleton<IClaudeAgentClientFactory, ClaudeAgentClientFactory>();

        // Register options infrastructure
        services.AddOptions();

        // Register the default ClaudeAgentClient for unnamed registration
        if (name is null)
        {
            services.TryAddScoped(sp =>
            {
                IClaudeAgentClientFactory factory = sp.GetRequiredService<IClaudeAgentClientFactory>();
                return factory.CreateClient();
            });
        }

        return new ClaudeAgentBuilder(services, name);
    }
}

/// <summary>
///     Builder for configuring Claude agent services.
/// </summary>
public interface IClaudeAgentBuilder
{
    /// <summary>
    ///     The service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     The name of this agent instance (null for default).
    /// </summary>
    string? Name { get; }

    /// <summary>
    ///     Adds an MCP tool server.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="server">The tool server instance.</param>
    /// <returns>This builder for chaining.</returns>
    IClaudeAgentBuilder AddMcpServer(string serverName, IMcpToolServer server);

    /// <summary>
    ///     Adds an MCP tool server using a factory.
    /// </summary>
    /// <typeparam name="TServer">The server type.</typeparam>
    /// <param name="serverName">The server name.</param>
    /// <returns>This builder for chaining.</returns>
    IClaudeAgentBuilder AddMcpServer<TServer>(string serverName) where TServer : class, IMcpToolServer;

    /// <summary>
    ///     Configures additional options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>This builder for chaining.</returns>
    IClaudeAgentBuilder Configure(Action<ClaudeAgentConfiguration> configure);

    /// <summary>
    ///     Adds post-configuration actions.
    /// </summary>
    /// <param name="postConfigure">Action to post-configure options.</param>
    /// <returns>This builder for chaining.</returns>
    IClaudeAgentBuilder PostConfigure(Action<ClaudeAgentConfiguration> postConfigure);
}

internal sealed class ClaudeAgentBuilder : IClaudeAgentBuilder
{
    internal ClaudeAgentBuilder(IServiceCollection services, string? name)
    {
        Services = services;
        Name = name;
    }

    public IServiceCollection Services { get; }
    public string? Name { get; }

    public IClaudeAgentBuilder AddMcpServer(string serverName, IMcpToolServer server)
    {
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(server);

        // Register the server instance
        Services.AddSingleton(new McpServerRegistration(Name, serverName, server));
        return this;
    }

    public IClaudeAgentBuilder AddMcpServer<TServer>(string serverName)
        where TServer : class, IMcpToolServer
    {
        ArgumentNullException.ThrowIfNull(serverName);

        // Register the server type
        Services.AddSingleton<TServer>();
        Services.AddSingleton(sp =>
            new McpServerRegistration(Name, serverName, sp.GetRequiredService<TServer>()));
        return this;
    }

    public IClaudeAgentBuilder Configure(Action<ClaudeAgentConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (Name is null)
        {
            Services.Configure(configure);
        }
        else
        {
            Services.Configure(Name, configure);
        }

        return this;
    }

    public IClaudeAgentBuilder PostConfigure(Action<ClaudeAgentConfiguration> postConfigure)
    {
        ArgumentNullException.ThrowIfNull(postConfigure);

        if (Name is null)
        {
            Services.PostConfigure(postConfigure);
        }
        else
        {
            Services.PostConfigure(Name, postConfigure);
        }

        return this;
    }
}

/// <summary>
///     Registration for MCP servers with their associated agent name.
/// </summary>
internal sealed record McpServerRegistration(string? AgentName, string ServerName, IMcpToolServer Server);

/// <summary>
///     Factory for creating Claude agent client instances.
/// </summary>
public interface IClaudeAgentClientFactory
{
    /// <summary>
    ///     Creates the default Claude agent client.
    /// </summary>
    ClaudeAgentClient CreateClient();

    /// <summary>
    ///     Creates a named Claude agent client.
    /// </summary>
    /// <param name="name">The agent name.</param>
    ClaudeAgentClient CreateClient(string name);

    /// <summary>
    ///     Creates a Claude agent client with custom options.
    /// </summary>
    /// <param name="options">The agent options.</param>
    ClaudeAgentClient CreateClient(ClaudeAgentOptions options);
}

internal sealed class ClaudeAgentClientFactory(
    IOptionsMonitor<ClaudeAgentConfiguration> optionsMonitor,
    IEnumerable<McpServerRegistration> mcpServers,
    ILoggerFactory? loggerFactory = null)
    : IClaudeAgentClientFactory
{
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;
    private readonly IEnumerable<McpServerRegistration> _mcpServers = mcpServers;
    private readonly IOptionsMonitor<ClaudeAgentConfiguration> _optionsMonitor = optionsMonitor;

    public ClaudeAgentClient CreateClient()
    {
        ClaudeAgentConfiguration config = _optionsMonitor.CurrentValue;
        return CreateClientFromConfig(config, null);
    }

    public ClaudeAgentClient CreateClient(string name)
    {
        ClaudeAgentConfiguration config = _optionsMonitor.Get(name);
        return CreateClientFromConfig(config, name);
    }

    public ClaudeAgentClient CreateClient(ClaudeAgentOptions options)
    {
        return new ClaudeAgentClient(options, _loggerFactory);
    }

    private ClaudeAgentClient CreateClientFromConfig(ClaudeAgentConfiguration config, string? agentName)
    {
        ClaudeAgentOptions options = config.ToOptions();

        // Add registered MCP servers for this agent
        Dictionary<string, McpServerConfig> servers = _mcpServers
            .Where(r => r.AgentName == agentName)
            .ToDictionary(
                r => r.ServerName,
                r => (McpServerConfig)new McpSdkServerConfig
                {
                    Name = r.ServerName,
                    Instance = r.Server
                });

        if (servers.Count > 0)
        {
            // Use 'with' expression to create a new options with merged MCP servers
            Dictionary<string, McpServerConfig> mergedServers = options.McpServers is null
                ? servers
                : options.McpServers.Concat(servers).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            options = options with { McpServers = mergedServers };
        }

        return new ClaudeAgentClient(options, _loggerFactory);
    }
}
