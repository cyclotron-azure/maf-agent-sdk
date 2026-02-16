using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Factory for creating <see cref="PersistentAgentsClient"/> instances with provider-specific authentication.
/// Uses <see cref="IProviderClientFactory"/> to support multiple provider types.
/// Creates new client instances per scope to avoid state sharing in parallel processing.
/// </summary>
/// <remarks>
/// <para>
/// Delegates to <see cref="IProviderClientFactory"/> for provider instantiation.
/// Only returns clients compatible with Azure AI Agents API (azure_foundry, azure_openai).
/// </para>
/// </remarks>
public class PersistentAgentsClientFactory : IPersistentAgentsClientFactory
{
    private readonly ILogger<PersistentAgentsClientFactory> _logger;
    private readonly IProviderClientFactory _providerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentAgentsClientFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="providerFactory">The provider factory for creating provider instances.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public PersistentAgentsClientFactory(
        ILogger<PersistentAgentsClientFactory> logger,
        IProviderClientFactory providerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    /// <inheritdoc/>
    public PersistentAgentsClient GetClient(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        // Get provider instance from factory
        var provider = _providerFactory.GetProvider(providerName);

        // Validate that provider supports Azure AI Agents API
        if (!IsAzureCompatibleProvider(provider))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' (type: {provider.ProviderType}) is not compatible with Azure AI Agents API. " +
                $"Supported types: azure_foundry, azure_openai");
        }

        _logger.LogInformation(
            "Creating PersistentAgentsClient for provider '{ProviderName}' (Type: {ProviderType})",
            providerName,
            provider.ProviderType);

        // Create client from provider
        var client = provider.CreateClient();

        if (client is not PersistentAgentsClient agentsClient)
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' did not return a PersistentAgentsClient instance");
        }

        return agentsClient;
    }

    private static bool IsAzureCompatibleProvider(IModelProvider provider)
    {
        return provider.ProviderType.Equals("azure_foundry", StringComparison.OrdinalIgnoreCase) ||
               provider.ProviderType.Equals("azure_openai", StringComparison.OrdinalIgnoreCase);
    }
}
