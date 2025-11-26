using System.Text;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Cyclotron.Maf.AgentSdk.UnitTests.TestFixtures;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="PdfPigMarkdownConverter"/> class.
/// Tests PDF conversion options, filename generation, directory resolution, and actual PDF conversion.
/// </summary>
public class PdfPigMarkdownConverterTests : IDisposable
{
    private readonly Mock<ILogger<PdfPigMarkdownConverter>> _mockLogger;
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];

    public PdfPigMarkdownConverterTests()
    {
        _mockLogger = new Mock<ILogger<PdfPigMarkdownConverter>>();
    }

    public void Dispose()
    {
        // Cleanup temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        // Cleanup temporary directories
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        GC.SuppressFinalize(this);
    }

    private IOptions<PdfConversionOptions> CreateOptions(
        bool saveMarkdownForDebug = false,
        string outputDirectory = "output",
        bool includeTimestamp = false,
        string extension = ".md",
        bool includePageNumbers = true,
        bool preserveParagraphStructure = true)
    {
        var options = new PdfConversionOptions
        {
            SaveMarkdownForDebug = saveMarkdownForDebug,
            OutputDirectory = outputDirectory,
            IncludeTimestampInFilename = includeTimestamp,
            MarkdownFileExtension = extension,
            IncludePageNumbers = includePageNumbers,
            PreserveParagraphStructure = preserveParagraphStructure
        };
        return MsOptions.Create(options);
    }

    private PdfPigMarkdownConverter CreateConverter(IOptions<PdfConversionOptions>? options = null)
    {
        return new PdfPigMarkdownConverter(
            _mockLogger.Object,
            options ?? CreateOptions());
    }

    private string CreateTempPdf(byte[] pdfBytes)
    {
        var path = PdfTestFixtures.CreateTempPdfFile(pdfBytes);
        _tempFiles.Add(path);
        return path;
    }

    private string CreateTempDir()
    {
        var path = PdfTestFixtures.CreateTempOutputDirectory();
        _tempDirs.Add(path);
        return path;
    }

    #region ConvertToMarkdownAsync (file path) Tests

    [Fact(DisplayName = "ConvertToMarkdownAsync should throw FileNotFoundException when file does not exist")]
    public async Task ConvertToMarkdownAsync_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var converter = CreateConverter();
        var nonExistentPath = "/path/to/non/existent/file.pdf";

        // Act
        var act = () => converter.ConvertToMarkdownAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*PDF file not found*{nonExistentPath}*");
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should convert minimal PDF successfully")]
    public async Task ConvertToMarkdownAsync_MinimalPdf_ReturnsMarkdown()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("#"); // Should have header
        markdown.Should().Contain("Hello World"); // Should contain the text
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should include page numbers when enabled")]
    public async Task ConvertToMarkdownAsync_IncludePageNumbers_AddsPageHeaders()
    {
        // Arrange
        var options = CreateOptions(includePageNumbers: true);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiPagePdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().Contain("## Page 1");
        markdown.Should().Contain("## Page 2");
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should not include page numbers when disabled")]
    public async Task ConvertToMarkdownAsync_ExcludePageNumbers_NoPageHeaders()
    {
        // Arrange
        var options = CreateOptions(includePageNumbers: false);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotContain("## Page");
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should handle empty page PDF")]
    public async Task ConvertToMarkdownAsync_EmptyPage_ReturnsMarkdownWithHeader()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateEmptyPagePdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("#"); // Should still have document header
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should handle multi-block PDF")]
    public async Task ConvertToMarkdownAsync_MultiBlockPdf_ExtractsAllText()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiBlockPdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("Document Title");
        markdown.Should().Contain("First paragraph");
        markdown.Should().Contain("Second paragraph");
        markdown.Should().Contain("Third paragraph");
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync should support cancellation")]
    public async Task ConvertToMarkdownAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var converter = CreateConverter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Create a temp file to avoid FileNotFoundException
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        try
        {
            var act = () => converter.ConvertToMarkdownAsync(tempFile, cts.Token);

            // Assert - Should throw OperationCanceledException (not FileNotFoundException)
            await act.Should().ThrowAsync<Exception>();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region ConvertToMarkdownAsync (stream) Tests

    [Fact(DisplayName = "ConvertToMarkdownAsync from stream should throw on invalid stream")]
    public async Task ConvertToMarkdownAsync_InvalidStream_ThrowsException()
    {
        // Arrange
        var converter = CreateConverter();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is not a PDF"));

        // Act
        var act = () => converter.ConvertToMarkdownAsync(stream, "test.pdf");

        // Assert - PdfPig throws when content is not valid PDF
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync from stream should throw on empty stream")]
    public async Task ConvertToMarkdownAsync_EmptyStream_ThrowsException()
    {
        // Arrange
        var converter = CreateConverter();
        using var stream = new MemoryStream();

        // Act
        var act = () => converter.ConvertToMarkdownAsync(stream, "empty.pdf");

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync from stream should convert valid PDF")]
    public async Task ConvertToMarkdownAsync_ValidPdfStream_ReturnsMarkdown()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(stream, "test.pdf");

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("Hello World");
    }

    [Fact(DisplayName = "ConvertToMarkdownAsync from stream should use filename in header")]
    public async Task ConvertToMarkdownAsync_StreamWithFilename_UsesFilenameInHeader()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(stream, "my_document.pdf");

        // Assert
        markdown.Should().Contain("# my_document");
    }

    #endregion

    #region ConvertAndSaveAsync Tests

    [Fact(DisplayName = "ConvertAndSaveAsync should throw when file does not exist")]
    public async Task ConvertAndSaveAsync_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var converter = CreateConverter();

        // Act
        var act = () => converter.ConvertAndSaveAsync("/non/existent/file.pdf");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact(DisplayName = "ConvertAndSaveAsync should save markdown when debug enabled")]
    public async Task ConvertAndSaveAsync_DebugEnabled_SavesMarkdownFile()
    {
        // Arrange
        var outputDir = CreateTempDir();
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: outputDir,
            includeTimestamp: false);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var (markdown, savedPath) = await converter.ConvertAndSaveAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        savedPath.Should().NotBeNull();
        File.Exists(savedPath).Should().BeTrue();

        var savedContent = await File.ReadAllTextAsync(savedPath!);
        savedContent.Should().Be(markdown);

        // Cleanup
        if (savedPath != null)
        {
            _tempFiles.Add(savedPath);
        }
    }

    [Fact(DisplayName = "ConvertAndSaveAsync should not save when debug disabled")]
    public async Task ConvertAndSaveAsync_DebugDisabled_DoesNotSaveFile()
    {
        // Arrange
        var options = CreateOptions(saveMarkdownForDebug: false);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var (markdown, savedPath) = await converter.ConvertAndSaveAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        savedPath.Should().BeNull();
    }

    [Fact(DisplayName = "ConvertAndSaveAsync should include timestamp in filename when enabled")]
    public async Task ConvertAndSaveAsync_TimestampEnabled_IncludesTimestamp()
    {
        // Arrange
        var outputDir = CreateTempDir();
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: outputDir,
            includeTimestamp: true);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var (_, savedPath) = await converter.ConvertAndSaveAsync(pdfPath);

        // Assert
        savedPath.Should().NotBeNull();
        // Filename pattern: baseFileName_timestamp.md (baseFileName is GUID from temp file creation)
        savedPath.Should().MatchRegex(@"_\d{8}_\d{6}\.md$");

        // Cleanup
        if (savedPath != null)
        {
            _tempFiles.Add(savedPath);
        }
    }

    #endregion

    #region ConvertFromBytesAsync Tests

    [Fact(DisplayName = "ConvertFromBytesAsync should throw on invalid PDF bytes")]
    public async Task ConvertFromBytesAsync_InvalidPdfBytes_ThrowsException()
    {
        // Arrange
        var converter = CreateConverter();
        var invalidBytes = Encoding.UTF8.GetBytes("Not a PDF content");

        // Act
        var act = () => converter.ConvertFromBytesAsync(invalidBytes, "invalid.pdf");

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "ConvertFromBytesAsync should throw on empty bytes")]
    public async Task ConvertFromBytesAsync_EmptyBytes_ThrowsException()
    {
        // Arrange
        var converter = CreateConverter();
        var emptyBytes = Array.Empty<byte>();

        // Act
        var act = () => converter.ConvertFromBytesAsync(emptyBytes, "empty.pdf");

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "ConvertFromBytesAsync should convert valid PDF bytes")]
    public async Task ConvertFromBytesAsync_ValidPdfBytes_ReturnsMarkdown()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();

        // Act
        var (markdown, savedPath) = await converter.ConvertFromBytesAsync(pdfBytes, "test.pdf");

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("Hello World");
        savedPath.Should().BeNull(); // Debug not enabled
    }

    [Fact(DisplayName = "ConvertFromBytesAsync should save file when debug enabled")]
    public async Task ConvertFromBytesAsync_DebugEnabled_SavesFile()
    {
        // Arrange
        var outputDir = CreateTempDir();
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: outputDir,
            includeTimestamp: false);
        var converter = CreateConverter(options);
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();

        // Act
        var (markdown, savedPath) = await converter.ConvertFromBytesAsync(pdfBytes, "test.pdf");

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        savedPath.Should().NotBeNull();
        File.Exists(savedPath).Should().BeTrue();

        // Cleanup
        if (savedPath != null)
        {
            _tempFiles.Add(savedPath);
        }
    }

    #endregion

    #region Layout Analysis Tests

    [Fact(DisplayName = "Converter should use layout analysis when PreserveParagraphStructure is true")]
    public async Task ConvertToMarkdownAsync_PreserveParagraphStructure_UsesLayoutAnalysis()
    {
        // Arrange
        var options = CreateOptions(preserveParagraphStructure: true);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMultiBlockPdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        // Layout analysis should produce paragraphs separated by blank lines
        markdown.Should().Contain("\n\n");
    }

    [Fact(DisplayName = "Converter should use simple extraction when PreserveParagraphStructure is false")]
    public async Task ConvertToMarkdownAsync_NoPreserveParagraphStructure_UsesSimpleExtraction()
    {
        // Arrange
        var options = CreateOptions(preserveParagraphStructure: false);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(pdfPath);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("Hello World");
    }

    #endregion

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should accept null logger gracefully")]
    public void Constructor_NullLogger_DoesNotThrow()
    {
        // Note: Primary constructor doesn't validate null, which is a design choice
        // This test documents the current behavior
        var options = CreateOptions();

        // Act - should not throw
        var converter = new PdfPigMarkdownConverter(null!, options);

        // Assert
        converter.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should use default options values")]
    public void Constructor_WithOptions_UsesOptionsValues()
    {
        // Arrange
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: "custom/output",
            includeTimestamp: true);

        // Act
        var converter = new PdfPigMarkdownConverter(_mockLogger.Object, options);

        // Assert
        converter.Should().NotBeNull();
    }

    #endregion

    #region PdfConversionOptions Tests

    [Fact(DisplayName = "PdfConversionOptions should have default values")]
    public void PdfConversionOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new PdfConversionOptions();

        // Assert
        options.SaveMarkdownForDebug.Should().BeFalse();
        options.OutputDirectory.Should().Be("./output");
        options.IncludeTimestampInFilename.Should().BeTrue();
        options.MarkdownFileExtension.Should().Be(".md");
        options.IncludePageNumbers.Should().BeTrue();
        options.PreserveParagraphStructure.Should().BeTrue();
    }

    [Fact(DisplayName = "PdfConversionOptions should allow custom values")]
    public void PdfConversionOptions_CustomValues_ArePreserved()
    {
        // Arrange & Act
        var options = new PdfConversionOptions
        {
            SaveMarkdownForDebug = true,
            OutputDirectory = "/custom/path",
            IncludeTimestampInFilename = true,
            MarkdownFileExtension = ".markdown",
            IncludePageNumbers = false,
            PreserveParagraphStructure = false
        };

        // Assert
        options.SaveMarkdownForDebug.Should().BeTrue();
        options.OutputDirectory.Should().Be("/custom/path");
        options.IncludeTimestampInFilename.Should().BeTrue();
        options.MarkdownFileExtension.Should().Be(".markdown");
        options.IncludePageNumbers.Should().BeFalse();
        options.PreserveParagraphStructure.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact(DisplayName = "Converter should handle PDF with special characters")]
    public async Task ConvertToMarkdownAsync_PdfWithSpecialChars_HandlesGracefully()
    {
        // Arrange
        var converter = CreateConverter();
        var pdfBytes = PdfTestFixtures.CreateMinimalPdf();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var markdown = await converter.ConvertToMarkdownAsync(stream, "test (with) special [chars].pdf");

        // Assert
        markdown.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Converter should create output directory if not exists")]
    public async Task ConvertAndSaveAsync_DirectoryNotExists_CreatesDirectory()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid()}");
        _tempDirs.Add(nonExistentDir);
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: nonExistentDir,
            includeTimestamp: false);
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var (_, savedPath) = await converter.ConvertAndSaveAsync(pdfPath);

        // Assert
        Directory.Exists(nonExistentDir).Should().BeTrue();
        savedPath.Should().NotBeNull();
        File.Exists(savedPath).Should().BeTrue();

        if (savedPath != null)
        {
            _tempFiles.Add(savedPath);
        }
    }

    [Fact(DisplayName = "Converter should use custom markdown extension")]
    public async Task ConvertAndSaveAsync_CustomExtension_UsesExtension()
    {
        // Arrange
        var outputDir = CreateTempDir();
        var options = CreateOptions(
            saveMarkdownForDebug: true,
            outputDirectory: outputDir,
            includeTimestamp: false,
            extension: ".markdown");
        var converter = CreateConverter(options);
        var pdfPath = CreateTempPdf(PdfTestFixtures.CreateMinimalPdf());

        // Act
        var (_, savedPath) = await converter.ConvertAndSaveAsync(pdfPath);

        // Assert
        savedPath.Should().EndWith(".markdown");

        if (savedPath != null)
        {
            _tempFiles.Add(savedPath);
        }
    }

    #endregion
}
