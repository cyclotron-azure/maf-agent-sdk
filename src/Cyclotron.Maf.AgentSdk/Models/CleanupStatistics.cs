namespace Cyclotron.Maf.AgentSdk.Models;

/// <summary>
/// Statistics about Azure AI Foundry cleanup operations.
/// Tracks the number of successfully deleted resources and failures for each resource type.
/// </summary>
public record CleanupStatistics
{
    /// <summary>
    /// Gets the number of files successfully deleted.
    /// </summary>
    public int FilesDeleted { get; init; }

    /// <summary>
    /// Gets the number of files that failed to delete.
    /// </summary>
    public int FilesFailedToDelete { get; init; }

    /// <summary>
    /// Gets the number of vector stores successfully deleted.
    /// </summary>
    public int VectorStoresDeleted { get; init; }

    /// <summary>
    /// Gets the number of vector stores that failed to delete.
    /// </summary>
    public int VectorStoresFailedToDelete { get; init; }

    /// <summary>
    /// Gets the number of threads successfully deleted.
    /// </summary>
    public int ThreadsDeleted { get; init; }

    /// <summary>
    /// Gets the number of threads that failed to delete.
    /// </summary>
    public int ThreadsFailedToDelete { get; init; }

    /// <summary>
    /// Gets the number of agents successfully deleted.
    /// </summary>
    public int AgentsDeleted { get; init; }

    /// <summary>
    /// Gets the number of agents that failed to delete.
    /// </summary>
    public int AgentsFailedToDelete { get; init; }

    /// <summary>
    /// Gets the total number of resources successfully deleted across all types.
    /// </summary>
    public int TotalDeleted => FilesDeleted + VectorStoresDeleted + ThreadsDeleted + AgentsDeleted;

    /// <summary>
    /// Gets the total number of resources that failed to delete across all types.
    /// </summary>
    public int TotalFailed => FilesFailedToDelete + VectorStoresFailedToDelete + ThreadsFailedToDelete + AgentsFailedToDelete;
}
