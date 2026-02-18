using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.Common.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Azure.AI.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

        // Register chunking implementations
        services.AddSingleton<IDocumentChunker, SemanticDocumentChunker>();
        services.AddSingleton<IDocumentChunker, SimpleDocumentChunker>();

        // Register Azure vector store manager
        services.AddScoped<AzureVectorStoreManager>(sp =>
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
        services.AddScoped<OllamaVectorStoreManager>(sp =>
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

/// <summary>
/// Adapter that wraps VectorStoreManagerFactory to provide direct IVectorStoreManager interface.
/// Delegates all operations to the factory-selected implementation based on provider name.
/// </summary>
internal class VectorStoreManagerAdapter(
    VectorStoreManagerFactory factory,
    IOptions<ModelProviderOptions> modelProviderOptions) : IVectorStoreManager
{
    private readonly VectorStoreManagerFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly IOptions<ModelProviderOptions> _modelProviderOptions = modelProviderOptions ?? throw new ArgumentNullException(nameof(modelProviderOptions));

    private IVectorStoreManager GetManager(string providerName)
    {
        try
        {
            var options = _modelProviderOptions.Value;
            if (!options.Providers.TryGetValue(providerName, out var providerDef))
            {
                var availableProviders = string.Join(", ", options.Providers.Keys);
                throw new InvalidOperationException(
                    $"Provider '{providerName}' not found in ModelProviderOptions configuration. " +
                    $"Available providers: {(string.IsNullOrEmpty(availableProviders) ? "none" : availableProviders)}");
            }

            var providerType = providerDef.Type;
            if (string.IsNullOrWhiteSpace(providerType))
            {
                throw new InvalidOperationException($"Provider type for '{providerName}' is not configured");
            }

            return _factory.GetManager(providerName, providerType);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get manager for provider '{providerName}'", ex);
        }
    }

    /// <inheritdoc/>
    public Task<string> GetOrCreateSharedVectorStoreAsync(
        string providerName,
        string key,
        string purpose,
        string name,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .GetOrCreateSharedVectorStoreAsync(providerName, key, purpose, name, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CleanupVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .CleanupVectorStoreAsync(providerName, vectorStoreId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .AddFileToVectorStoreAsync(providerName, vectorStoreId, fileContent, fileName, chunkingDelegate, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .AddFilesToVectorStoreAsync(providerName, vectorStoreId, files, chunkingDelegate, cancellationToken);
    }
}
