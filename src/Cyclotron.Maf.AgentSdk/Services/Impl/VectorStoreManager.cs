using Cyclotron.Maf.AgentSdk.Options;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Manages vector store lifecycle for AI agent document processing workflows.
/// Provides operations for creating, managing, and cleaning up vector stores
/// used by AI agents for file search and retrieval operations.
/// </summary>
/// <remarks>
/// <para>
/// Each workflow execution creates its own vector store to ensure isolation.
/// Files are uploaded and indexed before agents can query them.
/// </para>
/// <para>
/// The indexing process uses configurable polling with optional exponential backoff
/// to wait for files to be fully indexed before returning.
/// </para>
/// </remarks>
public class VectorStoreManager(
    ILogger<VectorStoreManager> logger,
    IPersistentAgentsClientFactory clientFactory,
    IOptions<ModelProviderOptions> providerOptions) : IVectorStoreManager
{
    private readonly ILogger<VectorStoreManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPersistentAgentsClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly VectorStoreIndexingOptions _indexingOptions = providerOptions?.Value?.VectorStoreIndexing ?? new VectorStoreIndexingOptions();

    /// <summary>
    /// Gets a <see cref="PersistentAgentsClient"/> from the <see cref="AIProjectClient"/> for the specified provider.
    /// </summary>
    private PersistentAgentsClient GetPersistentAgentsClient(string providerName)
    {
        var projectClient = _clientFactory.GetClient(providerName);
        return projectClient.GetPersistentAgentsClient();
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
            var client = GetPersistentAgentsClient(providerName);

            // Each workflow execution creates its own vector store
            // No need to search for existing ones since the key is unique per request
            _logger.LogInformation("Creating new vector store for workflow with key: {MetadataKey}", key);
            var metadata = new Dictionary<string, string>
            {
                { key, bool.TrueString },
                { "purpose", purpose },
                { "created", DateTime.UtcNow.ToString("O") }
            };

            var vectorStoreResponse = await client.VectorStores.CreateVectorStoreAsync(
                name: name,
                metadata: metadata,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created vector store: {VectorStoreId}", vectorStoreResponse.Value.Id);

            return vectorStoreResponse.Value.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create vector store with key: {MetadataKey}", key);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CleanupVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        var client = GetPersistentAgentsClient(providerName);

        _logger.LogInformation("Cleaning up vector store: {VectorStoreId}", vectorStoreId);

        try
        {
            // Get all files in the vector store
            await foreach (var file in client.VectorStores.GetVectorStoreFilesAsync(vectorStoreId, cancellationToken: cancellationToken))
            {
                try
                {
                    await client.Files.DeleteFileAsync(file.Id, cancellationToken);
                    _logger.LogDebug("Deleted file: {FileId}", file.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {FileId}", file.Id);
                }
            }

            // Delete the vector store
            await client.VectorStores.DeleteVectorStoreAsync(vectorStoreId, cancellationToken);
            _logger.LogInformation("Deleted vector store: {VectorStoreId}", vectorStoreId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup vector store: {VectorStoreId}", vectorStoreId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetPersistentAgentsClient(providerName);

            _logger.LogInformation("Uploading file {FileName} to vector store {VectorStoreId}", fileName, vectorStoreId);

            // Upload file to Azure AI Foundry
            var uploadedFile = await client.Files.UploadFileAsync(
                fileContent,
                PersistentAgentFilePurpose.Agents,
                fileName,
                cancellationToken);

            _logger.LogInformation("Uploaded file {FileName} with ID: {FileId}", fileName, uploadedFile.Value.Id);

            // Add file to vector store
            await client.VectorStores.CreateVectorStoreFileBatchAsync(
                vectorStoreId,
                [uploadedFile.Value.Id],
                cancellationToken: cancellationToken);

            _logger.LogInformation("Added file {FileId} to vector store {VectorStoreId}", uploadedFile.Value.Id, vectorStoreId);

            // Wait for file to be processed
            await WaitForFileProcessingAsync(providerName, vectorStoreId, uploadedFile.Value.Id, cancellationToken);

            return uploadedFile.Value.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add file {FileName} to vector store {VectorStoreId}", fileName, vectorStoreId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetPersistentAgentsClient(providerName);
            var fileIds = new List<string>();

            _logger.LogInformation("Uploading multiple files to vector store {VectorStoreId}", vectorStoreId);

            // Upload all files
            foreach (var (content, fileName) in files)
            {
                var uploadedFile = await client.Files.UploadFileAsync(
                    content,
                    PersistentAgentFilePurpose.Agents,
                    fileName,
                    cancellationToken);

                _logger.LogInformation("Uploaded file {FileName} with ID: {FileId} to vector store {VectorStoreId}", fileName, uploadedFile.Value.Id, vectorStoreId);
                fileIds.Add(uploadedFile.Value.Id);
            }

            // Add all files to vector store in a batch
            await client.VectorStores.CreateVectorStoreFileBatchAsync(
                vectorStoreId,
                fileIds,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Added {FileCount} files to vector store {VectorStoreId}", fileIds.Count, vectorStoreId);

            // Wait for all files to be processed
            foreach (var fileId in fileIds)
            {
                await WaitForFileProcessingAsync(providerName, vectorStoreId, fileId, cancellationToken);
            }

            return fileIds.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add multiple files to vector store {VectorStoreId}", vectorStoreId);
            throw;
        }
    }

    /// <summary>
    /// Waits for a file to be fully indexed in the vector store before returning.
    /// Uses configurable polling with optional exponential backoff.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use.</param>
    /// <param name="vectorStoreId">The unique identifier of the vector store.</param>
    /// <param name="fileId">The file ID to wait for indexing completion.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the file is indexed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when file indexing fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when file indexing is cancelled.</exception>
    /// <exception cref="TimeoutException">Thrown when indexing times out after max attempts.</exception>
    public async Task WaitForFileProcessingAsync(
        string providerName,
        string vectorStoreId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetPersistentAgentsClient(providerName);
            var maxAttempts = _indexingOptions.MaxWaitAttempts;
            var initialDelayMs = _indexingOptions.InitialWaitDelayMs;
            var useExponentialBackoff = _indexingOptions.UseExponentialBackoff;
            var maxDelayMs = _indexingOptions.MaxWaitDelayMs;

            _logger.LogInformation(
                "Waiting for file {FileId} to be indexed in vector store {VectorStoreId} (MaxAttempts: {MaxAttempts}, InitialDelay: {InitialDelay}ms, ExponentialBackoff: {ExponentialBackoff})",
                fileId,
                vectorStoreId,
                maxAttempts,
                initialDelayMs,
                useExponentialBackoff);

            var currentDelayMs = initialDelayMs;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var vectorStoreFile = await client.VectorStores.GetVectorStoreFileAsync(
                    vectorStoreId,
                    fileId,
                    cancellationToken);

                var status = vectorStoreFile.Value.Status;
                _logger.LogDebug(
                    "File {FileId} indexing status: {Status} (attempt {Attempt}/{MaxAttempts}, next wait: {Delay}ms)",
                    fileId,
                    status,
                    attempt + 1,
                    maxAttempts,
                    currentDelayMs);

                if (status == VectorStoreFileStatus.Completed)
                {
                    _logger.LogInformation(
                        "File {FileId} indexing completed successfully after {Attempts} attempts",
                        fileId,
                        attempt + 1);
                    return;
                }

                if (status == VectorStoreFileStatus.Failed)
                {
                    var errorMessage = $"File indexing failed for {fileId} in vector store {vectorStoreId}";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                if (status == VectorStoreFileStatus.Cancelled)
                {
                    var errorMessage = $"File indexing was cancelled for {fileId} in vector store {vectorStoreId}";
                    _logger.LogWarning(errorMessage);
                    throw new OperationCanceledException(errorMessage);
                }

                // Wait before next check
                await Task.Delay(currentDelayMs, cancellationToken);

                // Apply exponential backoff if enabled
                if (useExponentialBackoff)
                {
                    currentDelayMs = Math.Min(currentDelayMs * 2, maxDelayMs);
                }
            }

            var timeoutMessage = $"File indexing timed out for {fileId} after {maxAttempts} attempts. File may still be processing in the background.";
            _logger.LogError(timeoutMessage);
            throw new TimeoutException(timeoutMessage);
        }
        catch (Exception ex) when (ex is not TimeoutException && ex is not InvalidOperationException && ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Unexpected error while waiting for file {FileId} indexing in vector store {VectorStoreId}",
                fileId,
                vectorStoreId);
            throw;
        }
    }
}
