using Cyclotron.Maf.AgentSdk.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for configuring OpenTelemetry logging in the logging pipeline.
/// Provides structured log export to OTLP endpoints and Azure Application Insights.
/// </summary>
public static class TelemetryLoggingBuilderExtensions
{
    /// <summary>
    /// Configures OpenTelemetry logging with OTLP and Azure Monitor exporters.
    /// Enables structured logging with scopes, formatted messages, and parsed state values.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddAgentTelemetryLogging(this ILoggingBuilder builder)
    {
        builder.AddOpenTelemetry();

        builder.Services.AddOptions<OpenTelemetryLoggerOptions>()
            .Configure<IOptionsMonitor<TelemetryOptions>>((loggerOptions, telemetryAccessor) =>
            {
                var telemetry = telemetryAccessor.CurrentValue;
                if (!telemetry.Enabled)
                {
                    return;
                }

                loggerOptions.IncludeScopes = true;
                loggerOptions.IncludeFormattedMessage = true;
                loggerOptions.ParseStateValues = true;
                loggerOptions.SetResourceBuilder(TelemetryServiceCollectionExtensions.CreateResourceBuilder(telemetry));

                if (!string.IsNullOrWhiteSpace(telemetry.OtlpEndpoint))
                {
                    loggerOptions.AddOtlpExporter(exporterOptions =>
                    {
                        // Ensure endpoint includes the /v1/logs path for OTLP HTTP protocol
                        var baseUri = new Uri(telemetry.OtlpEndpoint);
                        exporterOptions.Endpoint = new Uri(baseUri, "v1/logs");
                        exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
                }

                if (!string.IsNullOrWhiteSpace(telemetry.ApplicationInsightsConnectionString))
                {
                    loggerOptions.AddAzureMonitorLogExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = telemetry.ApplicationInsightsConnectionString;
                    });
                }
            });

        return builder;
    }
}
