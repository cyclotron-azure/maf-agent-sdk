namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Defines the contract for AI model providers.
/// Implementations provide access to AI models through different platforms (Azure, Ollama, etc.).
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// Gets the provider type identifier (e.g., "azure_foundry", "azure_openai", "ollama").
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Gets the endpoint URL for the provider.
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// Gets the deployment or model name.
    /// </summary>
    string DeploymentName { get; }

    /// <summary>
    /// Validates that the provider configuration is complete and correct.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    bool IsValid();

    /// <summary>
    /// Creates a client instance for interacting with the AI model.
    /// </summary>
    /// <returns>A provider-specific client object.</returns>
    object CreateClient();
}
