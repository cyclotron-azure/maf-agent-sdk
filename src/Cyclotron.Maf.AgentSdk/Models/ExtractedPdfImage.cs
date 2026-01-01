namespace Cyclotron.Maf.AgentSdk.Models;

/// <summary>
/// Enumeration of supported image formats for extraction from PDFs.
/// </summary>
public enum ImageFormat
{
    /// <summary>
    /// JPEG image format.
    /// </summary>
    Jpeg,

    /// <summary>
    /// PNG image format.
    /// </summary>
    Png,

    /// <summary>
    /// WebP image format.
    /// </summary>
    WebP,

    /// <summary>
    /// GIF image format.
    /// </summary>
    Gif
}

/// <summary>
/// Represents an image extracted from a PDF document.
/// Contains the binary image data along with metadata required for integration with LLM vision models.
/// </summary>
/// <remarks>
/// This model is designed to work seamlessly with Microsoft Agent Framework's multimodal messaging,
/// providing data in formats compatible with Azure OpenAI and other vision-enabled LLMs.
/// </remarks>
public class ExtractedPdfImage
{
    /// <summary>
    /// Gets or sets the raw binary image data (bytes).
    /// Use this for direct passing to vision models via DataContent.
    /// </summary>
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the base64-encoded image data.
    /// Convenient for data URIs or JSON serialization.
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the image (e.g., "image/jpeg", "image/png").
    /// Required for vision model APIs.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image format enum.
    /// </summary>
    public ImageFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the 1-based page number in the PDF where this image was extracted from.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the friendly name/identifier for this extracted image.
    /// Example: "page_1_image_1.jpg".
    /// Useful for logging, debugging, and tracking extracted images.
    /// </summary>
    public string ImageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image dimensions (width, height) in pixels.
    /// Nullable in case dimensions cannot be determined.
    /// </summary>
    public (int Width, int Height)? Dimensions { get; set; }

    /// <summary>
    /// Gets or sets the index of this image within the page.
    /// Useful when a single page contains multiple images.
    /// </summary>
    public int ImageIndexOnPage { get; set; } = 0;

    /// <summary>
    /// Gets or sets an optional diagnostic message from the extraction process.
    /// </summary>
    public string? DiagnosticMessage { get; set; }
}
