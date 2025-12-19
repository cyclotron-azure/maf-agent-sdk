using Cyclotron.Maf.AgentSdk.Models;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Service for cleaning up Azure AI Foundry resources.
/// Provides methods to delete files, vector stores, threads, and agents
/// to prevent resource accumulation and quota exhaustion.
/// </summary>
/// <remarks>
/// <para>
/// This service maintains a list of protected agent names that are excluded from cleanup.
/// Protected agents are typically shared or persistent agents that should not be deleted.
/// </para>
/// <para>
/// Vector stores can be protected by specifying a metadata key during cleanup.
/// Any vector store with the protected metadata key will be excluded from deletion.
/// </para>
/// </remarks>
public class AzureFoundryCleanupService : IAzureFoundryCleanupService
{
    private readonly IPersistentAgentsClientFactory _clientFactory;
    private readonly ILogger<AzureFoundryCleanupService> _logger;
    private readonly HashSet<string> _protectedAgentNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFoundryCleanupService"/> class.
    /// </summary>
    /// <param name="clientFactory">The factory for creating Azure AI Foundry clients.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AzureFoundryCleanupService(
        IPersistentAgentsClientFactory clientFactory,
        ILogger<AzureFoundryCleanupService> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Protected agents that should not be deleted (shared/persistent agents)
        _protectedAgentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CorrespondentAgent",
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupAllResourcesAsync(
        string providerName,
        string? protectedMetadataKey = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive Azure AI Foundry resource cleanup for provider '{ProviderName}'", providerName);

        var filesStats = await CleanupFilesAsync(providerName, cancellationToken);
        var vectorStoresStats = await CleanupVectorStoresAsync(providerName, protectedMetadataKey, cancellationToken);
        var threadsStats = await CleanupThreadsAsync(providerName, cancellationToken);
        var agentsStats = await CleanupAgentsAsync(providerName, cancellationToken);

        var totalStats = new CleanupStatistics
        {
            FilesDeleted = filesStats.FilesDeleted,
            FilesFailedToDelete = filesStats.FilesFailedToDelete,
            VectorStoresDeleted = vectorStoresStats.VectorStoresDeleted,
            VectorStoresFailedToDelete = vectorStoresStats.VectorStoresFailedToDelete,
            ThreadsDeleted = threadsStats.ThreadsDeleted,
            ThreadsFailedToDelete = threadsStats.ThreadsFailedToDelete,
            AgentsDeleted = agentsStats.AgentsDeleted,
            AgentsFailedToDelete = agentsStats.AgentsFailedToDelete
        };

        _logger.LogInformation(
            "Cleanup completed: {TotalDeleted} resources deleted, {TotalFailed} failed " +
            "(Files: {FilesDeleted}/{FilesFailedToDelete}, VectorStores: {VectorStoresDeleted}/{VectorStoresFailedToDelete}, " +
            "Threads: {ThreadsDeleted}/{ThreadsFailedToDelete}, Agents: {AgentsDeleted}/{AgentsFailedToDelete})",
            totalStats.TotalDeleted,
            totalStats.TotalFailed,
            totalStats.FilesDeleted,
            totalStats.FilesFailedToDelete,
            totalStats.VectorStoresDeleted,
            totalStats.VectorStoresFailedToDelete,
            totalStats.ThreadsDeleted,
            totalStats.ThreadsFailedToDelete,
            totalStats.AgentsDeleted,
            totalStats.AgentsFailedToDelete);

        return totalStats;
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupFilesAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry files for provider '{ProviderName}'", providerName);

        var projectClient = _clientFactory.GetClient(providerName);
        var client = projectClient.GetPersistentAgentsClient();
        int deleted = 0;
        int failed = 0;

        try
        {
            var filesResponse = await client.Files.GetFilesAsync(cancellationToken: cancellationToken);
            foreach (var file in filesResponse.Value)
            {
                try
                {
                    _logger.LogDebug("Deleting file: {FileId}", file.Id);
                    await client.Files.DeleteFileAsync(file.Id, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {FileId}", file.Id);
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate files");
        }

        _logger.LogInformation("Files cleanup complete: {Deleted} deleted, {Failed} failed", deleted, failed);

        return new CleanupStatistics
        {
            FilesDeleted = deleted,
            FilesFailedToDelete = failed
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> DeleteFilesAsync(
        string providerName,
        IEnumerable<string> fileIds,
        CancellationToken cancellationToken = default)
    {
        var fileIdList = fileIds.ToList();
        _logger.LogInformation(
            "Deleting {FileCount} specific files for provider '{ProviderName}'",
            fileIdList.Count,
            providerName);

        var projectClient = _clientFactory.GetClient(providerName);
        var client = projectClient.GetPersistentAgentsClient();
        int deleted = 0;
        int failed = 0;

        foreach (var fileId in fileIdList)
        {
            try
            {
                _logger.LogDebug("Deleting file: {FileId}", fileId);
                await client.Files.DeleteFileAsync(fileId, cancellationToken);
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file: {FileId}", fileId);
                failed++;
            }
        }

        _logger.LogInformation(
            "Specific files deletion complete: {Deleted} deleted, {Failed} failed",
            deleted,
            failed);

        return new CleanupStatistics
        {
            FilesDeleted = deleted,
            FilesFailedToDelete = failed
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupVectorStoresAsync(
        string providerName,
        string? protectedMetadataKey = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry vector stores for provider '{ProviderName}' (protected key: {ProtectedKey})", providerName, protectedMetadataKey ?? "none");
        var projectClient = _clientFactory.GetClient(providerName);
        var client = projectClient.GetPersistentAgentsClient();
        int deleted = 0;
        int failed = 0;

        try
        {
            await foreach (var vectorStore in client.VectorStores.GetVectorStoresAsync(cancellationToken: cancellationToken))
            {
                // Skip shared knowledge base vector store if protected metadata key is provided
                if (!string.IsNullOrEmpty(protectedMetadataKey) &&
                    vectorStore.Metadata?.ContainsKey(protectedMetadataKey) == true)
                {
                    _logger.LogDebug(
                        "Skipping protected vector store: {VectorStoreId} (key: {MetadataKey})",
                        vectorStore.Id,
                        protectedMetadataKey);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Deleting vector store: {VectorStoreId}", vectorStore.Id);
                    await client.VectorStores.DeleteVectorStoreAsync(vectorStore.Id, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete vector store: {VectorStoreId}", vectorStore.Id);
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate vector stores");
        }

        _logger.LogInformation("Vector stores cleanup complete: {Deleted} deleted, {Failed} failed", deleted, failed);

        return new CleanupStatistics
        {
            VectorStoresDeleted = deleted,
            VectorStoresFailedToDelete = failed
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupThreadsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry threads for provider '{ProviderName}'", providerName);
        var projectClient = _clientFactory.GetClient(providerName);
        var client = projectClient.GetPersistentAgentsClient();

        int deleted = 0;
        int failed = 0;

        try
        {
            await foreach (var thread in client.Threads.GetThreadsAsync(cancellationToken: cancellationToken))
            {
                try
                {
                    _logger.LogDebug("Deleting thread: {ThreadId}", thread.Id);
                    await client.Threads.DeleteThreadAsync(thread.Id, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete thread: {ThreadId}", thread.Id);
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate threads");
        }

        _logger.LogInformation("Threads cleanup complete: {Deleted} deleted, {Failed} failed", deleted, failed);

        return new CleanupStatistics
        {
            ThreadsDeleted = deleted,
            ThreadsFailedToDelete = failed
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupAgentsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry agents for provider '{ProviderName}'", providerName);

        var projectClient = _clientFactory.GetClient(providerName);
        var client = projectClient.GetPersistentAgentsClient();
        int deleted = 0;
        int failed = 0;

        try
        {
            await foreach (var agent in client.Administration.GetAgentsAsync(cancellationToken: cancellationToken))
            {
                // Skip protected agents
                if (_protectedAgentNames.Contains(agent.Name))
                {
                    _logger.LogDebug(
                        "Skipping protected agent: {AgentName} (ID: {AgentId})",
                        agent.Name,
                        agent.Id);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Deleting agent: {AgentName} (ID: {AgentId})", agent.Name, agent.Id);
                    await client.Administration.DeleteAgentAsync(agent.Id, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete agent: {AgentName} (ID: {AgentId})",
                        agent.Name,
                        agent.Id);
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate agents");
        }

        _logger.LogInformation("Agents cleanup complete: {Deleted} deleted, {Failed} failed", deleted, failed);

        return new CleanupStatistics
        {
            AgentsDeleted = deleted,
            AgentsFailedToDelete = failed
        };
    }
}
