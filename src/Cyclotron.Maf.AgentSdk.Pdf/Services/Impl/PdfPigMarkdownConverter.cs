using System.Text;
using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Converts PDF files to Markdown format using the PdfPig library.
/// Supports layout analysis for paragraph detection and reading order.
/// Includes configurable content analysis to detect and handle image-only PDFs.
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
/// <para>
/// Before conversion, the PDF content can be analyzed to detect image-only content.
/// Based on configuration, image-only PDFs can be skipped, cause an error, or fallback
/// to conversion anyway.
/// </para>
/// </remarks>
public class PdfPigMarkdownConverter(
    ILogger<PdfPigMarkdownConverter> logger,
    IOptions<PdfConversionOptions> conversionOptions,
    IOptions<PdfContentAnalysisOptions> analysisOptions,
    [FromKeyedServices("pdfpig")] IPdfContentAnalyzer contentAnalyzer) : IPdfToMarkdownConverter
{
    private readonly ILogger<PdfPigMarkdownConverter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly PdfConversionOptions _conversionOptions = conversionOptions.Value;
    private readonly PdfContentAnalysisOptions _analysisOptions = analysisOptions.Value;
    private readonly IPdfContentAnalyzer _contentAnalyzer = contentAnalyzer ?? throw new ArgumentNullException(nameof(contentAnalyzer));

    /// <inheritdoc />
    public async Task<string> ConvertToMarkdownAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        _logger.LogInformation("Converting PDF to markdown: {FilePath}", pdfFilePath);

        // Analyze PDF content if enabled
        if (_analysisOptions.Enabled)
        {
            var analysisResult = await AnalyzeAndHandleResultAsync(pdfFilePath, Path.GetFileName(pdfFilePath), cancellationToken);
            if (analysisResult == null)
            {
                return string.Empty;
            }
        }

        // PdfPig operations are synchronous, wrap in Task.Run for async pattern
        return await Task.Run(() => ConvertToMarkdownInternal(pdfFilePath), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ConvertToMarkdownAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Converting PDF stream to markdown: {FileName}", fileName);

        // Analyze PDF content if enabled
        if (_analysisOptions.Enabled)
        {
            var analysisResult = await AnalyzeAndHandleResultAsync(pdfStream, fileName, cancellationToken);
            if (analysisResult == null)
            {
                return string.Empty;
            }

            // Reset stream position after analysis
            if (pdfStream.CanSeek)
            {
                pdfStream.Seek(0, SeekOrigin.Begin);
            }
        }

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

        if (_conversionOptions.SaveMarkdownForDebug && !string.IsNullOrEmpty(markdownContent))
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

        if (_conversionOptions.SaveMarkdownForDebug && !string.IsNullOrEmpty(markdownContent))
        {
            savedFilePath = await SaveMarkdownToFileAsync(
                markdownContent,
                Path.GetFileNameWithoutExtension(fileName),
                cancellationToken);
        }

        return (markdownContent, savedFilePath);
    }

    /// <summary>
    /// Analyzes PDF content and handles the result based on configuration.
    /// Returns null if content analysis indicates image-only PDF and failure strategy is Skip.
    /// Throws exception if failure strategy is Throw.
    /// Returns the analysis result if proceeding with conversion.
    /// </summary>
    private async Task<PdfContentAnalysisResult?> AnalyzeAndHandleResultAsync(
        string pdfFilePath,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysisResult = await _contentAnalyzer.AnalyzeAsync(pdfFilePath, cancellationToken);

            if (analysisResult.ContentType == PdfContentType.ImageOnly)
            {
                return HandleImageOnlyPdf(fileName, analysisResult);
            }

            return analysisResult;
        }
        catch (Exception ex)
        {
            return HandleAnalysisFailure(fileName, ex);
        }
    }

    /// <summary>
    /// Analyzes PDF content from stream and handles the result based on configuration.
    /// Returns null if content analysis indicates image-only PDF and failure strategy is Skip.
    /// Throws exception if failure strategy is Throw.
    /// Returns the analysis result if proceeding with conversion.
    /// </summary>
    private async Task<PdfContentAnalysisResult?> AnalyzeAndHandleResultAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysisResult = await _contentAnalyzer.AnalyzeAsync(pdfStream, fileName, cancellationToken);

            if (analysisResult.ContentType == PdfContentType.ImageOnly)
            {
                return HandleImageOnlyPdf(fileName, analysisResult);
            }

            return analysisResult;
        }
        catch (Exception ex)
        {
            return HandleAnalysisFailure(fileName, ex);
        }
    }

    /// <summary>
    /// Handles image-only PDF detection based on configured failure strategy.
    /// </summary>
    private PdfContentAnalysisResult? HandleImageOnlyPdf(string fileName, PdfContentAnalysisResult analysisResult)
    {
        switch (_analysisOptions.FailureStrategy)
        {
            case PdfAnalysisFailureStrategy.Skip:
                _logger.LogInformation(
                    "Skipping image-only PDF: {FileName} (TextRatio: {TextRatio:P})",
                    fileName,
                    analysisResult.TextRatio);
                return null;

            case PdfAnalysisFailureStrategy.Throw:
                var errorMsg = $"PDF is image-only and cannot be processed: {fileName} (TextRatio: {analysisResult.TextRatio:P})";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);

            case PdfAnalysisFailureStrategy.Fallback:
                _logger.LogWarning(
                    "Image-only PDF will be processed with fallback conversion: {FileName} (TextRatio: {TextRatio:P})",
                    fileName,
                    analysisResult.TextRatio);
                return analysisResult;

            default:
                _logger.LogWarning("Unknown failure strategy: {Strategy}, using Fallback", _analysisOptions.FailureStrategy);
                return analysisResult;
        }
    }

    /// <summary>
    /// Handles analysis failures based on configured failure strategy.
    /// </summary>
    private PdfContentAnalysisResult? HandleAnalysisFailure(string fileName, Exception analysisException)
    {
        _logger.LogWarning(analysisException, "PDF content analysis failed for: {FileName}", fileName);

        switch (_analysisOptions.FailureStrategy)
        {
            case PdfAnalysisFailureStrategy.Skip:
                _logger.LogInformation("Skipping PDF due to analysis failure: {FileName}", fileName);
                return null;

            case PdfAnalysisFailureStrategy.Throw:
                _logger.LogError("Throwing exception due to analysis failure for: {FileName}", fileName);
                throw new InvalidOperationException($"PDF content analysis failed for {fileName}", analysisException);

            case PdfAnalysisFailureStrategy.Fallback:
                _logger.LogInformation("Proceeding with conversion despite analysis failure: {FileName}", fileName);
                return new PdfContentAnalysisResult { ContentType = PdfContentType.Mixed, AnalyzerName = _contentAnalyzer.GetAnalyzerName() };

            default:
                _logger.LogWarning("Unknown failure strategy: {Strategy}, using Fallback", _analysisOptions.FailureStrategy);
                return new PdfContentAnalysisResult { ContentType = PdfContentType.Mixed, AnalyzerName = _contentAnalyzer.GetAnalyzerName() };
        }
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
            _logger.LogInformation("Created output directory: {OutputDir}", outputDir);
        }

        // Build filename
        var fileName = BuildOutputFileName(baseFileName);
        var filePath = Path.Combine(outputDir, fileName);

        await File.WriteAllTextAsync(filePath, markdownContent, cancellationToken);

        _logger.LogInformation("Saved markdown file for debugging: {FilePath}", filePath);

        return filePath;
    }

    /// <summary>
    /// Builds the output filename based on configuration options.
    /// </summary>
    private string BuildOutputFileName(string baseFileName)
    {
        var sb = new StringBuilder();
        sb.Append(baseFileName);

        if (_conversionOptions.IncludeTimestampInFilename)
        {
            sb.Append('_');
            sb.Append(DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        }

        sb.Append(_conversionOptions.MarkdownFileExtension);

        return sb.ToString();
    }

    /// <summary>
    /// Resolves the output directory path.
    /// </summary>
    private string ResolveOutputDirectory()
    {
        var outputPath = _conversionOptions.OutputDirectory;

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
        _logger.LogDebug("Processing PDF with {PageCount} pages", pageCount);

        foreach (var page in document.GetPages())
        {
            if (_conversionOptions.IncludePageNumbers)
            {
                sb.AppendLine($"## Page {page.Number}");
                sb.AppendLine();
            }

            if (_conversionOptions.PreserveParagraphStructure)
            {
                ExtractWithLayoutAnalysis(page, sb);
            }
            else
            {
                ExtractSimpleText(page, sb);
            }
        }

        var result = sb.ToString();
        _logger.LogDebug("Extracted {CharCount} characters from PDF", result.Length);

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
                _logger.LogDebug("No words found on page {PageNumber}", page.Number);
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
            _logger.LogWarning(
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
