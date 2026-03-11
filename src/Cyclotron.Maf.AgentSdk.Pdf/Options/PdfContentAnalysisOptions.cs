namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Enumeration of failure handling strategies for PDF content analysis.
/// </summary>
public enum PdfAnalysisFailureStrategy
{
    /// <summary>
    /// Skip PDF processing if content analysis fails. Returns empty content.
    /// Useful for lenient workflows that continue despite analysis errors.
    /// </summary>
    Skip,

    /// <summary>
    /// Throw an exception if content analysis fails. Stops processing immediately.
    /// Useful for strict workflows that require successful analysis.
    /// </summary>
    Throw,

    /// <summary>
    /// Attempt conversion anyway if content analysis fails (fallback behavior).
    /// Useful for workflows that want to attempt processing despite analysis errors.
    /// </summary>
    Fallback
}

/// <summary>
/// Configuration options for PDF content analysis.
/// Controls which analyzer to use, detection thresholds, and error handling behavior.
/// </summary>
public class PdfContentAnalysisOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PdfContentAnalysis";

    /// <summary>
    /// Whether to enable PDF content analysis.
    /// When enabled, PDFs are analyzed before conversion to detect image-only content.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The key of the registered analyzer implementation to use.
    /// Examples: "pdfpig", "vision", "custom".
    /// Must match a registered keyed implementation of IPdfContentAnalyzer.
    /// Default: "pdfpig".
    /// </summary>
    public string AnalyzerKey { get; set; } = "pdfpig";

    /// <summary>
    /// Strategy for handling analysis failures.
    /// Default: <see cref="PdfAnalysisFailureStrategy.Fallback"/>.
    /// </summary>
    public PdfAnalysisFailureStrategy FailureStrategy { get; set; } = PdfAnalysisFailureStrategy.Fallback;

    /// <summary>
    /// Minimum ratio of pages with extractable text (0.0 to 1.0) to classify as TextBased.
    /// Pages with text ratio below this threshold are classified as ImageOnly or Mixed.
    /// Default: 0.1 (10% of pages must have text to be considered text-based).
    /// </summary>
    public double TextRatioThreshold { get; set; } = 0.1;

    /// <summary>
    /// Maximum number of pages to analyze for performance optimization.
    /// If set to 0 or less, all pages are analyzed.
    /// Default: 0 (analyze all pages).
    /// </summary>
    public int MaxPagesToAnalyze { get; set; } = 0;

    /// <summary>
    /// Minimum number of characters required to consider a page as having text.
    /// Pages with fewer characters are not counted as text pages.
    /// Default: 5.
    /// </summary>
    public int MinCharactersPerPage { get; set; } = 5;

    /// <summary>
    /// Whether to log detailed analysis results (text ratio, image ratio, etc.).
    /// Default: true.
    /// </summary>
    public bool LogDetailedResults { get; set; } = true;

    /// <summary>
    /// Minimum area coverage ratio (0.0 to 1.0) for an image to be considered a dominant full-page image.
    /// An image whose area is at least this fraction of the total page area is classified as full-page.
    /// Default: 0.70 (image must cover at least 70% of the page area).
    /// </summary>
    public double FullPageImageAreaCoverageThreshold { get; set; } = 0.70;

    /// <summary>
    /// Primary dimension coverage ratio (0.0 to 1.0) used in the aspect-aware full-page image check.
    /// An image is considered full-page when its primary axis (width or height) spans at least this
    /// fraction of the page in that direction, combined with <see cref="FullPageImageSecondaryDimensionThreshold"/>.
    /// Default: 0.85 (primary dimension must cover at least 85% of the page).
    /// </summary>
    public double FullPageImagePrimaryDimensionThreshold { get; set; } = 0.85;

    /// <summary>
    /// Secondary dimension coverage ratio (0.0 to 1.0) used in the aspect-aware full-page image check.
    /// An image is considered full-page when its secondary axis spans at least this fraction of the page,
    /// combined with <see cref="FullPageImagePrimaryDimensionThreshold"/>.
    /// Default: 0.60 (secondary dimension must cover at least 60% of the page).
    /// </summary>
    public double FullPageImageSecondaryDimensionThreshold { get; set; } = 0.60;
}
