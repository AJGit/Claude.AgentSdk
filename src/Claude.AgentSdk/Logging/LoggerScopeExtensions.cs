using Microsoft.Extensions.Logging;

namespace Claude.AgentSdk.Logging;

/// <summary>
///     Extension methods for creating structured logging scopes with correlation data.
/// </summary>
internal static class LoggerScopeExtensions
{
    extension(ILogger logger)
    {
        /// <summary>
        ///     Creates a logging scope with the CLI process ID.
        /// </summary>
        public IDisposable? BeginCliScope(int cliPid)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                ["CliPid"] = cliPid
            });
        }

        /// <summary>
        ///     Creates a logging scope with a request ID.
        /// </summary>
        public IDisposable? BeginRequestScope(string requestId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId
            });
        }

        /// <summary>
        ///     Creates a logging scope with a correlation ID for tracing across log entries.
        /// </summary>
        public IDisposable? BeginCorrelationScope(string correlationId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });
        }

        /// <summary>
        ///     Creates a logging scope with CLI process ID and optional correlation ID.
        /// </summary>
        public IDisposable? BeginTransportScope(int cliPid, string? correlationId = null)
        {
            var scopeData = new Dictionary<string, object>
            {
                ["CliPid"] = cliPid
            };

            if (correlationId is not null)
            {
                scopeData["CorrelationId"] = correlationId;
            }

            return logger.BeginScope(scopeData);
        }
    }
}
