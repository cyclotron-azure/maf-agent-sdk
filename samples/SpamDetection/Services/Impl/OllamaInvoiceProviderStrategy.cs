using Cyclotron.Maf.AgentSdk.Agents;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Ollama invoice extraction strategy using local vector store indexing and in-process retrieval.
/// Does not use Azure AI Foundry vector stores or file_search tools because Ollama agents cannot access them.
/// Instead, documents are indexed locally and chunks are selected and embedded into prompts at runtime.
/// </summary>
public sealed class OllamaInvoiceProviderStrategy(
    ILogger<OllamaInvoiceProviderStrategy> logger,
    [FromKeyedServices("invoice_extractor_ollama")] IAgentFactory invoiceExtractorFactory,
    IVectorStoreManager vectorStoreManager) : IInvoiceProviderStrategy
{
    private readonly ILogger<OllamaInvoiceProviderStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAgentFactory _invoiceExtractorFactory = invoiceExtractorFactory ?? throw new ArgumentNullException(nameof(invoiceExtractorFactory));
    private readonly IVectorStoreManager _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));

    public string ProviderKey => "ollama";

    public IAgentFactory AgentFactory => _invoiceExtractorFactory;

    public bool UsesVectorStore => false;

    public async Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ollama invoice provider selected; using local vector store for RAG without Azure file_search.");
        return await Task.FromResult<string?>(null);
    }
}
