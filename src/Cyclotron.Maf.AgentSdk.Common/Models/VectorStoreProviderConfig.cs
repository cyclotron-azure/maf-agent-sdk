namespace Cyclotron.Maf.AgentSdk.Common.Models;

/// <summary>
/// Configuration for a vector store provider (Azure or Ollama).
/// </summary>
public record VectorStoreProviderConfig(
    string ProviderName,
    string ProviderType,
    string? Endpoint = null,
    string? DeploymentName = null,
    string? ApiKey = null)
{
    /// <summary>
    /// Gets or creates default configuration for a provider.
    /// </summary>
    public static VectorStoreProviderConfig Create(string providerName, string providerType) =>
        new(providerName, providerType);
}
