using System.Text;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Converts PDF files to Markdown format using the PdfPig library.
/// Supports layout analysis for paragraph detection and reading order.
/// </summary>
/// <remarks>
/// <para>
/// This converter uses PdfPig's DocstrumBoundingBoxes for text block detection
/// and UnsupervisedReadingOrderDetector for proper text ordering.
/// </para>
/// <para>
/// When layout analysis fails on a page, the converter falls back to simple
/// text extraction without structure preservation.
/// </para>
/// </remarks>
public class PdfPigMarkdownConverter(
    ILogger<PdfPigMarkdownConverter> logger,
    IOptions<PdfConversionOptions> options) : IPdfToMarkdownConverter
{
    private readonly PdfConversionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<string> ConvertToMarkdownAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        logger.LogInformation("Converting PDF to markdown: {FilePath}", pdfFilePath);

        // PdfPig operations are synchronous, wrap in Task.Run for async pattern
        return await Task.Run(() => ConvertToMarkdownInternal(pdfFilePath), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ConvertToMarkdownAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Converting PDF stream to markdown: {FileName}", fileName);

        // PdfPig can read from streams directly
        return await Task.Run(() => ConvertStreamToMarkdownInternal(pdfStream, fileName), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(string MarkdownContent, string? SavedFilePath)> ConvertAndSaveAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        var markdownContent = await ConvertToMarkdownAsync(pdfFilePath, cancellationToken);

        string? savedFilePath = null;

        if (_options.SaveMarkdownForDebug)
        {
            savedFilePath = await SaveMarkdownToFileAsync(
                markdownContent,
                Path.GetFileNameWithoutExtension(pdfFilePath),
                cancellationToken);
        }

        return (markdownContent, savedFilePath);
    }

    /// <inheritdoc />
    public async Task<(string MarkdownContent, string? SavedFilePath)> ConvertFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        var markdownContent = await ConvertToMarkdownAsync(pdfStream, fileName, cancellationToken);

        string? savedFilePath = null;

        if (_options.SaveMarkdownForDebug)
        {
            savedFilePath = await SaveMarkdownToFileAsync(
                markdownContent,
                Path.GetFileNameWithoutExtension(fileName),
                cancellationToken);
        }

        return (markdownContent, savedFilePath);
    }

    /// <summary>
    /// Saves markdown content to a file for debugging purposes.
    /// </summary>
    private async Task<string> SaveMarkdownToFileAsync(
        string markdownContent,
        string baseFileName,
        CancellationToken cancellationToken)
    {
        var outputDir = ResolveOutputDirectory();

        // Ensure directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            logger.LogInformation("Created output directory: {OutputDir}", outputDir);
        }

        // Build filename
        var fileName = BuildOutputFileName(baseFileName);
        var filePath = Path.Combine(outputDir, fileName);

        await File.WriteAllTextAsync(filePath, markdownContent, cancellationToken);

        logger.LogInformation("Saved markdown file for debugging: {FilePath}", filePath);

        return filePath;
    }

    /// <summary>
    /// Builds the output filename based on configuration options.
    /// </summary>
    private string BuildOutputFileName(string baseFileName)
    {
        var sb = new StringBuilder();
        sb.Append(baseFileName);

        if (_options.IncludeTimestampInFilename)
        {
            sb.Append('_');
            sb.Append(DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        }

        sb.Append(_options.MarkdownFileExtension);

        return sb.ToString();
    }

    /// <summary>
    /// Resolves the output directory path.
    /// </summary>
    private string ResolveOutputDirectory()
    {
        var outputPath = _options.OutputDirectory;

        if (Path.IsPathRooted(outputPath))
        {
            return outputPath;
        }

        // Resolve relative path from current directory
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
    }

    /// <summary>
    /// Internal method to convert PDF file to markdown.
    /// </summary>
    private string ConvertToMarkdownInternal(string pdfFilePath)
    {
        using var document = PdfDocument.Open(pdfFilePath);
        return ExtractMarkdownFromDocument(document, Path.GetFileName(pdfFilePath));
    }

    /// <summary>
    /// Internal method to convert PDF stream to markdown.
    /// </summary>
    private string ConvertStreamToMarkdownInternal(Stream pdfStream, string fileName)
    {
        using var document = PdfDocument.Open(pdfStream);
        return ExtractMarkdownFromDocument(document, fileName);
    }

    /// <summary>
    /// Extracts markdown content from a PdfPig document.
    /// </summary>
    private string ExtractMarkdownFromDocument(PdfDocument document, string fileName)
    {
        var sb = new StringBuilder();

        // Add document header
        sb.AppendLine($"# {Path.GetFileNameWithoutExtension(fileName)}");
        sb.AppendLine();

        var pageCount = document.NumberOfPages;
        logger.LogDebug("Processing PDF with {PageCount} pages", pageCount);

        foreach (var page in document.GetPages())
        {
            if (_options.IncludePageNumbers)
            {
                sb.AppendLine($"## Page {page.Number}");
                sb.AppendLine();
            }

            if (_options.PreserveParagraphStructure)
            {
                ExtractWithLayoutAnalysis(page, sb);
            }
            else
            {
                ExtractSimpleText(page, sb);
            }
        }

        var result = sb.ToString();
        logger.LogDebug("Extracted {CharCount} characters from PDF", result.Length);

        return result;
    }

    /// <summary>
    /// Extracts text with layout analysis for better paragraph structure.
    /// </summary>
    private void ExtractWithLayoutAnalysis(UglyToad.PdfPig.Content.Page page, StringBuilder sb)
    {
        try
        {
            var words = page.GetWords().ToList();

            if (words.Count == 0)
            {
                logger.LogDebug("No words found on page {PageNumber}", page.Number);
                return;
            }

            // Use DocstrumBoundingBoxes for text block detection
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

            // Order blocks by reading order
            var orderedBlocks = UnsupervisedReadingOrderDetector.Instance.Get(blocks);

            foreach (var block in orderedBlocks)
            {
                var text = block.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Each text block becomes a paragraph
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Layout analysis failed for page {PageNumber}, falling back to simple extraction",
                page.Number);

            // Fallback to simple extraction
            ExtractSimpleText(page, sb);
        }
    }

    /// <summary>
    /// Simple text extraction without layout analysis.
    /// </summary>
    private void ExtractSimpleText(UglyToad.PdfPig.Content.Page page, StringBuilder sb)
    {
        var text = page.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.AppendLine(text);
            sb.AppendLine();
        }
    }
}
