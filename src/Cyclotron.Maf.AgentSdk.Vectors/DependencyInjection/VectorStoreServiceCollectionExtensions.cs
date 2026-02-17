using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cyclotron.Maf.AgentSdk.VectorStore.Models;
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

                // Use reflection to get IProviderClientFactory to avoid circular dependency
                var factoryType = Type.GetType("Cyclotron.Maf.AgentSdk.Services.IProviderClientFactory, Cyclotron.Maf.AgentSdk");
                if (factoryType == null)
                {
                    throw new InvalidOperationException("IProviderClientFactory type not found. Ensure Cyclotron.Maf.AgentSdk is loaded.");
                }

                var factory = scopedSp.GetService(factoryType);
                if (factory == null)
                {
                    throw new InvalidOperationException($"IProviderClientFactory service not registered for provider {providerName}. Call AddAgentSdkServices() during DI configuration.");
                }

                var getClientMethod = factoryType.GetMethod("GetClient", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(string) }, null);
                if (getClientMethod == null)
                {
                    throw new InvalidOperationException("GetClient method not found on IProviderClientFactory.");
                }

                var client = getClientMethod.Invoke(factory, new object[] { providerName });
                return (AIProjectClient)(client ?? throw new InvalidOperationException($"Failed to get Azure client for provider {providerName}"));
            };

            // Create a factory that gets the provider config at method invocation time using a fresh scope
            Func<string, VectorStoreProviderConfig> configFactory = providerName =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedSp = scope.ServiceProvider;

                // Use reflection to load ModelProviderOptions to avoid circular dependency
                var agentSdkAssembly = System.Reflection.Assembly.Load("Cyclotron.Maf.AgentSdk");
                var modelProviderOptionsType = agentSdkAssembly?.GetType("Cyclotron.Maf.AgentSdk.Options.ModelProviderOptions");
                if (modelProviderOptionsType == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions type not found. Ensure Cyclotron.Maf.AgentSdk is loaded.");
                }

                var optionsType = typeof(IOptions<>).MakeGenericType(modelProviderOptionsType);
                var modelProviderOptionsObj = scopedSp.GetService(optionsType);
                if (modelProviderOptionsObj == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions not registered. Call AddAgentSdkServices() during DI configuration.");
                }

                // Get Value property using reflection
                var valueProperty = optionsType.GetProperty("Value");
                if (valueProperty == null)
                {
                    throw new InvalidOperationException("Options<ModelProviderOptions>.Value property not found");
                }

                var optionsValue = valueProperty.GetValue(modelProviderOptionsObj);
                if (optionsValue == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions.Value is null");
                }

                // Get Providers dictionary using reflection
                var providersProperty = modelProviderOptionsType.GetProperty("Providers");
                if (providersProperty == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions.Providers property not found");
                }

                var providers = providersProperty.GetValue(optionsValue) as System.Collections.IDictionary;
                if (providers == null || !providers.Contains(providerName))
                {
                    throw new InvalidOperationException($"Provider '{providerName}' not configured in ModelProviderOptions");
                }

                var providerDef = providers[providerName];
                if (providerDef == null)
                {
                    throw new InvalidOperationException($"Provider definition for '{providerName}' is null");
                }

                // Extract properties from provider definition using reflection
                var type = providerDef.GetType().GetProperty("Type")?.GetValue(providerDef) as string;
                var endpoint = providerDef.GetType().GetProperty("Endpoint")?.GetValue(providerDef) as string;
                var deploymentName = providerDef.GetType().GetProperty("DeploymentName")?.GetValue(providerDef) as string;
                var apiKey = providerDef.GetType().GetProperty("ApiKey")?.GetValue(providerDef) as string;

                return new VectorStoreProviderConfig(
                    providerName,
                    type ?? string.Empty,
                    endpoint,
                    deploymentName,
                    apiKey);
            };

            return new AzureVectorStoreManager(logger, options, telemetry, clientFactory, configFactory);
        });;

        // Register Ollama vector store manager
        services.AddScoped<OllamaVectorStoreManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OllamaVectorStoreManager>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var indexingOptions = sp.GetRequiredService<IOptions<VectorStoreIndexingOptions>>();
            var telemetry = sp.GetRequiredService<VectorStoreTelemetry>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create a factory that gets the provider config at method invocation time using a fresh scope
            Func<string, VectorStoreProviderConfig> configFactory = providerName =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedSp = scope.ServiceProvider;

                // Use reflection to load ModelProviderOptions to avoid circular dependency
                var agentSdkAssembly = System.Reflection.Assembly.Load("Cyclotron.Maf.AgentSdk");
                var modelProviderOptionsType = agentSdkAssembly?.GetType("Cyclotron.Maf.AgentSdk.Options.ModelProviderOptions");
                if (modelProviderOptionsType == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions type not found. Ensure Cyclotron.Maf.AgentSdk is loaded.");
                }

                var optionsType = typeof(IOptions<>).MakeGenericType(modelProviderOptionsType);
                var modelProviderOptionsObj = scopedSp.GetService(optionsType);
                if (modelProviderOptionsObj == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions not registered. Call AddAgentSdkServices() during DI configuration.");
                }

                // Get Value property using reflection
                var valueProperty = optionsType.GetProperty("Value");
                if (valueProperty == null)
                {
                    throw new InvalidOperationException("Options<ModelProviderOptions>.Value property not found");
                }

                var optionsValue = valueProperty.GetValue(modelProviderOptionsObj);
                if (optionsValue == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions.Value is null");
                }

                // Get Providers dictionary using reflection
                var providersProperty = modelProviderOptionsType.GetProperty("Providers");
                if (providersProperty == null)
                {
                    throw new InvalidOperationException("ModelProviderOptions.Providers property not found");
                }

                var providers = providersProperty.GetValue(optionsValue) as System.Collections.IDictionary;
                if (providers == null || !providers.Contains(providerName))
                {
                    throw new InvalidOperationException($"Provider '{providerName}' not configured in ModelProviderOptions");
                }

                var providerDef = providers[providerName];
                if (providerDef == null)
                {
                    throw new InvalidOperationException($"Provider definition for '{providerName}' is null");
                }

                // Extract properties from provider definition using reflection
                var type = providerDef.GetType().GetProperty("Type")?.GetValue(providerDef) as string;
                var endpoint = providerDef.GetType().GetProperty("Endpoint")?.GetValue(providerDef) as string;
                var deploymentName = providerDef.GetType().GetProperty("DeploymentName")?.GetValue(providerDef) as string;
                var apiKey = providerDef.GetType().GetProperty("ApiKey")?.GetValue(providerDef) as string;

                return new VectorStoreProviderConfig(
                    providerName,
                    type ?? string.Empty,
                    endpoint,
                    deploymentName,
                    apiKey);
            };

            return new OllamaVectorStoreManager(
                logger,
                httpClientFactory,
                indexingOptions,
                telemetry,
                configFactory);
        });;

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

            // Load the type by finding it at runtime using the loaded assembly
            Type? optionsType = null;
            try
            {
                // Try to get the ModelProviderOptions type from the loaded assembly
                var agentSdkAssembly = System.Reflection.Assembly.Load("Cyclotron.Maf.AgentSdk");
                var modelProviderOptionsType = agentSdkAssembly?.GetType("Cyclotron.Maf.AgentSdk.Options.ModelProviderOptions");
                if (modelProviderOptionsType != null)
                {
                    optionsType = typeof(IOptions<>).MakeGenericType(modelProviderOptionsType);
                }
            }
            catch
            {
                // Fall back - Type not found
            }

            if (optionsType == null)
            {
                throw new InvalidOperationException(
                    "ModelProviderOptions type not found. Ensure Cyclotron.Maf.AgentSdk assembly is loaded.");
            }

            var modelProviderOptions = sp.GetService(optionsType);
            if (modelProviderOptions == null)
            {
                throw new InvalidOperationException(
                    "ModelProviderOptions not registered in DI container. " +
                    "Ensure AddAgentSdkServices() is called before AddVectorStoreManagerService().");
            }

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
/// Uses reflection to access ModelProviderOptions since the Vectors package doesn't have a direct reference to the main SDK.
/// </summary>
internal class VectorStoreManagerAdapter(
    VectorStoreManagerFactory factory,
    object modelProviderOptionsObject) : IVectorStoreManager
{
    private readonly VectorStoreManagerFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly object _modelProviderOptionsObject = modelProviderOptionsObject ?? throw new ArgumentNullException(nameof(modelProviderOptionsObject));

    private IVectorStoreManager GetManager(string providerName)
    {
        try
        {
            // Use reflection to get the Value property from IOptions<ModelProviderOptions>
            var valueProperty = _modelProviderOptionsObject.GetType().GetProperty("Value");
            if (valueProperty == null)
            {
                throw new InvalidOperationException("ModelProviderOptions.Value property not found");
            }

            var optionsValue = valueProperty.GetValue(_modelProviderOptionsObject);
            if (optionsValue == null)
            {
                throw new InvalidOperationException("ModelProviderOptions.Value is null");
            }

            // Get the Providers property (Dictionary<string, ModelProviderDefinitionOptions>)
            var providersProperty = optionsValue.GetType().GetProperty("Providers");
            if (providersProperty == null)
            {
                throw new InvalidOperationException("ModelProviderOptions.Providers property not found");
            }

            var providers = providersProperty.GetValue(optionsValue) as System.Collections.IDictionary;
            if (providers == null || !providers.Contains(providerName))
            {
                var availableProviders = string.Join(", ",
                    providers?.Keys.Cast<string>() ?? Array.Empty<string>());
                throw new InvalidOperationException(
                    $"Provider '{providerName}' not found in ModelProviderOptions configuration. " +
                    $"Available providers: {(string.IsNullOrEmpty(availableProviders) ? "none" : availableProviders)}");
            }

            var providerDef = providers[providerName];
            if (providerDef == null)
            {
                throw new InvalidOperationException($"Provider definition for '{providerName}' is null");
            }

            // Get the Type property from ModelProviderDefinitionOptions
            var typeProperty = providerDef.GetType().GetProperty("Type");
            if (typeProperty == null)
            {
                throw new InvalidOperationException($"Provider type property not found for provider '{providerName}'");
            }

            var providerType = typeProperty.GetValue(providerDef) as string;
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
