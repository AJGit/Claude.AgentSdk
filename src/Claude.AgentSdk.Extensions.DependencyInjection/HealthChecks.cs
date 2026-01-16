using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     Health check for Claude agent availability.
/// </summary>
/// <remarks>
///     Checks:
///     - CLI is accessible
///     - Configuration is valid
/// </remarks>
public class ClaudeAgentHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<ClaudeAgentConfiguration> _options;
    private readonly string? _agentName;

    /// <summary>
    ///     Creates a health check for the default agent.
    /// </summary>
    public ClaudeAgentHealthCheck(IOptionsMonitor<ClaudeAgentConfiguration> options)
        : this(options, null) { }

    /// <summary>
    ///     Creates a health check for a named agent.
    /// </summary>
    public ClaudeAgentHealthCheck(IOptionsMonitor<ClaudeAgentConfiguration> options, string? agentName)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _agentName = agentName;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            var config = _agentName is null
                ? _options.CurrentValue
                : _options.Get(_agentName);

            // Check CLI availability
            var cliPath = config.CliPath ?? "claude";
            data["cli_path"] = cliPath;

            var cliAvailable = await CheckCliAvailableAsync(cliPath, cancellationToken).ConfigureAwait(false);
            if (!cliAvailable)
            {
                return HealthCheckResult.Unhealthy(
                    "Claude CLI is not accessible",
                    data: data);
            }

            data["cli_available"] = true;

            // Check configuration validity
            if (!string.IsNullOrEmpty(config.Model))
                data["model"] = config.Model;

            if (config.MaxTurns.HasValue)
                data["max_turns"] = config.MaxTurns.Value;

            if (config.MaxBudgetUsd.HasValue)
                data["max_budget_usd"] = config.MaxBudgetUsd.Value;

            if (!string.IsNullOrEmpty(config.WorkingDirectory))
            {
                data["working_directory"] = config.WorkingDirectory;
                if (!Directory.Exists(config.WorkingDirectory))
                {
                    return HealthCheckResult.Degraded(
                        $"Working directory does not exist: {config.WorkingDirectory}",
                        data: data);
                }
            }

            return HealthCheckResult.Healthy("Claude agent is configured and CLI is available", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check Claude agent health",
                ex,
                data);
        }
    }

    private static async Task<bool> CheckCliAvailableAsync(string cliPath, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Wait max 5 seconds for version check
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), ct);
            var waitTask = process.WaitForExitAsync(ct);

            var completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
///     Extension methods for adding Claude agent health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    ///     Adds a health check for the default Claude agent.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The failure status.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddClaudeAgent(
        this IHealthChecksBuilder builder,
        string name = "claude-agent",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new ClaudeAgentHealthCheck(sp.GetRequiredService<IOptionsMonitor<ClaudeAgentConfiguration>>()),
            failureStatus,
            tags));
    }

    /// <summary>
    ///     Adds a health check for a named Claude agent.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The failure status.</param>
    /// <param name="tags">The health check tags.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddClaudeAgent(
        this IHealthChecksBuilder builder,
        string agentName,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? $"claude-agent-{agentName}",
            sp => new ClaudeAgentHealthCheck(
                sp.GetRequiredService<IOptionsMonitor<ClaudeAgentConfiguration>>(),
                agentName),
            failureStatus,
            tags));
    }

    /// <summary>
    ///     Adds Claude agent health checks from the builder.
    /// </summary>
    /// <param name="agentBuilder">The agent builder.</param>
    /// <param name="configure">Optional configuration for the health check.</param>
    /// <returns>The builder for chaining.</returns>
    public static IClaudeAgentBuilder AddHealthCheck(
        this IClaudeAgentBuilder agentBuilder,
        Action<IHealthChecksBuilder>? configure = null)
    {
        agentBuilder.Services.AddHealthChecks()
            .AddClaudeAgent(
                agentBuilder.Name ?? "default",
                name: agentBuilder.Name is null ? "claude-agent" : $"claude-agent-{agentBuilder.Name}");

        configure?.Invoke(agentBuilder.Services.AddHealthChecks());

        return agentBuilder;
    }
}
