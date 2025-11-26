using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="DocumentWorkflowServiceExtensions"/> class.
/// Tests service registration for workflow services.
/// </summary>
public class DocumentWorkflowServiceExtensionsTests
{
    #region AddDocumentWorkflowServices Tests

    [Fact(DisplayName = "AddDocumentWorkflowServices should register IPersistentAgentsClientFactory as scoped")]
    public void AddDocumentWorkflowServices_RegistersPersistentAgentsClientFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetService<IPersistentAgentsClientFactory>();
        service.Should().NotBeNull();
        service.Should().BeOfType<PersistentAgentsClientFactory>();
    }

    [Fact(DisplayName = "AddDocumentWorkflowServices should register IVectorStoreManager as scoped")]
    public void AddDocumentWorkflowServices_RegistersVectorStoreManager()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetService<IVectorStoreManager>();
        service.Should().NotBeNull();
        service.Should().BeOfType<VectorStoreManager>();
    }

    [Fact(DisplayName = "AddDocumentWorkflowServices should register IAzureFoundryCleanupService as scoped")]
    public void AddDocumentWorkflowServices_RegistersAzureFoundryCleanupService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetService<IAzureFoundryCleanupService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<AzureFoundryCleanupService>();
    }

    [Fact(DisplayName = "AddDocumentWorkflowServices should register IPromptRenderingService as singleton")]
    public void AddDocumentWorkflowServices_RegistersPromptRenderingService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var service1 = serviceProvider.GetService<IPromptRenderingService>();
        var service2 = serviceProvider.GetService<IPromptRenderingService>();

        service1.Should().NotBeNull();
        service1.Should().BeOfType<PromptRenderingService>();
        service1.Should().BeSameAs(service2); // Singleton behavior
    }

    [Fact(DisplayName = "AddDocumentWorkflowServices should register AgentTemplateValidationService as hosted service")]
    public void AddDocumentWorkflowServices_RegistersAgentTemplateValidationService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        services.AddDocumentWorkflowServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s is AgentTemplateValidationService);
    }

    [Fact(DisplayName = "AddDocumentWorkflowServices should be chainable")]
    public void AddDocumentWorkflowServices_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();

        // Act
        var result = services.AddDocumentWorkflowServices();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region AddKeyedAgentFactories Tests

    [Fact(DisplayName = "AddKeyedAgentFactories should register keyed agent factory")]
    public void AddKeyedAgentFactories_SingleKey_RegistersKeyedFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        services.AddKeyedAgentFactories("classification");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetKeyedService<IAgentFactory>("classification");
        factory.Should().NotBeNull();
        factory.Should().BeOfType<AgentFactory>();
    }

    [Fact(DisplayName = "AddKeyedAgentFactories should register multiple keyed factories")]
    public void AddKeyedAgentFactories_MultipleKeys_RegistersAllFactories()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        services.AddKeyedAgentFactories("classification", "extraction");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var classificationFactory = scope.ServiceProvider.GetKeyedService<IAgentFactory>("classification");
        var extractionFactory = scope.ServiceProvider.GetKeyedService<IAgentFactory>("extraction");

        classificationFactory.Should().NotBeNull();
        extractionFactory.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddKeyedAgentFactories should return null for unregistered keys")]
    public void AddKeyedAgentFactories_UnregisteredKey_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        services.AddKeyedAgentFactories("classification");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetKeyedService<IAgentFactory>("unregistered");
        factory.Should().BeNull();
    }

    [Fact(DisplayName = "AddKeyedAgentFactories should create scoped instances")]
    public void AddKeyedAgentFactories_ScopedBehavior_CreatesDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        services.AddKeyedAgentFactories("classification");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var factory1 = scope1.ServiceProvider.GetKeyedService<IAgentFactory>("classification");
        var factory2 = scope2.ServiceProvider.GetKeyedService<IAgentFactory>("classification");

        // Different scopes should create different instances
        factory1.Should().NotBeSameAs(factory2);
    }

    [Fact(DisplayName = "AddKeyedAgentFactories should be chainable")]
    public void AddKeyedAgentFactories_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        var result = services.AddKeyedAgentFactories("classification");

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact(DisplayName = "AddKeyedAgentFactories should handle empty array")]
    public void AddKeyedAgentFactories_EmptyArray_ReturnsWithoutRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging();
        services.AddAgentSdkServices();
        services.AddDocumentWorkflowServices();

        // Act
        var result = services.AddKeyedAgentFactories();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateTestConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["providers:azure_foundry:type"] = "azure_foundry",
            ["providers:azure_foundry:endpoint"] = "https://test.azure.com",
            ["providers:azure_foundry:deployment_name"] = "gpt-4",
            ["agents:classification_agent:type"] = "classification",
            ["agents:classification_agent:framework_config:provider"] = "azure_foundry",
            ["agents:extraction_agent:type"] = "extraction",
            ["agents:extraction_agent:framework_config:provider"] = "azure_foundry"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    #endregion
}
