using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
/// </remarks>
public class PdfPigContentAnalyzer(
    ILogger<PdfPigContentAnalyzer> logger,
    IOptions<PdfContentAnalysisOptions> options,
    IPdfContentClassifier contentClassifier) : IPdfContentAnalyzer
{
    private readonly ILogger<PdfPigContentAnalyzer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly PdfContentAnalysisOptions _options = options?.Value ?? new PdfContentAnalysisOptions();
    private readonly IPdfContentClassifier _contentClassifier = contentClassifier ?? throw new ArgumentNullException(nameof(contentClassifier));

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
            var safeFileName = Path.GetFileName(fileName);
            var tempFileName = $"{Guid.NewGuid()}-{safeFileName}";
            var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
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
            var pagesWithFullPageImages = 0;
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
                var hasImages = PageHasImages(page);
                if (hasImages)
                {
                    pagesWithImages++;
                }

                var hasFullPageImage = HasFullPageImage(page);
                if (hasFullPageImage)
                {
                    _logger.LogInformation("Page {PageNumber} contains a full-page image.", pageIndex + 1);
                    pagesWithFullPageImages++;
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
            result.PagesWithFullPageImages = pagesWithFullPageImages;
            result.TotalCharactersExtracted = totalCharacters;
            result.TextRatio = maxPagesToAnalyze > 0 ? (double)pagesWithText / maxPagesToAnalyze : 0;
            result.ImageRatio = maxPagesToAnalyze > 0 ? (double)pagesWithImages / maxPagesToAnalyze : 0;
            result.ContentType = _contentClassifier.ClassifyContent(result);

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
    private bool PageHasImages(Page page)
    {
        try
        {
            return page.NumberOfImages > 0;
        }
        catch
        {
            // If any error occurs during image detection, assume no images
            return false;
        }
    }

    /// <summary>
    /// Determines if a page contains a full-page image by checking the size
    /// of images relative to the page dimensions.
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    private bool HasFullPageImage(Page page)
    {
        try
        {
            return page.GetImages()
                .Any(img =>
                {
                    var b = img.Bounds;

                    double widthCoverage = b.Width / page.Width;
                    double heightCoverage = b.Height / page.Height;
                    double areaCoverage = b.Width * b.Height / (page.Width * page.Height);

                    return
                        areaCoverage >= _options.FullPageImageAreaCoverageThreshold || // dominant image
                        (widthCoverage >= _options.FullPageImagePrimaryDimensionThreshold && heightCoverage >= _options.FullPageImageSecondaryDimensionThreshold) ||
                        (heightCoverage >= _options.FullPageImagePrimaryDimensionThreshold && widthCoverage >= _options.FullPageImageSecondaryDimensionThreshold);
                });

        }
        catch (OutOfMemoryException)
        {
            // If any error occurs during image detection, assume no full-page image
            // Do not swallow critical system exceptions
            throw;
        }
        catch (Exception ex) when (ex is not StackOverflowException
                                    and not ThreadAbortException
                                    and not AccessViolationException)
        {
            // If any non-critical error occurs during image detection, assume no full-page image
            _logger.LogWarning(ex, "Failed to detect full-page images on page {PageNumber}. Treating as no full-page image.", page.Number);
        }

        return false; // No full-page image detected
    }
}
