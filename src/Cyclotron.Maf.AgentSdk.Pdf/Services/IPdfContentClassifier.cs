using Cyclotron.Maf.AgentSdk.Models;

namespace Cyclotron.Maf.AgentSdk.Services;

public interface IPdfContentClassifier
{
    PdfContentType ClassifyContent(PdfContentAnalysisResult result);
}
