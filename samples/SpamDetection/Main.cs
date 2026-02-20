using SpamDetection.Services;

namespace SpamDetection;

/// <summary>
/// Main entry point for the spam detection sample.
/// Demonstrates using the AgentSdk to classify messages as spam or not spam.
/// Supports both traditional text parsing and structured output workflows.
/// </summary>
public class Main(
    IHostApplicationLifetime applicationLifetime,
    IConfiguration configuration,
    ILogger<Main> logger,
    ISpamWorkflow spamWorkflow,
    ISpamWorkflowStructuredOutput spamWorkflowStructuredOutput,
    IInvoiceExtractionWorkflow invoiceExtractionWorkflow) : IMain
{
    private readonly ILogger<Main> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    private readonly ISpamWorkflow _spamWorkflow = spamWorkflow ?? throw new ArgumentNullException(nameof(spamWorkflow));
    private readonly ISpamWorkflowStructuredOutput _spamWorkflowStructuredOutput = spamWorkflowStructuredOutput ?? throw new ArgumentNullException(nameof(spamWorkflowStructuredOutput));
    private readonly IInvoiceExtractionWorkflow _invoiceExtractionWorkflow = invoiceExtractionWorkflow ?? throw new ArgumentNullException(nameof(invoiceExtractionWorkflow));

    public IConfiguration Configuration { get; set; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<int> RunAsync()
    {
        var cancellationToken = _applicationLifetime.ApplicationStopping;
        var mode = NormalizeMode(Configuration["Workflow:Mode"] ?? "both");
        var spamProvider = Configuration["Workflow:SpamProvider"]?.ToLowerInvariant() ?? "azure";
        var invoiceProvider = Configuration["Workflow:InvoiceProvider"]?.ToLowerInvariant() ?? "azure";

        _logger.LogInformation("Workflow mode: {Mode}", mode);
        _logger.LogInformation("Spam detection provider: {Provider}", spamProvider == "ollama" ? "Ollama (Local)" : "Azure AI Foundry");
        _logger.LogInformation("Invoice extraction provider: {Provider}", invoiceProvider == "ollama" ? "Ollama (Local)" : "Azure AI Foundry");

        return mode.ToLowerInvariant() switch
        {
            "spam" => await _spamWorkflow.RunAsync(cancellationToken),
            "spam-structured" => await _spamWorkflowStructuredOutput.RunAsync(cancellationToken),
            "invoice" => await RunInvoiceAsync(cancellationToken),
            "both" => await RunBothAsync(cancellationToken),
            "both-structured" => await RunBothStructuredAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown Workflow:Mode '{mode}'. Use spam, spam-structured, invoice, both, or both-structured.")
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? "both").Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" => "both",
            "inv" => "invoice",
            "invoices" => "invoice",
            "spam-only" => "spam",
            "invoice-only" => "invoice",
            "structured" => "spam-structured",
            "spam-structured" => "spam-structured",
            _ => normalized
        };
    }

    private async Task<int> RunBothAsync(CancellationToken cancellationToken)
    {
        var spamResult = await _spamWorkflow.RunAsync(cancellationToken);
        if (spamResult != 0)
        {
            return spamResult;
        }

        return await RunInvoiceAsync(cancellationToken);
    }

    private async Task<int> RunBothStructuredAsync(CancellationToken cancellationToken)
    {
        var spamResult = await _spamWorkflowStructuredOutput.RunAsync(cancellationToken);
        if (spamResult != 0)
        {
            return spamResult;
        }

        return await RunInvoiceAsync(cancellationToken);
    }

    private async Task<int> RunInvoiceAsync(CancellationToken cancellationToken)
    {
        var pdfDirectory = Configuration["Workflow:InvoicePdfDirectory"] ?? "pdfs";
        var fullPath = Path.IsPathRooted(pdfDirectory)
            ? pdfDirectory
            : Path.Combine(AppContext.BaseDirectory, pdfDirectory);

        if (!Directory.Exists(fullPath))
        {
            _logger.LogError("Invoice PDF directory not found: {Directory}", fullPath);
            return 1;
        }

        var pdfFiles = Directory.GetFiles(fullPath, "*.pdf");
        if (pdfFiles.Length == 0)
        {
            _logger.LogWarning("No PDF files found in {Directory}", fullPath);
            return 0;
        }

        foreach (var pdfFile in pdfFiles)
        {
            await using var stream = File.OpenRead(pdfFile);
            var fileName = Path.GetFileName(pdfFile);
            _logger.LogInformation("Extracting invoice data from {FileName}", fileName);
            var result =await _invoiceExtractionWorkflow.ExtractInvoiceAsync(stream, fileName, cancellationToken);
            _logger.LogInformation("Extraction result for {FileName}: Invoice Number: {InvoiceNumber}, Total Amount: {TotalAmount} {Currency}, Notes: {Notes}",
                fileName,
                result.InvoiceData.InvoiceNumber,
                result.InvoiceData.TotalAmount,
                result.InvoiceData.Currency,
                result.InvoiceData.ExtractionNotes);
        }

        return 0;
    }
}
