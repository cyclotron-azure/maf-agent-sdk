using Cyclotron.Maf.AgentSdk.Models;

namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Service for extracting images from PDF documents.
/// Extracted images are formatted for direct use with LLM vision models through Microsoft Agent Framework.
/// </summary>
/// <remarks>
/// <para>
/// This service extracts embedded images and image-based page content from PDF documents.
/// Extracted images include both binary data and base64-encoded representations for flexibility
/// in vision model integration:
/// <list type="bullet">
/// <item><description>Binary data for DataContent in ChatMessage</description></item>
/// <item><description>Base64 for data URIs or JSON serialization</description></item>
/// <item><description>MIME type information for API headers</description></item>
/// </list>
/// </para>
/// <para>
/// Implementations handle multiple input formats (file path, stream, bytes) and can be
/// selected via configuration without code changes.
/// </para>
/// </remarks>
public interface IPdfImageExtractor
{
    /// <summary>
    /// Extracts all images from a PDF file.
    /// </summary>
    /// <param name="pdfFilePath">The full absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of extracted images ready for vision model processing.
    /// Returns empty array if no images are found or extraction is disabled.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified PDF file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when image extraction fails.</exception>
    Task<ExtractedPdfImage[]> ExtractImagesAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts all images from a PDF stream.
    /// </summary>
    /// <param name="pdfStream">A stream containing the PDF content.</param>
    /// <param name="fileName">The original filename (used for logging and diagnostics).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of extracted images ready for vision model processing.
    /// Returns empty array if no images are found or extraction is disabled.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when image extraction fails.</exception>
    Task<ExtractedPdfImage[]> ExtractImagesAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts images from PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">The binary content of the PDF file.</param>
    /// <param name="fileName">The original filename (used for logging and diagnostics).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of extracted images ready for vision model processing.
    /// Returns empty array if no images are found or extraction is disabled.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when image extraction fails.</exception>
    Task<ExtractedPdfImage[]> ExtractImagesFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts images from specific pages of a PDF file.
    /// </summary>
    /// <param name="pdfFilePath">The full absolute path to the PDF file.</param>
    /// <param name="pageNumbers">Collection of 1-based page numbers to extract images from.
    /// If empty, all pages are processed.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of extracted images from specified pages.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified PDF file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when image extraction fails.</exception>
    Task<ExtractedPdfImage[]> ExtractImagesAsync(
        string pdfFilePath,
        IEnumerable<int> pageNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts images from a PDF stream with a callback for streaming processing.
    /// Useful for large PDFs to process images as they're extracted without loading all into memory.
    /// </summary>
    /// <param name="pdfStream">A stream containing the PDF content.</param>
    /// <param name="fileName">The original filename (used for logging and diagnostics).</param>
    /// <param name="onImageExtracted">Callback invoked each time an image is successfully extracted.
    /// Return true to continue extraction, false to stop.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// Returns the total count of images processed.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when image extraction fails.</exception>
    Task<int> ExtractImagesStreamAsync(
        Stream pdfStream,
        string fileName,
        Func<ExtractedPdfImage, Task<bool>> onImageExtracted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name/identifier of this extractor implementation.
    /// </summary>
    /// <returns>A string identifying the extractor (e.g., "pdfpig", "vision", "custom").</returns>
    string GetExtractorName();
}
