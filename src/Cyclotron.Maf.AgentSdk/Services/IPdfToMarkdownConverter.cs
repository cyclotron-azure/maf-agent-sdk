namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Service for converting PDF files to Markdown format.
/// Provides multiple overloads for different input sources (file path, stream, byte array)
/// and optional debug output for troubleshooting document processing.
/// </summary>
public interface IPdfToMarkdownConverter
{
    /// <summary>
    /// Converts a PDF file to markdown text.
    /// </summary>
    /// <param name="pdfFilePath">The full absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the markdown representation of the PDF content.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified PDF file does not exist.</exception>
    Task<string> ConvertToMarkdownAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a PDF stream to markdown text.
    /// </summary>
    /// <param name="pdfStream">A stream containing the PDF content.</param>
    /// <param name="fileName">The original filename (used for logging and document header).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the markdown representation of the PDF content.
    /// </returns>
    Task<string> ConvertToMarkdownAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts PDF to markdown and optionally saves to disk for debugging.
    /// Saving behavior is controlled by <see cref="Options.PdfConversionOptions.SaveMarkdownForDebug"/>.
    /// </summary>
    /// <param name="pdfFilePath">The full absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a tuple with:
    /// <list type="bullet">
    /// <item><description>MarkdownContent: The converted markdown text.</description></item>
    /// <item><description>SavedFilePath: Path to the saved file, or null if saving is disabled.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified PDF file does not exist.</exception>
    Task<(string MarkdownContent, string? SavedFilePath)> ConvertAndSaveAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts PDF from byte array to markdown and optionally saves to disk for debugging.
    /// This overload is useful for processing uploaded files without writing to disk first.
    /// Saving behavior is controlled by <see cref="Options.PdfConversionOptions.SaveMarkdownForDebug"/>.
    /// </summary>
    /// <param name="pdfBytes">The binary content of the PDF file.</param>
    /// <param name="fileName">The original filename (used for logging, document header, and saved file naming).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a tuple with:
    /// <list type="bullet">
    /// <item><description>MarkdownContent: The converted markdown text.</description></item>
    /// <item><description>SavedFilePath: Path to the saved file, or null if saving is disabled.</description></item>
    /// </list>
    /// </returns>
    Task<(string MarkdownContent, string? SavedFilePath)> ConvertFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default);
}
