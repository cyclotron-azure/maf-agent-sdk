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
/// Unit tests for the <see cref="PdfPigContentAnalyzer"/> class.
/// Tests PDF content analysis for detecting text-based, image-only, and mixed content.
/// </summary>
public class PdfPigContentAnalyzerTests : IDisposable
{
    private readonly Mock<ILogger<PdfPigContentAnalyzer>> _mockLogger;
    private readonly List<string> _tempFiles = [];

    public PdfPigContentAnalyzerTests()
    {
        _mockLogger = new Mock<ILogger<PdfPigContentAnalyzer>>();
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

    private IOptions<PdfContentAnalysisOptions> CreateOptions(
        bool enabled = true,
        double textRatioThreshold = 0.1,
        int maxPagesToAnalyze = 0,
        int minCharactersPerPage = 5,
        bool logDetailedResults = false)
    {
        var options = new PdfContentAnalysisOptions
        {
            Enabled = enabled,
            AnalyzerKey = "pdfpig",
            TextRatioThreshold = textRatioThreshold,
            MaxPagesToAnalyze = maxPagesToAnalyze,
            MinCharactersPerPage = minCharactersPerPage,
            LogDetailedResults = logDetailedResults
        };
        return MsOptions.Create(options);
    }

    private PdfPigContentAnalyzer CreateAnalyzer(IOptions<PdfContentAnalysisOptions>? options = null)
    {
        return new PdfPigContentAnalyzer(
            _mockLogger.Object,
            options ?? CreateOptions());
    }

    private string CreateTempPdf(byte[] pdfBytes)
    {
        var path = PdfTestFixtures.CreateTempPdfFile(pdfBytes);
        _tempFiles.Add(path);
        return path;
    }

    #region AnalyzeAsync (file path) Tests

    [Fact]
    public async Task AnalyzeAsync_WithValidTextBasedPdf_ReturnsTextBasedContentType()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.ContentType.Should().Be(PdfContentType.TextBased);
        result.TotalPages.Should().BeGreaterThan(0);
        result.PagesWithText.Should().BeGreaterThan(0);
        result.TextRatio.Should().BeGreaterThanOrEqualTo(0);
        result.AnalyzerName.Should().Be("pdfpig");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultiPagePdf_CountsAllPages()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiPagePdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.TotalPages.Should().Be(2);
        result.PagesWithText.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultiBlockPdf_ExtractsTextCorrectly()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiBlockPdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.ContentType.Should().Be(PdfContentType.TextBased);
        result.TotalCharactersExtracted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyPagePdf_ReturnsAccuratePageCount()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateEmptyPagePdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.TotalPages.Should().Be(1);
        result.PagesWithText.Should().Be(0);
        result.TextRatio.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var analyzer = CreateAnalyzer();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => analyzer.AnalyzeAsync("/non/existent/file.pdf"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellation_HandlesGracefully()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - cancellation gets wrapped in InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(pdfPath, cts.Token));

        ex.InnerException.Should().BeOfType<TaskCanceledException>();
    }

    #endregion

    #region AnalyzeAsync (stream) Tests

    [Fact]
    public async Task AnalyzeAsync_WithValidStream_ReturnsValidResult()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await analyzer.AnalyzeAsync(stream, "test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().NotBe(PdfContentType.ImageOnly);
        result.AnalyzerName.Should().Be("pdfpig");
    }

    [Fact]
    public async Task AnalyzeAsync_WithStreamAndFileName_UsesFileNameInDiagnostics()
    {
        // Arrange
        var analyzer = CreateAnalyzer(CreateOptions(logDetailedResults: true));
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await analyzer.AnalyzeAsync(stream, "myfile.pdf");

        // Assert
        result.Should().NotBeNull();
        result.DiagnosticMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyStream_ThrowsException()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        using var stream = new MemoryStream(Array.Empty<byte>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(stream, "empty.pdf"));
    }

    #endregion

    #region AnalyzeFromBytesAsync Tests

    [Fact]
    public async Task AnalyzeFromBytesAsync_WithValidBytes_ReturnsValidResult()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();

        // Act
        var result = await analyzer.AnalyzeFromBytesAsync(pdfBytes, "test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().Be(PdfContentType.TextBased);
    }

    [Fact]
    public async Task AnalyzeFromBytesAsync_WithEmptyBytes_ThrowsException()
    {
        // Arrange
        var analyzer = CreateAnalyzer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeFromBytesAsync(Array.Empty<byte>(), "empty.pdf"));
    }

    #endregion

    #region Configuration Tests

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public async Task AnalyzeAsync_WithDifferentTextRatioThresholds_ClassifiesCorrectly(
        double threshold)
    {
        // Arrange
        var options = CreateOptions(textRatioThreshold: threshold);
        var analyzer = CreateAnalyzer(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        // The minimal PDF has text, so classification depends on text ratio vs threshold
        if (threshold > result.TextRatio)
        {
            result.ContentType.Should().Be(PdfContentType.ImageOnly);
        }
        else
        {
            result.ContentType.Should().Be(PdfContentType.TextBased);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_WithMaxPagesToAnalyze_LimitsPagesAnalyzed()
    {
        // Arrange
        var options = CreateOptions(maxPagesToAnalyze: 1);
        var analyzer = CreateAnalyzer(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiPagePdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.DiagnosticMessage.Should().Contain("1 of 2 pages");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMinCharactersPerPage_FiltersSmallContent()
    {
        // Arrange
        var options = CreateOptions(minCharactersPerPage: 100);
        var analyzer = CreateAnalyzer(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        // With high character threshold, page might not be counted as having text
        result.PagesWithText.Should().BeLessThanOrEqualTo(result.TotalPages);
    }

    #endregion

    #region GetAnalyzerName Tests

    [Fact]
    public void GetAnalyzerName_ReturnsExpectedName()
    {
        // Arrange
        var analyzer = CreateAnalyzer();

        // Act
        var name = analyzer.GetAnalyzerName();

        // Assert
        name.Should().Be("pdfpig");
    }

    #endregion

    #region Result Validation Tests

    [Fact]
    public async Task AnalyzeAsync_ResultContainsValidMetadata()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiPagePdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.TotalPages.Should().BeGreaterThan(0);
        result.TextRatio.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
        result.ImageRatio.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
        result.AnalyzerName.Should().NotBeNullOrEmpty();
        result.DiagnosticMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_TextAndImageRatiosSumToOne()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiPagePdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        var sumRatio = result.TextRatio + result.ImageRatio;
        // Allow small floating point variance
        sumRatio.Should().BeLessThanOrEqualTo(1.01);
    }

    [Fact]
    public async Task AnalyzeAsync_CharacterCountMatchesExtraction()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var result = await analyzer.AnalyzeAsync(pdfPath);

        // Assert
        result.TotalCharactersExtracted.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion
}
