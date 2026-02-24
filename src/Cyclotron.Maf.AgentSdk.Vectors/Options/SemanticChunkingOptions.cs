using System.ComponentModel.DataAnnotations;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Options;

/// <summary>
/// Configuration options for semantic document chunking.
/// </summary>
public class SemanticChunkingOptions
{
    /// <summary>
    /// Gets or sets the target size for document chunks in approximate characters.
    /// Default is 800 characters.
    /// </summary>
    [Range(100, 50000)]
    public int TargetChunkSize { get; set; } = 800;

    /// <summary>
    /// Gets or sets the minimum chunk size in characters.
    /// Chunks smaller than this threshold may be merged with adjacent chunks.
    /// Default is 200 characters.
    /// </summary>
    [Range(50, 10000)]
    public int MinChunkSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets the overlap size in characters between consecutive chunks.
    /// This preserves context between chunks for better semantic understanding.
    /// Default is 100 characters.
    /// </summary>
    [Range(0, 5000)]
    public int OverlapSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to preserve original text formatting.
    /// Default is true.
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;
}
