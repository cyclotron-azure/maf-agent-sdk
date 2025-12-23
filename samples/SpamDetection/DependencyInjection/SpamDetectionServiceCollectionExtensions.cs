using SpamDetection;
using SpamDetection.Services;
using SpamDetection.Services.Impl;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring spam detection services.
/// </summary>
public static class SpamDetectionServiceCollectionExtensions
{
    /// <summary>
    /// Configures all services required for the spam detection sample.
    /// </summary>
    /// <param name="hostBuilder">The host builder context.</param>
    /// <param name="services">The service collection to configure.</param>
    public static void ConfigureServices(HostBuilderContext hostBuilder, IServiceCollection services)
    {
        // Add core AgentSdk services
        services.AddAgentSdkServices();

        // Add document workflow services (includes vector store, prompt rendering, PDF services, etc.)
        services.AddDocumentWorkflowServices();

        // Register keyed agent factory for spam_detector
        services.AddKeyedAgentFactories("spam_detector");

        // Register the spam workflow service
        services.AddScoped<ISpamWorkflow, SpamWorkflow>();

        // Register the main application entry point
        services.AddScoped<IMain, Main>();
    }
}
