using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="AgentSdkServiceCollectionExtensions"/> class.
/// Tests service registration and configuration binding.
/// </summary>
public class AgentSdkServiceCollectionExtensionsTests
{
    #region AddAgentSdkServices Tests

    [Fact(DisplayName = "AddAgentSdkServices should register IConfigurationValueSubstitution as singleton")]
    public void AddAgentSdkServices_RegistersConfigurationValueSubstitution()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        services.AddAgentSdkServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var service = serviceProvider.GetService<IConfigurationValueSubstitution>();
        service.Should().NotBeNull();
        service.Should().BeOfType<ConfigurationValueSubstitution>();

        // Verify singleton behavior
        var service2 = serviceProvider.GetService<IConfigurationValueSubstitution>();
        service.Should().BeSameAs(service2);
    }

    [Fact(DisplayName = "AddAgentSdkServices should register IPdfToMarkdownConverter as singleton")]
    public void AddAgentSdkServices_RegistersPdfToMarkdownConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();

        // Act
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var service = serviceProvider.GetService<IPdfToMarkdownConverter>();
        service.Should().NotBeNull();
        service.Should().BeOfType<PdfPigMarkdownConverter>();
    }

    [Fact(DisplayName = "AddAgentSdkServices should be chainable")]
    public void AddAgentSdkServices_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        var result = services.AddAgentSdkServices();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region AddAgentOptions Tests

    [Fact(DisplayName = "AddAgentOptions should register AgentOptions with empty configuration")]
    public void AddAgentOptions_EmptyConfiguration_RegistersEmptyAgents()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddAgentOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<AgentOptions>>();
        options.Should().NotBeNull();
        options!.Value.Agents.Should().BeEmpty();
    }

    [Fact(DisplayName = "AddAgentOptions should bind agents from configuration")]
    public void AddAgentOptions_WithConfiguration_BindsAgents()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["agents:classification_agent:type"] = "classification",
            ["agents:classification_agent:auto_delete"] = "false",
            ["agents:classification_agent:auto_cleanup_resources"] = "true",
            ["agents:classification_agent:enabled"] = "true",
            ["agents:classification_agent:system_prompt_template"] = "You are a classifier.",
            ["agents:classification_agent:user_prompt_template"] = "Classify this: {{text}}"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<AgentOptions>>();
        options.Value.Agents.Should().ContainKey("classification_agent");

        var agentDef = options.Value.Agents["classification_agent"];
        agentDef.Type.Should().Be("classification");
        agentDef.AutoDelete.Should().BeFalse();
        agentDef.AutoCleanupResources.Should().BeTrue();
        agentDef.Enabled.Should().BeTrue();
        agentDef.SystemPromptTemplate.Should().Be("You are a classifier.");
        agentDef.UserPromptTemplate.Should().Be("Classify this: {{text}}");
    }

    [Fact(DisplayName = "AddAgentOptions should bind metadata section")]
    public void AddAgentOptions_WithMetadata_BindsMetadata()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["agents:test_agent:type"] = "test",
            ["agents:test_agent:metadata:description"] = "Test agent description",
            ["agents:test_agent:metadata:tools:0"] = "file_search",
            ["agents:test_agent:metadata:tools:1"] = "code_interpreter"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<AgentOptions>>();
        var agentDef = options.Value.Agents["test_agent"];

        agentDef.Metadata.Should().NotBeNull();
        agentDef.Metadata!.Description.Should().Be("Test agent description");
        agentDef.Metadata.Tools.Should().Contain("file_search");
        agentDef.Metadata.Tools.Should().Contain("code_interpreter");
    }

    [Fact(DisplayName = "AddAgentOptions should bind framework_config section")]
    public void AddAgentOptions_WithFrameworkConfig_BindsAIFrameworkOptions()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["agents:test_agent:type"] = "test",
            ["agents:test_agent:framework_config:provider"] = "azure_foundry"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<AgentOptions>>();
        var agentDef = options.Value.Agents["test_agent"];

        agentDef.AIFrameworkOptions.Should().NotBeNull();
        agentDef.AIFrameworkOptions!.Provider.Should().Be("azure_foundry");
    }

    [Fact(DisplayName = "AddAgentOptions should use default values when not specified")]
    public void AddAgentOptions_DefaultValues_UsesDefaults()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["agents:minimal_agent:type"] = "minimal"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<AgentOptions>>();
        var agentDef = options.Value.Agents["minimal_agent"];

        agentDef.AutoDelete.Should().BeTrue(); // Default
        agentDef.AutoCleanupResources.Should().BeTrue(); // Default
        agentDef.Enabled.Should().BeTrue(); // Default
    }

    [Fact(DisplayName = "AddAgentOptions should be chainable")]
    public void AddAgentOptions_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        var result = services.AddAgentOptions();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region AddTelemetryOptions Tests

    [Fact(DisplayName = "AddTelemetryOptions should register TelemetryOptions with empty configuration")]
    public void AddTelemetryOptions_EmptyConfiguration_RegistersDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddTelemetryOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<TelemetryOptions>>();
        options.Should().NotBeNull();
        options!.Value.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddTelemetryOptions should bind from Telemetry section")]
    public void AddTelemetryOptions_WithConfiguration_BindsValues()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:ServiceVersion"] = "1.0.0",
            ["Telemetry:SourceName"] = "TestSource",
            ["Telemetry:DeploymentEnvironment"] = "production"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddTelemetryOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<TelemetryOptions>>();
        options.Value.Enabled.Should().BeTrue();
        options.Value.ServiceName.Should().Be("TestService");
        options.Value.ServiceVersion.Should().Be("1.0.0");
        options.Value.SourceName.Should().Be("TestSource");
        options.Value.DeploymentEnvironment.Should().Be("production");
    }

    [Fact(DisplayName = "AddTelemetryOptions should be chainable")]
    public void AddTelemetryOptions_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        var result = services.AddTelemetryOptions();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region AddModelProviderOptions Tests

    [Fact(DisplayName = "AddModelProviderOptions should register ModelProviderOptions with empty configuration")]
    public void AddModelProviderOptions_EmptyConfiguration_RegistersEmptyProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        services.AddModelProviderOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<ModelProviderOptions>>();
        options.Should().NotBeNull();
        options!.Value.Providers.Should().BeEmpty();
    }

    [Fact(DisplayName = "AddModelProviderOptions should bind providers from configuration")]
    public void AddModelProviderOptions_WithConfiguration_BindsProviders()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["providers:azure_foundry:type"] = "azure_foundry",
            ["providers:azure_foundry:endpoint"] = "https://test.azure.com",
            ["providers:azure_foundry:deployment_name"] = "gpt-4",
            ["providers:azure_foundry:timeout_seconds"] = "600",
            ["providers:azure_foundry:max_retries"] = "5"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        services.AddModelProviderOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<ModelProviderOptions>>();
        options.Value.Providers.Should().ContainKey("azure_foundry");

        var provider = options.Value.Providers["azure_foundry"];
        provider.Type.Should().Be("azure_foundry");
        provider.Endpoint.Should().Be("https://test.azure.com");
        provider.DeploymentName.Should().Be("gpt-4");
        provider.TimeoutSeconds.Should().Be(600);
        provider.MaxRetries.Should().Be(5);
    }

    [Fact(DisplayName = "AddModelProviderOptions should use default values when not specified")]
    public void AddModelProviderOptions_DefaultValues_UsesDefaults()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["providers:test_provider:type"] = "azure_foundry",
            ["providers:test_provider:endpoint"] = "https://test.azure.com",
            ["providers:test_provider:deployment_name"] = "gpt-4"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        services.AddModelProviderOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<ModelProviderOptions>>();
        var provider = options.Value.Providers["test_provider"];

        provider.TimeoutSeconds.Should().Be(300); // Default
        provider.MaxRetries.Should().Be(3); // Default
    }

    [Fact(DisplayName = "AddModelProviderOptions should throw for invalid provider configuration")]
    public void AddModelProviderOptions_InvalidConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange - Missing required fields
        var configData = new Dictionary<string, string?>
        {
            ["providers:invalid_provider:type"] = "" // Empty type
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        services.AddModelProviderOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should throw when accessing the options
        var act = () => serviceProvider.GetRequiredService<IOptions<ModelProviderOptions>>().Value;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not properly configured*");
    }

    [Fact(DisplayName = "AddModelProviderOptions should be chainable")]
    public void AddModelProviderOptions_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();
        services.AddLogging(); // Required by ConfigurationValueSubstitution

        // Act
        var result = services.AddModelProviderOptions();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region AddPdfConversionOptions Tests

    [Fact(DisplayName = "AddPdfConversionOptions should register PdfConversionOptions with empty configuration")]
    public void AddPdfConversionOptions_EmptyConfiguration_RegistersDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddPdfConversionOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<PdfConversionOptions>>();
        options.Should().NotBeNull();
        options!.Value.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddPdfConversionOptions should bind from PdfConversion section")]
    public void AddPdfConversionOptions_WithConfiguration_BindsValues()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["PdfConversion:SaveMarkdownForDebug"] = "true",
            ["PdfConversion:OutputDirectory"] = "/custom/output",
            ["PdfConversion:IncludeTimestampInFilename"] = "false",
            ["PdfConversion:MarkdownFileExtension"] = ".markdown",
            ["PdfConversion:IncludePageNumbers"] = "false",
            ["PdfConversion:PreserveParagraphStructure"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddPdfConversionOptions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<PdfConversionOptions>>();
        options.Value.SaveMarkdownForDebug.Should().BeTrue();
        options.Value.OutputDirectory.Should().Be("/custom/output");
        options.Value.IncludeTimestampInFilename.Should().BeFalse();
        options.Value.MarkdownFileExtension.Should().Be(".markdown");
        options.Value.IncludePageNumbers.Should().BeFalse();
        options.Value.PreserveParagraphStructure.Should().BeFalse();
    }

    [Fact(DisplayName = "AddPdfConversionOptions should be chainable")]
    public void AddPdfConversionOptions_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        var result = services.AddPdfConversionOptions();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region Named Options Tests

    [Fact(DisplayName = "AddAgentOptions should support named options")]
    public void AddAgentOptions_NamedOptions_RegistersSeparately()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddAgentOptions();
        services.AddAgentOptions("custom");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var defaultOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<AgentOptions>>().Value;
        var customOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<AgentOptions>>().Get("custom");

        defaultOptions.Should().NotBeNull();
        customOptions.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddTelemetryOptions should support named options")]
    public void AddTelemetryOptions_NamedOptions_RegistersSeparately()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddTelemetryOptions();
        services.AddTelemetryOptions("custom");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var defaultOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<TelemetryOptions>>().Value;
        var customOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<TelemetryOptions>>().Get("custom");

        defaultOptions.Should().NotBeNull();
        customOptions.Should().NotBeNull();
    }

    #endregion
}
