using Cyclotron.Maf.AgentSdk.Agents;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Azure AI Foundry invoice extraction strategy using vector stores and file_search.
/// </summary>
public sealed class AzureInvoiceProviderStrategy(
    ILogger<AzureInvoiceProviderStrategy> logger,
    [FromKeyedServices("invoice_extractor")] IAgentFactory invoiceExtractorFactory,
    IVectorStoreManager vectorStoreManager) : IInvoiceProviderStrategy
{
    private readonly ILogger<AzureInvoiceProviderStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAgentFactory _invoiceExtractorFactory = invoiceExtractorFactory ?? throw new ArgumentNullException(nameof(invoiceExtractorFactory));
    private readonly IVectorStoreManager _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));

    public string ProviderKey => "azure";

    public IAgentFactory AgentFactory => _invoiceExtractorFactory;

    public bool UsesVectorStore => true;

    public async Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken)
    {
        // For Azure, no pre-setup needed; vector stores are created per-document in executors.
        // This method exists for interface consistency.
        _logger.LogDebug("Azure invoice provider; vector stores will be created per-document");
        return await Task.FromResult<string?>(null);
    }
}
