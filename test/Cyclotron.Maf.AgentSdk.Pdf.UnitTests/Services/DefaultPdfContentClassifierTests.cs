using AwesomeAssertions;
using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Options;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.Pdf.UnitTests.Services;

public class DefaultPdfContentClassifierTests
{
    private readonly IOptions<PdfContentAnalysisOptions> _options = MsOptions.Create(new PdfContentAnalysisOptions
    {
        TextRatioThreshold = 0.8
    });

    private readonly DefaultPdfContentClassifier _classifier;

    public DefaultPdfContentClassifierTests()
    {
        _classifier = new DefaultPdfContentClassifier(_options);
    }

    [Fact]
    public void ClassifyContent_AllPagesHaveFullPageImages_ReturnsImageOnly()
    {
        var result = new PdfContentAnalysisResult
        {
            TotalPages = 5,
            PagesWithFullPageImages = 5
        };

        var contentType = _classifier.ClassifyContent(result);

        contentType.Should().Be(PdfContentType.ImageOnly);
    }

    [Fact]
    public void ClassifyContent_SomePagesFullPageImagesAndHighTextRatio_ReturnsMixed()
    {
        var result = new PdfContentAnalysisResult
        {
            TotalPages = 5,
            PagesWithImages = 2, // Updated to match classifier logic
            TextRatio = 0.85
        };

        var contentType = _classifier.ClassifyContent(result);

        contentType.Should().Be(PdfContentType.Mixed);
    }

    [Fact]
    public void ClassifyContent_NoPagesFullPageImagesAndHighTextRatio_ReturnsTextBased()
    {
        var result = new PdfContentAnalysisResult
        {
            TotalPages = 5,
            PagesWithFullPageImages = 0,
            TextRatio = 0.9
        };

        var contentType = _classifier.ClassifyContent(result);

        contentType.Should().Be(PdfContentType.TextBased);
    }

    [Fact]
    public void ClassifyContent_SomePagesFullPageImagesButLowTextRatio_ReturnsTextBased()
    {
        var result = new PdfContentAnalysisResult
        {
            TotalPages = 5,
            PagesWithFullPageImages = 2,
            TextRatio = 0.5
        };

        var contentType = _classifier.ClassifyContent(result);

        contentType.Should().Be(PdfContentType.TextBased);
    }
}
