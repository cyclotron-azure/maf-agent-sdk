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
/// Unit tests for the <see cref="AIProjectClientFactory"/> class.
/// Tests constructor validation, provider lookup, and credential creation logic.
/// </summary>
public class PersistentAgentsClientFactoryTests
{
    private readonly Mock<ILogger<AIProjectClientFactory>> _mockLogger;

    public PersistentAgentsClientFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AIProjectClientFactory>>();
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
        var act = () => new AIProjectClientFactory(null!, providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when providerOptions is null")]
    public void Constructor_NullProviderOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AIProjectClientFactory(_mockLogger.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerOptions");
    }

    [Fact(DisplayName = "Constructor should throw InvalidOperationException when no providers configured")]
    public void Constructor_NoProvidersConfigured_ThrowsInvalidOperationException()
    {
        // Arrange - Empty providers dictionary
        var providerOptions = CreateProviderOptions([]);

        // Act
        var act = () => new AIProjectClientFactory(_mockLogger.Object, providerOptions);

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
        var factory = new AIProjectClientFactory(_mockLogger.Object, providerOptions);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should accept multiple providers")]
    public void Constructor_MultipleProviders_CreatesInstance()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://foundry.azure.com",
                DeploymentName = "gpt-4"
            },
            ["azure_openai"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_openai",
                Endpoint = "https://openai.azure.com",
                DeploymentName = "gpt-35-turbo",
                ApiKey = "test-key"
            }
        };

        // Act
        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region GetClient Tests

    [Theory(DisplayName = "GetClient should throw ArgumentException when providerName is null or empty")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetClient_NullOrEmptyProviderName_ThrowsArgumentException(string? providerName)
    {
        // Arrange
        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions());

        // Act
        var act = () => factory.GetClient(providerName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Provider name cannot be null or empty*");
    }

    [Fact(DisplayName = "GetClient should throw InvalidOperationException when provider not found")]
    public void GetClient_ProviderNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions());

        // Act
        var act = () => factory.GetClient("non_existent_provider");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in configuration*")
            .WithMessage("*Available providers*");
    }

    [Fact(DisplayName = "GetClient should throw InvalidOperationException when provider configuration is invalid")]
    public void GetClient_InvalidProviderConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange - Provider with missing required fields
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["invalid_provider"] = new ModelProviderDefinitionOptions
            {
                Type = "", // Invalid - empty type
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };

        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Act
        var act = () => factory.GetClient("invalid_provider");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configuration is invalid*");
    }

    [Fact(DisplayName = "GetClient should throw InvalidOperationException when azure_openai has no API key")]
    public void GetClient_AzureOpenAIWithoutApiKey_ThrowsInvalidOperationException()
    {
        // Arrange - azure_openai type requires API key
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_openai_no_key"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_openai",
                Endpoint = "https://test.openai.azure.com",
                DeploymentName = "gpt-35-turbo",
                ApiKey = null // Missing API key
            }
        };

        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Act
        var act = () => factory.GetClient("azure_openai_no_key");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configuration is invalid*");
    }

    [Fact(DisplayName = "GetClient should create client for valid azure_foundry provider")]
    public void GetClient_ValidAzureFoundryProvider_CreatesClient()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };

        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Act
        var client = factory.GetClient("azure_foundry");

        // Assert
        client.Should().NotBeNull();
    }

    [Fact(DisplayName = "GetClient should create client for valid azure_openai provider with API key")]
    public void GetClient_ValidAzureOpenAIProvider_CreatesClient()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_openai"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_openai",
                Endpoint = "https://test.openai.azure.com",
                DeploymentName = "gpt-35-turbo",
                ApiKey = "test-api-key"
            }
        };

        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Act
        var client = factory.GetClient("azure_openai");

        // Assert
        client.Should().NotBeNull();
    }

    [Fact(DisplayName = "GetClient should create new client instance per call")]
    public void GetClient_MultipleCalls_ReturnsNewInstances()
    {
        // Arrange
        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions());

        // Act
        var client1 = factory.GetClient("azure_foundry");
        var client2 = factory.GetClient("azure_foundry");

        // Assert - Should be different instances
        client1.Should().NotBeNull();
        client2.Should().NotBeNull();
        client1.Should().NotBeSameAs(client2);
    }

    [Fact(DisplayName = "GetClient should support case-sensitive provider names")]
    public void GetClient_CaseSensitiveProviderName_FindsCorrectProvider()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["Azure_Foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };

        var factory = new AIProjectClientFactory(
            _mockLogger.Object,
            CreateProviderOptions(providers));

        // Act - Try exact case
        var client = factory.GetClient("Azure_Foundry");

        // Assert
        client.Should().NotBeNull();

        // Act - Try different case (should fail since dictionary is case-sensitive by default)
        var act = () => factory.GetClient("azure_foundry");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in configuration*");
    }

    #endregion
}
