namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Configuration options for PDF to Markdown conversion.
/// Controls how PDF files are processed and optionally saved for debugging.
/// </summary>
public class PdfConversionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PdfConversion";

    /// <summary>
    /// Whether to enable PDF to Markdown conversion.
    /// When enabled, PDF files are converted to markdown before processing.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to save converted markdown files to disk for debugging.
    /// Default: false.
    /// </summary>
    public bool SaveMarkdownForDebug { get; set; } = false;

    /// <summary>
    /// Output directory for saving markdown files when debugging is enabled.
    /// Can be absolute or relative path. Relative paths are resolved from the application root.
    /// Default: "./output".
    /// </summary>
    public string OutputDirectory { get; set; } = "./output";

    /// <summary>
    /// Whether to include page numbers as headers in the markdown output.
    /// Default: true.
    /// </summary>
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>
    /// Whether to preserve paragraph structure using text block detection.
    /// When enabled, uses PdfPig's DocstrumBoundingBoxes for layout analysis.
    /// Default: true.
    /// </summary>
    public bool PreserveParagraphStructure { get; set; } = true;

    /// <summary>
    /// Whether to include a timestamp in saved markdown filenames.
    /// Default: true.
    /// </summary>
    public bool IncludeTimestampInFilename { get; set; } = true;

    /// <summary>
    /// File extension for saved markdown files.
    /// Default: ".md".
    /// </summary>
    public string MarkdownFileExtension { get; set; } = ".md";
}
