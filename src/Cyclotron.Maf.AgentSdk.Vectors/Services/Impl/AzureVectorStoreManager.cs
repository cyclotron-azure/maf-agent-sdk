using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Files;
using System.ClientModel;
using System.Diagnostics;
using OpenAIVectorStore = OpenAI.VectorStores.VectorStore;
using OpenAIVectorStoreStatus = OpenAI.VectorStores.VectorStoreFileStatus;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;

/// <summary>
/// Manages vector store lifecycle for AI agent document processing workflows using Azure AI Foundry V2 API.
/// Provides operations for creating, managing, and cleaning up vector stores with Azure's native server-side chunking.
/// </summary>
/// <remarks>
/// <para>
/// Each workflow execution creates its own vector store to ensure isolation.
/// Azure AI Foundry handles document chunking server-side using built-in strategies (Auto or Static).
/// Complete files are uploaded and indexed by Azure before agents can query them.
/// </para>
/// <para>
/// The indexing process uses configurable polling with optional exponential backoff
/// to wait for files to be fully indexed before returning.
/// </para>
/// <para>
/// <strong>Important:</strong> The chunking delegate parameter is ignored for Azure.
/// Azure handles chunking natively using FileChunkingStrategy.Auto (default: 800 tokens/chunk, 400 overlap).
/// For custom chunking strategies, configure FileChunkingStrategy in VectorStoreCreationOptions.
/// </para>
/// </remarks>
public class AzureVectorStoreManager(
    ILogger<AzureVectorStoreManager> logger,
    IOptions<VectorStoreIndexingOptions> indexingOptions,
    VectorStoreTelemetry telemetry,
    Func<string, AIProjectClient> clientFactory,
    Func<string, VectorStoreProviderConfig> configFactory) : IVectorStoreManager
{
    private readonly ILogger<AzureVectorStoreManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly VectorStoreIndexingOptions _indexingOptions = indexingOptions?.Value ?? new VectorStoreIndexingOptions();
    private readonly VectorStoreTelemetry _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly Func<string, AIProjectClient> _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly Func<string, VectorStoreProviderConfig> _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));

    /// <summary>
    /// Gets the AIProjectClient for the specified provider using the injected factory delegate.
    /// Called at invocation time to access the client from the scoped factory.
    /// </summary>
    private AIProjectClient GetProjectClient(string providerName)
    {
        return _clientFactory(providerName);
    }

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
            var projectClient = GetProjectClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();

            // Each workflow execution creates its own vector store
            // No need to search for existing ones since the key is unique per request
            _logger.LogInformation("Creating new Azure vector store for workflow with key: {MetadataKey}", key);

            ClientResult<OpenAIVectorStore> vectorStoreResponse = await vectorStoreClient.CreateVectorStoreAsync(
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created Azure vector store: {VectorStoreId}", vectorStoreResponse.Value.Id);

            return vectorStoreResponse.Value.Id;
        }
        catch (Exception ex) when (ex is not VectorStoreException)
        {
            var message = $"Failed to create Azure vector store with key: {key}";
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
            var projectClient = GetProjectClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();
            var fileClient = openAIClient.GetOpenAIFileClient();

            _logger.LogInformation("Cleaning up Azure vector store: {VectorStoreId}", vectorStoreId);

            try
            {
                // Get all files in the vector store
                await foreach (var file in vectorStoreClient.GetVectorStoreFilesAsync(vectorStoreId, cancellationToken: cancellationToken))
                {
                    try
                    {
                        await fileClient.DeleteFileAsync(file.FileId, cancellationToken);
                        _logger.LogDebug("Deleted Azure file: {FileId}", file.FileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete Azure file: {FileId}", file.FileId);
                    }
                }

                // Delete the vector store
                await vectorStoreClient.DeleteVectorStoreAsync(vectorStoreId, cancellationToken);
                _logger.LogInformation("Deleted Azure vector store: {VectorStoreId}", vectorStoreId);
            }
            catch (Exception ex) when (ex is not VectorStoreException)
            {
                var message = $"Failed to cleanup Azure vector store: {vectorStoreId}";
                _logger.LogError(ex, message);
                _telemetry.RecordError(providerName, ex.GetType().Name);
                throw new VectorStoreCleanupException(message, ex, providerName, vectorStoreId);
            }
        }
        catch (VectorStoreException)
        {
            throw; // Re-throw already transformed exceptions
        }
        catch (Exception ex)
        {
            var message = $"Unexpected error during Azure vector store cleanup: {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreCleanupException(message, ex, providerName, vectorStoreId);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <strong>Azure-specific behavior:</strong> The <paramref name="chunkingDelegate"/> parameter is ignored.
    /// Azure AI Foundry handles document chunking server-side using FileChunkingStrategy.Auto by default.
    /// </para>
    /// <para>
    /// This implementation uploads the complete file to Azure, which then performs chunking and embedding generation
    /// using its native capabilities. The file is added to the vector store and indexed before returning.
    /// </para>
    /// </remarks>
    public async Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        // NOTE: chunkingDelegate is ignored for Azure - server-side chunking is used
        ArgumentNullException.ThrowIfNull(fileContent);
        ArgumentNullException.ThrowIfNull(fileName);

        var sw = Stopwatch.StartNew();
        try
        {
            var projectClient = GetProjectClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var fileClient = openAIClient.GetOpenAIFileClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();

            _logger.LogInformation(
                "Uploading file {FileName} to Azure vector store {VectorStoreId} (using Azure native chunking)",
                fileName,
                vectorStoreId);

            // Reset stream position for upload
            if (fileContent.CanSeek)
            {
                fileContent.Seek(0, SeekOrigin.Begin);
            }

            // Upload complete file to Azure AI Foundry (not chunks)
            // Azure will handle chunking server-side using FileChunkingStrategy.Auto
            ClientResult<OpenAIFile> uploadedFile = await fileClient.UploadFileAsync(
                fileContent,
                fileName,
                FileUploadPurpose.Assistants,
                cancellationToken);

            var fileId = uploadedFile.Value.Id;
            _logger.LogInformation(
                "Uploaded file {FileName} with file ID: {FileId}",
                fileName,
                fileId);

            // Add file to vector store - Azure will chunk and embed server-side
            var vectorStoreFile = await vectorStoreClient.AddFileToVectorStoreAsync(
                vectorStoreId,
                fileId,
                cancellationToken);

            _logger.LogInformation(
                "Added file {FileName} (ID: {FileId}) to Azure vector store {VectorStoreId}",
                fileName,
                fileId,
                vectorStoreId);

            // Wait for Azure to complete chunking and indexing
            await WaitForFileProcessingAsync(providerName, vectorStoreId, fileId, vectorStoreClient, cancellationToken);

            sw.Stop();

            // Note: Chunk count is not available client-side since Azure chunks server-side
            // We estimate 1 chunk per 800 tokens (Azure's default) for telemetry
            var formatType = GetFormatType(fileName);
            var estimatedChunks = 1; // Azure doesn't expose chunk count
            _telemetry.RecordDocumentIndexed(providerName, formatType, estimatedChunks);
            _telemetry.RecordIndexingDuration(providerName, sw.ElapsedMilliseconds, estimatedChunks, "azure-native");

            return fileId;
        }
        catch (VectorStoreException)
        {
            sw.Stop();
            throw; // Re-throw already transformed exceptions
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = $"Failed to add file {fileName} to Azure vector store {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreIndexingException(message, ex, providerName);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <strong>Azure-specific behavior:</strong> The <paramref name="chunkingDelegate"/> parameter is ignored.
    /// Azure AI Foundry handles document chunking server-side for all files in the batch.
    /// </para>
    /// <para>
    /// This implementation uploads complete files to Azure, which then performs chunking and embedding generation
    /// using its native capabilities. All files are added to the vector store and indexed before returning.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        // NOTE: chunkingDelegate is ignored for Azure - server-side chunking is used
        ArgumentNullException.ThrowIfNull(files);

        var sw = Stopwatch.StartNew();
        try
        {
            var projectClient = GetProjectClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var fileClient = openAIClient.GetOpenAIFileClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();
            var allFileIds = new List<string>();
            var fileCount = 0;

            _logger.LogInformation(
                "Uploading multiple files to Azure vector store {VectorStoreId} (using Azure native chunking)",
                vectorStoreId);

            // Upload all files
            foreach (var (content, fileName) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation("Uploading file {FileName} from batch", fileName);

                // Reset stream position for upload
                if (content.CanSeek)
                {
                    content.Seek(0, SeekOrigin.Begin);
                }

                // Upload complete file to Azure AI Foundry (not chunks)
                ClientResult<OpenAIFile> uploadedFile = await fileClient.UploadFileAsync(
                    content,
                    fileName,
                    FileUploadPurpose.Assistants,
                    cancellationToken);

                var fileId = uploadedFile.Value.Id;
                allFileIds.Add(fileId);
                fileCount++;

                _logger.LogDebug(
                    "Uploaded file {FileName} with file ID: {FileId}",
                    fileName,
                    fileId);
            }

            _logger.LogInformation(
                "Adding {FileCount} files to Azure vector store {VectorStoreId}",
                fileCount,
                vectorStoreId);

            // Add all files to vector store - Azure will chunk and embed server-side
            foreach (var fileId in allFileIds)
            {
                await vectorStoreClient.AddFileToVectorStoreAsync(
                    vectorStoreId,
                    fileId,
                    cancellationToken);
            }

            // Wait for all files to be processed by Azure
            foreach (var fileId in allFileIds)
            {
                await WaitForFileProcessingAsync(providerName, vectorStoreId, fileId, vectorStoreClient, cancellationToken);
            }

            sw.Stop();

            // Note: Chunk count is not available client-side since Azure chunks server-side
            var estimatedChunks = fileCount; // Conservative estimate
            _telemetry.RecordDocumentIndexed(providerName, "batch", estimatedChunks);
            _telemetry.RecordIndexingDuration(providerName, sw.ElapsedMilliseconds, estimatedChunks, "azure-native");

            return allFileIds.AsReadOnly();
        }
        catch (VectorStoreException)
        {
            sw.Stop();
            throw; // Re-throw already transformed exceptions
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = $"Failed to add multiple files to Azure vector store {vectorStoreId}";
            _logger.LogError(ex, message);
            _telemetry.RecordError(providerName, ex.GetType().Name);
            throw new VectorStoreIndexingException(message, ex, providerName);
        }
    }

    /// <summary>
    /// Waits for a file to be fully indexed in the Azure vector store before returning.
    /// Uses configurable polling with optional exponential backoff.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use.</param>
    /// <param name="vectorStoreId">The unique identifier of the vector store.</param>
    /// <param name="fileId">The file ID to wait for indexing completion.</param>
    /// <param name="vectorStoreClient">Optional pre-resolved client; if null one is obtained from <see cref="GetProjectClient"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the file is indexed.</returns>
    /// <exception cref="VectorStoreIndexingException">Thrown when file indexing fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when file indexing is cancelled.</exception>
    /// <exception cref="TimeoutException">Thrown when indexing times out after max attempts.</exception>
    internal async Task WaitForFileProcessingAsync(
        string providerName,
        string vectorStoreId,
        string fileId,
        OpenAI.VectorStores.VectorStoreClient? vectorStoreClient = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (vectorStoreClient == null)
            {
                var projectClient = GetProjectClient(providerName);
                var openAIClient = projectClient.GetProjectOpenAIClient();
                var fileClient = openAIClient.GetOpenAIFileClient();
                vectorStoreClient = openAIClient.GetVectorStoreClient();
            }

            var maxAttempts = _indexingOptions.MaxWaitAttempts;
            var initialDelayMs = _indexingOptions.InitialWaitDelayMs;
            var useExponentialBackoff = _indexingOptions.UseExponentialBackoff;
            var maxDelayMs = _indexingOptions.MaxWaitDelayMs;

            _logger.LogInformation(
                "Waiting for Azure file {FileId} to be indexed in vector store {VectorStoreId} (MaxAttempts: {MaxAttempts}, InitialDelay: {InitialDelay}ms, ExponentialBackoff: {ExponentialBackoff})",
                fileId,
                vectorStoreId,
                maxAttempts,
                initialDelayMs,
                useExponentialBackoff);

            var currentDelayMs = initialDelayMs;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var vectorStoreFile = await vectorStoreClient.GetVectorStoreFileAsync(
                    vectorStoreId: vectorStoreId,
                    fileId: fileId,
                    cancellationToken: cancellationToken);

                var status = vectorStoreFile.Value.Status;
                _logger.LogDebug(
                    "Azure file {FileId} indexing status: {Status} (attempt {Attempt}/{MaxAttempts}, next wait: {Delay}ms)",
                    fileId,
                    status,
                    attempt + 1,
                    maxAttempts,
                    currentDelayMs);

                if (status == OpenAIVectorStoreStatus.Completed)
                {
                    _logger.LogInformation(
                        "Azure file {FileId} indexing completed successfully after {Attempts} attempts",
                        fileId,
                        attempt + 1);

                    return;
                }

                if (status == OpenAIVectorStoreStatus.Failed)
                {
                    // This occurs when Azure fails to process the file (e.g. image-based pdf index failure, unsupported format, etc.)
                    var lastError = vectorStoreFile.Value.LastError;
                    var errorMessage = $"Azure file indexing failed for {fileId} in vector store {vectorStoreId}. Error code: {lastError?.Code.ToString() ?? "unknown"}, message: {lastError?.Message ?? "unknown"}";
                    _logger.LogError(
                        "Azure file indexing failed for {FileId} in vector store {VectorStoreId}. Error code: {ErrorCode}, message: {ErrorMessage}",
                        fileId,
                        vectorStoreId,
                        lastError?.Code.ToString() ?? "unknown",
                        lastError?.Message ?? "unknown");
                    _telemetry.RecordError(providerName, "IndexingFailed");
                    throw new VectorStoreIndexingException(errorMessage, providerName);
                }

                if (status == OpenAIVectorStoreStatus.Cancelled)
                {
                    // This is Azure's server-side job cancellation (e.g. batch cancelled, vector store deleted mid-process)
                    // It is NOT a .NET CancellationToken cancellation — callers should handle this as an indexing failure
                    var errorMessage = $"Azure file indexing was cancelled by Azure (server-side) for {fileId} in vector store {vectorStoreId}";
                    _logger.LogError(
                        "Azure file indexing was cancelled by Azure (server-side) for {FileId} in vector store {VectorStoreId}",
                        fileId,
                        vectorStoreId);
                    _telemetry.RecordError(providerName, "IndexingCancelled");
                    throw new VectorStoreIndexingException(errorMessage, providerName);
                }

                // Wait before next check
                await Task.Delay(currentDelayMs, cancellationToken);

                // Apply exponential backoff if enabled
                if (useExponentialBackoff)
                {
                    currentDelayMs = Math.Min(currentDelayMs * 2, maxDelayMs);
                }
            }

            var timeoutMessage = $"Azure file indexing timed out for {fileId} after {maxAttempts} attempts. File may still be processing in the background.";
            _logger.LogError(timeoutMessage);
            _telemetry.RecordError(providerName, "IndexingTimeout");
            throw new TimeoutException(timeoutMessage);
        }
        catch (VectorStoreException)
        {
            throw; // Re-throw already transformed exceptions
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (TimeoutException)
        {
            throw; // Re-throw timeout
        }
        catch (Exception ex)
        {
            var message = $"Unexpected error while waiting for Azure file {fileId} indexing";
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

    /// <inheritdoc/>
    /// <remarks>
    /// Not supported for Azure - Azure uses file_search tool which handles retrieval internally.
    /// </remarks>
    public Task<IReadOnlyList<(string ChunkId, string Text)>> QuerySimilarChunksAsync(
        string providerName,
        string vectorStoreId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("QuerySimilarChunksAsync is not supported for Azure AI Foundry vector stores");
        return Task.FromResult<IReadOnlyList<(string ChunkId, string Text)>>(
            new List<(string, string)>() as IReadOnlyList<(string, string)>);
    }
}
