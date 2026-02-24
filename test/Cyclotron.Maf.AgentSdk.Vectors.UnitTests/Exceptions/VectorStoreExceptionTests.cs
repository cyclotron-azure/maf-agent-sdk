using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests.Exceptions;

/// <summary>
/// Unit tests for vector store exception hierarchy.
/// Tests proper exception construction and property assignment.
/// </summary>
public class VectorStoreExceptionTests
{
    [Fact(DisplayName = "VectorStoreException should initialize with message")]
    public void VectorStoreException_WithMessage_InitializesCorrectly()
    {
        // Arrange
        var message = "Test exception message";

        // Act
        var exception = new VectorStoreException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact(DisplayName = "VectorStoreException should preserve inner exception")]
    public void VectorStoreException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var message = "Outer error";

        // Act
        var exception = new VectorStoreException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact(DisplayName = "VectorStoreIndexingException should set ProviderName property")]
    public void VectorStoreIndexingException_WithProviderName_SetsProperty()
    {
        // Arrange
        var message = "Indexing failed";
        var providerName = "azure_foundry";

        // Act
        var exception = new VectorStoreIndexingException(message, providerName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(providerName, exception.ProviderName);
    }

    [Fact(DisplayName = "VectorStoreIndexingException should preserve inner exception and provider")]
    public void VectorStoreIndexingException_WithInnerExceptionAndProvider_PreservesBoth()
    {
        // Arrange
        var innerException = new HttpRequestException("HTTP error");
        var message = "Failed to generate embeddings";
        var providerName = "ollama";

        // Act
        var exception = new VectorStoreIndexingException(message, innerException, providerName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(providerName, exception.ProviderName);
    }

    [Fact(DisplayName = "VectorStoreCleanupException should set ProviderName and VectorStoreId properties")]
    public void VectorStoreCleanupException_WithProviderAndStoreId_SetsBothProperties()
    {
        // Arrange
        var message = "Cleanup failed";
        var providerName = "azure_foundry";
        var vectorStoreId = "vs_123456";

        // Act
        var exception = new VectorStoreCleanupException(message, providerName, vectorStoreId);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(providerName, exception.ProviderName);
        Assert.Equal(vectorStoreId, exception.VectorStoreId);
    }

    [Fact(DisplayName = "VectorStoreCleanupException should preserve inner exception")]
    public void VectorStoreCleanupException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new UnauthorizedAccessException("Access denied");
        var message = "Cannot delete vector store";
        var providerName = "azure_foundry";
        var vectorStoreId = "vs_789";

        // Act
        var exception = new VectorStoreCleanupException(message, innerException, providerName, vectorStoreId);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(providerName, exception.ProviderName);
        Assert.Equal(vectorStoreId, exception.VectorStoreId);
    }

    [Fact(DisplayName = "VectorStoreConfigurationException should set ProviderName property")]
    public void VectorStoreConfigurationException_WithProviderName_SetsProperty()
    {
        // Arrange
        var message = "Invalid configuration";
        var providerName = "invalid_provider";

        // Act
        var exception = new VectorStoreConfigurationException(message, providerName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(providerName, exception.ProviderName);
    }

    [Fact(DisplayName = "VectorStoreConfigurationException should preserve inner exception")]
    public void VectorStoreConfigurationException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new ArgumentException("Invalid argument");
        var message = "Provider configuration error";
        var providerName = "test_provider";

        // Act
        var exception = new VectorStoreConfigurationException(message, innerException, providerName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(providerName, exception.ProviderName);
    }

    [Fact(DisplayName = "Exception hierarchy should allow catching base VectorStoreException")]
    public void Exceptions_CatchAsBaseType_WorksCorrectly()
    {
        // Arrange
        var indexingException = new VectorStoreIndexingException("Indexing error", "provider1");
        var cleanupException = new VectorStoreCleanupException("Cleanup error", "provider2", "vs_123");
        var configException = new VectorStoreConfigurationException("Config error", "provider3");

        // Act & Assert
        Assert.IsAssignableFrom<VectorStoreException>(indexingException);
        Assert.IsAssignableFrom<VectorStoreException>(cleanupException);
        Assert.IsAssignableFrom<VectorStoreException>(configException);
    }
}
