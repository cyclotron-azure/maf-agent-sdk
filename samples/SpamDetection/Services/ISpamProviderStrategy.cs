using Cyclotron.Maf.AgentSdk.Agents;

namespace SpamDetection.Services;

/// <summary>
/// Provides provider-specific behavior for spam detection.
/// </summary>
public interface ISpamProviderStrategy
{
    /// <summary>
    /// Gets the provider key used in configuration (e.g., "azure", "ollama").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the agent factory for the provider.
    /// </summary>
    IAgentFactory AgentFactory { get; }

    /// <summary>
    /// Prepares an optional vector store and returns its ID if created.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The vector store ID or null if not used.</returns>
    Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken);
}
