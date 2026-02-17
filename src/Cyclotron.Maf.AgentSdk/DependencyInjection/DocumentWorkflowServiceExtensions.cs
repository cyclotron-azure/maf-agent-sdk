using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering generic document workflow services.
/// For domain-specific workflows, use the appropriate extension package (e.g., AgentSdk.HOA).
/// </summary>
public static class DocumentWorkflowServiceExtensions
{
    /// <summary>
    /// Registers generic workflow-based document services with Azure AI Foundry.
    /// This includes core services like vector store management, cleanup, prompt rendering, and PDF content analysis.
    /// Domain-specific services should be registered via separate extension methods.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentWorkflowServices(this IServiceCollection services)
    {
        // Register HttpClient factory for Ollama and other HTTP-based providers
        services.AddHttpClient("ollama", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(300); // Default 5 minutes
        });

        // Register provider client factory as scoped service (supports Azure and Ollama)
        services.AddScoped<IProviderClientFactory, ProviderClientFactory>();

        // Note: Vector store services have been moved to the AgentSdk.Vectors package.
        // To enable vector store functionality, add a reference to AgentSdk.Vectors
        // and call services.AddVectorStoreServices() in your startup configuration.
        // See: https://github.com/cyclotron-azure/maf-agent-sdk/tree/main/src/Cyclotron.Maf.AgentSdk.Vectors

        // Register Azure Foundry cleanup service
        services.AddScoped<IAIFoundryCleanupService, AIFoundryCleanupService>();

        // Register unified prompt rendering service
        services.AddSingleton<IPromptRenderingService, PromptRenderingService>();

        // Note: PDF services are registered via AddPdfServices() from AgentSdk.Pdf package
        // PDF services include: IPdfContentAnalyzer, IPdfImageExtractor, IPdfToMarkdownConverter

        // Note: Domain-specific executors (FileRead, VectorStore, etc.) should be registered
        // by domain-specific packages (e.g., AgentSdk.HOA)
        // CleanupExecutor<T> is generic and should also be registered by domain-specific packages

        // Register startup validation for agent templates
        services.AddHostedService<AgentTemplateValidationService>();

        return services;
    }

    /// <summary>
    /// Registers keyed agent factories for the specified agent keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="agentKeys">The agent keys to register factories for.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeyedAgentFactories(
        this IServiceCollection services,
        params string[] agentKeys)
    {
        foreach (var agentKey in agentKeys)
        {
            services.AddKeyedScoped<IAgentFactory>(
                agentKey,
                (sp, key) => new AgentFactory(
                    agentKey,
                    sp.GetRequiredService<ILogger<AgentFactory>>(),
                    sp.GetRequiredService<IPromptRenderingService>(),
                    sp.GetRequiredService<IOptions<ModelProviderOptions>>(),
                    sp.GetRequiredService<IOptions<AgentOptions>>(),
                    sp.GetRequiredService<IProviderClientFactory>(),
                    sp.GetService<VectorStoreManager>(), // Optional - returns null if AgentSdk.Vectors not registered
                    sp.GetRequiredService<IOptions<TelemetryOptions>>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<ILoggerFactory>()));
        }

        return services;
    }
}
