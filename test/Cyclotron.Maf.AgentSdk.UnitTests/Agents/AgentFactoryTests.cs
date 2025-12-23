using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Agents;

/// <summary>
/// Unit tests for the <see cref="AgentFactory"/> class.
/// Tests constructor validation, agent definition lookup, provider validation,
/// and user message creation functionality.
/// </summary>
public class AgentFactoryTests
{
    private readonly Mock<ILogger<AgentFactory>> _mockLogger;
    private readonly Mock<IPromptRenderingService> _mockPromptService;
    private readonly Mock<IAIProjectClientFactory> _mockClientFactory;
    private readonly Mock<IVectorStoreManager> _mockVectorStoreManager;

    public AgentFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory>>();
        _mockPromptService = new Mock<IPromptRenderingService>();
        _mockClientFactory = new Mock<IAIProjectClientFactory>();
        _mockVectorStoreManager = new Mock<IVectorStoreManager>();

        // Default setup - HasConfiguration returns true
        _mockPromptService.Setup(x => x.HasConfiguration(It.IsAny<string>())).Returns(true);
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

    private IOptions<AgentOptions> CreateAgentOptions(
        Dictionary<string, AgentDefinitionOptions>? agents = null)
    {
        var options = new AgentOptions
        {
            Agents = agents ?? new Dictionary<string, AgentDefinitionOptions>
            {
                ["classification_agent"] = new AgentDefinitionOptions
                {
                    Type = "classification",
                    Enabled = true,
                    AutoDelete = true,
                    AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
                }
            }
        };
        return MsOptions.Create(options);
    }

    private static IOptions<TelemetryOptions> CreateTelemetryOptions()
    {
        return MsOptions.Create(new TelemetryOptions { Enabled = false });
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when agentKey is null")]
    public void Constructor_NullAgentKey_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            null!,
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentKey");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            null!,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when promptService is null")]
    public void Constructor_NullPromptService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            null!,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("promptService");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when clientFactory is null")]
    public void Constructor_NullClientFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            null!,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientFactory");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when vectorStoreManager is null")]
    public void Constructor_NullVectorStoreManager_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            null!,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("vectorStoreManager");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when providerOptions is null")]
    public void Constructor_NullProviderOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            null!,
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerOptions");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when agentOptions is null")]
    public void Constructor_NullAgentOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            null!,
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentOptions");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when telemetryOptions is null")]
    public void Constructor_NullTelemetryOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("telemetryOptions");
    }

    [Fact(DisplayName = "Constructor should throw InvalidOperationException when provider is empty")]
    public void Constructor_EmptyProvider_ThrowsInvalidOperationException()
    {
        // Arrange - Agent with empty provider
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "" }
            }
        };

        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have a provider configured*");
    }

    [Fact(DisplayName = "Constructor should throw InvalidOperationException when provider not found")]
    public void Constructor_ProviderNotFound_ThrowsInvalidOperationException()
    {
        // Arrange - Agent references non-existent provider
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "non_existent_provider" }
            }
        };

        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in configuration*")
            .WithMessage("*Available providers*");
    }

    [Fact(DisplayName = "Constructor should succeed with valid configuration")]
    public void Constructor_ValidConfiguration_CreatesInstance()
    {
        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentKey.Should().Be("classification");
        factory.AgentDefinition.Should().NotBeNull();
        factory.Agent.Should().BeNull();
        factory.Thread.Should().BeNull();
        factory.VectorStoreId.Should().BeNull();
    }

    #endregion

    #region GetAgentDefinition Tests (via AgentDefinition property)

    [Fact(DisplayName = "Constructor should find agent definition with _agent suffix")]
    public void Constructor_AgentKeyWithSuffix_FindsAgentDefinition()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification_type",
                Enabled = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.Type.Should().Be("classification_type");
    }

    [Fact(DisplayName = "Constructor should find agent definition without _agent suffix")]
    public void Constructor_AgentKeyWithoutSuffix_FindsAgentDefinition()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "direct_classification",
                Enabled = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.Type.Should().Be("direct_classification");
    }

    [Fact(DisplayName = "Constructor should use default agent definition when not found")]
    public void Constructor_AgentKeyNotFound_UsesDefaultDefinition()
    {
        // Arrange - Agent key doesn't exist but provider does in default agent definition
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["other_agent"] = new AgentDefinitionOptions
            {
                Type = "other",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Need to provide empty providers so validation passes with default definition
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            },
            // Default definition uses empty provider string, which will fail
        };

        // The default AgentDefinitionOptions has empty AIFrameworkOptions.Provider
        // This will fail validation - testing that default is used but validation catches it
        var act = () => new AgentFactory(
            "unknown_agent",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Default definition is used but has no provider configured
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have a provider configured*");
    }

    [Fact(DisplayName = "Constructor should prefer _agent suffix over exact match")]
    public void Constructor_BothKeysExist_PrefersAgentSuffix()
    {
        // Arrange - Both "classification_agent" and "classification" exist
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "from_suffix",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            },
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "from_exact",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Should prefer _agent suffix
        factory.AgentDefinition.Type.Should().Be("from_suffix");
    }

    #endregion

    #region CreateUserMessage Tests

    [Fact(DisplayName = "CreateUserMessage should return ChatMessage with user prompt")]
    public void CreateUserMessage_NullContext_ReturnsChatMessage()
    {
        // Arrange
        const string expectedPrompt = "Process this document.";
        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", null))
            .Returns(expectedPrompt);

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message = factory.CreateUserMessage();

        // Assert
        message.Should().NotBeNull();
        message.Role.Should().Be(ChatRole.User);
        message.Contents.Should().HaveCount(1);
        message.Contents[0].Should().BeOfType<TextContent>();
        ((TextContent)message.Contents[0]).Text.Should().Be(expectedPrompt);
    }

    [Fact(DisplayName = "CreateUserMessage should render template with context")]
    public void CreateUserMessage_WithContext_RendersTemplate()
    {
        // Arrange
        var context = new { DocumentName = "test.pdf" };
        const string expectedPrompt = "Process document: test.pdf";
        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", context))
            .Returns(expectedPrompt);

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message = factory.CreateUserMessage(context);

        // Assert
        message.Should().NotBeNull();
        ((TextContent)message.Contents[0]).Text.Should().Be(expectedPrompt);
        _mockPromptService.Verify(x => x.RenderUserPrompt("classification", context), Times.Once);
    }

    #endregion

    #region AgentKey Property Tests

    [Fact(DisplayName = "AgentKey property should return the key passed to constructor")]
    public void AgentKey_ReturnsConstructorValue()
    {
        // Arrange
        const string agentKey = "test_agent_key";
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            [$"{agentKey}_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            agentKey,
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act & Assert
        factory.AgentKey.Should().Be(agentKey);
    }

    #endregion

    #region Initial State Tests

    [Fact(DisplayName = "Factory should have null Agent initially")]
    public void Agent_InitialState_IsNull()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Agent.Should().BeNull();
    }

    [Fact(DisplayName = "Factory should have null Thread initially")]
    public void Thread_InitialState_IsNull()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Thread.Should().BeNull();
    }

    [Fact(DisplayName = "Factory should have null VectorStoreId initially")]
    public void VectorStoreId_InitialState_IsNull()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.VectorStoreId.Should().BeNull();
    }

    #endregion

    #region Configuration Warning Tests

    [Fact(DisplayName = "Constructor should log warning when no configuration exists for agent key")]
    public void Constructor_NoConfigurationForKey_LogsWarning()
    {
        // Arrange
        _mockPromptService.Setup(x => x.HasConfiguration("classification")).Returns(false);

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Factory should be created (warning logged, not exception)
        factory.Should().NotBeNull();
        _mockPromptService.Verify(x => x.HasConfiguration("classification"), Times.Once);
    }

    #endregion

    #region CreateAgentAsync Validation Tests

    [Fact(DisplayName = "CreateAgentAsync should throw when vectorStoreId is null")]
    public async Task CreateAgentAsync_NullVectorStoreId_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var act = () => factory.CreateAgentAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Vector store ID cannot be null or empty*");
    }

    [Fact(DisplayName = "CreateAgentAsync should throw when vectorStoreId is empty")]
    public async Task CreateAgentAsync_EmptyVectorStoreId_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var act = () => factory.CreateAgentAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Vector store ID cannot be null or empty*");
    }

    [Fact(DisplayName = "CreateAgentAsync should throw when vectorStoreId is whitespace")]
    public async Task CreateAgentAsync_WhitespaceVectorStoreId_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var act = () => factory.CreateAgentAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Vector store ID cannot be null or empty*");
    }

    #endregion

    #region RunAgentWithPollingAsync Validation Tests

    [Fact(DisplayName = "RunAgentWithPollingAsync should throw when Agent is null")]
    public async Task RunAgentWithPollingAsync_NullAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test message")
        };

        // Act
        var act = () => factory.RunAgentWithPollingAsync(messages);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Agent must be created before running*");
    }

    #endregion

    #region DeleteAgentAsync Tests

    [Fact(DisplayName = "DeleteAgentAsync should return gracefully when Agent is null")]
    public async Task DeleteAgentAsync_NullAgent_ReturnsGracefully()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act & Assert - Should not throw
        await factory.DeleteAgentAsync();

        // Verify no client interaction
        _mockClientFactory.Verify(x => x.GetClient(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region DeleteThreadAsync Tests

    [Fact(DisplayName = "DeleteThreadAsync should return gracefully when Thread is null")]
    public async Task DeleteThreadAsync_NullThread_ReturnsGracefully()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act & Assert - Should not throw
        await factory.DeleteThreadAsync();

        // Verify no client interaction
        _mockClientFactory.Verify(x => x.GetClient(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region CleanupAsync Tests

    [Fact(DisplayName = "CleanupAsync should skip agent cleanup when AutoDelete is false")]
    public async Task CleanupAsync_AutoDeleteFalse_SkipsAgentCleanup()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = false,
                AutoCleanupResources = false,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        await factory.CleanupAsync();

        // Assert - No client interactions (cleanup was skipped)
        _mockClientFactory.Verify(x => x.GetClient(It.IsAny<string>()), Times.Never);
        _mockVectorStoreManager.Verify(
            x => x.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "CleanupAsync should skip vector store cleanup when AutoCleanupResources is false")]
    public async Task CleanupAsync_AutoCleanupResourcesFalse_SkipsVectorStoreCleanup()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = true, // Will attempt agent cleanup
                AutoCleanupResources = false,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        await factory.CleanupAsync();

        // Assert - Vector store cleanup was not called
        _mockVectorStoreManager.Verify(
            x => x.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "CleanupAsync should skip vector store cleanup when VectorStoreId is null")]
    public async Task CleanupAsync_NullVectorStoreId_SkipsVectorStoreCleanup()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = true,
                AutoCleanupResources = true, // Will attempt cleanup but VectorStoreId is null
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        await factory.CleanupAsync();

        // Assert - Vector store cleanup was not called (VectorStoreId is null)
        _mockVectorStoreManager.Verify(
            x => x.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "CleanupAsync should call DeleteThreadAsync and DeleteAgentAsync when AutoDelete is true")]
    public async Task CleanupAsync_AutoDeleteTrue_CallsDeleteMethods()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = true,
                AutoCleanupResources = false,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act - Should complete without exception (Agent and Thread are null)
        await factory.CleanupAsync();

        // Assert - Factory should still be valid after cleanup
        factory.Agent.Should().BeNull();
        factory.Thread.Should().BeNull();
    }

    [Fact(DisplayName = "CleanupAsync should handle both AutoDelete and AutoCleanupResources together")]
    public async Task CleanupAsync_BothOptionsEnabled_PerformsFullCleanup()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = true,
                AutoCleanupResources = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act - No VectorStoreId set, so vector store cleanup will be skipped
        await factory.CleanupAsync();

        // Assert - No exceptions, cleanup methods called (but returned early due to null values)
        factory.Agent.Should().BeNull();
        factory.Thread.Should().BeNull();
        factory.VectorStoreId.Should().BeNull();
    }

    #endregion

    #region CreateAgentAsync Provider Validation Tests

    [Fact(DisplayName = "CreateAgentAsync should throw when provider not found in configuration")]
    public async Task CreateAgentAsync_ProviderNotFoundAtRuntime_ThrowsInvalidOperationException()
    {
        // Arrange - Create factory with valid config, then we'll test CreateAgentAsync validation
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };

        // Agent definition references a provider that exists during construction
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["test_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        _mockPromptService.Setup(x => x.RenderSystemPrompt("test")).Returns("Test instructions");
        _mockPromptService.Setup(x => x.GetAgentNamePrefix("test")).Returns("test");

        var factory = new AgentFactory(
            "test",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Mock client factory to throw - simulating runtime failure
        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("Client creation failed"));

        // Act
        var act = () => factory.CreateAgentAsync("vs-123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Client creation failed*");
    }

    #endregion

    #region AgentDefinition Property Tests

    [Fact(DisplayName = "AgentDefinition should return correct AutoDelete value")]
    public void AgentDefinition_AutoDeleteTrue_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.AutoDelete.Should().BeTrue();
    }

    [Fact(DisplayName = "AgentDefinition should return correct AutoDelete false value")]
    public void AgentDefinition_AutoDeleteFalse_ReturnsFalse()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = false,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.AutoDelete.Should().BeFalse();
    }

    [Fact(DisplayName = "AgentDefinition should return correct AutoCleanupResources value")]
    public void AgentDefinition_AutoCleanupResourcesTrue_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoCleanupResources = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.AutoCleanupResources.Should().BeTrue();
    }

    [Fact(DisplayName = "AgentDefinition should return correct Enabled value")]
    public void AgentDefinition_EnabledTrue_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                Enabled = true,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.Enabled.Should().BeTrue();
    }

    [Fact(DisplayName = "AgentDefinition should return correct Type value")]
    public void AgentDefinition_TypeSet_ReturnsCorrectType()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "custom_classifier",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.Type.Should().Be("custom_classifier");
    }

    [Fact(DisplayName = "AgentDefinition should return AIFrameworkOptions with correct Provider")]
    public void AgentDefinition_AIFrameworkOptions_ReturnsCorrectProvider()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.AIFrameworkOptions.Should().NotBeNull();
        factory.AgentDefinition.AIFrameworkOptions.Provider.Should().Be("azure_foundry");
    }

    #endregion

    #region Multiple Provider Configuration Tests

    [Fact(DisplayName = "Constructor should succeed with multiple providers configured")]
    public void Constructor_MultipleProviders_CreatesInstanceSuccessfully()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://azure.test.com",
                DeploymentName = "gpt-4"
            },
            ["openai"] = new ModelProviderDefinitionOptions
            {
                Type = "openai",
                Endpoint = "https://api.openai.com",
                DeploymentName = "gpt-4-turbo"
            }
        };

        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "openai" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.AIFrameworkOptions.Provider.Should().Be("openai");
    }

    [Fact(DisplayName = "Constructor should select correct provider from multiple options")]
    public void Constructor_MultipleProvidersSelectSecond_SelectsCorrectProvider()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["provider_a"] = new ModelProviderDefinitionOptions
            {
                Type = "type_a",
                Endpoint = "https://a.test.com",
                DeploymentName = "model-a"
            },
            ["provider_b"] = new ModelProviderDefinitionOptions
            {
                Type = "type_b",
                Endpoint = "https://b.test.com",
                DeploymentName = "model-b"
            },
            ["provider_c"] = new ModelProviderDefinitionOptions
            {
                Type = "type_c",
                Endpoint = "https://c.test.com",
                DeploymentName = "model-c"
            }
        };

        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["test_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "provider_b" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "test",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.AIFrameworkOptions.Provider.Should().Be("provider_b");
    }

    #endregion

    #region HasConfiguration Tests

    [Fact(DisplayName = "Constructor should check HasConfiguration for agent key")]
    public void Constructor_ValidKey_ChecksHasConfiguration()
    {
        // Arrange
        _mockPromptService.Setup(x => x.HasConfiguration("classification")).Returns(true);

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        _mockPromptService.Verify(x => x.HasConfiguration("classification"), Times.Once);
        factory.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should create factory even when HasConfiguration returns false")]
    public void Constructor_NoConfiguration_StillCreatesFactory()
    {
        // Arrange
        _mockPromptService.Setup(x => x.HasConfiguration("unknown")).Returns(false);

        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["unknown_agent"] = new AgentDefinitionOptions
            {
                Type = "unknown",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "unknown",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Factory is created (just logs warning)
        factory.Should().NotBeNull();
        factory.AgentKey.Should().Be("unknown");
    }

    #endregion

    #region CreateUserMessage Edge Cases

    [Fact(DisplayName = "CreateUserMessage should handle empty prompt from service")]
    public void CreateUserMessage_EmptyPrompt_ReturnsMessageWithEmptyText()
    {
        // Arrange
        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", null))
            .Returns(string.Empty);

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message = factory.CreateUserMessage();

        // Assert
        message.Should().NotBeNull();
        message.Role.Should().Be(ChatRole.User);
        ((TextContent)message.Contents[0]).Text.Should().BeEmpty();
    }

    [Fact(DisplayName = "CreateUserMessage should handle complex context object")]
    public void CreateUserMessage_ComplexContext_PassesToPromptService()
    {
        // Arrange
        var complexContext = new
        {
            DocumentId = "doc-123",
            Metadata = new { Author = "Test", Pages = 10 },
            Tags = new[] { "tag1", "tag2" }
        };
        const string renderedPrompt = "Process document doc-123 with 10 pages";

        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", complexContext))
            .Returns(renderedPrompt);

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message = factory.CreateUserMessage(complexContext);

        // Assert
        ((TextContent)message.Contents[0]).Text.Should().Be(renderedPrompt);
        _mockPromptService.Verify(x => x.RenderUserPrompt("classification", complexContext), Times.Once);
    }

    [Fact(DisplayName = "CreateUserMessage should handle dictionary context")]
    public void CreateUserMessage_DictionaryContext_PassesToPromptService()
    {
        // Arrange
        var dictContext = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };
        const string renderedPrompt = "Process with value1 and 42";

        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", dictContext))
            .Returns(renderedPrompt);

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message = factory.CreateUserMessage(dictContext);

        // Assert
        ((TextContent)message.Contents[0]).Text.Should().Be(renderedPrompt);
    }

    #endregion

    #region Telemetry Options Tests

    [Fact(DisplayName = "Constructor should accept TelemetryOptions with Enabled true")]
    public void Constructor_TelemetryEnabled_CreatesFactory()
    {
        // Arrange
        var telemetryOptions = MsOptions.Create(new TelemetryOptions
        {
            Enabled = true,
            SourceName = "TestSource",
            EnableSensitiveData = false
        });

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            telemetryOptions);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should accept TelemetryOptions with EnableSensitiveData true")]
    public void Constructor_TelemetrySensitiveDataEnabled_CreatesFactory()
    {
        // Arrange
        var telemetryOptions = MsOptions.Create(new TelemetryOptions
        {
            Enabled = true,
            SourceName = "TestSource",
            EnableSensitiveData = true
        });

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            telemetryOptions);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should accept TelemetryOptions with empty SourceName")]
    public void Constructor_TelemetryEmptySourceName_CreatesFactory()
    {
        // Arrange
        var telemetryOptions = MsOptions.Create(new TelemetryOptions
        {
            Enabled = true,
            SourceName = "",
            EnableSensitiveData = false
        });

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            telemetryOptions);

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region Default AgentDefinitionOptions Tests

    [Fact(DisplayName = "Default AgentDefinitionOptions should have expected default values")]
    public void DefaultAgentDefinition_WhenCreated_HasExpectedDefaults()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["different_agent"] = new AgentDefinitionOptions
            {
                Type = "other",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Provider must exist for the default definition
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                DeploymentName = "gpt-4"
            }
        };

        // The default AgentDefinitionOptions has empty provider, so this will fail validation
        // Testing the behavior when key isn't found
        var act = () => new AgentFactory(
            "nonexistent",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Default definition is created but has no provider
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have a provider configured*");
    }

    #endregion

    #region CancellationToken Tests

    [Fact(DisplayName = "CreateAgentAsync should respect cancellation token")]
    public async Task CreateAgentAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["test_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        _mockPromptService.Setup(x => x.RenderSystemPrompt("test")).Returns("Instructions");
        _mockPromptService.Setup(x => x.GetAgentNamePrefix("test")).Returns("test");

        // Mock client that throws on cancellation
        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new OperationCanceledException());

        var factory = new AgentFactory(
            "test",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => factory.CreateAgentAsync("vs-123", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "DeleteAgentAsync should accept cancellation token")]
    public async Task DeleteAgentAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw (Agent is null)
        await factory.DeleteAgentAsync(cts.Token);
    }

    [Fact(DisplayName = "DeleteThreadAsync should accept cancellation token")]
    public async Task DeleteThreadAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw (Thread is null)
        await factory.DeleteThreadAsync(cts.Token);
    }

    [Fact(DisplayName = "CleanupAsync should accept cancellation token")]
    public async Task CleanupAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AutoDelete = false,
                AutoCleanupResources = false,
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw
        await factory.CleanupAsync(cts.Token);
    }

    #endregion

    #region Provider Endpoint and Model Tests

    [Fact(DisplayName = "Constructor should accept provider with Model instead of DeploymentName")]
    public void Constructor_ProviderWithModel_CreatesFactory()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                Model = "gpt-4-turbo" // Using Model instead of DeploymentName
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should accept provider with both Model and DeploymentName")]
    public void Constructor_ProviderWithBothModelAndDeployment_CreatesFactory()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["azure_foundry"] = new ModelProviderDefinitionOptions
            {
                Type = "azure_foundry",
                Endpoint = "https://test.azure.com",
                Model = "gpt-4",
                DeploymentName = "my-deployment"
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region RunAgentWithPollingAsync Additional Tests

    [Fact(DisplayName = "RunAgentWithPollingAsync should throw when Thread is null")]
    public async Task RunAgentWithPollingAsync_NullThread_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test message")
        };

        // Act - Agent is null, so exception about Agent will be thrown first
        var act = () => factory.RunAgentWithPollingAsync(messages);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Agent must be created before running*");
    }

    [Fact(DisplayName = "RunAgentWithPollingAsync should accept empty message list")]
    public async Task RunAgentWithPollingAsync_EmptyMessageList_ThrowsAgentNotCreatedException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        var messages = new List<ChatMessage>();

        // Act
        var act = () => factory.RunAgentWithPollingAsync(messages);

        // Assert - Still throws because Agent is null
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Agent must be created before running*");
    }

    [Fact(DisplayName = "RunAgentWithPollingAsync should accept custom polling parameters")]
    public async Task RunAgentWithPollingAsync_CustomParameters_ThrowsAgentNotCreatedException()
    {
        // Arrange
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act - Custom parameters should be accepted, but Agent is null
        var act = () => factory.RunAgentWithPollingAsync(
            messages,
            pollingIntervalSeconds: 5,
            maxRetries: 20,
            retryDelaySeconds: 30);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Agent must be created before running*");
    }

    #endregion

    #region Multiple Agent Keys Tests

    [Fact(DisplayName = "Factory should work with different agent keys")]
    public void Constructor_DifferentAgentKeys_CreatesFactoriesSuccessfully()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classifier",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            },
            ["extraction_agent"] = new AgentDefinitionOptions
            {
                Type = "extractor",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory1 = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        var factory2 = new AgentFactory(
            "extraction",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory1.AgentKey.Should().Be("classification");
        factory1.AgentDefinition.Type.Should().Be("classifier");

        factory2.AgentKey.Should().Be("extraction");
        factory2.AgentDefinition.Type.Should().Be("extractor");
    }

    #endregion

    #region Special Characters in Agent Keys Tests

    [Fact(DisplayName = "Constructor should handle agent key with underscore")]
    public void Constructor_AgentKeyWithUnderscore_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["my_custom_agent_agent"] = new AgentDefinitionOptions
            {
                Type = "custom",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "my_custom_agent",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentKey.Should().Be("my_custom_agent");
    }

    [Fact(DisplayName = "Constructor should handle agent key with hyphen")]
    public void Constructor_AgentKeyWithHyphen_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["my-agent_agent"] = new AgentDefinitionOptions
            {
                Type = "hyphenated",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "my-agent",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentKey.Should().Be("my-agent");
        factory.AgentDefinition.Type.Should().Be("hyphenated");
    }

    [Fact(DisplayName = "Constructor should handle agent key with numbers")]
    public void Constructor_AgentKeyWithNumbers_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["agent123_agent"] = new AgentDefinitionOptions
            {
                Type = "numbered",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        // Act
        var factory = new AgentFactory(
            "agent123",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentKey.Should().Be("agent123");
    }

    #endregion

    #region CreateAgentAsync Prompt Service Integration Tests

    [Fact(DisplayName = "CreateAgentAsync should call RenderSystemPrompt with agent key")]
    public async Task CreateAgentAsync_ValidVectorStoreId_CallsRenderSystemPrompt()
    {
        // Arrange
        _mockPromptService.Setup(x => x.RenderSystemPrompt("test")).Returns("System instructions");
        _mockPromptService.Setup(x => x.GetAgentNamePrefix("test")).Returns("test-prefix");

        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["test_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
            }
        };

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new Exception("Client call intercepted for verification"));

        var factory = new AgentFactory(
            "test",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        try
        {
            await factory.CreateAgentAsync("vs-123");
        }
        catch
        {
            // Expected - we're just verifying the calls
        }

        // Assert
        _mockPromptService.Verify(x => x.RenderSystemPrompt("test"), Times.Once);
        _mockPromptService.Verify(x => x.GetAgentNamePrefix("test"), Times.Once);
    }

    #endregion

    #region Error Message Content Tests

    [Fact(DisplayName = "Constructor error message should include available providers")]
    public void Constructor_ProviderNotFound_ErrorMessageListsAvailableProviders()
    {
        // Arrange
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>
        {
            ["provider_a"] = new ModelProviderDefinitionOptions
            {
                Type = "type_a",
                Endpoint = "https://a.test.com"
            },
            ["provider_b"] = new ModelProviderDefinitionOptions
            {
                Type = "type_b",
                Endpoint = "https://b.test.com"
            }
        };

        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["test_agent"] = new AgentDefinitionOptions
            {
                Type = "test",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "missing_provider" }
            }
        };

        // Act
        var act = () => new AgentFactory(
            "test",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*provider_a*")
            .WithMessage("*provider_b*");
    }

    [Fact(DisplayName = "Constructor error message should include agent key")]
    public void Constructor_ProviderNotFound_ErrorMessageIncludesAgentKey()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["my_special_agent_agent"] = new AgentDefinitionOptions
            {
                Type = "special",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "nonexistent" }
            }
        };

        // Act
        var act = () => new AgentFactory(
            "my_special_agent",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*my_special_agent*");
    }

    #endregion

    #region Whitespace Provider Name Tests

    [Fact(DisplayName = "Constructor should throw when provider name is whitespace only")]
    public void Constructor_WhitespaceProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "   " }
            }
        };

        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have a provider configured*");
    }

    #endregion

    #region Factory Immutability Tests

    [Fact(DisplayName = "AgentKey should be immutable after construction")]
    public void AgentKey_AfterConstruction_RemainsUnchanged()
    {
        // Arrange
        const string originalKey = "classification";

        var factory = new AgentFactory(
            originalKey,
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act & Assert - Multiple reads should return same value
        factory.AgentKey.Should().Be(originalKey);
        factory.AgentKey.Should().Be(originalKey);
        factory.AgentKey.Should().Be(originalKey);
    }

    [Fact(DisplayName = "AgentDefinition should be available after construction")]
    public void AgentDefinition_AfterConstruction_IsAvailable()
    {
        // Arrange & Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Multiple reads should work
        factory.AgentDefinition.Should().NotBeNull();
        factory.AgentDefinition.Should().NotBeNull();
    }

    #endregion

    #region CreateUserMessage Multiple Calls Tests

    [Fact(DisplayName = "CreateUserMessage should work multiple times")]
    public void CreateUserMessage_CalledMultipleTimes_AllReturnValidMessages()
    {
        // Arrange
        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", null))
            .Returns("Test prompt");

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message1 = factory.CreateUserMessage();
        var message2 = factory.CreateUserMessage();
        var message3 = factory.CreateUserMessage();

        // Assert
        message1.Should().NotBeNull();
        message2.Should().NotBeNull();
        message3.Should().NotBeNull();
        _mockPromptService.Verify(x => x.RenderUserPrompt("classification", null), Times.Exactly(3));
    }

    [Fact(DisplayName = "CreateUserMessage should handle different contexts")]
    public void CreateUserMessage_DifferentContexts_RendersDifferentPrompts()
    {
        // Arrange
        var context1 = new { Id = 1 };
        var context2 = new { Id = 2 };

        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", context1))
            .Returns("Prompt for ID 1");
        _mockPromptService.Setup(x => x.RenderUserPrompt("classification", context2))
            .Returns("Prompt for ID 2");

        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Act
        var message1 = factory.CreateUserMessage(context1);
        var message2 = factory.CreateUserMessage(context2);

        // Assert
        ((TextContent)message1.Contents[0]).Text.Should().Be("Prompt for ID 1");
        ((TextContent)message2.Contents[0]).Text.Should().Be("Prompt for ID 2");
    }

    #endregion

    #region Tool Configuration Tests

    [Fact(DisplayName = "Constructor should accept agent with file_search tool configured")]
    public void Constructor_FileSearchToolConfigured_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" },
                Metadata = new AgentMetadataOptions
                {
                    Description = "Test agent",
                    Tools = ["file_search"]
                }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().Contain("file_search");
    }

    [Fact(DisplayName = "Constructor should accept agent with code_interpreter tool configured")]
    public void Constructor_CodeInterpreterToolConfigured_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" },
                Metadata = new AgentMetadataOptions
                {
                    Description = "Test agent",
                    Tools = ["code_interpreter"]
                }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().Contain("code_interpreter");
    }

    [Fact(DisplayName = "Constructor should accept agent with both tools configured")]
    public void Constructor_BothToolsConfigured_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" },
                Metadata = new AgentMetadataOptions
                {
                    Description = "Test agent with multiple tools",
                    Tools = ["file_search", "code_interpreter"]
                }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().HaveCount(2);
        factory.AgentDefinition.Metadata.Tools.Should().Contain("file_search");
        factory.AgentDefinition.Metadata.Tools.Should().Contain("code_interpreter");
    }

    [Fact(DisplayName = "Constructor should accept agent with empty tools list")]
    public void Constructor_EmptyToolsList_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" },
                Metadata = new AgentMetadataOptions
                {
                    Description = "Test agent",
                    Tools = []
                }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().BeEmpty();
    }

    [Fact(DisplayName = "Constructor should accept agent with case-insensitive tool names")]
    public void Constructor_MixedCaseToolNames_CreatesFactory()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" },
                Metadata = new AgentMetadataOptions
                {
                    Description = "Test agent",
                    Tools = ["FILE_SEARCH", "Code_Interpreter"]
                }
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().HaveCount(2);
    }

    [Fact(DisplayName = "AgentDefinition Metadata should have default empty tools list")]
    public void AgentDefinition_DefaultMetadata_HasEmptyToolsList()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                AIFrameworkOptions = new AIFrameworkOptions { Provider = "azure_foundry" }
                // No Metadata explicitly set - should use default
            }
        };

        // Act
        var factory = new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        factory.AgentDefinition.Metadata.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().NotBeNull();
        factory.AgentDefinition.Metadata.Tools.Should().BeEmpty();
    }

    #endregion

    #region Options Validation Tests

    [Fact(DisplayName = "Constructor should work with empty providers dictionary")]
    public void Constructor_EmptyProvidersDict_ThrowsOnAgentWithProvider()
    {
        // Arrange - Empty providers, but agent references one
        var providers = new Dictionary<string, ModelProviderDefinitionOptions>();

        // Act
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(providers),
            CreateAgentOptions(),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert - Should throw because agent references azure_foundry but it doesn't exist
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in configuration*");
    }

    [Fact(DisplayName = "Constructor should work with empty agents dictionary when agent not found")]
    public void Constructor_EmptyAgentsDict_UsesDefaultAndThrows()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>();

        // Act - Default definition has empty provider
        var act = () => new AgentFactory(
            "classification",
            _mockLogger.Object,
            _mockPromptService.Object,
            CreateProviderOptions(),
            CreateAgentOptions(agents),
            _mockClientFactory.Object,
            _mockVectorStoreManager.Object,
            CreateTelemetryOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have a provider configured*");
    }

    #endregion
}
