namespace Cyclotron.Maf.AgentSdk.Models;

/// <summary>
/// Enumeration of PDF content types detected by content analyzers.
/// </summary>
public enum PdfContentType
{
    /// <summary>
    /// PDF contains primarily text content (text ratio above threshold).
    /// </summary>
    TextBased,

    /// <summary>
    /// PDF contains primarily image content (text ratio below threshold).
    /// Typically indicates a scanned document requiring OCR for text extraction.
    /// </summary>
    ImageOnly,

    /// <summary>
    /// PDF contains mixed content (both significant text and image components).
    /// </summary>
    Mixed
}

/// <summary>
/// Result of PDF content analysis containing content type classification and metadata.
/// </summary>
public class PdfContentAnalysisResult
{
    /// <summary>
    /// Gets or sets the detected content type of the PDF.
    /// </summary>
    public PdfContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the ratio of pages with text content (0.0 to 1.0).
    /// Example: 0.8 means 80% of pages contain extractable text.
    /// </summary>
    public double TextRatio { get; set; }

    /// <summary>
    /// Gets or sets the ratio of pages with image content (0.0 to 1.0).
    /// Example: 0.6 means 60% of pages contain images/XObjects.
    /// </summary>
    public double ImageRatio { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages analyzed.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages with extractable text.
    /// </summary>
    public int PagesWithText { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages with images or XObjects.
    /// </summary>
    public int PagesWithImages { get; set; }

    /// <summary>
    /// Gets or sets the total number of characters extracted during analysis.
    /// </summary>
    public long TotalCharactersExtracted { get; set; }

    /// <summary>
    /// Gets or sets the analyzer implementation that produced this result.
    /// Example: "pdfpig", "vision", "custom".
    /// </summary>
    public string AnalyzerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional diagnostic message from the analyzer.
    /// </summary>
    public string? DiagnosticMessage { get; set; }
}
