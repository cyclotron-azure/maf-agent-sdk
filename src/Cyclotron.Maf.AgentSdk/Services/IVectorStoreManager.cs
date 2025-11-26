namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Manages vector store lifecycle for AI agent document processing workflows.
/// Provides operations for creating, managing, and cleaning up vector stores
/// used by AI agents for file search and retrieval operations.
/// </summary>
public interface IVectorStoreManager
{
    /// <summary>
    /// Gets an existing shared vector store or creates a new one based on the specified parameters.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="key">Metadata key used to identify and retrieve the vector store.</param>
    /// <param name="purpose">Purpose description for the vector store (e.g., "HOA document classification").</param>
    /// <param name="name">Display name for the vector store.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the ID of the existing or newly created vector store.
    /// </returns>
    Task<string> GetOrCreateSharedVectorStoreAsync(
        string providerName,
        string key,
        string purpose,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up and deletes the specified vector store and its associated resources.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="vectorStoreId">The unique identifier of the vector store to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    Task CleanupVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to Azure AI Foundry and adds it to the specified vector store.
    /// The method waits for the file to be fully indexed before returning.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="vectorStoreId">The unique identifier of the target vector store.</param>
    /// <param name="fileContent">The stream containing the file content to upload.</param>
    /// <param name="fileName">The name of the file being uploaded.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the ID of the uploaded file in the vector store.
    /// </returns>
    Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads multiple files to Azure AI Foundry and adds them to the specified vector store.
    /// The method waits for all files to be fully indexed before returning.
    /// </summary>
    /// <param name="providerName">Name of the model provider to use (e.g., "azure_foundry").</param>
    /// <param name="vectorStoreId">The unique identifier of the target vector store.</param>
    /// <param name="files">A collection of tuples containing the file content stream and file name for each file to upload.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a read-only list of file IDs for the uploaded files in the vector store.
    /// </returns>
    Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        CancellationToken cancellationToken = default);
}
