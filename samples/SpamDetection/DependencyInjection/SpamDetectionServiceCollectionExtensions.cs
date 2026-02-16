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

        // Register keyed agent factories for both Azure and Ollama spam detectors
        services.AddKeyedAgentFactories("spam_detector");          // Azure AI Foundry
        services.AddKeyedAgentFactories("spam_detector_ollama");   // Ollama local

        // Register both spam workflow services
        services.AddScoped<SpamWorkflow>();         // Azure workflow (with vector stores)
        services.AddScoped<OllamaSpamWorkflow>();   // Ollama workflow (no vector stores)

        // Register a factory to choose the right spam workflow based on configuration
        services.AddScoped<ISpamWorkflow>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var provider = config["Workflow:SpamProvider"]?.ToLowerInvariant() ?? "azure";

            return provider switch
            {
                "ollama" => sp.GetRequiredService<OllamaSpamWorkflow>(),
                "azure" => sp.GetRequiredService<SpamWorkflow>(),
                _ => sp.GetRequiredService<SpamWorkflow>() // Default to Azure
            };
        });

        // Register invoice extraction services
        services.AddInvoiceExtractionServices();

        // Register the main application entry point
        services.AddScoped<IMain, Main>();
    }
}
