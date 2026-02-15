using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Images;
using UglyToad.PdfPig.XObjects;

using SystemDrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using ModelImageFormat = Cyclotron.Maf.AgentSdk.Models.ImageFormat;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// PDF image extractor implementation using the PdfPig library.
/// Extracts embedded images and image-based content from PDF documents for vision model processing.
/// </summary>
/// <remarks>
/// <para>
/// This extractor examines each page of a PDF to:
/// <list type="bullet">
/// <item><description>Extract embedded XObject images</description></item>
/// <item><description>Render image-only pages as rasterized images</description></item>
/// <item><description>Filter images by size and quality criteria</description></item>
/// <item><description>Encode extracted images as base64 for vision models</description></item>
/// </list>
/// </para>
/// <para>
/// Images are returned with metadata compatible with Microsoft Agent Framework's multimodal
/// ChatMessage format, ready for direct use with Azure OpenAI GPT-4 Vision and similar models.
/// </para>
/// </remarks>
public class PdfPigImageExtractor(
    ILogger<PdfPigImageExtractor> logger,
    IOptions<PdfImageExtractionOptions> options) : IPdfImageExtractor
{
    private readonly ILogger<PdfPigImageExtractor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly PdfImageExtractionOptions _options = options?.Value ?? new PdfImageExtractionOptions();

    /// <inheritdoc/>
    public async Task<ExtractedPdfImage[]> ExtractImagesAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsExtractionEnabled(Path.GetFileName(pdfFilePath)))
        {
            return Array.Empty<ExtractedPdfImage>();
        }

        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        try
        {
            _logger.LogInformation("Extracting images from PDF file: {FilePath}", pdfFilePath);
            return await Task.Run(
                () => ExtractImagesInternal(pdfFilePath, Path.GetFileName(pdfFilePath)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from PDF file: {FilePath}", pdfFilePath);
            throw new InvalidOperationException($"Image extraction failed for {pdfFilePath}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ExtractedPdfImage[]> ExtractImagesAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!IsExtractionEnabled(fileName))
        {
            return Array.Empty<ExtractedPdfImage>();
        }

        try
        {
            _logger.LogInformation("Extracting images from PDF stream: {FileName}", fileName);

            // Save stream to temporary file since PdfPig requires file path
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken);
                }

                return await Task.Run(
                    () => ExtractImagesInternal(tempFilePath, fileName),
                    cancellationToken);
            }
            finally
            {
                CleanupTempFile(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from PDF stream: {FileName}", fileName);
            throw new InvalidOperationException($"Image extraction failed for {fileName}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ExtractedPdfImage[]> ExtractImagesFromBytesAsync(
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!IsExtractionEnabled(fileName))
        {
            return Array.Empty<ExtractedPdfImage>();
        }

        using var stream = new MemoryStream(pdfBytes);
        return await ExtractImagesAsync(stream, fileName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExtractedPdfImage[]> ExtractImagesAsync(
        string pdfFilePath,
        IEnumerable<int> pageNumbers,
        CancellationToken cancellationToken = default)
    {
        if (!IsExtractionEnabled(Path.GetFileName(pdfFilePath)))
        {
            return Array.Empty<ExtractedPdfImage>();
        }

        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        try
        {
            var pages = pageNumbers.ToHashSet();
            _logger.LogInformation(
                "Extracting images from {PageCount} specific pages in PDF: {FilePath}",
                pages.Count,
                pdfFilePath);

            return await Task.Run(
                () => ExtractImagesInternal(pdfFilePath, Path.GetFileName(pdfFilePath), pages),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from specific pages in PDF: {FilePath}", pdfFilePath);
            throw new InvalidOperationException($"Image extraction failed for {pdfFilePath}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<int> ExtractImagesStreamAsync(
        Stream pdfStream,
        string fileName,
        Func<ExtractedPdfImage, Task<bool>> onImageExtracted,
        CancellationToken cancellationToken = default)
    {
        if (!IsExtractionEnabled(fileName))
        {
            return 0;
        }

        try
        {
            _logger.LogInformation("Starting streaming image extraction from PDF: {FileName}", fileName);

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken);
                }

                return await Task.Run(
                    async () => await ExtractImagesStreamInternal(tempFilePath, fileName, onImageExtracted, cancellationToken),
                    cancellationToken);
            }
            finally
            {
                CleanupTempFile(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during streaming image extraction from PDF: {FileName}", fileName);
            throw new InvalidOperationException($"Streaming image extraction failed for {fileName}", ex);
        }
    }

    /// <inheritdoc/>
    public string GetExtractorName() => "pdfpig";

    /// <summary>
    /// Internal method to extract images from PDF using PdfPig.
    /// </summary>
    private ExtractedPdfImage[] ExtractImagesInternal(string pdfFilePath, string fileName)
    {
        return ExtractImagesInternal(pdfFilePath, fileName, null);
    }

    /// <summary>
    /// Internal method to extract images from specified pages in PDF using PdfPig.
    /// </summary>
    private ExtractedPdfImage[] ExtractImagesInternal(
        string pdfFilePath,
        string fileName,
        HashSet<int>? pageNumbersToProcess)
    {
        var extractedImages = new List<ExtractedPdfImage>();
        LogPreferredFormatFallback();

        try
        {
            using var document = PdfDocument.Open(pdfFilePath);
            var totalPages = document.NumberOfPages;
            var maxPagesToProcess = _options.MaxPagesToProcess <= 0 ? totalPages : Math.Min(_options.MaxPagesToProcess, totalPages);

            for (int pageIndex = 0; pageIndex < maxPagesToProcess; pageIndex++)
            {
                var pageNumber = pageIndex + 1;

                // Skip if specific pages were requested and this isn't one of them
                if (pageNumbersToProcess != null && !pageNumbersToProcess.Contains(pageNumber))
                {
                    continue;
                }

                try
                {
                    var page = document.GetPage(pageNumber);
                    var pageImages = ExtractImagesFromPage(page, pageNumber, fileName);
                    extractedImages.AddRange(pageImages);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract images from page {PageNumber} in PDF: {FileName}", pageNumber, fileName);
                    // Continue processing other pages
                }
            }

            if (_options.LogDetailedResults)
            {
                _logger.LogInformation(
                    "Image extraction complete for {FileName}: {ImageCount} images extracted from {PagesProcessed} of {TotalPages} pages",
                    fileName,
                    extractedImages.Count,
                    Math.Min(maxPagesToProcess, totalPages),
                    totalPages);
            }

            return extractedImages.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF image extraction for {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Internal method for streaming image extraction with callback.
    /// </summary>
    private async Task<int> ExtractImagesStreamInternal(
        string pdfFilePath,
        string fileName,
        Func<ExtractedPdfImage, Task<bool>> onImageExtracted,
        CancellationToken cancellationToken)
    {
        var totalImagesProcessed = 0;
        LogPreferredFormatFallback();

        try
        {
            using var document = PdfDocument.Open(pdfFilePath);
            var totalPages = document.NumberOfPages;
            var maxPagesToProcess = _options.MaxPagesToProcess <= 0 ? totalPages : Math.Min(_options.MaxPagesToProcess, totalPages);

            for (int pageIndex = 0; pageIndex < maxPagesToProcess; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageNumber = pageIndex + 1;

                try
                {
                    var page = document.GetPage(pageNumber);
                    var pageImages = ExtractImagesFromPage(page, pageNumber, fileName);

                    foreach (var image in pageImages)
                    {
                        var shouldContinue = await onImageExtracted(image);
                        if (!shouldContinue)
                        {
                            _logger.LogInformation("Streaming extraction stopped by callback at {ImageCount} images", totalImagesProcessed);
                            return totalImagesProcessed;
                        }
                        totalImagesProcessed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract images from page {PageNumber} during streaming", pageNumber);
                    // Continue processing other pages
                }
            }

            _logger.LogInformation("Streaming image extraction complete: {ImageCount} images processed", totalImagesProcessed);
            return totalImagesProcessed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming PDF image extraction");
            throw;
        }
    }

    /// <summary>
    /// Extracts images from a single PDF page.
    /// </summary>
    private List<ExtractedPdfImage> ExtractImagesFromPage(Page page, int pageNumber, string fileName)
    {
        var pageImages = new List<ExtractedPdfImage>();

        try
        {
            _logger.LogDebug("Processing page {PageNumber} for image extraction from {FileName}", pageNumber, fileName);

            var images = page.GetImages().ToList();

            _logger.LogDebug("Found {ImageCount} images on page {PageNumber}", images.Count, pageNumber);

            if (_options.SkipTextOnlyPages && images.Count == 0 && !string.IsNullOrWhiteSpace(page.Text))
            {
                _logger.LogDebug("Skipping text-only page {PageNumber} during image extraction", pageNumber);
                return pageImages;
            }

            for (var imageIndex = 0; imageIndex < images.Count; imageIndex++)
            {
                var image = images[imageIndex];

                _logger.LogDebug(
                    "Image {ImageIndex} details: {Width}x{Height}, BitsPerComponent={BitsPerComponent}, Inline={IsInline}, ImageMask={IsImageMask}",
                    imageIndex + 1,
                    image.WidthInSamples,
                    image.HeightInSamples,
                    image.BitsPerComponent,
                    image.IsInlineImage,
                    image.IsImageMask);

                if (image is XObjectImage xObjectImage)
                {
                    _logger.LogDebug("Image {ImageIndex} XObject: IsJpxEncoded={IsJpxEncoded}", imageIndex + 1, xObjectImage.IsJpxEncoded);
                }

                if (image.WidthInSamples < _options.MinImageWidth || image.HeightInSamples < _options.MinImageHeight)
                {
                    _logger.LogDebug(
                        "Skipping image on page {PageNumber} due to size {Width}x{Height}",
                        pageNumber,
                        image.WidthInSamples,
                        image.HeightInSamples);
                    continue;
                }

                if (!TryGetImageBytes(image, out var imageBytes, out var format, out var mimeType))
                {
                    _logger.LogDebug("Skipping image on page {PageNumber} due to extraction failure", pageNumber);
                    continue;
                }

                if (_options.MaxImageSizeBytes > 0 && imageBytes.Length > _options.MaxImageSizeBytes)
                {
                    _logger.LogDebug(
                        "Skipping image on page {PageNumber} due to size {Size} bytes",
                        pageNumber,
                        imageBytes.Length);
                    continue;
                }

                var extractedImage = new ExtractedPdfImage
                {
                    ImageBytes = imageBytes,
                    ImageBase64 = _options.EncodeAsBase64 ? Convert.ToBase64String(imageBytes) : string.Empty,
                    MimeType = mimeType,
                    Format = format,
                    PageNumber = pageNumber,
                    ImageIndexOnPage = imageIndex + 1,
                    ImageName = BuildImageName(fileName, pageNumber, imageIndex + 1, format),
                    Dimensions = (image.WidthInSamples, image.HeightInSamples)
                };

                pageImages.Add(extractedImage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting images from page {PageNumber}", pageNumber);
        }

        return pageImages;
    }

    /// <summary>
    /// Cleans up temporary files with best-effort error handling.
    /// </summary>
    private void CleanupTempFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Best effort cleanup - don't fail if temp file deletion fails
            }
        }
    }

    private bool IsExtractionEnabled(string? fileName)
    {
        if (_options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogInformation("PDF image extraction disabled.");
        }
        else
        {
            _logger.LogInformation("PDF image extraction disabled. Skipping: {FileName}", fileName);
        }

        return false;
    }

    private void LogPreferredFormatFallback()
    {
        if (!string.Equals(_options.PreferredFormat, "png", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "PreferredFormat '{PreferredFormat}' is not supported by PdfPig extraction. Falling back to PNG.",
                _options.PreferredFormat);
        }
    }

    private static string BuildImageName(string fileName, int pageNumber, int imageIndex, ModelImageFormat format)
    {
        var extension = format switch
        {
            ModelImageFormat.Jpeg => ".jpg",
            ModelImageFormat.Png => ".png",
            ModelImageFormat.WebP => ".webp",
            ModelImageFormat.Gif => ".gif",
            _ => ".img"
        };

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return $"{baseName}_page_{pageNumber}_image_{imageIndex}{extension}";
    }

    private bool TryGetImageBytes(IPdfImage image, out byte[] imageBytes, out ModelImageFormat format, out string mimeType)
    {
        if (image.TryGetPng(out var pngBytes) && pngBytes.Length > 0)
        {
            imageBytes = pngBytes;
            format = ModelImageFormat.Png;
            mimeType = "image/png";
            return true;
        }

        if (!IsSystemDrawingSupported())
        {
            _logger.LogWarning(
                "System.Drawing is not supported on this platform. Set DOTNET_SYSTEM_DRAWING_ENABLE_UNIX_SUPPORT=1 and ensure libgdiplus is installed.");
            return TryGetRawJpeg(image, out imageBytes, out format, out mimeType);
        }

        if (!image.TryGetBytesAsMemory(out var rawMemory))
        {
            return TryGetRawJpeg(image, out imageBytes, out format, out mimeType);
        }

        if (image.ColorSpaceDetails == null)
        {
            return TryGetRawJpeg(image, out imageBytes, out format, out mimeType);
        }

        var rawBytes = rawMemory.ToArray();
        var pixelBytes = ColorSpaceDetailsByteConverter.Convert(
            image.ColorSpaceDetails,
            rawBytes,
            image.BitsPerComponent,
            image.WidthInSamples,
            image.HeightInSamples);

        if (pixelBytes.Length == 0)
        {
            return TryGetRawJpeg(image, out imageBytes, out format, out mimeType);
        }

        if (!TryEncodePng(pixelBytes, image.WidthInSamples, image.HeightInSamples, out var encodedPng))
        {
            return TryGetRawJpeg(image, out imageBytes, out format, out mimeType);
        }

        imageBytes = encodedPng;
        format = ModelImageFormat.Png;
        mimeType = "image/png";
        return true;
    }

    private static bool TryGetRawJpeg(IPdfImage image, out byte[] imageBytes, out ModelImageFormat format, out string mimeType)
    {
        var rawBytes = image.RawBytes;

        if (LooksLikeJpeg(rawBytes))
        {
            imageBytes = rawBytes.ToArray();
            format = ModelImageFormat.Jpeg;
            mimeType = "image/jpeg";
            return true;
        }

        imageBytes = Array.Empty<byte>();
        format = ModelImageFormat.Png;
        mimeType = "image/png";
        return false;
    }

    private static bool LooksLikeJpeg(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }

    private static bool IsSystemDrawingSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        var enableUnixSupport = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_DRAWING_ENABLE_UNIX_SUPPORT");
        return string.Equals(enableUnixSupport, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enableUnixSupport, "true", StringComparison.OrdinalIgnoreCase);
    }

    #pragma warning disable CA1416 // System.Drawing is used when explicitly enabled for Unix.
    private static bool TryEncodePng(ReadOnlySpan<byte> rgbBytes, int width, int height, out byte[] pngBytes)
    {
        pngBytes = Array.Empty<byte>();

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        const int bytesPerPixel = 3;
        var expectedLength = width * height * bytesPerPixel;
        if (rgbBytes.Length < expectedLength)
        {
            return false;
        }

        var rgbArray = rgbBytes.ToArray();

        try
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

            try
            {
                var stride = data.Stride;
                for (var y = 0; y < height; y++)
                {
                    var sourceIndex = y * width * bytesPerPixel;
                    var destination = data.Scan0 + (y * stride);
                    Marshal.Copy(rgbArray, sourceIndex, destination, width * bytesPerPixel);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            using var output = new MemoryStream();
            bitmap.Save(output, SystemDrawingImageFormat.Png);
            pngBytes = output.ToArray();
            return pngBytes.Length > 0;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
    #pragma warning restore CA1416
}
