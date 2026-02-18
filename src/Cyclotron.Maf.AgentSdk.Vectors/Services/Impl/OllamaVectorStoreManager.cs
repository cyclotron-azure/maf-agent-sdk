using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

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
    IHttpClientFactory httpClientFactory,
    IOptions<VectorStoreIndexingOptions> indexingOptions,
    VectorStoreTelemetry telemetry,
    Func<string, VectorStoreProviderConfig> configFactory) : IVectorStoreManager
{
    private readonly ILogger<OllamaVectorStoreManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
            // Validate provider configuration
            var providerConfig = _configFactory(providerName) ?? throw new VectorStoreConfigurationException(
                    $"Provider configuration not found for: {providerName}",
                    providerName);

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
            var httpClient = _httpClientFactory.CreateClient();

            // Process each chunk
            foreach (var (chunkText, chunkId) in chunks)
            {
                var fileId = Guid.NewGuid().ToString();

                try
                {
                    // Generate embedding using Ollama
                    var embedding = await GenerateEmbeddingAsync(
                        httpClient,
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
            var httpClient = _httpClientFactory.CreateClient();

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
                        // Generate embedding using Ollama
                        var embedding = await GenerateEmbeddingAsync(
                            httpClient,
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
    /// Generates an embedding for text using the Ollama API.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(
        HttpClient httpClient,
        VectorStoreProviderConfig providerConfig,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = providerConfig.Endpoint?.TrimEnd('/') ?? "http://localhost:11434";
            var embeddingModel = providerConfig.DeploymentName ?? "nomic-embed-text";

            var requestUri = $"{endpoint}/api/embed";
            var requestBody = new
            {
                model = embeddingModel,
                input = text
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Ollama embedding API failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("embeddings", out var embeddingsElement) &&
                embeddingsElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                embeddingsElement.GetArrayLength() > 0)
            {
                var firstEmbedding = embeddingsElement[0];
                if (firstEmbedding.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var embedding = new List<float>();
                    foreach (var element in firstEmbedding.EnumerateArray())
                    {
                        if (element.TryGetSingle(out var value))
                        {
                            embedding.Add(value);
                        }
                    }
                    return embedding.ToArray();
                }
            }

            throw new VectorStoreIndexingException(
                "Invalid embedding response format from Ollama API");
        }
        catch (HttpRequestException ex)
        {
            throw new VectorStoreIndexingException(
                $"Failed to call Ollama embedding API: {ex.Message}",
                ex);
        }
        catch (JsonException ex)
        {
            throw new VectorStoreIndexingException(
                $"Failed to parse Ollama embedding response: {ex.Message}",
                ex);
        }
        catch (VectorStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VectorStoreIndexingException(
                $"Unexpected error generating embedding: {ex.Message}",
                ex);
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
