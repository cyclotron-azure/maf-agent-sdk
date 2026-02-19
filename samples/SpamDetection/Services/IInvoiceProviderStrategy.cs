using Cyclotron.Maf.AgentSdk.Agents;

namespace SpamDetection.Services;

/// <summary>
/// Provides provider-specific behavior for invoice extraction workflows.
/// Abstracts differences between Azure (vector store + file_search) and Ollama (local retrieval).
/// </summary>
public interface IInvoiceProviderStrategy
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
    /// Gets whether this provider uses vector stores (Azure) vs. local retrieval (Ollama).
    /// </summary>
    bool UsesVectorStore { get; }

    /// <summary>
    /// Prepares vector store for this provider if needed, otherwise returns null.
    /// For Azure: creates and returns a vector store ID.
    /// For Ollama: returns null (uses local indexing instead).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The vector store ID or null if not used.</returns>
    Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken);
}
