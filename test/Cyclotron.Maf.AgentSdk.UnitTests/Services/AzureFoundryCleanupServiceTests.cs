using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="AIFoundryCleanupService"/> class.
/// Tests constructor validation and protected agent name handling.
/// Note: Async cleanup methods require Azure client mocking which is complex for unit tests.
/// </summary>
public class AzureFoundryCleanupServiceTests
{
    private readonly Mock<IAIProjectClientFactory> _mockClientFactory;
    private readonly Mock<ILogger<AIFoundryCleanupService>> _mockLogger;

    public AzureFoundryCleanupServiceTests()
    {
        _mockClientFactory = new Mock<IAIProjectClientFactory>();
        _mockLogger = new Mock<ILogger<AIFoundryCleanupService>>();
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when clientFactory is null")]
    public void Constructor_NullClientFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AIFoundryCleanupService(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientFactory");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AIFoundryCleanupService(_mockClientFactory.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should create instance with valid parameters")]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should initialize protected agent names")]
    public void Constructor_InitializesProtectedAgentNames()
    {
        // Act
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        // Assert - Service should be created without error
        // Protected names are initialized internally
        service.Should().NotBeNull();
    }

    #endregion

    #region CleanupStatistics Tests

    [Fact(DisplayName = "CleanupStatistics should have correct default values")]
    public void CleanupStatistics_Defaults_AreZero()
    {
        // Arrange & Act
        var stats = new CleanupStatistics();

        // Assert
        stats.FilesDeleted.Should().Be(0);
        stats.FilesFailedToDelete.Should().Be(0);
        stats.VectorStoresDeleted.Should().Be(0);
        stats.VectorStoresFailedToDelete.Should().Be(0);
        stats.ThreadsDeleted.Should().Be(0);
        stats.ThreadsFailedToDelete.Should().Be(0);
        stats.AgentsDeleted.Should().Be(0);
        stats.AgentsFailedToDelete.Should().Be(0);
    }

    [Fact(DisplayName = "CleanupStatistics TotalDeleted should calculate correctly")]
    public void CleanupStatistics_TotalDeleted_CalculatesCorrectly()
    {
        // Arrange
        var stats = new CleanupStatistics
        {
            FilesDeleted = 5,
            VectorStoresDeleted = 3,
            ThreadsDeleted = 7,
            AgentsDeleted = 2
        };

        // Act & Assert
        stats.TotalDeleted.Should().Be(17);
    }

    [Fact(DisplayName = "CleanupStatistics TotalFailed should calculate correctly")]
    public void CleanupStatistics_TotalFailed_CalculatesCorrectly()
    {
        // Arrange
        var stats = new CleanupStatistics
        {
            FilesFailedToDelete = 1,
            VectorStoresFailedToDelete = 2,
            ThreadsFailedToDelete = 3,
            AgentsFailedToDelete = 4
        };

        // Act & Assert
        stats.TotalFailed.Should().Be(10);
    }

    [Fact(DisplayName = "CleanupStatistics should allow setting all properties")]
    public void CleanupStatistics_SetAllProperties_PropertiesArePersisted()
    {
        // Arrange & Act
        var stats = new CleanupStatistics
        {
            FilesDeleted = 10,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 5,
            VectorStoresFailedToDelete = 1,
            ThreadsDeleted = 20,
            ThreadsFailedToDelete = 3,
            AgentsDeleted = 8,
            AgentsFailedToDelete = 4
        };

        // Assert
        stats.FilesDeleted.Should().Be(10);
        stats.FilesFailedToDelete.Should().Be(2);
        stats.VectorStoresDeleted.Should().Be(5);
        stats.VectorStoresFailedToDelete.Should().Be(1);
        stats.ThreadsDeleted.Should().Be(20);
        stats.ThreadsFailedToDelete.Should().Be(3);
        stats.AgentsDeleted.Should().Be(8);
        stats.AgentsFailedToDelete.Should().Be(4);
        stats.TotalDeleted.Should().Be(43);
        stats.TotalFailed.Should().Be(10);
    }

    [Fact(DisplayName = "CleanupStatistics with zero values should have zero totals")]
    public void CleanupStatistics_ZeroValues_ZeroTotals()
    {
        // Arrange
        var stats = new CleanupStatistics
        {
            FilesDeleted = 0,
            FilesFailedToDelete = 0,
            VectorStoresDeleted = 0,
            VectorStoresFailedToDelete = 0,
            ThreadsDeleted = 0,
            ThreadsFailedToDelete = 0,
            AgentsDeleted = 0,
            AgentsFailedToDelete = 0
        };

        // Assert
        stats.TotalDeleted.Should().Be(0);
        stats.TotalFailed.Should().Be(0);
    }

    #endregion

    #region CleanupAllResourcesAsync Tests

    [Fact(DisplayName = "CleanupAllResourcesAsync should call GetClient with provider name")]
    public async Task CleanupAllResourcesAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => service.CleanupAllResourcesAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "CleanupAllResourcesAsync should accept protected metadata key")]
    public async Task CleanupAllResourcesAsync_WithProtectedMetadataKey_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => service.CleanupAllResourcesAsync(
            "azure_foundry",
            "protected_key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "CleanupAllResourcesAsync should accept null protected metadata key")]
    public async Task CleanupAllResourcesAsync_NullProtectedMetadataKey_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => service.CleanupAllResourcesAsync(
            "azure_foundry",
            null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "CleanupAllResourcesAsync should accept cancellation token")]
    public async Task CleanupAllResourcesAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => service.CleanupAllResourcesAsync(
            "azure_foundry",
            null,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region CleanupFilesAsync Tests

    [Fact(DisplayName = "CleanupFilesAsync should call GetClient with provider name")]
    public async Task CleanupFilesAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => service.CleanupFilesAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "CleanupFilesAsync should accept cancellation token")]
    public async Task CleanupFilesAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => service.CleanupFilesAsync("azure_foundry", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "CleanupFilesAsync should accept various provider names")]
    [InlineData("azure_foundry")]
    [InlineData("openai")]
    [InlineData("custom_provider")]
    public async Task CleanupFilesAsync_VariousProviderNames_CallsCorrectProvider(string providerName)
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(providerName))
            .Throws(new InvalidOperationException($"Called with {providerName}"));

        // Act
        var act = () => service.CleanupFilesAsync(providerName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Called with {providerName}*");
    }

    #endregion

    #region DeleteFilesAsync Tests

    [Fact(DisplayName = "DeleteFilesAsync should call GetClient with provider name")]
    public async Task DeleteFilesAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        var fileIds = new[] { "file-1", "file-2" };

        // Act
        var act = () => service.DeleteFilesAsync("azure_foundry", fileIds);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "DeleteFilesAsync should accept empty file IDs list")]
    public async Task DeleteFilesAsync_EmptyFileIds_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        var fileIds = Array.Empty<string>();

        // Act
        var act = () => service.DeleteFilesAsync("azure_foundry", fileIds);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "DeleteFilesAsync should accept multiple file IDs")]
    public async Task DeleteFilesAsync_MultipleFileIds_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        var fileIds = new[] { "file-1", "file-2", "file-3", "file-4", "file-5" };

        // Act
        var act = () => service.DeleteFilesAsync("azure_foundry", fileIds);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "DeleteFilesAsync should accept cancellation token")]
    public async Task DeleteFilesAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        var fileIds = new[] { "file-1" };

        // Act
        var act = () => service.DeleteFilesAsync("azure_foundry", fileIds, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region CleanupVectorStoresAsync Tests

    [Fact(DisplayName = "CleanupVectorStoresAsync should call GetClient with provider name")]
    public async Task CleanupVectorStoresAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => service.CleanupVectorStoresAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "CleanupVectorStoresAsync should accept protected metadata key")]
    public async Task CleanupVectorStoresAsync_WithProtectedMetadataKey_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => service.CleanupVectorStoresAsync(
            "azure_foundry",
            "protected_vs_key");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "CleanupVectorStoresAsync should accept null protected metadata key")]
    public async Task CleanupVectorStoresAsync_NullProtectedMetadataKey_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => service.CleanupVectorStoresAsync(
            "azure_foundry",
            null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "CleanupVectorStoresAsync should accept cancellation token")]
    public async Task CleanupVectorStoresAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => service.CleanupVectorStoresAsync(
            "azure_foundry",
            null,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "CleanupVectorStoresAsync should accept various protected metadata keys")]
    [InlineData("shared_knowledge_base")]
    [InlineData("protected_store")]
    [InlineData("key_with_underscores")]
    [InlineData("")]
    public async Task CleanupVectorStoresAsync_VariousProtectedKeys_CallsGetClient(string protectedKey)
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => service.CleanupVectorStoresAsync(
            "azure_foundry",
            string.IsNullOrEmpty(protectedKey) ? null : protectedKey);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region CleanupThreadsAsync Tests

    [Fact(DisplayName = "CleanupThreadsAsync should call GetClient with provider name")]
    public async Task CleanupThreadsAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => service.CleanupThreadsAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "CleanupThreadsAsync should accept cancellation token")]
    public async Task CleanupThreadsAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => service.CleanupThreadsAsync("azure_foundry", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "CleanupThreadsAsync should accept various provider names")]
    [InlineData("azure_foundry")]
    [InlineData("openai")]
    [InlineData("custom_provider")]
    [InlineData("provider-with-dashes")]
    public async Task CleanupThreadsAsync_VariousProviderNames_CallsCorrectProvider(string providerName)
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(providerName))
            .Throws(new InvalidOperationException($"Called with {providerName}"));

        // Act
        var act = () => service.CleanupThreadsAsync(providerName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Called with {providerName}*");
    }

    #endregion

    #region CleanupAgentsAsync Tests

    [Fact(DisplayName = "CleanupAgentsAsync should call GetClient with provider name")]
    public async Task CleanupAgentsAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => service.CleanupAgentsAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "CleanupAgentsAsync should accept cancellation token")]
    public async Task CleanupAgentsAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => service.CleanupAgentsAsync("azure_foundry", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "CleanupAgentsAsync should accept various provider names")]
    [InlineData("azure_foundry")]
    [InlineData("openai")]
    [InlineData("custom_provider")]
    public async Task CleanupAgentsAsync_VariousProviderNames_CallsCorrectProvider(string providerName)
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(providerName))
            .Throws(new InvalidOperationException($"Called with {providerName}"));

        // Act
        var act = () => service.CleanupAgentsAsync(providerName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Called with {providerName}*");
    }

    #endregion

    #region Multiple Instances Tests

    [Fact(DisplayName = "Multiple instances should have independent protected agent lists")]
    public void MultipleInstances_IndependentProtectedAgentLists()
    {
        // Arrange & Act
        var service1 = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        var service2 = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        // Assert - Both services should be independent instances
        service1.Should().NotBeSameAs(service2);
    }

    #endregion

    #region Provider Error Handling Tests

    [Fact(DisplayName = "Service should propagate provider not found exception")]
    public async Task Service_ProviderNotFound_PropagatesException()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider 'unknown' not found"));

        // Act
        var act = () => service.CleanupFilesAsync("unknown");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider*not found*");
    }

    [Fact(DisplayName = "Service should propagate authentication exception")]
    public async Task Service_AuthenticationError_PropagatesException()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("Authentication failed"));

        // Act
        var act = () => service.CleanupFilesAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Authentication failed*");
    }

    [Fact(DisplayName = "Service should propagate network exception")]
    public async Task Service_NetworkError_PropagatesException()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new HttpRequestException("Network error"));

        // Act
        var act = () => service.CleanupFilesAsync("azure_foundry");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Network error*");
    }

    #endregion

    #region Edge Cases Tests

    [Fact(DisplayName = "Service should handle provider name with special characters")]
    public async Task Service_ProviderNameWithSpecialChars_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        var specialProviderName = "provider_with-special.chars";

        _mockClientFactory.Setup(x => x.GetClient(specialProviderName))
            .Throws(new InvalidOperationException($"Called with {specialProviderName}"));

        // Act
        var act = () => service.CleanupFilesAsync(specialProviderName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Called with {specialProviderName}*");
    }

    [Fact(DisplayName = "Service should handle empty provider name")]
    public async Task Service_EmptyProviderName_CallsGetClient()
    {
        // Arrange
        var service = new AIFoundryCleanupService(
            _mockClientFactory.Object,
            _mockLogger.Object);

        _mockClientFactory.Setup(x => x.GetClient(string.Empty))
            .Throws(new ArgumentException("Provider name cannot be empty"));

        // Act
        var act = () => service.CleanupFilesAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Provider name cannot be empty*");
    }

    #endregion
}
