using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// Unit tests for <see cref="SimpleDocumentChunker"/>.
/// Tests fixed-size chunking with natural break points.
/// </summary>
public class SimpleDocumentChunkerTests
{
    private readonly Mock<ILogger<SimpleDocumentChunker>> _mockLogger;
    private readonly IOptions<SemanticChunkingOptions> _options;

    public SimpleDocumentChunkerTests()
    {
        _mockLogger = new Mock<ILogger<SimpleDocumentChunker>>();
        _options = MsOptions.Create(new SemanticChunkingOptions
        {
            TargetChunkSize = 1000,
            MinChunkSize = 50,
            OverlapSize = 50,
            PreserveFormatting = true
        });
    }

    [Fact(DisplayName = "ChunkAsync should create fixed-size chunks")]
    public async Task ChunkAsync_LongText_CreatesFixedSizeChunks()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = new string('a', 5000); // 5000 characters

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.True(chunks.Count > 1, "Should create multiple chunks for long text");
        Assert.All(chunks, chunk => Assert.True(chunk.Text.Length > 0));
    }

    [Fact(DisplayName = "ChunkAsync should handle short text in single chunk")]
    public async Task ChunkAsync_ShortText_ReturnsSingleChunk()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = "This is a short text that fits in one chunk.";

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal(text, chunks[0].Text);
        Assert.Equal("test.txt#0", chunks[0].ChunkId);
    }

    [Fact(DisplayName = "ChunkAsync should handle empty text")]
    public async Task ChunkAsync_EmptyText_ReturnsNoChunks()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
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

    [Fact(DisplayName = "ChunkAsync should generate sequential chunk IDs")]
    public async Task ChunkAsync_MultipleChunks_GeneratesSequentialIds()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = new string('x', 3000);

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

    [Fact(DisplayName = "ChunkAsync should break at word boundaries when possible")]
    public async Task ChunkAsync_TextWithSpaces_BreaksAtWordBoundaries()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = string.Join(" ", Enumerable.Repeat("word", 1000));

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.True(chunks.Count > 1);
        // Verify chunks don't split words unnecessarily
        Assert.All(chunks.Take(chunks.Count - 1), chunk =>
        {
            if (chunk.Text.Contains(' '))
            {
                Assert.True(chunk.Text.EndsWith(' ') || chunk.Text.EndsWith("word"),
                    "Chunk should end at word boundary");
            }
        });
    }

    [Fact(DisplayName = "ChunkerType should return 'simple'")]
    public void ChunkerType_WhenAccessed_ReturnsSimple()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);

        // Act
        var type = chunker.ChunkerType;

        // Assert
        Assert.Equal("simple", type);
    }

    [Fact(DisplayName = "ChunkAsync must handle text without spaces")]
    public async Task ChunkAsync_NoSpaces_ChunksAnyway()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = new string('a', 2000); // No spaces

        // Act
        var chunks = new List<(string Text, string ChunkId)>();
        await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Text.Length > 0));
    }

    [Fact(DisplayName = "ChunkAsync should handle cancellation")]
    public async Task ChunkAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var chunker = new SimpleDocumentChunker(_mockLogger.Object, _options);
        var text = new string('x', 10000);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in chunker.ChunkAsync(text, "test.txt", cts.Token))
            {
                // Should not reach here
            }
        });
    }
}
