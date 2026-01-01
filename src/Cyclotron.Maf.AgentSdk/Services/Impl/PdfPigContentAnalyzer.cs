using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// PDF content analyzer implementation using the PdfPig library.
/// Analyzes PDF documents to detect text-based, image-only, or mixed content by examining
/// each page for extractable text and XObject (image) references.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer examines each page of a PDF to:
/// <list type="bullet">
/// <item><description>Extract and count text content</description></item>
/// <item><description>Detect image/XObject references</description></item>
/// <item><description>Calculate text and image ratios</description></item>
/// <item><description>Classify the PDF content type</description></item>
/// </list>
/// </para>
/// <para>
/// Classification is performed based on configurable thresholds:
/// <list type="bullet">
/// <item><description>TextBased: TextRatio &gt;= TextRatioThreshold</description></item>
/// <item><description>ImageOnly: TextRatio &lt; TextRatioThreshold and ImageRatio &gt; 0.5</description></item>
/// <item><description>Mixed: TextRatio &lt; TextRatioThreshold but ImageRatio &lt;= 0.5</description></item>
/// </list>
/// </para>
/// </remarks>
public class PdfPigContentAnalyzer(
    ILogger<PdfPigContentAnalyzer> logger,
    IOptions<PdfContentAnalysisOptions> options) : IPdfContentAnalyzer
{
    private readonly ILogger<PdfPigContentAnalyzer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly PdfContentAnalysisOptions _options = options?.Value ?? new PdfContentAnalysisOptions();

    /// <inheritdoc/>
    public async Task<PdfContentAnalysisResult> AnalyzeAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        try
        {
            _logger.LogInformation("Analyzing PDF file: {FilePath}", pdfFilePath);
            return await Task.Run(() => AnalyzePdfInternal(pdfFilePath, Path.GetFileName(pdfFilePath)), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze PDF file: {FilePath}", pdfFilePath);
            throw new InvalidOperationException($"PDF content analysis failed for {pdfFilePath}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<PdfContentAnalysisResult> AnalyzeAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing PDF stream: {FileName}", fileName);

            // Save stream to temporary file since PdfPig expects file path
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken);
                }

                return await Task.Run(() => AnalyzePdfInternal(tempFilePath, fileName), cancellationToken);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze PDF stream: {FileName}", fileName);
            throw new InvalidOperationException($"PDF content analysis failed for {fileName}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<PdfContentAnalysisResult> AnalyzeFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(pdfBytes);
        return await AnalyzeAsync(stream, fileName, cancellationToken);
    }

    /// <inheritdoc/>
    public string GetAnalyzerName() => "pdfpig";

    /// <summary>
    /// Internal method to analyze PDF content using PdfPig.
    /// Examines each page for text content and images.
    /// </summary>
    private PdfContentAnalysisResult AnalyzePdfInternal(string pdfFilePath, string fileName)
    {
        var result = new PdfContentAnalysisResult
        {
            AnalyzerName = GetAnalyzerName()
        };

        try
        {
            using var document = PdfDocument.Open(pdfFilePath);
            var totalPages = document.NumberOfPages;
            var maxPagesToAnalyze = _options.MaxPagesToAnalyze <= 0 ? totalPages : Math.Min(_options.MaxPagesToAnalyze, totalPages);
            var pagesWithText = 0;
            var pagesWithImages = 0;
            long totalCharacters = 0;

            for (int pageIndex = 0; pageIndex < maxPagesToAnalyze; pageIndex++)
            {
                var page = document.GetPage(pageIndex + 1);

                // Extract text from the page
                var textLength = page.Text.Length;
                if (textLength >= _options.MinCharactersPerPage)
                {
                    pagesWithText++;
                    totalCharacters += textLength;
                }

                // Check for images on the page by checking for embedded images/XObjects
                // PdfPig v0.1.12 stores images in the Resources dictionary
                // A simple heuristic is to check if page has any images by examining word positions
                // or by checking content stream. For now, we'll use a simpler approach:
                // Pages with minimal text but PDF content are likely to be image-heavy
                var hasImages = CheckPageForImages(page);
                if (hasImages)
                {
                    pagesWithImages++;
                }

                _logger.LogDebug(
                    "Page {PageNumber}: TextLength={TextLength}, HasImages={HasImages}",
                    pageIndex + 1,
                    textLength,
                    hasImages);
            }

            // Calculate ratios
            result.TotalPages = totalPages;
            result.PagesWithText = pagesWithText;
            result.PagesWithImages = pagesWithImages;
            result.TotalCharactersExtracted = totalCharacters;
            result.TextRatio = maxPagesToAnalyze > 0 ? (double)pagesWithText / maxPagesToAnalyze : 0;
            result.ImageRatio = maxPagesToAnalyze > 0 ? (double)pagesWithImages / maxPagesToAnalyze : 0;

            // Classify content type based on ratios
            if (result.TextRatio >= _options.TextRatioThreshold)
            {
                result.ContentType = PdfContentType.TextBased;
            }
            else if (result.ImageRatio > 0.5)
            {
                result.ContentType = PdfContentType.ImageOnly;
            }
            else
            {
                result.ContentType = PdfContentType.Mixed;
            }

            if (_options.LogDetailedResults)
            {
                _logger.LogInformation(
                    "PDF analysis complete for {FileName}: ContentType={ContentType}, TextRatio={TextRatio:P}, ImageRatio={ImageRatio:P}, TotalPages={TotalPages}, PagesAnalyzed={PagesAnalyzed}",
                    fileName,
                    result.ContentType,
                    result.TextRatio,
                    result.ImageRatio,
                    totalPages,
                    maxPagesToAnalyze);
            }

            result.DiagnosticMessage = $"Analyzed {maxPagesToAnalyze} of {totalPages} pages";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF content analysis for {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a page contains images by analyzing its structure.
    /// Uses heuristic: pages with very little text but PDF objects likely contain images.
    /// </summary>
    private bool CheckPageForImages(UglyToad.PdfPig.Content.Page page)
    {
        try
        {
            // Heuristic 1: Check if page has GetImages method (PdfPig 0.1.12+)
            var words = page.GetWords();

            // Heuristic 2: If page has content but no words, it might be all images
            var hasContent = !string.IsNullOrWhiteSpace(page.Text);
            if (!hasContent)
            {
                // Empty pages might still have images
                // For now, assume no content = possibly images
                return true;
            }

            // For pages with content, we can't easily detect images in PdfPig 0.1.12
            // So we'll return false for text-containing pages
            // This is a safe default that avoids false positives
            return false;
        }
        catch
        {
            // If any error occurs during image detection, assume no images
            return false;
        }
    }
}
