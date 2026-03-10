using Cyclotron.Maf.AgentSdk.Models;

namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Interface for classifying PDF content based on analysis results.
/// Implementations can use various heuristics to determine if a PDF is text-based, image-only, or mixed content.
/// </summary>
public interface IPdfContentClassifier
{
    /// <summary>
    /// Classifies the PDF content type based on the provided analysis result.
    /// </summary>
    /// <param name="result">The result of PDF content analysis containing metrics like text ratio and image presence.</param>
    /// <returns>A <see cref="PdfContentType"/> indicating the classified content type of the PDF.</returns>
    PdfContentType ClassifyContent(PdfContentAnalysisResult result);
}
