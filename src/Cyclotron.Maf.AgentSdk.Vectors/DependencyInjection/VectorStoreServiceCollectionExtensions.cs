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
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        return services.AddVectorStoreServices(builder =>
            builder.AddManager()
                .AddChunking());
    }

    /// <summary>
    /// Registers core vector store services with optional additions via a builder.
    /// Use the builder to include manager and chunking services when needed.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Builder callback to include optional registrations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreServices(
        this IServiceCollection services,
        Action<VectorStoreServiceBuilder> configure)
    {
        AddVectorStoreCoreServices(services);

        var builder = new VectorStoreServiceBuilder(services);
        configure?.Invoke(builder);

        if (builder.IncludeManager)
        {
            services.AddVectorStoreManagerService();
        }

        if (builder.IncludeChunking)
        {
            services.AddVectorStoreChunkingServices();
        }

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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentChunker, SemanticDocumentChunker>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentChunker, SimpleDocumentChunker>());

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

    private static void AddVectorStoreCoreServices(IServiceCollection services)
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
                var providerClient = factory.GetClient(providerName);
                if (!providerClient.TryGetAzureClient(out var projectClient) || projectClient == null)
                {
                    throw new InvalidOperationException(
                        $"Provider '{providerName}' does not support Azure vector store operations.");
                }

                return projectClient;
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
            var indexingOptions = sp.GetRequiredService<IOptions<VectorStoreIndexingOptions>>();
            var telemetry = sp.GetRequiredService<VectorStoreTelemetry>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create a factory that gets the provider config at method invocation time using a fresh scope
            // This prevents ObjectDisposedException when the original scope is disposed
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

            return new OllamaVectorStoreManager(
                logger,
                indexingOptions,
                telemetry,
                configFactory);
        });

        // Register factory for provider dispatch
        services.AddScoped<VectorStoreManagerFactory>();
    }
}

/// <summary>
/// Builder for optional vector store service registrations.
/// </summary>
public sealed class VectorStoreServiceBuilder
{
    internal VectorStoreServiceBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    internal bool IncludeManager { get; private set; }

    internal bool IncludeChunking { get; private set; }

    public VectorStoreServiceBuilder AddManager()
    {
        IncludeManager = true;
        return this;
    }

    public VectorStoreServiceBuilder AddChunking()
    {
        IncludeChunking = true;
        return this;
    }
}
