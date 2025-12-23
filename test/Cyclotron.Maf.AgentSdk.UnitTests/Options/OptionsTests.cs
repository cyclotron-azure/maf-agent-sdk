using Cyclotron.Maf.AgentSdk.Options;
using AwesomeAssertions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Options;

/// <summary>
/// Unit tests for the <see cref="ModelProviderDefinitionOptions"/> class.
/// Tests default values, validation logic, and utility methods.
/// </summary>
public class ModelProviderDefinitionOptionsTests
{
    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new ModelProviderDefinitionOptions();

        // Assert
        options.Type.Should().Be(string.Empty);
        options.Endpoint.Should().Be(string.Empty);
        options.DeploymentName.Should().Be(string.Empty);
        options.Model.Should().BeNull();
        options.ApiVersion.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.TimeoutSeconds.Should().Be(300);
        options.MaxRetries.Should().Be(3);
    }

    [Fact(DisplayName = "GetEffectiveModel should return Model when Model is specified")]
    public void GetEffectiveModel_ModelSpecified_ReturnsModel()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            DeploymentName = "gpt-4-deployment",
            Model = "gpt-4-turbo"
        };

        // Act
        var result = options.GetEffectiveModel();

        // Assert
        result.Should().Be("gpt-4-turbo");
    }

    [Fact(DisplayName = "GetEffectiveModel should return DeploymentName when Model is not specified")]
    public void GetEffectiveModel_ModelNotSpecified_ReturnsDeploymentName()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            DeploymentName = "gpt-4-deployment",
            Model = null
        };

        // Act
        var result = options.GetEffectiveModel();

        // Assert
        result.Should().Be("gpt-4-deployment");
    }

    [Theory(DisplayName = "UsesApiKey should return true only when ApiKey is not null or empty")]
    [InlineData("api-key-value", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UsesApiKey_VariousApiKeyValues_ReturnsExpected(string? apiKey, bool expected)
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            ApiKey = apiKey
        };

        // Act
        var result = options.UsesApiKey();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "IsValid should return true for valid azure_foundry configuration")]
    public void IsValid_ValidAzureFoundryConfig_ReturnsTrue()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            Type = "azure_foundry",
            Endpoint = "https://my-endpoint.cognitiveservices.azure.com",
            DeploymentName = "gpt-4"
        };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsValid should return true for valid azure_openai configuration with API key")]
    public void IsValid_ValidAzureOpenAIConfigWithApiKey_ReturnsTrue()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            Type = "azure_openai",
            Endpoint = "https://my-endpoint.openai.azure.com",
            DeploymentName = "gpt-4",
            ApiKey = "my-api-key"
        };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsValid should return false for azure_openai configuration without API key")]
    public void IsValid_AzureOpenAIConfigWithoutApiKey_ReturnsFalse()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            Type = "azure_openai",
            Endpoint = "https://my-endpoint.openai.azure.com",
            DeploymentName = "gpt-4",
            ApiKey = null
        };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Theory(DisplayName = "IsValid should return false when required fields are empty")]
    [InlineData("", "https://endpoint.com", "deployment")]
    [InlineData("azure_foundry", "", "deployment")]
    [InlineData("azure_foundry", "https://endpoint.com", "")]
    public void IsValid_MissingRequiredFields_ReturnsFalse(string type, string endpoint, string deploymentName)
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            Type = type,
            Endpoint = endpoint,
            DeploymentName = deploymentName
        };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "IsValid should be case insensitive for azure_openai type check")]
    public void IsValid_AzureOpenAICaseInsensitive_RequiresApiKey()
    {
        // Arrange
        var options = new ModelProviderDefinitionOptions
        {
            Type = "AZURE_OPENAI",
            Endpoint = "https://my-endpoint.openai.azure.com",
            DeploymentName = "gpt-4",
            ApiKey = "my-api-key"
        };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeTrue();
    }
}

/// <summary>
/// Unit tests for the <see cref="ModelProviderOptions"/> class.
/// Tests dictionary operations and default provider resolution.
/// </summary>
public class ModelProviderOptionsTests
{
    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new ModelProviderOptions();

        // Assert
        options.Providers.Should().NotBeNull();
        options.Providers.Should().BeEmpty();
        options.DefaultProviderName.Should().BeNull();
        options.VectorStoreIndexing.Should().NotBeNull();
    }

    [Fact(DisplayName = "GetDefaultProviderName should return configured default when it exists")]
    public void GetDefaultProviderName_ConfiguredDefault_ReturnsConfiguredName()
    {
        // Arrange
        var options = new ModelProviderOptions
        {
            DefaultProviderName = "my-default-provider",
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["my-default-provider"] = new ModelProviderDefinitionOptions(),
                ["other-provider"] = new ModelProviderDefinitionOptions()
            }
        };

        // Act
        var result = options.GetDefaultProviderName();

        // Assert
        result.Should().Be("my-default-provider");
    }

    [Fact(DisplayName = "GetDefaultProviderName should return first provider when default is not set")]
    public void GetDefaultProviderName_NoDefaultConfigured_ReturnsFirstProvider()
    {
        // Arrange
        var options = new ModelProviderOptions
        {
            DefaultProviderName = null,
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["first-provider"] = new ModelProviderDefinitionOptions(),
                ["second-provider"] = new ModelProviderDefinitionOptions()
            }
        };

        // Act
        var result = options.GetDefaultProviderName();

        // Assert
        result.Should().Be("first-provider");
    }

    [Fact(DisplayName = "GetDefaultProviderName should fall back to first provider when configured default doesn't exist")]
    public void GetDefaultProviderName_DefaultNotInProviders_ReturnsFirstProvider()
    {
        // Arrange
        var options = new ModelProviderOptions
        {
            DefaultProviderName = "non-existent",
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["existing-provider"] = new ModelProviderDefinitionOptions()
            }
        };

        // Act
        var result = options.GetDefaultProviderName();

        // Assert
        result.Should().Be("existing-provider");
    }

    [Fact(DisplayName = "GetDefaultProviderName should throw when no providers are configured")]
    public void GetDefaultProviderName_NoProviders_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ModelProviderOptions
        {
            Providers = []
        };

        // Act
        var act = () => options.GetDefaultProviderName();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No providers configured*");
    }
}

/// <summary>
/// Unit tests for the <see cref="TelemetryOptions"/> class.
/// Tests default values and configuration section name.
/// </summary>
public class TelemetryOptionsTests
{
    [Fact(DisplayName = "SectionName constant should be 'Telemetry'")]
    public void SectionName_StaticValue_IsTelemetry()
    {
        // Assert
        TelemetryOptions.SectionName.Should().Be("Telemetry");
    }

    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new TelemetryOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.ServiceName.Should().Be("AgentSdk");
        options.ServiceVersion.Should().Be("1.0.0");
        options.SourceName.Should().Be("AgentSdk");
        options.DeploymentEnvironment.Should().Be("development");
        options.ServiceInstanceId.Should().Be(Environment.MachineName);
        options.EnableSensitiveData.Should().BeFalse();
        options.OtlpEndpoint.Should().Be("http://localhost:4318");
    }

    [Fact(DisplayName = "Properties should be settable")]
    public void Properties_SetValues_ValuesArePersisted()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = false,
            ServiceName = "MyService",
            ServiceVersion = "2.0.0",
            SourceName = "MySource",
            DeploymentEnvironment = "production",
            ServiceInstanceId = "instance-123",
            EnableSensitiveData = true,
            OtlpEndpoint = "http://otlp:4318",
            ApplicationInsightsConnectionString = "InstrumentationKey=xxx"
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.ServiceName.Should().Be("MyService");
        options.ServiceVersion.Should().Be("2.0.0");
        options.SourceName.Should().Be("MySource");
        options.DeploymentEnvironment.Should().Be("production");
        options.ServiceInstanceId.Should().Be("instance-123");
        options.EnableSensitiveData.Should().BeTrue();
        options.OtlpEndpoint.Should().Be("http://otlp:4318");
        options.ApplicationInsightsConnectionString.Should().Be("InstrumentationKey=xxx");
    }
}

/// <summary>
/// Unit tests for the <see cref="VectorStoreIndexingOptions"/> class.
/// Tests default values for indexing configuration.
/// </summary>
public class VectorStoreIndexingOptionsTests
{
    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new VectorStoreIndexingOptions();

        // Assert
        options.MaxWaitAttempts.Should().Be(60);
        options.InitialWaitDelayMs.Should().Be(2000);
        options.UseExponentialBackoff.Should().BeTrue();
        options.MaxWaitDelayMs.Should().Be(30000);
        options.TotalTimeoutMs.Should().Be(0);
    }

    [Fact(DisplayName = "Properties should be settable")]
    public void Properties_SetValues_ValuesArePersisted()
    {
        // Arrange
        var options = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 30,
            InitialWaitDelayMs = 1000,
            UseExponentialBackoff = false,
            MaxWaitDelayMs = 60000,
            TotalTimeoutMs = 120000
        };

        // Assert
        options.MaxWaitAttempts.Should().Be(30);
        options.InitialWaitDelayMs.Should().Be(1000);
        options.UseExponentialBackoff.Should().BeFalse();
        options.MaxWaitDelayMs.Should().Be(60000);
        options.TotalTimeoutMs.Should().Be(120000);
    }
}

/// <summary>
/// Unit tests for the <see cref="AgentDefinitionOptions"/> class.
/// Tests default values and configuration structure.
/// </summary>
public class AgentDefinitionOptionsTests
{
    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new AgentDefinitionOptions();

        // Assert
        options.Type.Should().Be(string.Empty);
        options.Enabled.Should().BeTrue();
        options.AutoDelete.Should().BeTrue();
        options.AutoCleanupResources.Should().BeFalse();
        options.SystemPromptTemplate.Should().BeNull();
        options.UserPromptTemplate.Should().BeNull();
        options.AIFrameworkOptions.Should().NotBeNull();
        options.Metadata.Should().NotBeNull();
    }

    [Fact(DisplayName = "Properties should be settable")]
    public void Properties_SetValues_ValuesArePersisted()
    {
        // Arrange
        var options = new AgentDefinitionOptions
        {
            Type = "classification",
            Enabled = false,
            AutoDelete = false,
            AutoCleanupResources = true,
            SystemPromptTemplate = "You are a {{role}} assistant.",
            UserPromptTemplate = "Classify the following: {{text}}"
        };

        // Assert
        options.Type.Should().Be("classification");
        options.Enabled.Should().BeFalse();
        options.AutoDelete.Should().BeFalse();
        options.AutoCleanupResources.Should().BeTrue();
        options.SystemPromptTemplate.Should().Be("You are a {{role}} assistant.");
        options.UserPromptTemplate.Should().Be("Classify the following: {{text}}");
    }
}

/// <summary>
/// Unit tests for the <see cref="AgentOptions"/> class.
/// Tests dictionary initialization and configuration.
/// </summary>
public class AgentOptionsTests
{
    [Fact(DisplayName = "Default Agents dictionary should be empty")]
    public void Constructor_DefaultValues_AgentsDictionaryIsEmpty()
    {
        // Arrange & Act
        var options = new AgentOptions();

        // Assert
        options.Agents.Should().NotBeNull();
        options.Agents.Should().BeEmpty();
    }

    [Fact(DisplayName = "Agents dictionary should support adding agents")]
    public void Agents_AddAgent_AgentIsStored()
    {
        // Arrange
        var options = new AgentOptions();
        var agentDefinition = new AgentDefinitionOptions
        {
            Type = "classification",
            Enabled = true
        };

        // Act
        options.Agents["classification_agent"] = agentDefinition;

        // Assert
        options.Agents.Should().ContainKey("classification_agent");
        options.Agents["classification_agent"].Should().Be(agentDefinition);
    }
}

/// <summary>
/// Unit tests for the <see cref="AIFrameworkOptions"/> class.
/// Tests required provider configuration.
/// </summary>
public class AIFrameworkOptionsTests
{
    [Fact(DisplayName = "Default Provider should be empty string")]
    public void Constructor_DefaultValues_ProviderIsEmpty()
    {
        // Arrange & Act
        var options = new AIFrameworkOptions();

        // Assert
        options.Provider.Should().Be(string.Empty);
    }

    [Fact(DisplayName = "Provider property should be settable")]
    public void Provider_SetValue_ValueIsPersisted()
    {
        // Arrange
        var options = new AIFrameworkOptions
        {
            Provider = "azure_foundry"
        };

        // Assert
        options.Provider.Should().Be("azure_foundry");
    }
}

/// <summary>
/// Unit tests for the <see cref="AgentMetadataOptions"/> class.
/// Tests metadata configuration structure.
/// </summary>
public class AgentMetadataOptionsTests
{
    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new AgentMetadataOptions();

        // Assert
        options.Description.Should().Be(string.Empty);
        options.Tools.Should().NotBeNull();
        options.Tools.Should().BeEmpty();
    }

    [Fact(DisplayName = "Properties should be settable")]
    public void Properties_SetValues_ValuesArePersisted()
    {
        // Arrange
        var options = new AgentMetadataOptions
        {
            Description = "A classification agent",
            Tools = ["file_search", "code_interpreter"]
        };

        // Assert
        options.Description.Should().Be("A classification agent");
        options.Tools.Should().HaveCount(2);
        options.Tools.Should().Contain("file_search");
        options.Tools.Should().Contain("code_interpreter");
    }
}

/// <summary>
/// Unit tests for the <see cref="PdfConversionOptions"/> class.
/// Tests PDF conversion configuration defaults.
/// </summary>
public class PdfConversionOptionsTests
{
    [Fact(DisplayName = "SectionName constant should be 'PdfConversion'")]
    public void SectionName_StaticValue_IsPdfConversion()
    {
        // Assert
        PdfConversionOptions.SectionName.Should().Be("PdfConversion");
    }

    [Fact(DisplayName = "Default values should be properly initialized")]
    public void Constructor_DefaultValues_AreProperlyInitialized()
    {
        // Arrange & Act
        var options = new PdfConversionOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.SaveMarkdownForDebug.Should().BeFalse();
        options.OutputDirectory.Should().Be("./output");
        options.IncludePageNumbers.Should().BeTrue();
        options.PreserveParagraphStructure.Should().BeTrue();
        options.IncludeTimestampInFilename.Should().BeTrue();
        options.MarkdownFileExtension.Should().Be(".md");
    }

    [Fact(DisplayName = "Properties should be settable")]
    public void Properties_SetValues_ValuesArePersisted()
    {
        // Arrange
        var options = new PdfConversionOptions
        {
            Enabled = false,
            SaveMarkdownForDebug = true,
            OutputDirectory = "/tmp/markdown",
            IncludePageNumbers = false,
            PreserveParagraphStructure = false,
            IncludeTimestampInFilename = false,
            MarkdownFileExtension = ".txt"
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.SaveMarkdownForDebug.Should().BeTrue();
        options.OutputDirectory.Should().Be("/tmp/markdown");
        options.IncludePageNumbers.Should().BeFalse();
        options.PreserveParagraphStructure.Should().BeFalse();
        options.IncludeTimestampInFilename.Should().BeFalse();
        options.MarkdownFileExtension.Should().Be(".txt");
    }
}
