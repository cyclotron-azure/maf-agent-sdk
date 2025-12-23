using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        // Register PersistentAgentsClient factory as scoped service
        services.AddScoped<IPersistentAgentsClientFactory, PersistentAgentsClientFactory>();

        // Register vector store manager (now depends on IPersistentAgentsClientFactory)
        services.AddScoped<IVectorStoreManager, VectorStoreManager>();

        // Register Azure Foundry cleanup service
        services.AddScoped<IAzureFoundryCleanupService, AzureFoundryCleanupService>();

        // Register unified prompt rendering service
        services.AddSingleton<IPromptRenderingService, PromptRenderingService>();

        // Register PDF content analyzers as keyed services for pluggable implementation support
        services.AddKeyedSingleton<IPdfContentAnalyzer>(
            "pdfpig",
            (sp, _) => new PdfPigContentAnalyzer(
                sp.GetRequiredService<ILogger<PdfPigContentAnalyzer>>(),
                sp.GetRequiredService<IOptions<PdfContentAnalysisOptions>>()));

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
                    sp.GetRequiredService<IPersistentAgentsClientFactory>(),
                    sp.GetRequiredService<IVectorStoreManager>(),
                    sp.GetRequiredService<IOptions<TelemetryOptions>>()));
        }

        return services;
    }
}
