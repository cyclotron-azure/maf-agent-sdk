namespace Cyclotron.Maf.AgentSdk.Models.Workflow;

/// <summary>
/// Generic state constants for workflow context.
/// Domain-specific constants should be defined in consuming packages.
/// </summary>
public static class WorkflowStateConstants
{
    /// <summary>
    /// Key for storing the workflow instance ID in context.
    /// </summary>
    public const string WorkflowInstanceIdKey = "WorkflowInstanceId";

    /// <summary>
    /// State scope for file content operations.
    /// </summary>
    public static readonly string FileContentStateScope = nameof(FileContentStateScope);

    /// <summary>
    /// State scope for vector storage operations.
    /// </summary>
    public static readonly string VectorStorageStateScope = nameof(VectorStorageStateScope);

    /// <summary>
    /// Generates a unique scope name for the workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The unique workflow instance identifier.</param>
    /// <param name="baseScopeName">The base scope name to append the instance ID to.</param>
    /// <returns>A unique scope name in the format "{baseScopeName}_{workflowInstanceId}".</returns>
    public static string GetScopeName(string workflowInstanceId, string baseScopeName)
        => $"{baseScopeName}_{workflowInstanceId}";
}
