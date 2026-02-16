using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Cyclotron.Maf.AgentSdk.Middleware;

/// <summary>
/// Centralized helper for applying middleware to AI agents in a consistent order.
/// Ensures all agents in the SDK follow the same middleware pipeline pattern.
/// </summary>
/// <remarks>
/// Middleware Application Order (fixed):
/// 1. RawToolCallDetails (simple inspection)
/// 2. OpenTelemetry (observability)
/// 3. ToolCallingMiddleware (advanced interception)
/// 4. Logging (captures everything)
/// </remarks>
public static class AgentMiddlewareHelper
{
    /// <summary>
    /// Applies middleware to an agent according to the provided configuration.
    /// </summary>
    /// <param name="innerAgent">The base agent to wrap with middleware.</param>
    /// <param name="configuration">The middleware configuration specifying which middleware to apply.</param>
    /// <param name="services">Optional service provider for dependency injection in middleware.</param>
    /// <returns>An agent with all configured middleware applied in the correct order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when innerAgent is null.</exception>
    public static AIAgent ApplyMiddleware(
        AIAgent innerAgent,
        MiddlewareConfiguration? configuration,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(innerAgent, nameof(innerAgent));

        // If no configuration, return agent unchanged
        if (configuration == null)
        {
            return innerAgent;
        }

        // Start building the middleware pipeline
        AIAgentBuilder builder = innerAgent.AsBuilder();

        // 1. Apply RawToolCallDetails (simple inspection first)
        if (configuration.RawToolCallDetails != null)
        {
            var handler = new ToolCallsHandler(configuration.RawToolCallDetails);
            builder = builder.Use(handler.ToolCallingMiddlewareAsync);
        }

        // 2. Apply OpenTelemetry (observability layer)
        if (configuration.OpenTelemetryOptions != null)
        {
            var telemetryOptions = configuration.OpenTelemetryOptions;
            builder = builder.UseOpenTelemetry(
                telemetryOptions.Source,
                telemetryOptions.Configure ?? (_ => { }));
        }

        // 3. Apply ToolCallingMiddleware (advanced interception/modification)
        if (configuration.ToolCallingMiddleware != null)
        {
            builder = builder.Use(configuration.ToolCallingMiddleware.Invoke);
        }

        // 4. Apply Logging (last to capture everything)
        if (configuration.LoggingOptions != null)
        {
            var loggingOptions = configuration.LoggingOptions;
            builder = builder.UseLogging(
                loggingOptions.LoggerFactory,
                loggingOptions.Configure ?? (_ => { }));
        }

        // Build and return the configured agent
        return builder.Build(services);
    }
}
