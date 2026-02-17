namespace Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;

/// <summary>
/// Exception thrown when document chunking, embedding generation, or indexing operations fail.
/// </summary>
public class VectorStoreIndexingException : VectorStoreException
{
    /// <summary>
    /// Gets the name of the provider where the operation failed.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreIndexingException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public VectorStoreIndexingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreIndexingException"/> class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VectorStoreIndexingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreIndexingException"/> class with provider context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="providerName">The name of the provider where the operation failed.</param>
    public VectorStoreIndexingException(string message, string? providerName)
        : base(message)
    {
        ProviderName = providerName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreIndexingException"/> class with provider context and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="providerName">The name of the provider where the operation failed.</param>
    public VectorStoreIndexingException(string message, Exception? innerException, string? providerName)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }
}
