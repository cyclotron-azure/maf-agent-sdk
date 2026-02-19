using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Services;
using SpamDetection.Models;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Implementation of the invoice extraction workflow.
/// Orchestrates PDF analysis, content-based routing, and multi-provider invoice data extraction.
/// Supports both Azure (vector store + file_search) and Ollama (local retrieval) providers.
/// </summary>
public sealed class InvoiceExtractionWorkflow(
    ILogger<InvoiceExtractionWorkflow> logger,
    IConfiguration configuration,
    [FromKeyedServices("pdfpig")] IPdfContentAnalyzer pdfContentAnalyzer,
    IPdfToMarkdownConverter pdfToMarkdownConverter,
    [FromKeyedServices("pdfpig")] IPdfImageExtractor pdfImageExtractor,
    IVectorStoreManager vectorStoreManager,
    TextBasedInvoiceExecutor textBasedExecutor,
    ImageOnlyInvoiceExecutor imageOnlyExecutor,
    MixedInvoiceExecutor mixedExecutor,
    IEnumerable<IInvoiceProviderStrategy> providerStrategies) : IInvoiceExtractionWorkflow
{
    private readonly ILogger<InvoiceExtractionWorkflow> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IPdfContentAnalyzer _pdfContentAnalyzer = pdfContentAnalyzer ?? throw new ArgumentNullException(nameof(pdfContentAnalyzer));
    private readonly IPdfToMarkdownConverter _pdfToMarkdownConverter = pdfToMarkdownConverter ?? throw new ArgumentNullException(nameof(pdfToMarkdownConverter));
    private readonly IPdfImageExtractor _pdfImageExtractor = pdfImageExtractor ?? throw new ArgumentNullException(nameof(pdfImageExtractor));
    private readonly IVectorStoreManager _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));
    private readonly TextBasedInvoiceExecutor _textBasedExecutor = textBasedExecutor ?? throw new ArgumentNullException(nameof(textBasedExecutor));
    private readonly ImageOnlyInvoiceExecutor _imageOnlyExecutor = imageOnlyExecutor ?? throw new ArgumentNullException(nameof(imageOnlyExecutor));
    private readonly MixedInvoiceExecutor _mixedExecutor = mixedExecutor ?? throw new ArgumentNullException(nameof(mixedExecutor));
    private readonly IReadOnlyDictionary<string, IInvoiceProviderStrategy> _providerStrategies = BuildProviderStrategies(providerStrategies);

    /// <summary>
    /// Executes the invoice extraction workflow on the provided PDF document.
    /// Routes to appropriate executor based on PDF content type.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExtractInvoiceAsync(
        Stream pdfContent,
        string fileName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting invoice extraction workflow for file: {FileName}", fileName);

        try
        {
            // Resolve provider strategy
            var strategy = ResolveProviderStrategy();
            _logger.LogInformation("Invoice extraction provider: {Provider}", strategy.ProviderKey);

            // Buffer the PDF once so non-seekable streams can be reused across steps
            await using var bufferedStream = new MemoryStream();
            await pdfContent.CopyToAsync(bufferedStream, cancellationToken);
            var pdfBytes = bufferedStream.ToArray();

            if (pdfBytes.Length == 0)
            {
                throw new InvalidOperationException($"PDF content is empty for file: {fileName}");
            }

            // Analyze PDF content type
            _logger.LogInformation("Analyzing PDF content type...");
            var analysisResult = await _pdfContentAnalyzer.AnalyzeFromBytesAsync(pdfBytes, fileName, cancellationToken);
            _logger.LogInformation("Detected PDF content type: {ContentType}", analysisResult.ContentType);

            InvoiceExtractionResult result;

            // Route to appropriate executor based on content type
            switch (analysisResult.ContentType)
            {
                case PdfContentType.TextBased:
                    using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
                    {
                        result = await _textBasedExecutor.ExecuteAsync(
                            pdfStream,
                            fileName,
                            strategy,
                            _pdfToMarkdownConverter,
                            _vectorStoreManager,
                            cancellationToken);
                    }
                    break;

                case PdfContentType.ImageOnly:
                    using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
                    {
                        result = await _imageOnlyExecutor.ExecuteAsync(
                            pdfStream,
                            fileName,
                            strategy,
                            _pdfImageExtractor,
                            _vectorStoreManager,
                            cancellationToken);
                    }
                    break;

                case PdfContentType.Mixed:
                    using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
                    {
                        result = await _mixedExecutor.ExecuteAsync(
                            pdfStream,
                            fileName,
                            strategy,
                            _pdfToMarkdownConverter,
                            _pdfImageExtractor,
                            _vectorStoreManager,
                            cancellationToken);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown PDF content type: {analysisResult.ContentType}");
            }

            result.ContentType = analysisResult.ContentType.ToString();

            _logger.LogInformation(
                "Invoice extraction completed. Invoice Number: {InvoiceNumber}, Total Amount: {TotalAmount} {Currency}",
                result.InvoiceData.InvoiceNumber ?? "N/A",
                result.InvoiceData.TotalAmount?.ToString("F2") ?? "N/A",
                result.InvoiceData.Currency ?? "N/A");

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Invoice extraction was cancelled for file: {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during invoice extraction for file: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Resolves the provider strategy based on configuration.
    /// </summary>
    private IInvoiceProviderStrategy ResolveProviderStrategy()
    {
        var provider = _configuration["Workflow:InvoiceProvider"]?.ToLowerInvariant() ?? "azure";

        if (_providerStrategies.TryGetValue(provider, out var strategy))
        {
            return strategy;
        }

        if (_providerStrategies.TryGetValue("azure", out var azureStrategy))
        {
            return azureStrategy;
        }

        return _providerStrategies.Values.First();
    }

    /// <summary>
    /// Builds a dictionary of provider strategies keyed by provider name.
    /// </summary>
    private static IReadOnlyDictionary<string, IInvoiceProviderStrategy> BuildProviderStrategies(
        IEnumerable<IInvoiceProviderStrategy> providerStrategies)
    {
        if (providerStrategies is null)
        {
            throw new ArgumentNullException(nameof(providerStrategies));
        }

        var strategies = providerStrategies.ToList();
        if (strategies.Count == 0)
        {
            throw new ArgumentException("At least one invoice provider strategy must be registered.", nameof(providerStrategies));
        }

        return strategies.ToDictionary(
            strategy => strategy.ProviderKey,
            StringComparer.OrdinalIgnoreCase);
    }

}
