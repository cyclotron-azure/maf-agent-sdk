using SpamDetection.Models;

namespace SpamDetection.Services;

/// <summary>
/// Defines the invoice extraction workflow interface.
/// Orchestrates PDF analysis, routing to appropriate executors, and invoice data extraction.
/// </summary>
public interface IInvoiceExtractionWorkflow
{
    /// <summary>
    /// Executes the invoice extraction workflow on the provided PDF document.
    /// Routes to appropriate executor based on PDF content type (TextBased, ImageOnly, or Mixed).
    /// </summary>
    /// <param name="pdfContent">The PDF file content as a stream.</param>
    /// <param name="fileName">The name of the PDF file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the extraction result with invoice data.</returns>
    Task<InvoiceExtractionResult> ExtractInvoiceAsync(
        Stream pdfContent,
        string fileName,
        CancellationToken cancellationToken);
}
