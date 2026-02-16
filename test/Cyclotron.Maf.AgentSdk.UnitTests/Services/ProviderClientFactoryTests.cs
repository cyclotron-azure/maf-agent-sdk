using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="ProviderClientFactory"/> class.
/// Tests provider creation, validation, and factory patterns.
/// </summary>
public class ProviderClientFactoryTests
{
    private readonly Mock<ILogger<ProviderClientFactory>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;

    public ProviderClientFactoryTests()
    {
        _mockLogger = new Mock<ILogger<ProviderClientFactory>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        
        // Setup logger factory to return appropriate loggers
        _mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns((string name) => new Mock<ILogger>().Object);
    }

    private IOptions<ModelProviderOptions> CreateProviderOptions(
        Dictionary<string, ModelProviderDefinitionOptions>? providers = null)
    {
        var options = new ModelProviderOptions
        {
            Providers = providers ?? new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["azure_foundry"] = new ModelProviderDefinitionOptions
                {
                    Type = "azure_foundry",
                    Endpoint = "https://test.azure.com",
                    DeploymentName = "gpt-4"
                }
            }
        };
        return MsOptions.Create(options);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();

        // Act
        var act = () => new ProviderClientFactory(null!, _mockLoggerFactory.Object, providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when loggerFactory is null")]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();

        // Act
        var act = () => new ProviderClientFactory(_mockLogger.Object, null!, providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when providerOptions is null")]
    public void Constructor_NullProviderOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerOptions");
    }

    [Fact(DisplayName = "Constructor should throw InvalidOperationException when no providers configured")]
    public void Constructor_NoProvidersConfigured_ThrowsInvalidOperationException()
    {
        // Arrange - Empty providers dictionary
        var providerOptions = CreateProviderOptions(new Dictionary<string, ModelProviderDefinitionOptions>());

        // Act
        var act = () => new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No providers configured*");
    }

    [Fact(DisplayName = "Constructor should create instance with valid configuration")]
    public void Constructor_ValidConfiguration_CreatesInstance()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();

        // Act
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region GetProvider Tests

    [Fact(DisplayName = "GetProvider should throw ArgumentException when providerName is null")]
    public void GetProvider_NullProviderName_ThrowsArgumentException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var act = () => factory.GetProvider(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("providerName");
    }

    [Fact(DisplayName = "GetProvider should throw ArgumentException when providerName is empty")]
    public void GetProvider_EmptyProviderName_ThrowsArgumentException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var act = () => factory.GetProvider(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("providerName");
    }

    [Fact(DisplayName = "GetProvider should throw InvalidOperationException when provider not found")]
    public void GetProvider_ProviderNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var act = () => factory.GetProvider("nonexistent");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in configuration*");
    }

    [Fact(DisplayName = "GetProvider should create AzureFoundryProvider for azure_foundry type")]
    public void GetProvider_AzureFoundryType_CreatesAzureFoundryProvider()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["test_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };
        var providerOptions = CreateProviderOptions(providers);
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var provider = factory.GetProvider("test_foundry");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderType.Should().Be("azure_foundry");
        provider.Endpoint.Should().Be("https://test.azure.com");
        provider.DeploymentName.Should().Be("gpt-4");
    }

    [Fact(DisplayName = "GetProvider should create AzureOpenAIProvider for azure_openai type")]
    public void GetProvider_AzureOpenAIType_CreatesAzureOpenAIProvider()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["test_openai"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_openai",
                Endpoint = "https://test-openai.com",
                DeploymentName = "gpt-4o",
                ApiKey = "test-key"
            }
        };
        var providerOptions = CreateProviderOptions(providers);
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var provider = factory.GetProvider("test_openai");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderType.Should().Be("azure_openai");
        provider.Endpoint.Should().Be("https://test-openai.com");
        provider.DeploymentName.Should().Be("gpt-4o");
    }

    [Fact(DisplayName = "GetProvider should create OllamaProvider for ollama type")]
    public void GetProvider_OllamaType_CreatesOllamaProvider()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["test_ollama"] = new ModelProviderDefinitionOptions
            {
                Type = "ollama",
                Endpoint = "http://localhost:11434",
                DeploymentName = "llama2"
            }
        };
        var providerOptions = CreateProviderOptions(providers);
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var provider = factory.GetProvider("test_ollama");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderType.Should().Be("ollama");
        provider.Endpoint.Should().Be("http://localhost:11434");
        provider.DeploymentName.Should().Be("llama2");
    }

    [Fact(DisplayName = "GetProvider should throw NotSupportedException for unknown provider type")]
    public void GetProvider_UnknownType_ThrowsNotSupportedException()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["test_unknown"] = new ModelProviderDefinitionOptions
            {
                Type = "unknown_provider",
                Endpoint = "https://test.com",
                DeploymentName = "model"
            }
        };
        var providerOptions = CreateProviderOptions(providers);
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var act = () => factory.GetProvider("test_unknown");

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported*");
    }

    #endregion

    #region HasProvider Tests

    [Fact(DisplayName = "HasProvider should return true for existing provider")]
    public void HasProvider_ExistingProvider_ReturnsTrue()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var result = factory.HasProvider("azure_foundry");

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "HasProvider should return false for non-existing provider")]
    public void HasProvider_NonExistingProvider_ReturnsFalse()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var result = factory.HasProvider("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasProvider should return false for null provider name")]
    public void HasProvider_NullProviderName_ReturnsFalse()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();
        var factory = new ProviderClientFactory(_mockLogger.Object, _mockLoggerFactory.Object, providerOptions);

        // Act
        var result = factory.HasProvider(null!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
