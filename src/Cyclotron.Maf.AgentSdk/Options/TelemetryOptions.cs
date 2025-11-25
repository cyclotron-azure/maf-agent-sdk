using System.ComponentModel.DataAnnotations;

namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Configuration options for OpenTelemetry instrumentation and telemetry export.
/// Controls tracing, metrics, and logging export to OTLP endpoints and Azure Application Insights.
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Gets or sets whether telemetry collection is enabled.
    /// When disabled, no traces, metrics, or logs are exported.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the service name used in telemetry resource attributes.
    /// This value identifies the service in tracing backends.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = "AgentSdk";

    /// <summary>
    /// Gets or sets the service version used in telemetry resource attributes.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the ActivitySource and Meter name for custom telemetry.
    /// This name is used when creating spans and metrics.
    /// </summary>
    [Required]
    public string SourceName { get; set; } = "AgentSdk";

    /// <summary>
    /// Gets or sets the deployment environment name (e.g., "development", "staging", "production").
    /// Added as a resource attribute for filtering in telemetry backends.
    /// </summary>
    public string DeploymentEnvironment { get; set; } = "development";

    /// <summary>
    /// Gets or sets the unique service instance identifier.
    /// Defaults to the machine name if not specified.
    /// </summary>
    public string? ServiceInstanceId { get; set; } = Environment.MachineName;

    /// <summary>
    /// Gets or sets whether to include sensitive data in telemetry.
    /// When enabled, prompt content and AI responses may be logged.
    /// Use with caution in production environments.
    /// </summary>
    public bool EnableSensitiveData { get; set; } = false;

    /// <summary>
    /// Gets or sets the OTLP exporter endpoint URL.
    /// The path suffixes (/v1/traces, /v1/metrics, /v1/logs) are appended automatically.
    /// Example: "http://localhost:4318".
    /// </summary>
    public string? OtlpEndpoint { get; set; } = "http://localhost:4318";

    /// <summary>
    /// Gets or sets the Azure Application Insights connection string.
    /// When provided, telemetry is also exported to Application Insights.
    /// Falls back to the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }
        = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
}
