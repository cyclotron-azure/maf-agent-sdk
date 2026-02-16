using Microsoft.Agents.AI;

namespace Cyclotron.Maf.AgentSdk.Middleware;

/// <summary>
/// Configuration options for OpenTelemetry middleware in agent pipelines.
/// </summary>
/// <param name="Source">The activity source name for OpenTelemetry tracing.</param>
/// <param name="Configure">Optional configuration action to customize OpenTelemetry agent settings.</param>
public record AgentOpenTelemetryOptions(
    string? Source,
    Action<OpenTelemetryAgent>? Configure = null);
