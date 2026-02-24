namespace Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;

/// <summary>
/// Base exception for vector store operations.
/// </summary>
public class VectorStoreException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public VectorStoreException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VectorStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
