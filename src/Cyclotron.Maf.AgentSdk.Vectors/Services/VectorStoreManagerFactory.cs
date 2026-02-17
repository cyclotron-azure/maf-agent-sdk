using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Services;

/// <summary>
/// Factory for creating appropriate vector store manager implementations based on provider type.
/// Dispatches to Azure or Ollama implementations based on provider configuration.
/// </summary>
/// <remarks>
/// This factory resolves manager instances from the DI container on-demand rather than caching them,
/// ensuring proper scope management and avoiding disposed service provider issues.
/// </remarks>
public class VectorStoreManagerFactory(
    ILogger<VectorStoreManagerFactory> logger,
    Impl.AzureVectorStoreManager azureManager,
    Impl.OllamaVectorStoreManager ollamaManager)
{
    private readonly ILogger<VectorStoreManagerFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Impl.AzureVectorStoreManager _azureManager = azureManager ?? throw new ArgumentNullException(nameof(azureManager));
    private readonly Impl.OllamaVectorStoreManager _ollamaManager = ollamaManager ?? throw new ArgumentNullException(nameof(ollamaManager));

    /// <summary>
    /// Gets the appropriate vector store manager for the specified provider type.
    /// </summary>
    /// <param name="providerName">The name of the provider (for logging purposes).</param>
    /// <param name="providerType">The provider type (e.g., "azure_foundry", "ollama").</param>
    /// <returns>An IVectorStoreManager implementation appropriate for the provider type.</returns>
    /// <exception cref="Exceptions.VectorStoreConfigurationException">Thrown when provider type is not supported.</exception>
    public IVectorStoreManager GetManager(string providerName, string providerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        // Dispatch to appropriate implementation based on provider type
        IVectorStoreManager manager = providerType switch
        {
            "azure_foundry" => _azureManager,
            "ollama" => _ollamaManager,
            _ => throw new Exceptions.VectorStoreConfigurationException(
                $"Vector store provider type '{providerType}' not supported or not yet implemented",
                providerName)
        };

        _logger.LogDebug("Resolved vector store manager for provider {ProviderName} with type {ProviderType}", providerName, providerType);
        return manager;
    }
}
