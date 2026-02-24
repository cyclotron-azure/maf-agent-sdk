using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Middleware;

/// <summary>
/// Configuration options for logging middleware in agent pipelines.
/// </summary>
/// <param name="LoggerFactory">The logger factory to use for creating loggers.</param>
/// <param name="Configure">Optional configuration action to customize logging agent settings.</param>
public record AgentLoggingOptions(
    ILoggerFactory? LoggerFactory,
    Action<LoggingAgent>? Configure = null);
