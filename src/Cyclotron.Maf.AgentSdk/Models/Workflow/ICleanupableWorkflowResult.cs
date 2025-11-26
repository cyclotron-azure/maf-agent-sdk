namespace Cyclotron.Maf.AgentSdk.Models.Workflow;

/// <summary>
/// Interface for workflow results that support cleanup operations.
/// Implement this interface to enable the CleanupExecutor to clean up resources.
/// </summary>
public interface ICleanupableWorkflowResult
{
    /// <summary>
    /// Gets the file IDs created during the workflow.
    /// </summary>
    IReadOnlyList<string> FileIds { get; }

    /// <summary>
    /// Gets the vector store IDs created during the workflow.
    /// </summary>
    IReadOnlyList<string> VectorStoreIds { get; }

    /// <summary>
    /// Gets the agent IDs created during the workflow.
    /// </summary>
    IReadOnlyList<string> AgentIds { get; }

    /// <summary>
    /// Gets the action determined by the workflow.
    /// </summary>
    string Action { get; }
}
