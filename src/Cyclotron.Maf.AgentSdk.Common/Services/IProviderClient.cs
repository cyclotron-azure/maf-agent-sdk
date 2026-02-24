using Azure.AI.Projects;
using OllamaSharp;

namespace Cyclotron.Maf.AgentSdk.Common.Services;

/// <summary>
/// Provider client abstraction that exposes typed accessors for provider-specific clients.
/// </summary>
public interface IProviderClient
{
    /// <summary>
    /// Gets the provider name configured in agent.config.yaml.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the provider type (e.g., azure_foundry, azure_openai, ollama).
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Attempts to get the Azure AI Projects client for this provider.
    /// </summary>
    /// <param name="client">The Azure client when available.</param>
    /// <returns>True when the provider is Azure-based.</returns>
    bool TryGetAzureClient(out AIProjectClient? client);

    /// <summary>
    /// Attempts to get the Ollama client for this provider.
    /// </summary>
    /// <param name="client">The Ollama client when available.</param>
    /// <returns>True when the provider is Ollama-based.</returns>
    bool TryGetOllamaClient(out OllamaApiClient? client);
}
