namespace Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;

/// <summary>
/// Exception thrown when vector store provider configuration is invalid or missing.
/// </summary>
public class VectorStoreConfigurationException : VectorStoreException
{
    /// <summary>
    /// Gets the name of the provider with invalid configuration.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public VectorStoreConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreConfigurationException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VectorStoreConfigurationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreConfigurationException"/> class with provider context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="providerName">The name of the provider with invalid configuration.</param>
    public VectorStoreConfigurationException(string message, string? providerName)
        : base(message)
    {
        ProviderName = providerName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreConfigurationException"/> class with provider context and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="providerName">The name of the provider with invalid configuration.</param>
    public VectorStoreConfigurationException(string message, Exception? innerException, string? providerName)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }
}
