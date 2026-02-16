namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Factory for creating model provider instances based on configuration.
/// Supports multiple provider types with different authentication and connection methods.
/// </summary>
public interface IProviderClientFactory
{
    /// <summary>
    /// Creates a provider instance for the specified provider name.
    /// </summary>
    /// <param name="providerName">The provider key from configuration.</param>
    /// <returns>An <see cref="IModelProvider"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not found or invalid.</exception>
    IModelProvider GetProvider(string providerName);

    /// <summary>
    /// Checks if a provider with the specified name is registered.
    /// </summary>
    /// <param name="providerName">The provider name to check.</param>
    /// <returns>True if the provider exists, false otherwise.</returns>
    bool HasProvider(string providerName);
}
