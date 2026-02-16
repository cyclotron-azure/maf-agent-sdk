using Azure.AI.Agents.Persistent;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="PersistentAgentsClientFactory"/> class.
/// Tests constructor validation, provider lookup, and delegation to IProviderClientFactory.
/// </summary>
public class PersistentAgentsClientFactoryTests
{
    private readonly Mock<ILogger<PersistentAgentsClientFactory>> _mockLogger;
    private readonly Mock<IProviderClientFactory> _mockProviderFactory;

    public PersistentAgentsClientFactoryTests()
    {
        _mockLogger = new Mock<ILogger<PersistentAgentsClientFactory>>();
        _mockProviderFactory = new Mock<IProviderClientFactory>();
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentAgentsClientFactory(null!, _mockProviderFactory.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when providerFactory is null")]
    public void Constructor_NullProviderFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentAgentsClientFactory(_mockLogger.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerFactory");
    }

    [Fact(DisplayName = "Constructor should create instance with valid dependencies")]
    public void Constructor_ValidDependencies_CreatesInstance()
    {
        // Act
        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region GetClient Tests

    [Fact(DisplayName = "GetClient should throw ArgumentException when providerName is null")]
    public void GetClient_NullProviderName_ThrowsArgumentException()
    {
        // Arrange
        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Act
        var act = () => factory.GetClient(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("providerName");
    }

    [Fact(DisplayName = "GetClient should throw ArgumentException when providerName is empty")]
    public void GetClient_EmptyProviderName_ThrowsArgumentException()
    {
        // Arrange
        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Act
        var act = () => factory.GetClient(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("providerName");
    }

    [Fact(DisplayName = "GetClient should throw InvalidOperationException when provider is not Azure compatible")]
    public void GetClient_NonAzureProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = new Mock<IModelProvider>();
        mockProvider.Setup(p => p.ProviderType).Returns("ollama");
        
        _mockProviderFactory
            .Setup(f => f.GetProvider("test_ollama"))
            .Returns(mockProvider.Object);

        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Act
        var act = () => factory.GetClient("test_ollama");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not compatible with Azure AI Agents API*");
    }

    [Fact(DisplayName = "GetClient should throw InvalidOperationException when provider returns non-PersistentAgentsClient")]
    public void GetClient_ProviderReturnsWrongType_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = new Mock<IModelProvider>();
        mockProvider.Setup(p => p.ProviderType).Returns("azure_foundry");
        mockProvider.Setup(p => p.CreateClient()).Returns(new object()); // Wrong type

        _mockProviderFactory
            .Setup(f => f.GetProvider("test_provider"))
            .Returns(mockProvider.Object);

        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Act
        var act = () => factory.GetClient("test_provider");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*did not return a PersistentAgentsClient instance*");
    }

    [Fact(DisplayName = "GetClient should delegate to IProviderClientFactory.GetProvider")]
    public void GetClient_ValidProvider_DelegatesToProviderFactory()
    {
        // Arrange
        var mockProvider = new Mock<IModelProvider>();
        mockProvider.Setup(p => p.ProviderType).Returns("azure_foundry");
        
        var mockClient = new Mock<PersistentAgentsClient>("https://test.com", new Mock<Azure.Core.TokenCredential>().Object);
        mockProvider.Setup(p => p.CreateClient()).Returns(mockClient.Object);

        _mockProviderFactory
            .Setup(f => f.GetProvider("test_provider"))
            .Returns(mockProvider.Object);

        var factory = new PersistentAgentsClientFactory(_mockLogger.Object, _mockProviderFactory.Object);

        // Act
        factory.GetClient("test_provider");

        // Assert
        _mockProviderFactory.Verify(f => f.GetProvider("test_provider"), Times.Once);
        mockProvider.Verify(p => p.CreateClient(), Times.Once);
    }

    #endregion
}
