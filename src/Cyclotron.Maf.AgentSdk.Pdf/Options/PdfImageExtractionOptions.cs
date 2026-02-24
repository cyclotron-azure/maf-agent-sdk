namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Configuration options for PDF image extraction.
/// Controls extraction behavior, image format, quality, and performance characteristics.
/// </summary>
public class PdfImageExtractionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PdfImageExtraction";

    /// <summary>
    /// Whether to enable PDF image extraction.
    /// When enabled, images can be extracted from PDFs for vision model processing.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The key of the registered extractor implementation to use.
    /// Examples: "pdfpig", "vision", "custom".
    /// Must match a registered keyed implementation of IPdfImageExtractor.
    /// Default: "pdfpig".
    /// </summary>
    public string ExtractorKey { get; set; } = "pdfpig";

    /// <summary>
    /// Maximum number of pages to process for image extraction.
    /// If set to 0 or less, all pages are processed.
    /// Useful for large PDFs to limit processing time and memory usage.
    /// Default: 0 (process all pages).
    /// </summary>
    public int MaxPagesToProcess { get; set; } = 0;

    /// <summary>
    /// Maximum file size (in bytes) for extracted images.
    /// If an extracted image exceeds this size, it will be skipped or downscaled.
    /// Default: 5MB (5242880 bytes).
    /// </summary>
    public long MaxImageSizeBytes { get; set; } = 5242880; // 5MB

    /// <summary>
    /// Preferred image format for extracted images.
    /// Options: "jpeg", "png", "webp".
    /// Default: "jpeg" (best compression for vision models).
    /// </summary>
    public string PreferredFormat { get; set; } = "jpeg";

    /// <summary>
    /// JPEG quality when saving extracted images (0-100).
    /// Only applicable when PreferredFormat is "jpeg".
    /// Default: 85.
    /// </summary>
    public int JpegQuality { get; set; } = 85;

    /// <summary>
    /// Whether to encode images as base64 strings immediately upon extraction.
    /// Useful for JSON serialization and data URI generation.
    /// Default: true.
    /// </summary>
    public bool EncodeAsBase64 { get; set; } = true;

    /// <summary>
    /// Whether to log detailed extraction results (count, sizes, etc.).
    /// Default: true.
    /// </summary>
    public bool LogDetailedResults { get; set; } = true;

    /// <summary>
    /// Whether to skip pages that are detected as text-only.
    /// When true, extraction will not process pages that analyzer detected as TextBased.
    /// Requires prior analysis via PdfContentAnalyzer.
    /// Default: true (optimization to skip unnecessary processing).
    /// </summary>
    public bool SkipTextOnlyPages { get; set; } = true;

    /// <summary>
    /// Minimum image width (pixels) to include in extraction.
    /// Images narrower than this value are skipped.
    /// Useful for filtering out small UI elements or artifacts.
    /// Default: 50.
    /// </summary>
    public int MinImageWidth { get; set; } = 50;

    /// <summary>
    /// Minimum image height (pixels) to include in extraction.
    /// Images shorter than this value are skipped.
    /// Default: 50.
    /// </summary>
    public int MinImageHeight { get; set; } = 50;
}
