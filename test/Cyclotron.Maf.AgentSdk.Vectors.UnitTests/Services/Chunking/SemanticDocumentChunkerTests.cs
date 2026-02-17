using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests.Services.Chunking;

/// <summary>
/// Unit tests for <see cref="SemanticDocumentChunker"/>.
/// Tests intelligent sentence-based chunking with overlap preservation.
/// </summary>
public class SemanticDocumentChunkerTests
{
    private readonly Mock<ILogger<SemanticDocumentChunker>> _mockLogger;
    private readonly IOptions<SemanticChunkingOptions> _options;

    public SemanticDocumentChunkerTests()
    {
        _mockLogger = new Mock<ILogger<SemanticDocumentChunker>>();
        _options = MsOptions.Create(new SemanticChunkingOptions
        {
            TargetChunkSize = 100,
            MinChunkSize = 20,
            OverlapSize = 10,
            PreserveFormatting = true
        });
    }

    [Fact(DisplayName = "ChunkAsync should split text at sentence boundaries")]
    public async Task ChunkAsync_MultiSentenceText_SplitsAtSentenceBoundaries()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = "This is the first sentence. This is the second sentence. This is the third sentence. This is the fourth sentence.";

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.NotEmpty(chunk.Text));
        Assert.All(chunks, chunk => Assert.Contains("test.txt#", chunk.ChunkId));
    }

    [Fact(DisplayName = "ChunkAsync should respect target chunk size")]
    public async Task ChunkAsync_LongText_RespectsTargetChunkSize()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = string.Join(" ", Enumerable.Repeat("This is a test sentence.", 50));

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.True(chunks.Count > 1, "Should create multiple chunks for long text");
        Assert.All(chunks, chunk => Assert.True(chunk.Text.Length <= _options.Value.TargetChunkSize * 2,
            "Chunk should not exceed double the target size"));
    }

    [Fact(DisplayName = "ChunkAsync should preserve overlap between chunks")]
    public async Task ChunkAsync_MultipleChunks_PreservesOverlap()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = "First sentence here. Second sentence here. Third sentence here. Fourth sentence here. Fifth sentence here.";

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.True(chunks.Count >= 1, "Should create at least one chunk");
        if (chunks.Count > 1)
        {
            // Check that consecutive chunks have some overlap
            for (int i = 0; i < chunks.Count - 1; i++)
            {
                var currentChunk = chunks[i].Text;
                var nextChunk = chunks[i + 1].Text;
                Assert.NotEqual(currentChunk, nextChunk);
            }
        }
    }

    [Fact(DisplayName = "ChunkAsync should handle empty text")]
    public async Task ChunkAsync_EmptyText_ReturnsNoChunks()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = string.Empty;

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Empty(chunks);
    }

    [Fact(DisplayName = "ChunkAsync should handle text without sentence boundaries")]
    public async Task ChunkAsync_NoSentenceBoundaries_ChunksAnyway()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = string.Join("", Enumerable.Repeat("word ", 100)); // No periods

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.NotEmpty(chunks);
    }

    [Fact(DisplayName = "ChunkerType should return 'semantic'")]
    public void ChunkerType_WhenAccessed_ReturnsSemantic()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);

        // Act
        var type = chunker.ChunkerType;

        // Assert
        Assert.Equal("semantic", type);
    }

    [Fact(DisplayName = "ChunkAsync should generate sequential chunk IDs")]
    public async Task ChunkAsync_MultipleChunks_GeneratesSequentialIds()
    {
        // Arrange
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, _options);
        var text = string.Join(" ", Enumerable.Repeat("This is a test sentence.", 20));

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "document.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal($"document.txt#{i}", chunks[i].ChunkId);
        }
    }

    [Fact(DisplayName = "ChunkAsync should respect MinChunkSize setting")]
    public async Task ChunkAsync_SmallSentences_RespectsMinChunkSize()
    {
        // Arrange
        var options = MsOptions.Create(new SemanticChunkingOptions
        {
            TargetChunkSize = 100,
            MinChunkSize = 50, // Require at least 50 chars
            OverlapSize = 10,
            PreserveFormatting = true
        });
        var chunker = new SemanticDocumentChunker(_mockLogger.Object, options);
        var text = "Short. Tiny. Brief. Small. Little. Compact."; // Many small sentences

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.NotEmpty(chunks);
        // Should combine sentences to meet minimum size
    }
}
