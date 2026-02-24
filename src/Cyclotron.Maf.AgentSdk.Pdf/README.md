# Cyclotron.Maf.AgentSdk.Pdf

PDF processing extensions for [Cyclotron.Maf.AgentSdk](../Cyclotron.Maf.AgentSdk). Provides PDF image extraction, content analysis, and markdown conversion using [PdfPig](https://github.com/UglyToad/PdfPig).

## Features

### 📄 PDF Content Analysis

- **Automatic content type detection** - Classifies PDFs as TextBased, ImageOnly, or Mixed
- **Configurable thresholds** - Customizable text/image ratio for classification
- **Page-level analysis** - Per-page statistics and diagnostics
- **Pluggable analyzers** - Keyed DI pattern for custom implementations

### 🖼️ PDF Image Extraction

- **Embedded image extraction** - Extract XObject images from PDF pages
- **Image rendering** - Rasterize image-only pages for vision models
- **Format conversion** - PNG, JPEG output with configurable quality
- **Base64 encoding** - Ready for Azure OpenAI GPT-4 Vision integration
- **Size filtering** - Configurable min/max dimensions and file size limits
- **Platform support** - Cross-platform with libgdiplus on Linux

### 📝 PDF to Markdown Conversion

- **Smart layout detection** - Uses DocstrumBoundingBoxes for text block detection
- **Reading order preservation** - Maintains logical document flow
- **Debug output** - Optional markdown file saving for troubleshooting
- **Content-aware processing** - Integrates with content analyzer for optimal results

## Installation

```bash
dotnet add package AgentSdk.Pdf
```

**Platform Requirements:**
- **Windows**: No additional dependencies
- **Linux**: Install libgdiplus for image processing
  ```bash
  # Ubuntu/Debian
  sudo apt-get install libgdiplus

  # Alpine (Docker)
  apk add libgdiplus
  ```
- **Environment Variable**: Set `DOTNET_SYSTEM_DRAWING_ENABLE_UNIX_SUPPORT=1` on Linux

## Quick Start

```csharp
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Microsoft.Extensions.DependencyInjection;

// Register PDF services
services.AddPdfServices();

// Use PDF content analyzer
var analyzer = serviceProvider.GetRequiredKeyedService<IPdfContentAnalyzer>("pdfpig");
var analysis = await analyzer.AnalyzeAsync("invoice.pdf", cancellationToken);

if (analysis.ContentType == PdfContentType.TextBased)
{
    // Convert to markdown
    var converter = serviceProvider.GetRequiredService<IPdfToMarkdownConverter>();
    var markdown = await converter.ConvertToMarkdownAsync("invoice.pdf", cancellationToken);
}
else if (analysis.ContentType == PdfContentType.ImageOnly)
{
    // Extract images for vision model
    var extractor = serviceProvider.GetRequiredKeyedService<IPdfImageExtractor>("pdfpig");
    var images = await extractor.ExtractImagesAsync("invoice.pdf", cancellationToken);

    // Use with Azure OpenAI GPT-4 Vision
    foreach (var image in images)
    {
        // image.ImageBase64 ready for DataContent
        // image.MimeType for content type
    }
}
```

## Configuration

Configure PDF processing in `appsettings.json` or `agent.config.yaml`:

### PDF Content Analysis

```json
{
  "PdfContentAnalysis": {
    "Enabled": true,
    "AnalyzerKey": "pdfpig",
    "FailureStrategy": "fallback",
    "TextRatioThreshold": 0.1,
    "MaxPagesToAnalyze": 0,
    "MinCharactersPerPage": 10,
    "LogDetailedResults": false
  }
}
```

**Options:**
- `Enabled` - Enable/disable PDF content analysis
- `AnalyzerKey` - Keyed service name ("pdfpig" by default)
- `FailureStrategy` - Skip, Throw, or Fallback on errors
- `TextRatioThreshold` - Minimum text ratio for TextBased classification (0.0 - 1.0)
- `MaxPagesToAnalyze` - Limit pages to analyze (0 = all pages)
- `MinCharactersPerPage` - Minimum characters to consider page as text
- `LogDetailedResults` - Enable detailed logging

### PDF Image Extraction

```json
{
  "PdfImageExtraction": {
    "Enabled": true,
    "ExtractorKey": "pdfpig",
    "MaxPagesToProcess": 0,
    "MaxImageSizeBytes": 5242880,
    "PreferredFormat": "jpeg",
    "JpegQuality": 85,
    "EncodeAsBase64": true,
    "MinImageWidth": 50,
    "MinImageHeight": 50,
    "SkipTextOnlyPages": true,
    "LogDetailedResults": false
  }
}
```

**Options:**
- `Enabled` - Enable/disable image extraction
- `ExtractorKey` - Keyed service name ("pdfpig" by default)
- `MaxPagesToProcess` - Limit pages to process (0 = all pages)
- `MaxImageSizeBytes` - Maximum image file size (5MB default)
- `PreferredFormat` - "jpeg" or "png" (PdfPig always outputs PNG)
- `JpegQuality` - JPEG compression quality (1-100)
- `EncodeAsBase64` - Encode images as base64 strings
- `MinImageWidth`/`MinImageHeight` - Minimum dimensions to extract
- `SkipTextOnlyPages` - Skip pages with text but no images
- `LogDetailedResults` - Enable detailed logging

### PDF to Markdown Conversion

```json
{
  "PdfConversion": {
    "Enabled": true,
    "SaveMarkdownForDebug": false,
    "OutputDirectory": "./output",
    "IncludePageNumbers": true,
    "PreserveParagraphStructure": true,
    "IncludeTimestampInFilename": false,
    "MarkdownFileExtension": ".md"
  }
}
```

**Options:**
- `Enabled` - Enable/disable markdown conversion
- `SaveMarkdownForDebug` - Save markdown files for debugging
- `OutputDirectory` - Directory for debug markdown files
- `IncludePageNumbers` - Add page number markers in markdown
- `PreserveParagraphStructure` - Maintain paragraph breaks
- `IncludeTimestampInFilename` - Add timestamp to debug filenames
- `MarkdownFileExtension` - File extension for markdown files

## Advanced Usage

### Workflow Pattern

```csharp
// 1. Analyze PDF content
var analyzer = serviceProvider.GetRequiredKeyedService<IPdfContentAnalyzer>("pdfpig");
var analysis = await analyzer.AnalyzeAsync(pdfPath, cancellationToken);

// 2. Route based on content type
switch (analysis.ContentType)
{
    case PdfContentType.TextBased:
        // Text extraction workflow
        var converter = serviceProvider.GetRequiredService<IPdfToMarkdownConverter>();
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath, cancellationToken);
        // Process markdown with LLM
        break;

    case PdfContentType.ImageOnly:
        // Vision model workflow
        var extractor = serviceProvider.GetRequiredKeyedService<IPdfImageExtractor>("pdfpig");
        var images = await extractor.ExtractImagesAsync(pdfPath, cancellationToken);
        // Process images with GPT-4 Vision
        break;

    case PdfContentType.Mixed:
        // Hybrid workflow - use both
        var markdownContent = await converter.ConvertToMarkdownAsync(pdfPath, cancellationToken);
        var extractedImages = await extractor.ExtractImagesAsync(pdfPath, cancellationToken);
        // Combine text and image processing
        break;
}
```

### Streaming Image Extraction

For large PDFs, use streaming extraction to process images one at a time:

```csharp
var extractor = serviceProvider.GetRequiredKeyedService<IPdfImageExtractor>("pdfpig");

var imageCount = await extractor.ExtractImagesStreamAsync(
    pdfStream,
    "large-document.pdf",
    async (image) =>
    {
        // Process each image as it's extracted
        await ProcessImageWithVisionModelAsync(image);

        // Return false to stop extraction
        return true;
    },
    cancellationToken);
```

### Extract from Specific Pages

```csharp
var extractor = serviceProvider.GetRequiredKeyedService<IPdfImageExtractor>("pdfpig");
var pageNumbers = new[] { 1, 3, 5 }; // Extract from pages 1, 3, and 5
var images = await extractor.ExtractImagesAsync(pdfPath, pageNumbers, cancellationToken);
```

### Custom Analyzer Implementation

Register a custom analyzer alongside the default PdfPig implementation:

```csharp
services.AddKeyedSingleton<IPdfContentAnalyzer>(
    "custom",
    (sp, _) => new MyCustomAnalyzer(
        sp.GetRequiredService<ILogger<MyCustomAnalyzer>>(),
        sp.GetRequiredService<IOptions<PdfContentAnalysisOptions>>()));

// Configure to use custom analyzer
services.Configure<PdfContentAnalysisOptions>(options =>
{
    options.AnalyzerKey = "custom";
});
```

## Integration with AgentSdk

PDF services integrate seamlessly with [AgentSdk](../Cyclotron.Maf.AgentSdk) workflows:

```csharp
// In your workflow executor
public class InvoiceExtractionWorkflow : IInvoiceExtractionWorkflow
{
    private readonly IPdfContentAnalyzer _analyzer;
    private readonly IPdfToMarkdownConverter _converter;
    private readonly IPdfImageExtractor _extractor;
    private readonly IAgentFactory _agentFactory;

    public InvoiceExtractionWorkflow(
        [FromKeyedServices("pdfpig")] IPdfContentAnalyzer analyzer,
        IPdfToMarkdownConverter converter,
        [FromKeyedServices("pdfpig")] IPdfImageExtractor extractor,
        [FromKeyedServices("extraction")] IAgentFactory agentFactory)
    {
        _analyzer = analyzer;
        _converter = converter;
        _extractor = extractor;
        _agentFactory = agentFactory;
    }

    public async Task<WorkflowResult<InvoiceData>> ExecuteAsync(
        WorkflowInput input,
        CancellationToken cancellationToken = default)
    {
        // Analyze content
        var analysis = await _analyzer.AnalyzeAsync(input.FilePath, cancellationToken);

        // Extract data based on content type
        var context = analysis.ContentType == PdfContentType.TextBased
            ? await _converter.ConvertToMarkdownAsync(input.FilePath, cancellationToken)
            : string.Empty;

        var images = analysis.ContentType != PdfContentType.TextBased
            ? await _extractor.ExtractImagesAsync(input.FilePath, cancellationToken)
            : Array.Empty<ExtractedPdfImage>();

        // Process with agent
        await _agentFactory.CreateAgentAsync(vectorStoreId: null, cancellationToken);
        var response = await _agentFactory.RunAgentWithPollingAsync(
            messages: BuildMessages(context, images),
            cancellationToken: cancellationToken);

        return ParseInvoiceData(response);
    }
}
```

See [SpamDetection sample](../../samples/SpamDetection) for complete workflow examples.

## Performance Tips

1. **Set MaxPagesToProcess** - Limit pages for large documents
2. **Use SkipTextOnlyPages** - Skip text pages during image extraction
3. **Configure MaxImageSizeBytes** - Limit memory usage for large images
4. **Use streaming extraction** - Process large PDFs efficiently
5. **Enable content analysis caching** - Analyze once, use results for routing

## Troubleshooting

### libgdiplus not found on Linux

```bash
# Install libgdiplus
sudo apt-get update && sudo apt-get install -y libgdiplus

# Set environment variable
export DOTNET_SYSTEM_DRAWING_ENABLE_UNIX_SUPPORT=1
```

### Images not extracting

- Check `Enabled` is `true` in `PdfImageExtraction` configuration
- Verify `MinImageWidth` and `MinImageHeight` thresholds
- Enable `LogDetailedResults` to see detailed extraction logs
- Ensure PDF contains actual images (not scanned text rendering as images)

### Poor markdown quality

- Enable `PreserveParagraphStructure` for better formatting
- Use `IncludePageNumbers` for document navigation
- Consider using vision model for image-heavy documents instead

## Dependencies

- **[PdfPig](https://github.com/UglyToad/PdfPig)** v0.1.13 - PDF parsing and extraction
- **System.Drawing.Common** v8.0.2 - Image processing (requires libgdiplus on Linux)
- **Microsoft.Extensions.*** - DI, configuration, and logging abstractions

## Related Packages

- **AgentSdk** - Core SDK with agent factories, workflow orchestration, and vector stores
- **AgentSdk.HOA** (planned) - Domain-specific workflows for HOA document processing

## Contributing

See the main [repository](https://github.com/cyclotron-azure/maf-agent-sdk) for contribution guidelines.

## License

MIT License - see [LICENSE](../../LICENSE) for details.
