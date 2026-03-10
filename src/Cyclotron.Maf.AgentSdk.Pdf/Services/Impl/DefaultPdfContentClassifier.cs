using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Default implementation of <see cref="IPdfContentClassifier"/> that classifies
/// PDF content based on the presence of full-page images and text ratio.
/// </summary>
public class DefaultPdfContentClassifier(IOptions<PdfContentAnalysisOptions> options) : IPdfContentClassifier
{
    private readonly PdfContentAnalysisOptions _options = options?.Value ?? new PdfContentAnalysisOptions();

    /// <inheritdoc/>
    public PdfContentType ClassifyContent(PdfContentAnalysisResult result)
    {
        if (result.PagesWithFullPageImages == result.TotalPages)
        {
            // Even if there is some text, if every page has a full-page image, we classify
            // as ImageOnly since the text has a high chance of being poor OCR which fails
            // to index correctly.
            return result.TextRatio >= _options.TextRatioThreshold
                ? PdfContentType.Mixed
                : PdfContentType.ImageOnly;
        }

        if (result.PagesWithImages > 0 && result.TextRatio >= _options.TextRatioThreshold)
        {
            return PdfContentType.Mixed;
        }

        return PdfContentType.TextBased;
    }
}
