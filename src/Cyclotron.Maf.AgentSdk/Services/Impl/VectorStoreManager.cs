using Cyclotron.Maf.AgentSdk.Options;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Files;
using OpenAI.VectorStores;
using System.ClientModel;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Manages vector store lifecycle for AI agent document processing workflows using Azure.AI.Projects V2 API.
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
            var projectClient = _clientFactory.GetClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();

            // Each workflow execution creates its own vector store
            // No need to search for existing ones since the key is unique per request
            _logger.LogInformation("Creating new vector store for workflow with key: {MetadataKey}", key);
            
            // Note: Metadata is read-only in SDK, so we can't set custom metadata via creation options
            // Metadata would need to be set via separate update call if supported

            ClientResult<VectorStore> vectorStoreResponse = await vectorStoreClient.CreateVectorStoreAsync(
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
        var projectClient = _clientFactory.GetClient(providerName);
        var openAIClient = projectClient.GetProjectOpenAIClient();
        var vectorStoreClient = openAIClient.GetVectorStoreClient();
        var fileClient = openAIClient.GetOpenAIFileClient();

        _logger.LogInformation("Cleaning up vector store: {VectorStoreId}", vectorStoreId);

        try
        {
            // Get all files in the vector store
            await foreach (var file in vectorStoreClient.GetVectorStoreFilesAsync(vectorStoreId, cancellationToken: cancellationToken))
            {
                try
                {
                    await fileClient.DeleteFileAsync(file.FileId, cancellationToken);
                    _logger.LogDebug("Deleted file: {FileId}", file.FileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {FileId}", file.FileId);
                }
            }

            // Delete the vector store
            await vectorStoreClient.DeleteVectorStoreAsync(vectorStoreId, cancellationToken);
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
            var projectClient = _clientFactory.GetClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var fileClient = openAIClient.GetOpenAIFileClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();

            _logger.LogInformation("Uploading file {FileName} to vector store {VectorStoreId}", fileName, vectorStoreId);

            // Save stream to temporary file since SDK expects file path
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await fileContent.CopyToAsync(fileStream, cancellationToken);
                }

                // Upload file to Azure AI Foundry
                ClientResult<OpenAIFile> uploadedFile = await fileClient.UploadFileAsync(
                    filePath: tempFilePath,
                    purpose: FileUploadPurpose.Assistants);

                _logger.LogInformation("Uploaded file {FileName} with ID: {FileId}", fileName, uploadedFile.Value.Id);

                // Add file to vector store
                await vectorStoreClient.AddFileToVectorStoreAsync(
                    vectorStoreId: vectorStoreId,
                    fileId: uploadedFile.Value.Id,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Added file {FileId} to vector store {VectorStoreId}", uploadedFile.Value.Id, vectorStoreId);

                // Wait for file to be processed
                await WaitForFileProcessingAsync(providerName, vectorStoreId, uploadedFile.Value.Id, cancellationToken);

                return uploadedFile.Value.Id;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }
            }
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
            var projectClient = _clientFactory.GetClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var fileClient = openAIClient.GetOpenAIFileClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();
            var fileIds = new List<string>();
            var tempFiles = new List<string>();

            _logger.LogInformation("Uploading multiple files to vector store {VectorStoreId}", vectorStoreId);

            try
            {
                // Upload all files
                foreach (var (content, fileName) in files)
                {
                    // Save stream to temporary file since SDK expects file path
                    var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
                    tempFiles.Add(tempFilePath);

                    using (var fileStream = File.Create(tempFilePath))
                    {
                        await content.CopyToAsync(fileStream, cancellationToken);
                    }

                    ClientResult<OpenAIFile> uploadedFile = await fileClient.UploadFileAsync(
                        filePath: tempFilePath,
                        purpose: FileUploadPurpose.Assistants);

                    _logger.LogInformation("Uploaded file {FileName} with ID: {FileId} to vector store {VectorStoreId}", fileName, uploadedFile.Value.Id, vectorStoreId);
                    fileIds.Add(uploadedFile.Value.Id);
                }

                // Add all files to vector store
                foreach (var fileId in fileIds)
                {
                    await vectorStoreClient.AddFileToVectorStoreAsync(
                        vectorStoreId: vectorStoreId,
                        fileId: fileId,
                        cancellationToken: cancellationToken);
                }

                _logger.LogInformation("Added {FileCount} files to vector store {VectorStoreId}", fileIds.Count, vectorStoreId);

                // Wait for all files to be processed
                foreach (var fileId in fileIds)
                {
                    await WaitForFileProcessingAsync(providerName, vectorStoreId, fileId, cancellationToken);
                }

                return fileIds.AsReadOnly();
            }
            finally
            {
                // Clean up temp files
                foreach (var tempFile in tempFiles)
                {
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                            // Best effort cleanup
                        }
                    }
                }
            }
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
            var projectClient = _clientFactory.GetClient(providerName);
            var openAIClient = projectClient.GetProjectOpenAIClient();
            var vectorStoreClient = openAIClient.GetVectorStoreClient();

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
                var vectorStoreFile = await vectorStoreClient.GetVectorStoreFileAsync(
                    vectorStoreId: vectorStoreId,
                    fileId: fileId,
                    cancellationToken: cancellationToken);

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
