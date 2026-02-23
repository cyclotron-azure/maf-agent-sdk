namespace Cyclotron.Maf.AgentSdk.Common.Services;

/// <summary>
/// Factory for creating AI provider clients with proper authentication.
/// Supports multiple model providers (Azure AI Foundry, Azure OpenAI, Ollama) with different endpoints and authentication methods.
/// Registered as a scoped service to ensure proper resource management and avoid state sharing.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates new client instances per request to avoid state sharing across parallel processing.
/// Provider configurations are loaded from the <c>providers:</c> section in agent.config.yaml.
/// Returns <see cref="IProviderClient"/> instances with typed accessors for provider-specific clients.
/// </para>
/// <para>
/// Supported authentication methods:
/// <list type="bullet">
/// <item><description>DefaultAzureCredential: For Azure AI Foundry providers.</description></item>
/// <item><description>API Key: For Azure OpenAI providers (via adapter - not recommended for production).</description></item>
/// <item><description>Local: For Ollama and other local providers (no authentication required).</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IProviderClientFactory
{
    /// <summary>
    /// Gets an <see cref="IProviderClient"/> instance configured for the specified provider.
    /// Creates a new client instance per call to avoid state sharing.
    /// </summary>
    /// <param name="providerName">The provider key from the <c>providers:</c> section in agent.config.yaml.</param>
    /// <returns>A configured <see cref="IProviderClient"/> for the specified provider.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified provider is not found or has invalid configuration.</exception>
    IProviderClient GetClient(string providerName);
}
