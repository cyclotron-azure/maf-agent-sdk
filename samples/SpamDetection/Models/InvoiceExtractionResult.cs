namespace SpamDetection.Models;

/// <summary>
/// Represents the result of invoice extraction workflow execution.
/// Implements ICleanupableWorkflowResult for resource cleanup tracking.
/// </summary>
public class InvoiceExtractionResult : Cyclotron.Maf.AgentSdk.Models.Workflow.ICleanupableWorkflowResult
{
    // Helper properties for adding items (since IReadOnlyList doesn't support Add)
    public List<string> MutableFileIds { get; } = [];
    public List<string> MutableVectorStoreIds { get; } = [];
    public List<string> MutableAgentIds { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> FileIds => MutableFileIds.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<string> VectorStoreIds => MutableVectorStoreIds.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<string> AgentIds => MutableAgentIds.AsReadOnly();

    /// <inheritdoc />
    public string Action { get; set; } = "success";

    /// <summary>
    /// The extracted invoice data.
    /// </summary>
    public required InvoiceData InvoiceData { get; set; }

    /// <summary>
    /// Content type of the processed PDF (TextBased, ImageOnly, or Mixed).
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// The raw agent response before parsing into InvoiceData.
    /// </summary>
    public string? AgentResponse { get; set; }
}
