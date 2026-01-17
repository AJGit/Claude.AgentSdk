using Claude.AgentSdk.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     Base class for implementing long-running Claude agent background services.
/// </summary>
/// <remarks>
///     <para>
///         Extend this class to create background services that continuously process
///         messages from a Claude agent session.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     public class LogMonitorService : ClaudeAgentBackgroundService
///     {
///         public LogMonitorService(
///             IClaudeAgentClientFactory factory,
///             ILogger&lt;LogMonitorService&gt; logger)
///             : base(factory, logger) { }
/// 
///         protected override string GetInitialPrompt() =>
///             "Monitor the system logs and alert on errors.";
/// 
///         protected override async Task OnMessageAsync(
///             Message message,
///             CancellationToken ct)
///         {
///             if (message is AssistantMessage assistant)
///             {
///                 Logger.LogInformation("Agent: {Text}", assistant.GetText());
///             }
///         }
///     }
/// 
///     // Registration:
///     services.AddHostedService&lt;LogMonitorService&gt;();
///     </code>
///     </para>
/// </remarks>
public abstract class ClaudeAgentBackgroundService : BackgroundService
{
    private readonly IClaudeAgentClientFactory _factory;
    private ClaudeAgentClient? _client;
    private ClaudeAgentSession? _session;

    /// <summary>
    ///     Creates a new background service.
    /// </summary>
    /// <param name="factory">The client factory.</param>
    /// <param name="logger">The logger.</param>
    protected ClaudeAgentBackgroundService(
        IClaudeAgentClientFactory factory,
        ILogger logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     The logger instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    ///     The current client instance.
    /// </summary>
    protected ClaudeAgentClient Client => _client ?? throw new InvalidOperationException("Client not initialized.");

    /// <summary>
    ///     The current session instance.
    /// </summary>
    protected ClaudeAgentSession Session => _session ?? throw new InvalidOperationException("Session not initialized.");

    /// <summary>
    ///     Gets the name of the agent to use (for named agents). Return null for default.
    /// </summary>
    protected virtual string? GetAgentName()
    {
        return null;
    }

    /// <summary>
    ///     Gets the initial prompt to send to the agent.
    /// </summary>
    protected abstract string GetInitialPrompt();

    /// <summary>
    ///     Configures the agent options before creating the client.
    ///     Override to customize agent configuration.
    /// </summary>
    protected virtual void ConfigureAgent(ClaudeAgentOptions options) { }

    /// <summary>
    ///     Called for each message received from the agent.
    /// </summary>
    protected abstract Task OnMessageAsync(Message message, CancellationToken ct);

    /// <summary>
    ///     Called when the session completes normally.
    /// </summary>
    protected virtual Task OnSessionCompletedAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Called when an error occurs during processing.
    /// </summary>
    protected virtual Task OnErrorAsync(Exception exception, CancellationToken ct)
    {
        Logger.LogError(exception, "Error in Claude agent background service");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Determines whether the service should restart after an error.
    ///     Default is true with exponential backoff.
    /// </summary>
    protected virtual bool ShouldRestartOnError(Exception exception, int attemptCount)
    {
        return attemptCount < 5;
        // Max 5 restart attempts
    }

    /// <summary>
    ///     Gets the delay before restarting after an error.
    /// </summary>
    protected virtual TimeSpan GetRestartDelay(int attemptCount)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, attemptCount));
        // Exponential backoff
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int attemptCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attemptCount++;
                await RunAgentAsync(stoppingToken).ConfigureAwait(false);

                // If we get here, the session completed normally
                await OnSessionCompletedAsync(stoppingToken).ConfigureAwait(false);

                // Reset attempt count on successful completion
                attemptCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                Logger.LogInformation("Claude agent background service stopping");
                break;
            }
            catch (Exception ex)
            {
                await OnErrorAsync(ex, stoppingToken).ConfigureAwait(false);

                if (ShouldRestartOnError(ex, attemptCount))
                {
                    TimeSpan delay = GetRestartDelay(attemptCount);
                    Logger.LogWarning(
                        "Restarting Claude agent background service in {Delay} (attempt {Attempt})",
                        delay, attemptCount);

                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    Logger.LogError("Claude agent background service stopped after {Attempts} failed attempts",
                        attemptCount);
                    break;
                }
            }
        }
    }

    private async Task RunAgentAsync(CancellationToken ct)
    {
        string? agentName = GetAgentName();

        _client = agentName is null
            ? _factory.CreateClient()
            : _factory.CreateClient(agentName);

        Logger.LogInformation("Starting Claude agent session");

        _session = await _client.CreateSessionAsync(ct).ConfigureAwait(false);
        string prompt = GetInitialPrompt();

        // Send the initial prompt
        await _session.SendAsync(prompt, cancellationToken: ct).ConfigureAwait(false);

        // Receive responses until completion
        await foreach (Message message in _session.ReceiveResponseAsync(ct).ConfigureAwait(false))
        {
            await OnMessageAsync(message, ct).ConfigureAwait(false);
        }

        Logger.LogInformation("Claude agent session completed");
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }

        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Extension methods for registering Claude agent background services.
/// </summary>
public static class BackgroundServiceExtensions
{
    /// <summary>
    ///     Adds a hosted Claude agent background service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="builder">The Claude agent builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IClaudeAgentBuilder AddClaudeAgentHostedService<TService>(this IClaudeAgentBuilder builder)
        where TService : ClaudeAgentBackgroundService
    {
        builder.Services.AddHostedService<TService>();
        return builder;
    }
}
