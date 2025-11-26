using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Options;
using SpamDetection;

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

        // Register prompt rendering service
        services.AddSingleton<IPromptRenderingService, PromptRenderingService>();

        // Register the persistent agents client factory
        services.AddSingleton<IPersistentAgentsClientFactory, PersistentAgentsClientFactory>();

        // Register the vector store manager
        services.AddSingleton<IVectorStoreManager, VectorStoreManager>();

        // Register the spam detector agent factory as a keyed service
        services.AddKeyedSingleton<IAgentFactory>("spam_detector", (sp, key) =>
        {
            var logger = sp.GetRequiredService<ILogger<AgentFactory>>();
            var promptService = sp.GetRequiredService<IPromptRenderingService>();
            var providerOptions = sp.GetRequiredService<IOptions<ModelProviderOptions>>();
            var agentOptions = sp.GetRequiredService<IOptions<AgentOptions>>();
            var clientFactory = sp.GetRequiredService<IPersistentAgentsClientFactory>();
            var vectorStoreManager = sp.GetRequiredService<IVectorStoreManager>();
            var telemetryOptions = sp.GetRequiredService<IOptions<TelemetryOptions>>();

            return new AgentFactory(
                key as string ?? "spam_detector",
                logger,
                promptService,
                providerOptions,
                agentOptions,
                clientFactory,
                vectorStoreManager,
                telemetryOptions);
        });

        // Register the main application entry point
        services.AddScoped<IMain, Main>();
    }
}
