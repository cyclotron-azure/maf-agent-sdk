using Azure.AI.Agents.Persistent;

namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Factory for creating <see cref="PersistentAgentsClient"/> instances with proper authentication.
/// Supports multiple model providers with different endpoints and authentication methods.
/// Registered as a scoped service to ensure proper resource management and avoid state sharing.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates new client instances per request to avoid state sharing across parallel processing.
/// Provider configurations are loaded from the <c>providers:</c> section in agent.config.yaml.
/// </para>
/// <para>
/// Supported authentication methods:
/// <list type="bullet">
/// <item><description>DefaultAzureCredential: For Azure AI Foundry providers.</description></item>
/// <item><description>API Key: For Azure OpenAI providers (via adapter).</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPersistentAgentsClientFactory
{
    /// <summary>
    /// Gets a <see cref="PersistentAgentsClient"/> instance configured for the specified provider.
    /// Creates a new client instance per call to avoid state sharing.
    /// </summary>
    /// <param name="providerName">The provider key from the <c>providers:</c> section in agent.config.yaml.</param>
    /// <returns>A configured <see cref="PersistentAgentsClient"/> for the specified provider.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified provider is not found or has invalid configuration.</exception>
    PersistentAgentsClient GetClient(string providerName);
}
