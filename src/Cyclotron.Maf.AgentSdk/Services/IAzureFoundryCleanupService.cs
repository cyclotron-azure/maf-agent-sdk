using Cyclotron.Maf.AgentSdk.Models;

namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Service for cleaning up Azure AI Foundry resources.
/// Provides methods to delete files, vector stores, threads, and agents
/// to prevent resource accumulation and quota exhaustion.
/// </summary>
public interface IAzureFoundryCleanupService
{
    /// <summary>
    /// Performs comprehensive cleanup of all Azure AI Foundry resources.
    /// Deletes files, vector stores, threads, and agents in sequence.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="protectedMetadataKey">Optional metadata key to protect specific vector stores from deletion.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about the cleanup operation including counts of deleted and failed resources.
    /// </returns>
    Task<CleanupStatistics> CleanupAllResourcesAsync(
        string providerName,
        string? protectedMetadataKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up all files in Azure AI Foundry for the specified provider.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about files deleted and any failures.
    /// </returns>
    Task<CleanupStatistics> CleanupFilesAsync(
        string providerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes specific files by their IDs for workflow-specific cleanup.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="fileIds">Collection of file IDs to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about files deleted and any failures.
    /// </returns>
    Task<CleanupStatistics> DeleteFilesAsync(
        string providerName,
        IEnumerable<string> fileIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up vector stores in Azure AI Foundry.
    /// Protected vector stores (identified by metadata key) are excluded from deletion.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="protectedMetadataKey">Optional metadata key to identify vector stores that should not be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about vector stores deleted and any failures.
    /// </returns>
    Task<CleanupStatistics> CleanupVectorStoresAsync(
        string providerName,
        string? protectedMetadataKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up all threads in Azure AI Foundry for the specified provider.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about threads deleted and any failures.
    /// </returns>
    Task<CleanupStatistics> CleanupThreadsAsync(
        string providerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up agents in Azure AI Foundry, excluding protected agents.
    /// Protected agents are identified by a predefined list of agent names.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains statistics about agents deleted and any failures.
    /// </returns>
    Task<CleanupStatistics> CleanupAgentsAsync(
        string providerName,
        CancellationToken cancellationToken = default);
}