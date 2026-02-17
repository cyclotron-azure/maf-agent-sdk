namespace Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;

/// <summary>
/// Exception thrown when vector store cleanup or deletion operations fail.
/// </summary>
public class VectorStoreCleanupException : VectorStoreException
{
    /// <summary>
    /// Gets the name of the provider where the cleanup failed.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Gets the ID of the vector store that failed to clean up.
    /// </summary>
    public string? VectorStoreId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreCleanupException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public VectorStoreCleanupException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreCleanupException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VectorStoreCleanupException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreCleanupException"/> class with context information.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="providerName">The name of the provider where the cleanup failed.</param>
    /// <param name="vectorStoreId">The ID of the vector store that failed to clean up.</param>
    public VectorStoreCleanupException(string message, string? providerName, string? vectorStoreId)
        : base(message)
    {
        ProviderName = providerName;
        VectorStoreId = vectorStoreId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreCleanupException"/> class with context information and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="providerName">The name of the provider where the cleanup failed.</param>
    /// <param name="vectorStoreId">The ID of the vector store that failed to clean up.</param>
    public VectorStoreCleanupException(string message, Exception? innerException, string? providerName, string? vectorStoreId)
        : base(message, innerException)
    {
        ProviderName = providerName;
        VectorStoreId = vectorStoreId;
    }
}
