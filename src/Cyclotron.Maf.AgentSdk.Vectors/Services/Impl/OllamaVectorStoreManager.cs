using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using System.Diagnostics;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;

/// <summary>
/// Manages vector store lifecycle for AI agent document processing workflows using local Ollama models and SharpVector.
/// Provides in-memory vector storage with embeddings generated via Ollama API.
/// </summary>
/// <remarks>
/// <para>
/// Each workflow execution creates its own ephemeral in-memory vector store to ensure isolation.
/// Files are chunked, embedded using Ollama API, and stored in SharpVector for semantic search.
/// </para>
/// <para>
/// Stores are automatically discarded when the workflow completes, no persistent storage is maintained.
/// </para>
/// </remarks>
public class OllamaVectorStoreManager(
    ILogger<OllamaVectorStoreManager> logger,
    IOptions<VectorStoreIndexingOptions> indexingOptions,
    VectorStoreTelemetry telemetry,
    Func<string, VectorStoreProviderConfig> configFactory) : IVectorStoreManager
{
    private readonly ILogger<OllamaVectorStoreManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IOptions<VectorStoreIndexingOptions> _indexingOptions = indexingOptions ?? throw new ArgumentNullException(nameof(indexingOptions));
    private readonly VectorStoreTelemetry _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly Func<string, VectorStoreProviderConfig> _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));

    // In-memory store of vector stores: vectorStoreId -> list of documents
    private static readonly Dictionary<string, VectorStoreData> _vectorStores = [];
    private static readonly object _lock = new();

    /// <inheritdoc/>
    public async Task<string> GetOrCreateSharedVectorStoreAsync(
        string providerName,
        string key,
        string purpose,
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {

            // Create a new vector store ID
            var vectorStoreId = Guid.NewGuid().ToString();

            lock (_lock)
            {
                _vectorStores[vectorStoreId] = new VectorStoreData
                {
                    Id = vectorStoreId,
                    Key = key,
                    Name = name,
                    Purpose = purpose,
                    CreatedAt = DateTime.UtcNow,
                    Documents = []
                };
            }

            _logger.LogInformation(
                "Created Ollama in-memory vector store {VectorStoreId} with key {Key}",
                vectorStoreId,
                key);

            return await Task.FromResult(vectorStoreId);
        }
        catch (VectorStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create Ollama vector store with key: {key}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreConfigurationException(message, ex, providerName);
        }
    }

    /// <inheritdoc/>
    public async Task CleanupVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (_vectorStores.Remove(vectorStoreId))
                {
                    _logger.LogInformation("Cleaned up Ollama in-memory vector store: {VectorStoreId}", vectorStoreId);
                }
                else
                {
                    _logger.LogWarning("Vector store not found for cleanup: {VectorStoreId}", vectorStoreId);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            var message = $"Failed to cleanup Ollama vector store: {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreCleanupException(message, ex, providerName, vectorStoreId);
        }
    }

    /// <inheritdoc/>
    public async Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        if (chunkingDelegate == null)
            throw new ArgumentNullException(nameof(chunkingDelegate));

        var sw = Stopwatch.StartNew();
        try
        {
            var providerConfig = _configFactory(providerName) ?? throw new VectorStoreConfigurationException(
                    $"Provider configuration not found for: {providerName}",
                    providerName);
            _logger.LogInformation(
                "Processing file {FileName} for Ollama vector store {VectorStoreId}",
                fileName,
                vectorStoreId);

            // Reset stream position for chunking
            if (fileContent.CanSeek)
            {
                fileContent.Seek(0, SeekOrigin.Begin);
            }

            // Get chunks using the provided delegate
            var chunks = new List<(string Text, string ChunkId)>();
            await foreach (var chunk in chunkingDelegate(fileContent, fileName))
            {
                chunks.Add(chunk);
            }
            _logger.LogInformation("Chunked {FileName} into {ChunkCount} chunks", fileName, chunks.Count);

            var fileIds = new List<string>();

            // Create OllamaApiClient for embeddings
            using var ollamaClient = CreateOllamaClient(providerConfig);

            // Process each chunk
            foreach (var (chunkText, chunkId) in chunks)
            {
                var fileId = Guid.NewGuid().ToString();

                try
                {
                    // Generate embedding using OllamaSharp
                    var embedding = await GenerateEmbeddingAsync(
                        ollamaClient,
                        providerConfig,
                        chunkText,
                        cancellationToken);

                    // Store document in vector store
                    lock (_lock)
                    {
                        if (_vectorStores.TryGetValue(vectorStoreId, out var store))
                        {
                            store.Documents.Add(new DocumentData
                            {
                                Id = fileId,
                                ChunkId = chunkId,
                                FileName = fileName,
                                Text = chunkText,
                                Embedding = embedding,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            throw new VectorStoreIndexingException(
                                $"Vector store not found: {vectorStoreId}",
                                providerName);
                        }
                    }

                    _logger.LogDebug("Indexed chunk {ChunkId} with document ID: {FileId}", chunkId, fileId);
                    fileIds.Add(fileId);

                    // Record embedding generation
                    _telemetry.RecordEmbeddingsGenerated(providerName, 1);
                }
                catch (Exception ex)
                {
                    var message = $"Failed to process chunk {chunkId} for file {fileName}";
                    _logger.LogError(ex, message);
                    throw new VectorStoreIndexingException(message, ex, providerName);
                }
            }

            sw.Stop();
            var formatType = GetFormatType(fileName);
            _telemetry.RecordDocumentIndexed(providerName, formatType, chunks.Count);
            _telemetry.RecordIndexingDuration(providerName, sw.ElapsedMilliseconds, chunks.Count, "semantic");

            _logger.LogInformation(
                "Added {ChunkCount} chunks from {FileName} to Ollama vector store {VectorStoreId}",
                fileIds.Count,
                fileName,
                vectorStoreId);

            return fileIds.FirstOrDefault() ?? Guid.NewGuid().ToString();
        }
        catch (VectorStoreException)
        {
            sw.Stop();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = $"Failed to add file {fileName} to Ollama vector store {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreIndexingException(message, ex, providerName);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        if (chunkingDelegate == null)
            throw new ArgumentNullException(nameof(chunkingDelegate));

        var sw = Stopwatch.StartNew();
        try
        {
            var providerConfig = _configFactory(providerName) ?? throw new VectorStoreConfigurationException(
                    $"Provider configuration not found for: {providerName}",
                    providerName);
            var allFileIds = new List<string>();
            var totalChunks = 0;

            // Create OllamaApiClient for embeddings
            using var ollamaClient = CreateOllamaClient(providerConfig);

            _logger.LogInformation("Processing multiple files for Ollama vector store {VectorStoreId}", vectorStoreId);

            // Process each file
            foreach (var (content, fileName) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation("Processing file {FileName} from batch", fileName);

                // Reset stream position for chunking
                if (content.CanSeek)
                {
                    content.Seek(0, SeekOrigin.Begin);
                }

                // Get chunks using the provided delegate
                var fileChunks = new List<(string Text, string ChunkId)>();
                await foreach (var chunk in chunkingDelegate(content, fileName))
                {
                    fileChunks.Add(chunk);
                }

                _logger.LogInformation("Chunked {FileName} into {ChunkCount} chunks", fileName, fileChunks.Count);
                totalChunks += fileChunks.Count;

                // Process each chunk
                foreach (var (chunkText, chunkId) in fileChunks)
                {
                    var fileId = Guid.NewGuid().ToString();

                    try
                    {
                        // Generate embedding using OllamaSharp
                        var embedding = await GenerateEmbeddingAsync(
                            ollamaClient,
                            providerConfig,
                            chunkText,
                            cancellationToken);

                        // Store document in vector store
                        lock (_lock)
                        {
                            if (_vectorStores.TryGetValue(vectorStoreId, out var store))
                            {
                                store.Documents.Add(new DocumentData
                                {
                                    Id = fileId,
                                    ChunkId = chunkId,
                                    FileName = fileName,
                                    Text = chunkText,
                                    Embedding = embedding,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                throw new VectorStoreIndexingException(
                                    $"Vector store not found: {vectorStoreId}",
                                    providerName);
                            }
                        }

                        _logger.LogDebug("Indexed chunk {ChunkId} with document ID: {FileId}", chunkId, fileId);
                        allFileIds.Add(fileId);

                        // Record embedding generation
                        _telemetry.RecordEmbeddingsGenerated(providerName, 1);
                    }
                    catch (Exception ex)
                    {
                        var message = $"Failed to process chunk {chunkId} for file {fileName}";
                        _logger.LogError(ex, message);
                        throw new VectorStoreIndexingException(message, ex, providerName);
                    }
                }
            }

            sw.Stop();
            _telemetry.RecordDocumentIndexed(providerName, "mixed", totalChunks);
            _telemetry.RecordIndexingDuration(providerName, sw.ElapsedMilliseconds, totalChunks, "semantic");

            _logger.LogInformation(
                "Added {TotalChunks} chunks to Ollama vector store {VectorStoreId}",
                totalChunks,
                vectorStoreId);

            return allFileIds.AsReadOnly();
        }
        catch (VectorStoreException)
        {
            sw.Stop();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = $"Failed to add multiple files to Ollama vector store {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreIndexingException(message, ex, providerName);
        }
    }

    /// <summary>
    /// Creates an OllamaApiClient instance from provider configuration.
    /// </summary>
    private static OllamaApiClient CreateOllamaClient(VectorStoreProviderConfig providerConfig)
    {
        var endpoint = providerConfig.Endpoint?.TrimEnd('/') ?? "http://localhost:11434";
        var embeddingModel = providerConfig.DeploymentName ?? "nomic-embed-text";

        return new OllamaApiClient(new Uri(endpoint), embeddingModel);
    }

    /// <summary>
    /// Generates an embedding for text using the OllamaSharp SDK.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(
        OllamaApiClient ollamaClient,
        VectorStoreProviderConfig providerConfig,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var embeddingModel = providerConfig.DeploymentName ?? "nomic-embed-text";

            _logger.LogDebug(
                "Generating embedding using model '{Model}' for text of length {Length}",
                embeddingModel,
                text.Length);

            // Generate embeddings using OllamaSharp - ensure model is set correctly
            ollamaClient.SelectedModel = embeddingModel;

            var embedResponse = await ollamaClient.EmbedAsync(text, cancellationToken);

            if (embedResponse?.Embeddings == null || embedResponse.Embeddings.Count == 0)
            {
                throw new VectorStoreIndexingException(
                    "No embeddings returned from Ollama API");
            }

            // Take the first embedding (we only sent one text input)
            var embedding = embedResponse.Embeddings[0];

            _logger.LogDebug(
                "Generated embedding with {Dimensions} dimensions",
                embedding.Length);

            return embedding;
        }
        catch (VectorStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VectorStoreIndexingException(
                $"Failed to generate embedding using OllamaSharp: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Queries the vector store for semantically similar chunks using cosine similarity.
    /// Generates an embedding for the query and returns the top-K chunks.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "ollama").</param>
    /// <param name="vectorStoreId">The vector store ID to query.</param>
    /// <param name="query">The query text to embed and search for.</param>
    /// <param name="topK">The maximum number of similar chunks to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of (chunkId, text) tuples from the top-K similar documents.</returns>
    public async Task<IReadOnlyList<(string ChunkId, string Text)>> QuerySimilarChunksAsync(
        string providerName,
        string vectorStoreId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providerConfig = _configFactory(providerName) ?? throw new VectorStoreConfigurationException(
                    $"Provider configuration not found for: {providerName}",
                    providerName);

            _logger.LogDebug(
                "Querying Ollama vector store {VectorStoreId} for top {TopK} chunks with query: {Query}",
                vectorStoreId,
                topK,
                query);

            // Check if store exists first (outside async embedding)
            lock (_lock)
            {
                if (!_vectorStores.TryGetValue(vectorStoreId, out var store))
                {
                    _logger.LogWarning("Vector store not found: {VectorStoreId}", vectorStoreId);
                    return Array.Empty<(string, string)>();
                }

                if (store.Documents.Count == 0)
                {
                    _logger.LogDebug("Vector store is empty: {VectorStoreId}", vectorStoreId);
                    return Array.Empty<(string, string)>();
                }
            }

            // Generate embedding for query asynchronously (outside lock)
            using var ollamaClient = CreateOllamaClient(providerConfig);
            var queryEmbedding = await GenerateEmbeddingAsync(
                ollamaClient,
                providerConfig,
                query,
                cancellationToken);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for query");
                return Array.Empty<(string, string)>();
            }

            // Now perform similarity search within lock
            lock (_lock)
            {
                if (!_vectorStores.TryGetValue(vectorStoreId, out var store))
                {
                    _logger.LogWarning("Vector store not found after embedding generation: {VectorStoreId}", vectorStoreId);
                    return Array.Empty<(string, string)>();
                }

                // Calculate cosine similarity with all documents
                var similarities = store.Documents
                    .Select(doc => (
                        ChunkId: doc.ChunkId,
                        Text: doc.Text,
                        Similarity: CosineSimilarity(queryEmbedding, doc.Embedding)))
                    .OrderByDescending(x => x.Similarity)
                    .Take(topK)
                    .ToList();

                _logger.LogDebug(
                    "Found {Count} similar chunks for query in vector store {VectorStoreId}",
                    similarities.Count,
                    vectorStoreId);

                return similarities
                    .Select(x => (x.ChunkId, x.Text))
                    .ToList()
                    .AsReadOnly();
            }
        }
        catch (VectorStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to query Ollama vector store {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreIndexingException(message, ex, providerName);
        }
    }

    /// <summary>
    /// Determines the document format type from the file name.
    /// </summary>
    private static string GetFormatType(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "pdf" => "pdf",
            "txt" => "text",
            "md" => "markdown",
            "json" => "json",
            "csv" => "csv",
            _ => "other"
        };
    }

    /// <summary>
    /// Calculates cosine similarity between two embedding vectors.
    /// </summary>
    /// <param name="a">First embedding vector.</param>
    /// <param name="b">Second embedding vector.</param>
    /// <returns>Cosine similarity score between -1 and 1, where 1 is perfect similarity.</returns>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Represents an in-memory vector store data structure.
    /// </summary>
    private class VectorStoreData
    {
        public required string Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Purpose { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required List<DocumentData> Documents { get; set; }
    }

    /// <summary>
    /// Represents a chunked document with its embedding.
    /// </summary>
    private class DocumentData
    {
        public required string Id { get; set; }
        public required string ChunkId { get; set; }
        public required string FileName { get; set; }
        public required string Text { get; set; }
        public required float[] Embedding { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}

