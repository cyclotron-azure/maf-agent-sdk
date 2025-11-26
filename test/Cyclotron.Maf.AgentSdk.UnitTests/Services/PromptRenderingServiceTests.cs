using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="PromptRenderingService"/> class.
/// Tests agent name prefix conversion, configuration checks, and template rendering.
/// </summary>
public class PromptRenderingServiceTests
{
    private readonly Mock<ILogger<PromptRenderingService>> _mockLogger;

    public PromptRenderingServiceTests()
    {
        _mockLogger = new Mock<ILogger<PromptRenderingService>>();
    }

    private PromptRenderingService CreateService(Dictionary<string, AgentDefinitionOptions>? agents = null)
    {
        var agentOptions = new AgentOptions
        {
            Agents = agents ?? []
        };

        var options = Microsoft.Extensions.Options.Options.Create(agentOptions);
        return new PromptRenderingService(options, _mockLogger.Object);
    }

    #region GetAgentNamePrefix Tests

    [Theory(DisplayName = "GetAgentNamePrefix should convert snake_case to PascalCase")]
    [InlineData("classification", "ClassificationAgent")]
    [InlineData("text_analyzer", "TextAnalyzerAgent")]
    [InlineData("pdf_processor", "PdfProcessorAgent")]
    [InlineData("multi_word_name", "MultiWordNameAgent")]
    public void GetAgentNamePrefix_SnakeCaseInput_ReturnsPascalCaseWithAgentSuffix(
        string agentKey, string expected)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAgentNamePrefix(agentKey);

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName = "GetAgentNamePrefix should handle single word names")]
    [InlineData("simple", "SimpleAgent")]
    [InlineData("TEST", "TestAgent")]
    [InlineData("Mixed", "MixedAgent")]
    public void GetAgentNamePrefix_SingleWord_ReturnsPascalCaseWithAgentSuffix(
        string agentKey, string expected)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAgentNamePrefix(agentKey);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "GetAgentNamePrefix should not duplicate Agent suffix")]
    public void GetAgentNamePrefix_AlreadyHasAgentSuffix_DoesNotDuplicate()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAgentNamePrefix("classification_agent");

        // Assert
        result.Should().Be("ClassificationAgent");
    }

    [Fact(DisplayName = "GetAgentNamePrefix should throw when agent key is null or empty")]
    public void GetAgentNamePrefix_NullOrEmptyAgentKey_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var actNull = () => service.GetAgentNamePrefix(null!);
        var actEmpty = () => service.GetAgentNamePrefix("");
        var actWhitespace = () => service.GetAgentNamePrefix("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    #endregion

    #region HasConfiguration Tests

    [Fact(DisplayName = "HasConfiguration should return true when exact agent key exists")]
    public void HasConfiguration_ExactKeyExists_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions { Type = "classification" }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasConfiguration("classification");

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "HasConfiguration should return true when agent key with _agent suffix exists")]
    public void HasConfiguration_AgentSuffixKeyExists_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions { Type = "classification" }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasConfiguration("classification");

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "HasConfiguration should return false when agent key does not exist")]
    public void HasConfiguration_KeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["existing_agent"] = new AgentDefinitionOptions { Type = "existing" }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasConfiguration("non_existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasConfiguration should return false when agent key is null or empty")]
    public void HasConfiguration_NullOrEmptyKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        service.HasConfiguration(null!).Should().BeFalse();
        service.HasConfiguration("").Should().BeFalse();
        service.HasConfiguration("   ").Should().BeFalse();
    }

    #endregion

    #region HasSystemPromptTemplate Tests

    [Fact(DisplayName = "HasSystemPromptTemplate should return true when template exists")]
    public void HasSystemPromptTemplate_TemplateExists_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = "You are a classification assistant."
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasSystemPromptTemplate("classification");

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "HasSystemPromptTemplate should return false when template is null")]
    public void HasSystemPromptTemplate_TemplateIsNull_ReturnsFalse()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = null
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasSystemPromptTemplate("classification");

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasSystemPromptTemplate should return false when agent does not exist")]
    public void HasSystemPromptTemplate_AgentDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasSystemPromptTemplate("non_existent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasUserPromptTemplate Tests

    [Fact(DisplayName = "HasUserPromptTemplate should return true when template exists")]
    public void HasUserPromptTemplate_TemplateExists_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = "Classify the following: {{text}}"
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasUserPromptTemplate("classification");

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "HasUserPromptTemplate should return false when template is null")]
    public void HasUserPromptTemplate_TemplateIsNull_ReturnsFalse()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = null
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasUserPromptTemplate("classification");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RenderSystemPrompt Tests

    [Fact(DisplayName = "RenderSystemPrompt should return raw template when context is null")]
    public void RenderSystemPrompt_NullContext_ReturnsRawTemplate()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant.";
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = systemPrompt
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.RenderSystemPrompt("classification", context: null);

        // Assert
        result.Should().Be(systemPrompt);
    }

    [Fact(DisplayName = "RenderSystemPrompt should render Handlebars template with context")]
    public void RenderSystemPrompt_WithContext_RendersTemplate()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = "You are a {{role}} assistant for {{company}}."
            }
        };
        var service = CreateService(agents);
        var context = new { role = "classification", company = "Contoso" };

        // Act
        var result = service.RenderSystemPrompt("classification", context);

        // Assert
        result.Should().Be("You are a classification assistant for Contoso.");
    }

    [Fact(DisplayName = "RenderSystemPrompt should throw when agent key is null or empty")]
    public void RenderSystemPrompt_NullOrEmptyAgentKey_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var actNull = () => service.RenderSystemPrompt(null!);
        var actEmpty = () => service.RenderSystemPrompt("");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RenderSystemPrompt should throw when no template configured")]
    public void RenderSystemPrompt_NoTemplateConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = null
            }
        };
        var service = CreateService(agents);

        // Act
        var act = () => service.RenderSystemPrompt("classification");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No system_prompt_template configured*");
    }

    [Fact(DisplayName = "RenderSystemPrompt should find agent with _agent suffix")]
    public void RenderSystemPrompt_AgentWithSuffix_FindsAndRendersTemplate()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = "You are a classification assistant."
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.RenderSystemPrompt("classification");

        // Assert
        result.Should().Be("You are a classification assistant.");
    }

    #endregion

    #region RenderUserPrompt Tests

    [Fact(DisplayName = "RenderUserPrompt should return raw template when context is null")]
    public void RenderUserPrompt_NullContext_ReturnsRawTemplate()
    {
        // Arrange
        var userPrompt = "Classify the following document.";
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = userPrompt
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.RenderUserPrompt("classification", context: null);

        // Assert
        result.Should().Be(userPrompt);
    }

    [Fact(DisplayName = "RenderUserPrompt should render Handlebars template with context")]
    public void RenderUserPrompt_WithContext_RendersTemplate()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = "Classify this {{documentType}}: {{content}}"
            }
        };
        var service = CreateService(agents);
        var context = new { documentType = "email", content = "Hello world" };

        // Act
        var result = service.RenderUserPrompt("classification", context);

        // Assert
        result.Should().Be("Classify this email: Hello world");
    }

    [Fact(DisplayName = "RenderUserPrompt should throw when agent key is null or empty")]
    public void RenderUserPrompt_NullOrEmptyAgentKey_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var actNull = () => service.RenderUserPrompt(null!);
        var actEmpty = () => service.RenderUserPrompt("");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RenderUserPrompt should throw when no template configured")]
    public void RenderUserPrompt_NoTemplateConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = null
            }
        };
        var service = CreateService(agents);

        // Act
        var act = () => service.RenderUserPrompt("classification");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No user_prompt_template configured*");
    }

    #endregion

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw when agentOptions is null")]
    public void Constructor_NullAgentOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PromptRenderingService(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentOptions");
    }

    [Fact(DisplayName = "Constructor should throw when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new AgentOptions());

        // Act
        var act = () => new PromptRenderingService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should compile all templates at startup")]
    public void Constructor_WithValidTemplates_CompilesTemplatesSuccessfully()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["agent1"] = new AgentDefinitionOptions
            {
                SystemPromptTemplate = "System: {{name}}",
                UserPromptTemplate = "User: {{query}}"
            },
            ["agent2"] = new AgentDefinitionOptions
            {
                SystemPromptTemplate = "Another system prompt"
            }
        };

        // Act - should not throw
        var service = CreateService(agents);

        // Assert
        service.HasSystemPromptTemplate("agent1").Should().BeTrue();
        service.HasUserPromptTemplate("agent1").Should().BeTrue();
        service.HasSystemPromptTemplate("agent2").Should().BeTrue();
        service.HasUserPromptTemplate("agent2").Should().BeFalse();
    }

    [Fact(DisplayName = "Constructor should handle invalid Handlebars template gracefully")]
    public void Constructor_InvalidHandlebarsTemplate_HandlesGracefully()
    {
        // Arrange - Invalid Handlebars syntax (unclosed tag)
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["invalid_agent"] = new AgentDefinitionOptions
            {
                SystemPromptTemplate = "Hello {{name", // Invalid - unclosed tag
                UserPromptTemplate = "Valid template"
            }
        };

        // Act - should not throw during construction (logs error instead)
        var service = CreateService(agents);

        // Assert - Service created but template not available
        service.Should().NotBeNull();
        // The invalid template won't be compiled, so HasSystemPromptTemplate returns false
        service.HasSystemPromptTemplate("invalid_agent").Should().BeFalse();
    }

    [Fact(DisplayName = "Constructor should handle empty agents dictionary")]
    public void Constructor_EmptyAgentsDictionary_CreatesService()
    {
        // Act
        var service = CreateService(new Dictionary<string, AgentDefinitionOptions>());

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region RenderSystemPrompt Edge Case Tests

    [Fact(DisplayName = "RenderSystemPrompt should throw when agent does not exist with context")]
    public void RenderSystemPrompt_AgentDoesNotExist_WithContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var context = new { name = "test" };

        // Act
        var act = () => service.RenderSystemPrompt("nonexistent", context);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No system_prompt_template configured*");
    }

    [Fact(DisplayName = "RenderSystemPrompt should throw when template is whitespace only")]
    public void RenderSystemPrompt_WhitespaceTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = "   " // Whitespace only
            }
        };
        var service = CreateService(agents);

        // Act
        var act = () => service.RenderSystemPrompt("classification");

        // Assert - whitespace is considered null/empty
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region RenderUserPrompt Edge Case Tests

    [Fact(DisplayName = "RenderUserPrompt should throw when agent does not exist with context")]
    public void RenderUserPrompt_AgentDoesNotExist_WithContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var context = new { text = "test" };

        // Act
        var act = () => service.RenderUserPrompt("nonexistent", context);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No user_prompt_template configured*");
    }

    [Fact(DisplayName = "RenderUserPrompt should handle complex Handlebars expressions")]
    public void RenderUserPrompt_ComplexHandlebarsExpression_RendersCorrectly()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = "Document: {{document.name}} by {{document.author}}"
            }
        };
        var service = CreateService(agents);
        var context = new { document = new { name = "Test Doc", author = "John Doe" } };

        // Act
        var result = service.RenderUserPrompt("classification", context);

        // Assert
        result.Should().Be("Document: Test Doc by John Doe");
    }

    [Fact(DisplayName = "RenderUserPrompt should find agent with _agent suffix and render with context")]
    public void RenderUserPrompt_AgentWithSuffix_RendersWithContext()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                UserPromptTemplate = "Process: {{item}}"
            }
        };
        var service = CreateService(agents);
        var context = new { item = "test item" };

        // Act
        var result = service.RenderUserPrompt("classification", context);

        // Assert
        result.Should().Be("Process: test item");
    }

    #endregion

    #region HasUserPromptTemplate Edge Case Tests

    [Fact(DisplayName = "HasUserPromptTemplate should return false for null key")]
    public void HasUserPromptTemplate_NullKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasUserPromptTemplate(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasUserPromptTemplate should return false for whitespace key")]
    public void HasUserPromptTemplate_WhitespaceKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasUserPromptTemplate("   ");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasSystemPromptTemplate Edge Case Tests

    [Fact(DisplayName = "HasSystemPromptTemplate should return false for null key")]
    public void HasSystemPromptTemplate_NullKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasSystemPromptTemplate(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasSystemPromptTemplate should return false for whitespace key")]
    public void HasSystemPromptTemplate_WhitespaceKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasSystemPromptTemplate("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "HasSystemPromptTemplate should find template with _agent suffix")]
    public void HasSystemPromptTemplate_AgentWithSuffix_ReturnsTrue()
    {
        // Arrange
        var agents = new Dictionary<string, AgentDefinitionOptions>
        {
            ["classification_agent"] = new AgentDefinitionOptions
            {
                Type = "classification",
                SystemPromptTemplate = "You are a helper."
            }
        };
        var service = CreateService(agents);

        // Act
        var result = service.HasSystemPromptTemplate("classification");

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
