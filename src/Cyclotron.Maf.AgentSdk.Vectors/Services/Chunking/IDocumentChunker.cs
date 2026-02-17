namespace Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;

/// <summary>
/// Provides document chunking functionality for splitting large documents into manageable semantic units.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Gets the identifier for this chunker implementation (e.g., "semantic", "simple").
    /// </summary>
    string ChunkerType { get; }

    /// <summary>
    /// Asynchronously chunks a document into semantic units.
    /// </summary>
    /// <param name="documentText">The full text content of the document to chunk.</param>
    /// <param name="fileName">The name of the document being chunked, used for generating chunk IDs.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// An async enumerable of tuples containing the chunk text and a unique chunk identifier.
    /// The chunk ID should follow the pattern "{fileName}#{index}" or similar.
    /// </returns>
    IAsyncEnumerable<(string Text, string ChunkId)> ChunkAsync(
        string documentText,
        string fileName,
        CancellationToken cancellationToken = default);
}
