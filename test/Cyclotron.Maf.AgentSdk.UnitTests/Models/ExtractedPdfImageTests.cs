using Cyclotron.Maf.AgentSdk.Models;
using AwesomeAssertions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Models;

/// <summary>
/// Unit tests for the <see cref="ExtractedPdfImage"/> class.
/// Tests PDF image model creation and property access.
/// </summary>
public class ExtractedPdfImageTests
{
    [Fact]
    public void ExtractedPdfImage_WithDefaultConstructor_InitializesProperties()
    {
        // Arrange & Act
        var image = new ExtractedPdfImage();

        // Assert
        image.ImageBytes.Should().BeEmpty();
        image.ImageBase64.Should().BeEmpty();
        image.MimeType.Should().BeEmpty();
        image.PageNumber.Should().Be(0);
        image.ImageName.Should().BeEmpty();
        image.Dimensions.Should().BeNull();
        image.ImageIndexOnPage.Should().Be(0);
        image.DiagnosticMessage.Should().BeNull();
    }

    [Fact]
    public void ExtractedPdfImage_CanSetAllProperties()
    {
        // Arrange
        var imageBytes = new byte[] { 255, 216, 255, 224 }; // JPEG magic bytes
        var base64 = Convert.ToBase64String(imageBytes);
        var imageName = "page_1_image_1.jpg";
        var mimeType = "image/jpeg";
        var pageNumber = 1;
        var dimensions = (640, 480);
        var imageIndex = 0;
        var diagnostic = "Extracted from page 1";

        // Act
        var image = new ExtractedPdfImage
        {
            ImageBytes = imageBytes,
            ImageBase64 = base64,
            MimeType = mimeType,
            Format = ImageFormat.Jpeg,
            PageNumber = pageNumber,
            ImageName = imageName,
            Dimensions = dimensions,
            ImageIndexOnPage = imageIndex,
            DiagnosticMessage = diagnostic
        };

        // Assert
        image.ImageBytes.Should().Equal(imageBytes);
        image.ImageBase64.Should().Be(base64);
        image.MimeType.Should().Be(mimeType);
        image.Format.Should().Be(ImageFormat.Jpeg);
        image.PageNumber.Should().Be(pageNumber);
        image.ImageName.Should().Be(imageName);
        image.Dimensions.Should().Be(dimensions);
        image.ImageIndexOnPage.Should().Be(imageIndex);
        image.DiagnosticMessage.Should().Be(diagnostic);
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    [InlineData(ImageFormat.Gif)]
    public void ExtractedPdfImage_WithDifferentFormats_StoresCorrectly(ImageFormat format)
    {
        // Arrange & Act
        var image = new ExtractedPdfImage { Format = format };

        // Assert
        image.Format.Should().Be(format);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    public void ExtractedPdfImage_WithDifferentMimeTypes_StoresCorrectly(string mimeType)
    {
        // Arrange & Act
        var image = new ExtractedPdfImage { MimeType = mimeType };

        // Assert
        image.MimeType.Should().Be(mimeType);
    }



    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void ExtractedPdfImage_WithVariousPageNumbers_StoresCorrectly(int pageNumber)
    {
        // Arrange & Act
        var image = new ExtractedPdfImage { PageNumber = pageNumber };

        // Assert
        image.PageNumber.Should().Be(pageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ExtractedPdfImage_WithImageIndexOnPage_StoresCorrectly(int imageIndex)
    {
        // Arrange & Act
        var image = new ExtractedPdfImage { ImageIndexOnPage = imageIndex };

        // Assert
        image.ImageIndexOnPage.Should().Be(imageIndex);
    }

    [Fact]
    public void ExtractedPdfImage_WithDimensions_CanStoreAndRetrieve()
    {
        // Arrange
        var dimensions = (width: 1920, height: 1080);

        // Act
        var image = new ExtractedPdfImage { Dimensions = dimensions };

        // Assert
        image.Dimensions.Should().Be(dimensions);
        image.Dimensions?.Width.Should().Be(1920);
        image.Dimensions?.Height.Should().Be(1080);
    }

    [Fact]
    public void ExtractedPdfImage_WithLargeImageBytes_StoresCorrectly()
    {
        // Arrange
        var largeBytes = new byte[1024 * 1024]; // 1MB
        new Random().NextBytes(largeBytes);

        // Act
        var image = new ExtractedPdfImage { ImageBytes = largeBytes };

        // Assert
        image.ImageBytes.Should().HaveCount(1024 * 1024);
        image.ImageBytes.Should().Equal(largeBytes);
    }

    [Fact]
    public void ExtractedPdfImage_WithBase64EncodedData_StoresCorrectly()
    {
        // Arrange
        var originalBytes = new byte[] { 1, 2, 3, 4, 5 };
        var base64 = Convert.ToBase64String(originalBytes);

        // Act
        var image = new ExtractedPdfImage { ImageBase64 = base64 };

        // Assert
        image.ImageBase64.Should().Be(base64);
        Convert.FromBase64String(image.ImageBase64).Should().Equal(originalBytes);
    }

    [Fact]
    public void ExtractedPdfImage_MultipleInstances_AreIndependent()
    {
        // Arrange
        var image1 = new ExtractedPdfImage { PageNumber = 1, ImageName = "image1.jpg" };
        var image2 = new ExtractedPdfImage { PageNumber = 2, ImageName = "image2.jpg" };

        // Act & Assert
        image1.PageNumber.Should().Be(1);
        image2.PageNumber.Should().Be(2);
        image1.ImageName.Should().Be("image1.jpg");
        image2.ImageName.Should().Be("image2.jpg");
    }

    [Fact]
    public void ImageFormat_ContainsExpectedValues()
    {
        // Arrange & Act
        var formats = (ImageFormat[])typeof(ImageFormat).GetEnumValues();

        // Assert
        formats.Should().HaveCount(4);
        formats.Should().Contain(ImageFormat.Jpeg);
        formats.Should().Contain(ImageFormat.Png);
        formats.Should().Contain(ImageFormat.WebP);
        formats.Should().Contain(ImageFormat.Gif);
    }

    [Fact]
    public void ExtractedPdfImage_CanBeCreatedAsArray()
    {
        // Arrange & Act
        var images = new[]
        {
            new ExtractedPdfImage { PageNumber = 1, ImageName = "img1.jpg" },
            new ExtractedPdfImage { PageNumber = 2, ImageName = "img2.jpg" },
            new ExtractedPdfImage { PageNumber = 3, ImageName = "img3.jpg" }
        };

        // Assert
        images.Should().HaveCount(3);
        images[0].PageNumber.Should().Be(1);
        images[1].PageNumber.Should().Be(2);
        images[2].PageNumber.Should().Be(3);
    }
}
