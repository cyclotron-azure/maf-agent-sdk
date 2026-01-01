using Cyclotron.Maf.AgentSdk.Models;

namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Service for analyzing PDF content to determine whether the PDF contains text-based or image-only content.
/// This analysis helps determine if additional processing (e.g., OCR) is needed before document indexing.
/// </summary>
/// <remarks>
/// <para>
/// Implementations analyze PDF structure and content to classify documents as:
/// <list type="bullet">
/// <item><description>TextBased: PDF with sufficient extractable text content</description></item>
/// <item><description>ImageOnly: Scanned PDF or image-heavy document with minimal text</description></item>
/// <item><description>Mixed: PDF with both text and image components</description></item>
/// </list>
/// </para>
/// <para>
/// This interface allows for multiple pluggable implementations (e.g., PdfPig-based, Vision API-based, custom)
/// selected via configuration without code changes.
/// </para>
/// </remarks>
public interface IPdfContentAnalyzer
{
    /// <summary>
    /// Analyzes a PDF file to determine its content type and characteristics.
    /// </summary>
    /// <param name="pdfFilePath">The full absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the analysis result with content type and metadata.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified PDF file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when PDF analysis fails.</exception>
    Task<PdfContentAnalysisResult> AnalyzeAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a PDF stream to determine its content type and characteristics.
    /// </summary>
    /// <param name="pdfStream">A stream containing the PDF content.</param>
    /// <param name="fileName">The original filename (used for logging and diagnostics).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the analysis result with content type and metadata.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when PDF analysis fails.</exception>
    Task<PdfContentAnalysisResult> AnalyzeAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes PDF from byte array to determine its content type and characteristics.
    /// </summary>
    /// <param name="pdfBytes">The binary content of the PDF file.</param>
    /// <param name="fileName">The original filename (used for logging and diagnostics).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the analysis result with content type and metadata.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when PDF analysis fails.</exception>
    Task<PdfContentAnalysisResult> AnalyzeFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name/identifier of this analyzer implementation.
    /// </summary>
    /// <returns>A string identifying the analyzer (e.g., "pdfpig", "vision", "custom").</returns>
    string GetAnalyzerName();
}
