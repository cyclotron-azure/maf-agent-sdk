namespace Cyclotron.Maf.AgentSdk.Middleware;

/// <summary>
/// Unified configuration for agent middleware pipeline.
/// Middleware is applied in a fixed order: RawToolCall → OpenTelemetry → ToolCalling → Logging.
/// </summary>
public class MiddlewareConfiguration
{
    /// <summary>
    /// Gets or sets an action for simple tool call inspection (applied first in pipeline).
    /// </summary>
    public Action<ToolCallingDetails>? RawToolCallDetails { get; set; }

    /// <summary>
    /// Gets or sets the tool calling middleware delegate for advanced interception (applied after OpenTelemetry).
    /// </summary>
    public ToolCallingMiddlewareDelegate? ToolCallingMiddleware { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry configuration (applied second in pipeline).
    /// </summary>
    public AgentOpenTelemetryOptions? OpenTelemetryOptions { get; set; }

    /// <summary>
    /// Gets or sets the logging configuration (applied last in pipeline).
    /// </summary>
    public AgentLoggingOptions? LoggingOptions { get; set; }
}
