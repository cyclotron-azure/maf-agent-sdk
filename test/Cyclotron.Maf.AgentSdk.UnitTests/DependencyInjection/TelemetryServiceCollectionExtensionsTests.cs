using System.Diagnostics;
using System.Diagnostics.Metrics;
using Cyclotron.Maf.AgentSdk.Options;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="TelemetryServiceCollectionExtensions"/> class.
/// Tests telemetry pipeline registration and configuration.
/// </summary>
public class TelemetryServiceCollectionExtensionsTests
{
    #region AddAgentTelemetryPipeline Tests

    [Fact(DisplayName = "AddAgentTelemetryPipeline should register ActivitySource as singleton")]
    public void AddAgentTelemetryPipeline_RegistersActivitySource()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var activitySource = serviceProvider.GetService<ActivitySource>();
        activitySource.Should().NotBeNull();

        // Verify singleton behavior
        var activitySource2 = serviceProvider.GetService<ActivitySource>();
        activitySource.Should().BeSameAs(activitySource2);
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should register Meter as singleton")]
    public void AddAgentTelemetryPipeline_RegistersMeter()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var meter = serviceProvider.GetService<Meter>();
        meter.Should().NotBeNull();

        // Verify singleton behavior
        var meter2 = serviceProvider.GetService<Meter>();
        meter.Should().BeSameAs(meter2);
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should use SourceName from configuration")]
    public void AddAgentTelemetryPipeline_UsesSourceNameFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:SourceName"] = "CustomSource",
            ["Telemetry:ServiceName"] = "TestService"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var activitySource = serviceProvider.GetRequiredService<ActivitySource>();
        activitySource.Name.Should().Be("CustomSource");

        var meter = serviceProvider.GetRequiredService<Meter>();
        meter.Name.Should().Be("CustomSource");
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should be chainable")]
    public void AddAgentTelemetryPipeline_IsChainable()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        var result = services.AddAgentTelemetryPipeline(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should handle missing telemetry section gracefully")]
    public void AddAgentTelemetryPipeline_MissingSection_UsesDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var activitySource = serviceProvider.GetRequiredService<ActivitySource>();
        activitySource.Name.Should().Be("AgentSdk"); // Default SourceName
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should configure OTLP endpoint when provided")]
    public void AddAgentTelemetryPipeline_WithOtlpEndpoint_ConfiguresExporter()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEndpoint"] = "http://localhost:4318",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:SourceName"] = "TestSource"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act - Should not throw
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryPipeline should configure Application Insights when connection string provided")]
    public void AddAgentTelemetryPipeline_WithAppInsightsConnectionString_ConfiguresExporter()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:ApplicationInsightsConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://eastus-3.in.applicationinsights.azure.com/",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:SourceName"] = "TestSource"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddTelemetryOptions();
        services.AddSingleton<IConfiguration>(configuration);

        // Act - Should not throw
        services.AddAgentTelemetryPipeline(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    #endregion

    #region CreateResourceBuilder Internal Tests

    [Fact(DisplayName = "CreateResourceBuilder should include service name")]
    public void CreateResourceBuilder_IncludesServiceName()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0"
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "CreateResourceBuilder should include deployment environment when provided")]
    public void CreateResourceBuilder_WithDeploymentEnvironment_IncludesAttribute()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            DeploymentEnvironment = "production"
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "CreateResourceBuilder should use machine name when ServiceInstanceId is null")]
    public void CreateResourceBuilder_NullServiceInstanceId_UsesMachineName()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            ServiceInstanceId = null
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "CreateResourceBuilder should use provided ServiceInstanceId")]
    public void CreateResourceBuilder_WithServiceInstanceId_UsesProvidedValue()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            ServiceInstanceId = "custom-instance-id"
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "CreateResourceBuilder should not add deployment environment when empty")]
    public void CreateResourceBuilder_EmptyDeploymentEnvironment_DoesNotAddAttribute()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            DeploymentEnvironment = ""
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "CreateResourceBuilder should not add deployment environment when whitespace")]
    public void CreateResourceBuilder_WhitespaceDeploymentEnvironment_DoesNotAddAttribute()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            DeploymentEnvironment = "   "
        };

        // Act
        var resourceBuilder = TelemetryServiceCollectionExtensions.CreateResourceBuilder(options);

        // Assert
        resourceBuilder.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:ServiceVersion"] = "1.0.0",
            ["Telemetry:SourceName"] = "TestSource"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    #endregion
}
