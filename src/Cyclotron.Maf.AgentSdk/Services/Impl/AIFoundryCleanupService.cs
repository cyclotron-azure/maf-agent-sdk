using Cyclotron.Maf.AgentSdk.Models;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using OpenAI.Files;
using OpenAI.VectorStores;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Service for cleaning up Azure AI Foundry resources using V2 API.
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
public class AIFoundryCleanupService : IAIFoundryCleanupService
{
    private readonly IAIProjectClientFactory _clientFactory;
    private readonly ILogger<AIFoundryCleanupService> _logger;
    private readonly HashSet<string> _protectedAgentNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIFoundryCleanupService"/> class.
    /// </summary>
    /// <param name="clientFactory">The factory for creating Azure AI Foundry clients.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AIFoundryCleanupService(
        IAIProjectClientFactory clientFactory,
        ILogger<AIFoundryCleanupService> logger)
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
        var openAIClient = projectClient.GetProjectOpenAIClient();
        var fileClient = openAIClient.GetOpenAIFileClient();
        int deleted = 0;
        int failed = 0;

        try
        {
            var filesResult = await fileClient.GetFilesAsync(cancellationToken: cancellationToken);
            foreach (var file in filesResult.Value)
            {
                try
                {
                    _logger.LogDebug("Deleting file: {FileId}", file.Id);
                    await fileClient.DeleteFileAsync(file.Id, cancellationToken);
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
        var openAIClient = projectClient.GetProjectOpenAIClient();
        var fileClient = openAIClient.GetOpenAIFileClient();
        int deleted = 0;
        int failed = 0;

        foreach (var fileId in fileIdList)
        {
            try
            {
                _logger.LogDebug("Deleting file: {FileId}", fileId);
                await fileClient.DeleteFileAsync(fileId, cancellationToken);
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
        var openAIClient = projectClient.GetProjectOpenAIClient();
        var vectorStoreClient = openAIClient.GetVectorStoreClient();
        int deleted = 0;
        int failed = 0;

        try
        {
            await foreach (var vectorStore in vectorStoreClient.GetVectorStoresAsync(cancellationToken: cancellationToken))
            {
                // Skip shared knowledge base vector store if protected metadata key is provided
                if (!string.IsNullOrEmpty(protectedMetadataKey) &&
                    vectorStore.Metadata?.ContainsKey(protectedMetadataKey) == true)
                {
                    _logger.LogDebug(
                        "Skipping protected vector store: {VectorStoreId} (Name: {Name})",
                        vectorStore.Id,
                        vectorStore.Name);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Deleting vector store: {VectorStoreId} (Name: {Name})", vectorStore.Id, vectorStore.Name);
                    await vectorStoreClient.DeleteVectorStoreAsync(vectorStore.Id, cancellationToken);
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
    public async Task<CleanupStatistics> CleanupThreadsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry threads for provider '{ProviderName}'", providerName);
        var projectClient = _clientFactory.GetClient(providerName);

        // Note: V2 API uses conversations, not threads. The AIProjectClient doesn't expose
        // a direct way to list and delete all threads/conversations via OpenAI client.
        // This would need to be done via the Agents API which is not exposed in the same way.
        // For now, log that this operation is not supported in V2 API.

        _logger.LogWarning("Thread cleanup is not directly supported in V2 API. Threads are managed through agent conversations.");

        return new CleanupStatistics
        {
            ThreadsDeleted = 0,
            ThreadsFailedToDelete = 0
        };
    }

    /// <inheritdoc/>
    public async Task<CleanupStatistics> CleanupAgentsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up Azure AI Foundry agents for provider '{ProviderName}' (excluding protected agents)", providerName);
        var projectClient = _clientFactory.GetClient(providerName);
        int deleted = 0;
        int failed = 0;

        try
        {
            // Note: V2 API agents are managed through AIProjectClient.Agents
            // The Agents property provides access to agent operations
            await foreach (var agent in projectClient.Agents.GetAgentsAsync(cancellationToken: cancellationToken))
            {
                // Skip protected agents
                if (_protectedAgentNames.Contains(agent.Name))
                {
                    _logger.LogDebug("Skipping protected agent: {AgentName}", agent.Name);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Deleting agent: {AgentId} (Name: {AgentName})", agent.Id, agent.Name);
                    await projectClient.Agents.DeleteAgentAsync(agent.Name, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete agent: {AgentId} (Name: {AgentName})", agent.Id, agent.Name);
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
