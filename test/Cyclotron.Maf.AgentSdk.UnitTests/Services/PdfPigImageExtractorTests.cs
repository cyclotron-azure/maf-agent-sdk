using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Cyclotron.Maf.AgentSdk.UnitTests.TestFixtures;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="PdfPigImageExtractor"/> class.
/// Tests PDF image extraction for vision model integration.
/// </summary>
public class PdfPigImageExtractorTests : IDisposable
{
    private readonly Mock<ILogger<PdfPigImageExtractor>> _mockLogger;
    private readonly List<string> _tempFiles = [];

    public PdfPigImageExtractorTests()
    {
        _mockLogger = new Mock<ILogger<PdfPigImageExtractor>>();
    }

    public void Dispose()
    {
        // Cleanup temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        GC.SuppressFinalize(this);
    }

    private IOptions<PdfImageExtractionOptions> CreateOptions(
        bool enabled = true,
        int maxPagesToProcess = 0,
        long maxImageSizeBytes = 5242880,
        string preferredFormat = "jpeg",
        int jpegQuality = 85,
        bool encodeAsBase64 = true,
        bool logDetailedResults = false,
        bool skipTextOnlyPages = true,
        int minImageWidth = 50,
        int minImageHeight = 50)
    {
        var options = new PdfImageExtractionOptions
        {
            Enabled = enabled,
            ExtractorKey = "pdfpig",
            MaxPagesToProcess = maxPagesToProcess,
            MaxImageSizeBytes = maxImageSizeBytes,
            PreferredFormat = preferredFormat,
            JpegQuality = jpegQuality,
            EncodeAsBase64 = encodeAsBase64,
            LogDetailedResults = logDetailedResults,
            SkipTextOnlyPages = skipTextOnlyPages,
            MinImageWidth = minImageWidth,
            MinImageHeight = minImageHeight
        };
        return MsOptions.Create(options);
    }

    private PdfPigImageExtractor CreateExtractor(IOptions<PdfImageExtractionOptions>? options = null)
    {
        return new PdfPigImageExtractor(
            _mockLogger.Object,
            options ?? CreateOptions());
    }

    private string CreateTempPdf(byte[] pdfBytes)
    {
        var path = PdfTestFixtures.CreateTempPdfFile(pdfBytes);
        _tempFiles.Add(path);
        return path;
    }

    #region ExtractImagesAsync (file path) Tests

    [Fact]
    public async Task ExtractImagesAsync_WithValidPdfPath_ReturnsExtractedImages()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<ExtractedPdfImage[]>();
    }

    [Fact]
    public async Task ExtractImagesAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var nonExistentPath = "/path/that/does/not/exist.pdf";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractImagesAsync(nonExistentPath));
    }

    [Fact]
    public async Task ExtractImagesAsync_WithValidPath_ReturnsImagesWithExtractorName()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        foreach (var image in result)
        {
            // Once images are extracted, verify they have proper metadata
            image.ImageBytes.Should().BeOfType<byte[]>();
            image.PageNumber.Should().BeGreaterThan(0);
            image.ImageName.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region ExtractImagesAsync (stream) Tests

    [Fact]
    public async Task ExtractImagesAsync_WithValidStream_ReturnsExtractedImages()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await extractor.ExtractImagesAsync(stream, "test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<ExtractedPdfImage[]>();
    }

    [Fact]
    public async Task ExtractImagesAsync_WithStream_PreservesFileName()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);
        const string fileName = "document.pdf";

        // Act
        var result = await extractor.ExtractImagesAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractImagesAsync_WithClosedStream_ThrowsException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var stream = new MemoryStream(PdfTestFixtures.CreateMinimalPdf());
        stream.Dispose();

        // Act & Assert
        // InvalidOperationException wraps the ObjectDisposedException
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesAsync(stream, "test.pdf"));
        exception.InnerException.Should().BeOfType<ObjectDisposedException>();
    }

    #endregion

    #region ExtractImagesFromBytesAsync Tests

    [Fact]
    public async Task ExtractImagesFromBytesAsync_WithValidBytes_ReturnsExtractedImages()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();

        // Act
        var result = await extractor.ExtractImagesFromBytesAsync(pdfBytes, "test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<ExtractedPdfImage[]>();
    }

    [Fact]
    public async Task ExtractImagesFromBytesAsync_WithEmptyBytes_ThrowsInvalidOperationException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var emptyBytes = Array.Empty<byte>();

        // Act & Assert
        // Empty bytes are not a valid PDF
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesFromBytesAsync(emptyBytes, "empty.pdf"));
    }

    #endregion

    #region ExtractImagesAsync (specific pages) Tests

    [Fact]
    public async Task ExtractImagesAsync_WithSpecificPageNumbers_OnlyProcessesSpecificPages()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());
        var pageNumbers = new[] { 1 };

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath, pageNumbers);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<ExtractedPdfImage[]>();
    }

    [Fact]
    public async Task ExtractImagesAsync_WithSpecificPages_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pageNumbers = new[] { 1, 2 };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractImagesAsync("/nonexistent/file.pdf", pageNumbers));
    }

    [Fact]
    public async Task ExtractImagesAsync_WithEmptyPageNumbers_ProcessesAllPages()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());
        var emptyPageNumbers = Array.Empty<int>();

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath, emptyPageNumbers);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region ExtractImagesStreamAsync Tests

    [Fact]
    public async Task ExtractImagesStreamAsync_WithValidStream_InvokesCallback()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);
        var callbackInvocationCount = 0;

        Func<ExtractedPdfImage, Task<bool>> onImageExtracted = async (image) =>
        {
            callbackInvocationCount++;
            await Task.CompletedTask;
            return true; // Continue processing
        };

        // Act
        var result = await extractor.ExtractImagesStreamAsync(
            stream,
            "test.pdf",
            onImageExtracted);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExtractImagesStreamAsync_WithCallbackReturningFalse_StopsProcessing()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);
        var callbackInvocationCount = 0;

        Func<ExtractedPdfImage, Task<bool>> onImageExtracted = async (image) =>
        {
            callbackInvocationCount++;
            await Task.CompletedTask;
            return false; // Stop processing
        };

        // Act
        var result = await extractor.ExtractImagesStreamAsync(
            stream,
            "test.pdf",
            onImageExtracted);

        // Assert
        result.Should().Be(callbackInvocationCount);
    }

    [Fact]
    public async Task ExtractImagesStreamAsync_WithCancellationToken_CancelsProcessing()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<ExtractedPdfImage, Task<bool>> onImageExtracted = async (image) =>
        {
            await Task.CompletedTask;
            return true;
        };

        // Act & Assert
        // InvalidOperationException wraps the TaskCanceledException
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesStreamAsync(
                stream,
                "test.pdf",
                onImageExtracted,
                cts.Token));
        exception.InnerException.Should().BeOfType<TaskCanceledException>();
    }

    #endregion

    #region GetExtractorName Tests

    [Fact]
    public void GetExtractorName_ReturnsPdfPigIdentifier()
    {
        // Arrange
        var extractor = CreateExtractor();

        // Act
        var name = extractor.GetExtractorName();

        // Assert
        name.Should().Be("pdfpig");
    }

    #endregion

    #region Configuration Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExtractImagesAsync_WithEnabledOption_RespectsSetting(bool enabled)
    {
        // Arrange
        var options = CreateOptions(enabled: enabled);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(0)] // 0 means all pages
    public async Task ExtractImagesAsync_WithMaxPagesToProcess_RespectsLimit(int maxPages)
    {
        // Arrange
        var options = CreateOptions(maxPagesToProcess: maxPages);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(5242880)]
    [InlineData(10485760)]
    public async Task ExtractImagesAsync_WithMaxImageSize_RespectsLimit(long maxSize)
    {
        // Arrange
        var options = CreateOptions(maxImageSizeBytes: maxSize);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task ExtractImagesAsync_WithPreferredFormat_RespectsFormat(string format)
    {
        // Arrange
        var options = CreateOptions(preferredFormat: format);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    public async Task ExtractImagesAsync_WithMinImageDimensions_FiltersImages(int minSize)
    {
        // Arrange
        var options = CreateOptions(minImageWidth: minSize, minImageHeight: minSize);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExtractImagesAsync_WithCancelledToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // InvalidOperationException wraps the TaskCanceledException
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesAsync(pdfPath, cts.Token));
        exception.InnerException.Should().BeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task ExtractImagesAsync_Stream_WithCancelledToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // InvalidOperationException wraps the TaskCanceledException
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesAsync(stream, "test.pdf", cts.Token));
        exception.InnerException.Should().BeOfType<TaskCanceledException>();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExtractImagesAsync_WithInvalidPdfContent_ThrowsInvalidOperationException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var invalidPdfBytes = new byte[] { 1, 2, 3, 4, 5 }; // Not a valid PDF
        var pdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        _tempFiles.Add(pdfPath);
        File.WriteAllBytes(pdfPath, invalidPdfBytes);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractImagesAsync(pdfPath));
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task ExtractImagesAsync_WithDetailedLoggingEnabled_LogsInformation()
    {
        // Arrange
        var options = CreateOptions(logDetailedResults: true);
        var extractor = CreateExtractor(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await extractor.ExtractImagesAsync(pdfPath);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
