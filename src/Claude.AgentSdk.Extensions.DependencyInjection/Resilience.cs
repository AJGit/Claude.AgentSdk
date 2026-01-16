using Microsoft.Extensions.DependencyInjection;

namespace Claude.AgentSdk.Extensions.DependencyInjection;

/// <summary>
///     Configuration options for resilience policies.
/// </summary>
public class ResilienceOptions
{
    /// <summary>
    ///     Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Base delay between retries. Default is 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Whether to use exponential backoff for retries. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    ///     Circuit breaker failure threshold. Default is 5 failures.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    ///     Duration the circuit stays open before attempting to close. Default is 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Overall timeout for operations. Default is 2 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Whether to enable circuit breaker. Default is true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    ///     Whether to enable retry policy. Default is true.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    ///     Whether to enable timeout policy. Default is true.
    /// </summary>
    public bool EnableTimeout { get; set; } = true;
}

/// <summary>
///     Builder for resilience policies.
/// </summary>
public class ResiliencePolicyBuilder
{
    private readonly ResilienceOptions _options = new();

    /// <summary>
    ///     Configures retry policy.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts.</param>
    /// <param name="delay">Base delay between retries.</param>
    /// <param name="useExponentialBackoff">Whether to use exponential backoff.</param>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder AddRetry(
        int maxAttempts = 3,
        TimeSpan? delay = null,
        bool useExponentialBackoff = true)
    {
        _options.EnableRetry = true;
        _options.MaxRetryAttempts = maxAttempts;
        _options.RetryDelay = delay ?? TimeSpan.FromSeconds(1);
        _options.UseExponentialBackoff = useExponentialBackoff;
        return this;
    }

    /// <summary>
    ///     Disables retry policy.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder NoRetry()
    {
        _options.EnableRetry = false;
        return this;
    }

    /// <summary>
    ///     Configures circuit breaker policy.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening circuit.</param>
    /// <param name="breakDuration">Duration circuit stays open.</param>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder AddCircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? breakDuration = null)
    {
        _options.EnableCircuitBreaker = true;
        _options.CircuitBreakerFailureThreshold = failureThreshold;
        _options.CircuitBreakerBreakDuration = breakDuration ?? TimeSpan.FromSeconds(30);
        return this;
    }

    /// <summary>
    ///     Disables circuit breaker policy.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder NoCircuitBreaker()
    {
        _options.EnableCircuitBreaker = false;
        return this;
    }

    /// <summary>
    ///     Configures timeout policy.
    /// </summary>
    /// <param name="timeout">Operation timeout.</param>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder AddTimeout(TimeSpan timeout)
    {
        _options.EnableTimeout = true;
        _options.Timeout = timeout;
        return this;
    }

    /// <summary>
    ///     Disables timeout policy.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ResiliencePolicyBuilder NoTimeout()
    {
        _options.EnableTimeout = false;
        return this;
    }

    /// <summary>
    ///     Builds the resilience options.
    /// </summary>
    public ResilienceOptions Build() => _options;
}

/// <summary>
///     Extension methods for adding resilience to Claude agent.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    ///     Adds resilience policies to the Claude agent configuration.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="configure">Action to configure resilience policies.</param>
    /// <returns>The builder for chaining.</returns>
    public static IClaudeAgentBuilder AddResilience(
        this IClaudeAgentBuilder builder,
        Action<ResiliencePolicyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var policyBuilder = new ResiliencePolicyBuilder();
        configure(policyBuilder);

        builder.Services.Configure<ResilienceOptions>(options =>
        {
            var built = policyBuilder.Build();
            options.MaxRetryAttempts = built.MaxRetryAttempts;
            options.RetryDelay = built.RetryDelay;
            options.UseExponentialBackoff = built.UseExponentialBackoff;
            options.CircuitBreakerFailureThreshold = built.CircuitBreakerFailureThreshold;
            options.CircuitBreakerBreakDuration = built.CircuitBreakerBreakDuration;
            options.Timeout = built.Timeout;
            options.EnableCircuitBreaker = built.EnableCircuitBreaker;
            options.EnableRetry = built.EnableRetry;
            options.EnableTimeout = built.EnableTimeout;
        });

        return builder;
    }

    /// <summary>
    ///     Adds default resilience policies (retry with exponential backoff, circuit breaker, timeout).
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IClaudeAgentBuilder AddDefaultResilience(this IClaudeAgentBuilder builder)
    {
        return builder.AddResilience(policy => policy
            .AddRetry()
            .AddCircuitBreaker()
            .AddTimeout(TimeSpan.FromMinutes(2)));
    }
}

/// <summary>
///     Resilient wrapper for executing operations with retry/circuit breaker/timeout.
/// </summary>
/// <remarks>
///     <para>
///     This provides a lightweight implementation of common resilience patterns
///     without requiring Polly as a dependency. For more advanced scenarios,
///     consider using Polly directly with Microsoft.Extensions.Http.Resilience.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     var executor = new ResilientExecutor(options);
///
///     var result = await executor.ExecuteAsync(async ct => {
///         return await session.StreamAsync(prompt, ct).ToListAsync(ct);
///     }, cancellationToken);
///     </code>
///     </para>
/// </remarks>
public class ResilientExecutor
{
    private readonly ResilienceOptions _options;
    private int _consecutiveFailures;
    private DateTime _circuitOpenedAt;
    private bool _circuitIsOpen;

    /// <summary>
    ///     Creates a new resilient executor with the specified options.
    /// </summary>
    public ResilientExecutor(ResilienceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    ///     Executes an operation with configured resilience policies.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Check circuit breaker
        if (_options.EnableCircuitBreaker && _circuitIsOpen)
        {
            if (DateTime.UtcNow - _circuitOpenedAt < _options.CircuitBreakerBreakDuration)
            {
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open. Will retry after {_options.CircuitBreakerBreakDuration - (DateTime.UtcNow - _circuitOpenedAt)}");
            }

            // Half-open state - allow one request through
            _circuitIsOpen = false;
        }

        var attempt = 0;
        Exception? lastException = null;

        while (true)
        {
            attempt++;

            try
            {
                T result;

                if (_options.EnableTimeout)
                {
                    using var timeoutCts = new CancellationTokenSource(_options.Timeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    try
                    {
                        result = await operation(linkedCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Operation timed out after {_options.Timeout}");
                    }
                }
                else
                {
                    result = await operation(ct).ConfigureAwait(false);
                }

                // Success - reset circuit breaker
                _consecutiveFailures = 0;
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Explicit cancellation - don't retry
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _consecutiveFailures++;

                // Check circuit breaker threshold
                if (_options.EnableCircuitBreaker &&
                    _consecutiveFailures >= _options.CircuitBreakerFailureThreshold)
                {
                    _circuitIsOpen = true;
                    _circuitOpenedAt = DateTime.UtcNow;
                }

                // Check if we should retry
                if (!_options.EnableRetry || attempt >= _options.MaxRetryAttempts)
                {
                    throw;
                }

                // Calculate delay
                var delay = _options.UseExponentialBackoff
                    ? TimeSpan.FromTicks(_options.RetryDelay.Ticks * (long)Math.Pow(2, attempt - 1))
                    : _options.RetryDelay;

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Executes an operation with configured resilience policies.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }
}

/// <summary>
///     Exception thrown when a circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    ///     Creates a new circuit breaker open exception.
    /// </summary>
    public CircuitBreakerOpenException(string message) : base(message) { }

    /// <summary>
    ///     Creates a new circuit breaker open exception with an inner exception.
    /// </summary>
    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException) { }
}
