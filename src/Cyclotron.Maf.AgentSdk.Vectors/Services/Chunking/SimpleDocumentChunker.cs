using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;

/// <summary>
/// Implements a simple, fixed-size document chunking strategy with configurable overlap.
/// Useful as a fallback when semantic chunking is not suitable.
/// </summary>
public class SimpleDocumentChunker(
    ILogger<SimpleDocumentChunker> logger,
    IOptions<SemanticChunkingOptions> options) : IDocumentChunker
{
    private readonly ILogger<SimpleDocumentChunker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemanticChunkingOptions _options = options?.Value ?? new SemanticChunkingOptions();

    /// <inheritdoc/>
    public string ChunkerType => "simple";

    /// <inheritdoc/>
    public async IAsyncEnumerable<(string Text, string ChunkId)> ChunkAsync(
        string documentText,
        string fileName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Attempted to chunk empty document: {FileName}", fileName);
            yield break;
        }

        var chunkSize = _options.TargetChunkSize;
        var overlapSize = _options.OverlapSize;
        var chunkIndex = 0;
        var position = 0;

        while (position < documentText.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Calculate the end of the current chunk
            var endPosition = Math.Min(position + chunkSize, documentText.Length);

            // Try to find a natural break point (space or newline) near the end
            if (endPosition < documentText.Length)
            {
                var searchStart = Math.Max(position, endPosition - 100); // Look back up to 100 chars
                var lastSpace = documentText.LastIndexOf(' ', endPosition - 1, endPosition - searchStart);

                if (lastSpace > searchStart)
                {
                    endPosition = lastSpace + 1; // Include the space
                }
            }

            var chunk = documentText[position..endPosition].Trim();

            if (!string.IsNullOrEmpty(chunk))
            {
                var chunkId = GenerateChunkId(fileName, chunkIndex);
                _logger.LogDebug(
                    "Created chunk {ChunkId} for document {FileName} (size: {ChunkSize}, position: {Position})",
                    chunkId,
                    fileName,
                    chunk.Length,
                    position);

                yield return (chunk, chunkId);
                chunkIndex++;
            }

            // Move position forward with overlap
            position = endPosition - overlapSize;
            if (position <= 0 || endPosition >= documentText.Length)
            {
                break;
            }
        }

        await Task.CompletedTask; // Ensure this is treated as async
    }

    /// <summary>
    /// Generates a unique identifier for a chunk.
    /// </summary>
    private static string GenerateChunkId(string fileName, int chunkIndex)
    {
        return $"{fileName}#{chunkIndex}";
    }
}
