using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.Common.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.Options;
using Azure.AI.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Cyclotron.Maf.AgentSdk.Vectors.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering vector store services in the dependency injection container.
/// Provides vector store lifecycle management, file upload, and indexing capabilities for AI agents.
/// </summary>
public static class VectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all vector store services including managers, factory, chunking, and telemetry.
    /// Supports multiple backend implementations (Azure, Ollama) with automatic provider dispatch.
    /// Uses scoped IServiceProvider injection to ensure delegates access the current request scope,
    /// avoiding disposed service provider issues.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreServices(
        this IServiceCollection services)
    {
        // Register configuration options
        services.AddVectorStoreIndexingOptions();
        services.AddOptions<SemanticChunkingOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                var section = configuration.GetSection("SemanticChunking");
                if (section.Exists())
                {
                    section.Bind(options);
                }
            })
            .ValidateDataAnnotations();

        // Register telemetry
        services.AddSingleton<VectorStoreTelemetry>();

        // Register Azure vector store manager
        services.AddScoped(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureVectorStoreManager>>();
            var options = sp.GetRequiredService<IOptions<VectorStoreIndexingOptions>>();
            var telemetry = sp.GetRequiredService<VectorStoreTelemetry>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create a factory that gets the Azure client at method invocation time using a fresh scope
            Func<string, AIProjectClient> clientFactory = providerName =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedSp = scope.ServiceProvider;

                // Get IProviderClientFactory from the scoped service provider
                var factory = scopedSp.GetRequiredService<IProviderClientFactory>();
                return factory.GetClient(providerName);
            };

            // Create a factory that gets the provider config at method invocation time using a fresh scope
            Func<string, VectorStoreProviderConfig> configFactory = providerName =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedSp = scope.ServiceProvider;

                var modelProviderOptions = scopedSp.GetRequiredService<IOptions<ModelProviderOptions>>();
                var providerDef = modelProviderOptions.Value.Providers[providerName];

                return new VectorStoreProviderConfig(
                    providerName,
                    providerDef.Type ?? string.Empty,
                    providerDef.Endpoint,
                    providerDef.DeploymentName,
                    providerDef.ApiKey);
            };

            return new AzureVectorStoreManager(logger, options, telemetry, clientFactory, configFactory);
        });

        // Register Ollama vector store manager
        services.AddScoped(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OllamaVectorStoreManager>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var indexingOptions = sp.GetRequiredService<IOptions<VectorStoreIndexingOptions>>();
            var telemetry = sp.GetRequiredService<VectorStoreTelemetry>();

            // Create a factory that gets the provider config at method invocation time using a fresh scope
            Func<string, VectorStoreProviderConfig> configFactory = providerName =>
            {
                var modelProviderOptions = sp.GetRequiredService<IOptions<ModelProviderOptions>>();
                var providerDef = modelProviderOptions.Value.Providers[providerName];

                return new VectorStoreProviderConfig(
                    providerName,
                    providerDef.Type ?? string.Empty,
                    providerDef.Endpoint,
                    providerDef.DeploymentName,
                    providerDef.ApiKey);
            };

            return new OllamaVectorStoreManager(
                logger,
                httpClientFactory,
                indexingOptions,
                telemetry,
                configFactory);
        });

        // Register factory for provider dispatch
        services.AddScoped<VectorStoreManagerFactory>();

        return services;
    }

    /// <summary>
    /// Registers document chunking services.
    /// Use this when you want DI-resolved chunkers available to consumers.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreChunkingServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IDocumentChunker, SemanticDocumentChunker>();
        services.AddSingleton<IDocumentChunker, SimpleDocumentChunker>();

        return services;
    }

    /// <summary>
    /// Adds the global IVectorStoreManager service with access to model provider configuration.
    /// This is typically called from the main SDK's DI configuration where ModelProviderOptions is available.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreManagerService(
        this IServiceCollection services)
    {
        services.AddScoped<IVectorStoreManager>(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreManagerFactory>();
            var modelProviderOptions = sp.GetRequiredService<IOptions<ModelProviderOptions>>();
            return new VectorStoreManagerAdapter(factory, modelProviderOptions);
        });

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="VectorStoreIndexingOptions"/> using the <c>VectorStoreIndexing</c> section.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreIndexingOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<VectorStoreIndexingOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var section = configuration.GetSection(VectorStoreIndexingOptions.SectionName);
                if (section.Exists())
                {
                    section.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
