using System.Diagnostics;
using System.Diagnostics.Metrics;
using Cyclotron.Maf.AgentSdk.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring OpenTelemetry tracing and metrics in the dependency injection container.
/// Provides automatic instrumentation for ASP.NET Core, HTTP clients, and custom agent telemetry.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Configures OpenTelemetry tracing and metrics pipeline for agent instrumentation.
    /// Adds ASP.NET Core and HTTP client instrumentation, and configures exporters for OTLP and Azure Monitor.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration containing telemetry settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentTelemetryPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetry = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(CreateResourceBuilder(telemetry))
                    .AddSource(telemetry.SourceName)
                    .AddSource("*Microsoft.Agents.AI")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                AddTracingExporters(builder, telemetry);
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(CreateResourceBuilder(telemetry))
                    .AddMeter(telemetry.SourceName)
                    .AddMeter("*Microsoft.Agents.AI")
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation();

                AddMetricExporters(builder, telemetry);
            });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<TelemetryOptions>>().CurrentValue;
            return new ActivitySource(options.SourceName);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<TelemetryOptions>>().CurrentValue;
            return new Meter(options.SourceName);
        });

        return services;
    }

    internal static ResourceBuilder CreateResourceBuilder(TelemetryOptions options)
    {
        var builder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: options.ServiceInstanceId ?? Environment.MachineName);

        if (!string.IsNullOrWhiteSpace(options.DeploymentEnvironment))
        {
            builder = builder.AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment", options.DeploymentEnvironment)
            });
        }

        return builder;
    }

    private static void AddTracingExporters(TracerProviderBuilder builder, TelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            builder.AddOtlpExporter(exporterOptions =>
            {
                // Ensure endpoint includes the /v1/traces path for OTLP HTTP protocol
                var baseUri = new Uri(options.OtlpEndpoint);
                exporterOptions.Endpoint = new Uri(baseUri, "v1/traces");
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }

        if (!string.IsNullOrWhiteSpace(options.ApplicationInsightsConnectionString))
        {
            builder.AddAzureMonitorTraceExporter(exporterOptions =>
            {
                exporterOptions.ConnectionString = options.ApplicationInsightsConnectionString;
            });
        }
    }

    private static void AddMetricExporters(MeterProviderBuilder builder, TelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            builder.AddOtlpExporter(exporterOptions =>
            {
                // Ensure endpoint includes the /v1/metrics path for OTLP HTTP protocol
                var baseUri = new Uri(options.OtlpEndpoint);
                exporterOptions.Endpoint = new Uri(baseUri, "v1/metrics");
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }
    }
}
