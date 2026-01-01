using Cyclotron.Maf.AgentSdk.Options;
using AwesomeAssertions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Options;

/// <summary>
/// Unit tests for the <see cref="PdfImageExtractionOptions"/> class.
/// Tests configuration options for PDF image extraction.
/// </summary>
public class PdfImageExtractionOptionsTests
{
    [Fact]
    public void PdfImageExtractionOptions_WithDefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.ExtractorKey.Should().Be("pdfpig");
        options.MaxPagesToProcess.Should().Be(0);
        options.MaxImageSizeBytes.Should().Be(5242880); // 5MB
        options.PreferredFormat.Should().Be("jpeg");
        options.JpegQuality.Should().Be(85);
        options.EncodeAsBase64.Should().BeTrue();
        options.LogDetailedResults.Should().BeTrue();
        options.SkipTextOnlyPages.Should().BeTrue();
        options.MinImageWidth.Should().Be(50);
        options.MinImageHeight.Should().Be(50);
    }

    [Fact]
    public void PdfImageExtractionOptions_HasConfigurationSectionName()
    {
        // Arrange & Act
        var sectionName = PdfImageExtractionOptions.SectionName;

        // Assert
        sectionName.Should().Be("PdfImageExtraction");
        sectionName.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PdfImageExtractionOptions_CanSetEnabled(bool enabled)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { Enabled = enabled };

        // Assert
        options.Enabled.Should().Be(enabled);
    }

    [Theory]
    [InlineData("pdfpig")]
    [InlineData("vision")]
    [InlineData("custom")]
    public void PdfImageExtractionOptions_CanSetExtractorKey(string extractorKey)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { ExtractorKey = extractorKey };

        // Assert
        options.ExtractorKey.Should().Be(extractorKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void PdfImageExtractionOptions_CanSetMaxPagesToProcess(int maxPages)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { MaxPagesToProcess = maxPages };

        // Assert
        options.MaxPagesToProcess.Should().Be(maxPages);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(5242880)]
    [InlineData(10485760)]
    public void PdfImageExtractionOptions_CanSetMaxImageSizeBytes(long maxSize)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { MaxImageSizeBytes = maxSize };

        // Assert
        options.MaxImageSizeBytes.Should().Be(maxSize);
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    public void PdfImageExtractionOptions_CanSetPreferredFormat(string format)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { PreferredFormat = format };

        // Assert
        options.PreferredFormat.Should().Be(format);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void PdfImageExtractionOptions_CanSetJpegQuality(int quality)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { JpegQuality = quality };

        // Assert
        options.JpegQuality.Should().Be(quality);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PdfImageExtractionOptions_CanSetEncodeAsBase64(bool encodeAsBase64)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { EncodeAsBase64 = encodeAsBase64 };

        // Assert
        options.EncodeAsBase64.Should().Be(encodeAsBase64);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PdfImageExtractionOptions_CanSetLogDetailedResults(bool logDetailed)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { LogDetailedResults = logDetailed };

        // Assert
        options.LogDetailedResults.Should().Be(logDetailed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PdfImageExtractionOptions_CanSetSkipTextOnlyPages(bool skipTextOnly)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { SkipTextOnlyPages = skipTextOnly };

        // Assert
        options.SkipTextOnlyPages.Should().Be(skipTextOnly);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public void PdfImageExtractionOptions_CanSetMinImageWidth(int minWidth)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { MinImageWidth = minWidth };

        // Assert
        options.MinImageWidth.Should().Be(minWidth);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public void PdfImageExtractionOptions_CanSetMinImageHeight(int minHeight)
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions { MinImageHeight = minHeight };

        // Assert
        options.MinImageHeight.Should().Be(minHeight);
    }

    [Fact]
    public void PdfImageExtractionOptions_CanSetAllPropertiesTogether()
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions
        {
            Enabled = false,
            ExtractorKey = "vision",
            MaxPagesToProcess = 10,
            MaxImageSizeBytes = 10485760,
            PreferredFormat = "png",
            JpegQuality = 95,
            EncodeAsBase64 = false,
            LogDetailedResults = false,
            SkipTextOnlyPages = false,
            MinImageWidth = 100,
            MinImageHeight = 100
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.ExtractorKey.Should().Be("vision");
        options.MaxPagesToProcess.Should().Be(10);
        options.MaxImageSizeBytes.Should().Be(10485760);
        options.PreferredFormat.Should().Be("png");
        options.JpegQuality.Should().Be(95);
        options.EncodeAsBase64.Should().BeFalse();
        options.LogDetailedResults.Should().BeFalse();
        options.SkipTextOnlyPages.Should().BeFalse();
        options.MinImageWidth.Should().Be(100);
        options.MinImageHeight.Should().Be(100);
    }

    [Fact]
    public void PdfImageExtractionOptions_MultipleInstances_AreIndependent()
    {
        // Arrange
        var options1 = new PdfImageExtractionOptions { MaxPagesToProcess = 5 };
        var options2 = new PdfImageExtractionOptions { MaxPagesToProcess = 10 };

        // Act & Assert
        options1.MaxPagesToProcess.Should().Be(5);
        options2.MaxPagesToProcess.Should().Be(10);
        options1.MaxPagesToProcess.Should().NotBe(options2.MaxPagesToProcess);
    }

    [Fact]
    public void PdfImageExtractionOptions_DefaultQualityIsReasonable()
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions();

        // Assert
        options.JpegQuality.Should().BeGreaterThan(0);
        options.JpegQuality.Should().BeLessThanOrEqualTo(100);
        options.JpegQuality.Should().Be(85); // Good balance for vision models
    }

    [Fact]
    public void PdfImageExtractionOptions_DefaultMaxImageSizeIsReasonable()
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions();

        // Assert
        options.MaxImageSizeBytes.Should().Be(5242880); // 5MB default
        options.MaxImageSizeBytes.Should().BeGreaterThan(1024); // At least 1KB
    }

    [Fact]
    public void PdfImageExtractionOptions_DefaultMinimumDimensionsAreReasonable()
    {
        // Arrange & Act
        var options = new PdfImageExtractionOptions();

        // Assert
        options.MinImageWidth.Should().Be(50);
        options.MinImageHeight.Should().Be(50);
        options.MinImageWidth.Should().Be(options.MinImageHeight);
    }
}
