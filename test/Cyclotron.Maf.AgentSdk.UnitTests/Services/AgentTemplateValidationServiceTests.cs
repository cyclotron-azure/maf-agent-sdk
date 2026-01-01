using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="AgentTemplateValidationService"/> class.
/// Tests template validation logic, constructor validation, and startup behavior.
/// </summary>
public class AgentTemplateValidationServiceTests
{
    private readonly Mock<IPromptRenderingService> _mockPromptService;
    private readonly Mock<ILogger<AgentTemplateValidationService>> _mockLogger;

    public AgentTemplateValidationServiceTests()
    {
        _mockPromptService = new Mock<IPromptRenderingService>();
        _mockLogger = new Mock<ILogger<AgentTemplateValidationService>>();
    }

    private IOptions<AgentOptions> CreateAgentOptions(
        Dictionary<string, AgentDefinitionOptions>? agents = null)
    {
        var options = new AgentOptions
        {
            Agents = agents ?? []
        };
        return MsOptions.Create(options);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when promptService is null")]
    public void Constructor_NullPromptService_ThrowsArgumentNullException()
    {
        // Arrange
        var agentOptions = CreateAgentOptions();

        // Act
        var act = () => new AgentTemplateValidationService(
            null!,
            agentOptions,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("promptService");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when agentOptions is null")]
    public void Constructor_NullAgentOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentTemplateValidationService(
            _mockPromptService.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentOptions");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var agentOptions = CreateAgentOptions();

        // Act
        var act = () => new AgentTemplateValidationService(
            _mockPromptService.Object,
            agentOptions,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should create instance with valid parameters")]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Arrange
        var agentOptions = CreateAgentOptions();

        // Act
        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            agentOptions,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region StartAsync Tests

    [Fact(DisplayName = "StartAsync should complete successfully when all templates exist")]
    public async Task StartAsync_AllTemplatesExist_CompletesSuccessfully()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions { Type = "classification", Enabled = true },
            ["extraction_agent"] = new AgentDefinitionOptions { Type = "extraction", Enabled = true }
        };

        _mockPromptService.Setup(x => x.HasSystemPromptTemplate(It.IsAny<string>())).Returns(true);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate(It.IsAny<string>())).Returns(true);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - No exception means success
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("classification_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("classification_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("extraction_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("extraction_agent"), Times.Once);
    }

    [Fact(DisplayName = "StartAsync should skip disabled agents")]
    public async Task StartAsync_DisabledAgents_SkipsValidation()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["enabled_agent"] = new AgentDefinitionOptions { Type = "enabled", Enabled = true },
            ["disabled_agent"] = new AgentDefinitionOptions { Type = "disabled", Enabled = false }
        };

        _mockPromptService.Setup(x => x.HasSystemPromptTemplate(It.IsAny<string>())).Returns(true);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate(It.IsAny<string>())).Returns(true);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Disabled agent should not be validated
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("enabled_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("enabled_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("disabled_agent"), Times.Never);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("disabled_agent"), Times.Never);
    }

    [Fact(DisplayName = "StartAsync should throw when system prompt template is missing")]
    public async Task StartAsync_MissingSystemPromptTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions { Type = "classification", Enabled = true }
        };

        _mockPromptService.Setup(x => x.HasSystemPromptTemplate("classification_agent")).Returns(false);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate("classification_agent")).Returns(true);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        var act = () => service.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CONFIGURATION ERROR*")
            .WithMessage("*system_prompt_template*");
    }

    [Fact(DisplayName = "StartAsync should throw when user prompt template is missing")]
    public async Task StartAsync_MissingUserPromptTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions { Type = "classification", Enabled = true }
        };

        _mockPromptService.Setup(x => x.HasSystemPromptTemplate("classification_agent")).Returns(true);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate("classification_agent")).Returns(false);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        var act = () => service.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CONFIGURATION ERROR*")
            .WithMessage("*user_prompt_template*");
    }

    [Fact(DisplayName = "StartAsync should report all missing templates in error message")]
    public async Task StartAsync_MultipleMissingTemplates_ReportsAllInErrorMessage()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["agent1"] = new AgentDefinitionOptions { Type = "type1", Enabled = true },
            ["agent2"] = new AgentDefinitionOptions { Type = "type2", Enabled = true }
        };

        // Both agents missing both templates
        _mockPromptService.Setup(x => x.HasSystemPromptTemplate(It.IsAny<string>())).Returns(false);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate(It.IsAny<string>())).Returns(false);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        var act = () => service.StartAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("*agent1*")
            .WithMessage("*agent2*");
    }

    [Fact(DisplayName = "StartAsync should complete when no agents configured")]
    public async Task StartAsync_NoAgentsConfigured_CompletesSuccessfully()
    {
        // Arrange - Empty agents dictionary
        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions([]),
            _mockLogger.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - No validation calls needed
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate(It.IsAny<string>()), Times.Never);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "StartAsync should validate all enabled agents before throwing")]
    public async Task StartAsync_MultipleEnabledAgents_ValidatesAllBeforeThrowing()
    {
        // Arrange - Multiple agents, first one valid, second one missing templates
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["valid_agent"] = new AgentDefinitionOptions { Type = "valid", Enabled = true },
            ["invalid_agent"] = new AgentDefinitionOptions { Type = "invalid", Enabled = true }
        };

        _mockPromptService.Setup(x => x.HasSystemPromptTemplate("valid_agent")).Returns(true);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate("valid_agent")).Returns(true);
        _mockPromptService.Setup(x => x.HasSystemPromptTemplate("invalid_agent")).Returns(false);
        _mockPromptService.Setup(x => x.HasUserPromptTemplate("invalid_agent")).Returns(false);

        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(agents),
            _mockLogger.Object);

        // Act
        var act = () => service.StartAsync(CancellationToken.None);

        // Assert - Both agents should be validated
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("valid_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("valid_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasSystemPromptTemplate("invalid_agent"), Times.Once);
        _mockPromptService.Verify(x => x.HasUserPromptTemplate("invalid_agent"), Times.Once);
    }

    #endregion

    #region StopAsync Tests

    [Fact(DisplayName = "StopAsync should complete immediately")]
    public async Task StopAsync_Always_CompletesImmediately()
    {
        // Arrange
        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(),
            _mockLogger.Object);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - Just verify no exception is thrown
    }

    [Fact(DisplayName = "StopAsync should support cancellation")]
    public async Task StopAsync_WithCancellationToken_Completes()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var service = new AgentTemplateValidationService(
            _mockPromptService.Object,
            CreateAgentOptions(),
            _mockLogger.Object);

        // Act
        await service.StopAsync(cts.Token);

        // Assert - Method returns Task.CompletedTask regardless of token
    }

    #endregion
}
