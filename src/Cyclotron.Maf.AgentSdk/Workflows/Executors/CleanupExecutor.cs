using Cyclotron.Maf.AgentSdk.Models.Workflow;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cyclotron.Maf.AgentSdk.Services;

namespace Cyclotron.Maf.AgentSdk.Workflows.Executors;

/// <summary>
/// Generic workflow cleanup executor for Azure AI Foundry resources.
/// Deletes vector stores created during the current workflow execution.
/// Each workflow creates its own isolated vector stores which are cleaned up after processing.
/// Agents are deleted by individual executors when AutoDelete=true.
/// </summary>
/// <typeparam name="TResult">The workflow result type that implements <see cref="ICleanupableWorkflowResult"/>.</typeparam>
public class CleanupExecutor<TResult>(
    IVectorStoreManager vectorStoreManager,
    IAIFoundryCleanupService cleanupService,
    ILogger<CleanupExecutor<TResult>> logger,
    IOptions<ModelProviderOptions> providerOptions) : Executor<TResult, TResult>(nameof(CleanupExecutor<TResult>))
    where TResult : ICleanupableWorkflowResult
{
    private readonly IVectorStoreManager _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));
    private readonly IAIFoundryCleanupService _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
    private readonly ILogger<CleanupExecutor<TResult>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ModelProviderOptions _providerOptions = providerOptions?.Value ?? throw new ArgumentNullException(nameof(providerOptions));

    /// <inheritdoc/>
    public override async ValueTask<TResult> HandleAsync(
        TResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[{ExecutorName}] Starting execution - Input: Action={Action}, FileIds={FileIds}, VectorStoreIds={VectorStoreIds}, AgentIds={AgentIds}",
            nameof(CleanupExecutor<TResult>),
            result.Action,
            string.Join(", ", result.FileIds),
            string.Join(", ", result.VectorStoreIds),
            string.Join(", ", result.AgentIds));

        _logger.LogInformation("Starting workflow-specific resource cleanup");

        // Get provider name from configuration (uses default if not specified)
        var providerName = _providerOptions.GetDefaultProviderName();
        _logger.LogDebug("Using provider: {ProviderName} for cleanup operations", providerName);

        try
        {
            // Delete vector stores created during THIS workflow execution
            // Each workflow creates unique vector stores that must be cleaned up
            // Agents are already deleted by individual executors when AutoDelete=true
            _logger.LogInformation(
                "Cleaning up {VectorStoreCount} workflow-specific vector stores (includes all files)",
                result.VectorStoreIds.Count);

            int vectorStoresDeleted = 0;
            int vectorStoresFailed = 0;

            foreach (var vectorStoreId in result.VectorStoreIds)
            {
                try
                {
                    _logger.LogDebug("Deleting vector store: {VectorStoreId}", vectorStoreId);
                    await _vectorStoreManager.CleanupVectorStoreAsync(providerName, vectorStoreId, cancellationToken);
                    vectorStoresDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete vector store: {VectorStoreId}", vectorStoreId);
                    vectorStoresFailed++;
                }
            }

            _logger.LogInformation(
                "Workflow cleanup completed: {VectorStoresDeleted} vector stores deleted, {VectorStoresFailed} failed",
                vectorStoresDeleted,
                vectorStoresFailed);

            _logger.LogInformation(
                "[{ExecutorName}] Execution completed - Output: VectorStoresDeleted={VectorStoresDeleted}, VectorStoresFailedToDelete={VectorStoresFailed}, ResultAction={ResultAction}",
                nameof(CleanupExecutor<TResult>),
                vectorStoresDeleted,
                vectorStoresFailed,
                result.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during workflow-specific cleanup");
            // Don't throw - cleanup failures shouldn't fail the workflow
        }

        // Pass through the result unchanged
        return result;
    }
}
