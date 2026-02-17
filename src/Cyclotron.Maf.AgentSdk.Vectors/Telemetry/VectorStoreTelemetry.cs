using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;

/// <summary>
/// Provides OpenTelemetry instrumentation for vector store operations.
/// Records metrics across document indexing, embedding generation, and performance measurements.
/// </summary>
public class VectorStoreTelemetry
{
    private readonly ILogger<VectorStoreTelemetry> _logger;
    private readonly Counter<long> _documentsIndexed;
    private readonly Counter<long> _embeddingsGenerated;
    private readonly Histogram<long> _indexingDurationMs;
    private readonly Histogram<int> _chunksPerDocument;
    private readonly Counter<long> _errors;

    private const string MeterName = "Cyclotron.Maf.AgentSdk.VectorStore";
    private const string MeterVersion = "1.0.0";

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreTelemetry"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory for creating OpenTelemetry instruments.</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    public VectorStoreTelemetry(IMeterFactory meterFactory, ILogger<VectorStoreTelemetry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (meterFactory == null)
            throw new ArgumentNullException(nameof(meterFactory));

        var meter = meterFactory.Create(MeterName, MeterVersion);

        _documentsIndexed = meter.CreateCounter<long>(
            "vectorstore.documents.indexed",
            unit: "count",
            description: "Total number of documents indexed in vector stores");

        _embeddingsGenerated = meter.CreateCounter<long>(
            "vectorstore.embeddings.generated",
            unit: "count",
            description: "Total number of embeddings generated for document chunks");

        _indexingDurationMs = meter.CreateHistogram<long>(
            "vectorstore.indexing.duration",
            unit: "ms",
            description: "Duration of document indexing operations in milliseconds");

        _chunksPerDocument = meter.CreateHistogram<int>(
            "vectorstore.chunks.per_document",
            unit: "count",
            description: "Number of chunks created per document");

        _errors = meter.CreateCounter<long>(
            "vectorstore.errors",
            unit: "count",
            description: "Total number of vector store operation errors");
    }

    /// <summary>
    /// Records that a document has been indexed.
    /// </summary>
    /// <param name="providerName">The name of the vector store provider (e.g., "azure_foundry", "ollama").</param>
    /// <param name="formatType">The document format type (e.g., "pdf", "text", "markdown").</param>
    /// <param name="chunkCount">The number of chunks created from the document.</param>
    public virtual void RecordDocumentIndexed(string providerName, string formatType, int chunkCount)
    {
        _documentsIndexed.Add(1, new KeyValuePair<string, object?>("provider", providerName), new KeyValuePair<string, object?>("format", formatType));
        _chunksPerDocument.Record(chunkCount, new KeyValuePair<string, object?>("provider", providerName), new KeyValuePair<string, object?>("format", formatType));

        _logger.LogDebug(
            "Recorded document indexed for provider {Provider}, format {Format} with {ChunkCount} chunks",
            providerName,
            formatType,
            chunkCount);
    }

    /// <summary>
    /// Records that embeddings have been generated for document chunks.
    /// </summary>
    /// <param name="providerName">The name of the vector store provider.</param>
    /// <param name="count">The number of embeddings generated.</param>
    public virtual void RecordEmbeddingsGenerated(string providerName, int count = 1)
    {
        _embeddingsGenerated.Add(count, new KeyValuePair<string, object?>("provider", providerName));

        _logger.LogDebug("Recorded {Count} embeddings generated for provider {Provider}", count, providerName);
    }

    /// <summary>
    /// Records the duration of an indexing operation.
    /// </summary>
    /// <param name="providerName">The name of the vector store provider.</param>
    /// <param name="durationMs">The duration of the operation in milliseconds.</param>
    /// <param name="chunkCount">The number of chunks indexed in this operation.</param>
    /// <param name="chunkerType">The type of chunker used (e.g., "semantic", "simple").</param>
    public virtual void RecordIndexingDuration(string providerName, long durationMs, int chunkCount, string chunkerType = "unknown")
    {
        _indexingDurationMs.Record(durationMs,
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("chunker_type", chunkerType));

        _logger.LogDebug(
            "Recorded indexing duration {DurationMs}ms for provider {Provider} with {ChunkCount} chunks using {ChunkerType}",
            durationMs,
            providerName,
            chunkCount,
            chunkerType);
    }

    /// <summary>
    /// Records that an error occurred during vector store operations.
    /// </summary>
    /// <param name="providerName">The name of the vector store provider.</param>
    /// <param name="errorType">The type of error that occurred (e.g., "IndexingException", "EmbeddingException").</param>
    public virtual void RecordError(string providerName, string errorType)
    {
        _errors.Add(1,
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("error_type", errorType));

        _logger.LogWarning(
            "Recorded error of type {ErrorType} for provider {Provider}",
            errorType,
            providerName);
    }
}
