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
        // Add core AgentSdk services (registers ModelProviderOptions)
        services.AddAgentSdkServices();

        // Add vector store services from the Vectors package
        services.AddVectorStoreServices();

        // Add IVectorStoreManager service (requires ModelProviderOptions to be registered)
        services.AddVectorStoreManagerService();

        // Add document workflow services (includes prompt rendering, PDF services, etc.)
        services.AddDocumentWorkflowServices();

        // Register spam detection services
        services.AddSpamDetectionServices();

        // Register invoice extraction services
        services.AddInvoiceExtractionServices();

        // Register the main application entry point
        services.AddScoped<IMain, Main>();
    }

    /// <summary>
    /// Configures all services required for the spam detection workflow, including both Azure AI Foundry and Ollama implementations.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSpamDetectionServices(this IServiceCollection services)
    {
        // Register keyed agent factories for both Azure and Ollama spam detectors
        services.AddKeyedAgentFactories("spam_detector");          // Azure AI Foundry
        services.AddKeyedAgentFactories("spam_detector_ollama");   // Ollama local

        // Register provider strategies (Azure uses vector stores, Ollama does not)
        services.AddScoped<ISpamProviderStrategy, AzureSpamProviderStrategy>();
        services.AddScoped<ISpamProviderStrategy, OllamaSpamProviderStrategy>();

        // Register a single workflow that selects the provider strategy via configuration
        services.AddScoped<ISpamWorkflow, SpamWorkflow>();

        return services;
    }

    /// <summary>
    /// Configures all services required for the invoice extraction workflow.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInvoiceExtractionServices(this IServiceCollection services)
    {
        // Register keyed agent factories for both Azure and Ollama invoice extractors
        services.AddKeyedAgentFactories("invoice_extractor");          // Azure AI Foundry
        services.AddKeyedAgentFactories("invoice_extractor_ollama");   // Ollama local

        // Register provider strategies (Azure uses vector stores, Ollama uses local retrieval)
        services.AddScoped<IInvoiceProviderStrategy, AzureInvoiceProviderStrategy>();
        services.AddScoped<IInvoiceProviderStrategy, OllamaInvoiceProviderStrategy>();

        // Register the three executors as transient services
        services.AddTransient<TextBasedInvoiceExecutor>();
        services.AddTransient<ImageOnlyInvoiceExecutor>();
        services.AddTransient<MixedInvoiceExecutor>();

        // Register the invoice extraction workflow service
        services.AddScoped<IInvoiceExtractionWorkflow, InvoiceExtractionWorkflow>();

        return services;
    }
}

