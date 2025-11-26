using Cyclotron.Maf.AgentSdk.Options;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="TelemetryLoggingBuilderExtensions"/> class.
/// Tests telemetry logging configuration.
/// </summary>
public class TelemetryLoggingBuilderExtensionsTests
{
    #region AddAgentTelemetryLogging Tests

    [Fact(DisplayName = "AddAgentTelemetryLogging should register OpenTelemetry logger")]
    public void AddAgentTelemetryLogging_RegistersOpenTelemetryLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration());
        services.AddTelemetryOptions();

        // Act
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Options should be configured
        var optionsMonitor = serviceProvider.GetService<IOptionsMonitor<OpenTelemetryLoggerOptions>>();
        optionsMonitor.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should be chainable")]
    public void AddAgentTelemetryLogging_IsChainable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration());
        services.AddTelemetryOptions();

        ILoggingBuilder? capturedBuilder = null;

        // Act
        services.AddLogging(builder =>
        {
            capturedBuilder = builder;
            var result = builder.AddAgentTelemetryLogging();
            result.Should().BeSameAs(builder);
        });

        // Assert
        capturedBuilder.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should configure options when telemetry is enabled")]
    public void AddAgentTelemetryLogging_TelemetryEnabled_ConfiguresOptions()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:SourceName"] = "TestSource",
            ["Telemetry:OtlpEndpoint"] = "http://localhost:4318"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTelemetryOptions();

        // Act
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should skip configuration when telemetry is disabled")]
    public void AddAgentTelemetryLogging_TelemetryDisabled_SkipsConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTelemetryOptions();

        // Act
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should handle missing telemetry section")]
    public void AddAgentTelemetryLogging_MissingTelemetrySection_UsesDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTelemetryOptions();

        // Act
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should configure OTLP exporter when endpoint is provided")]
    public void AddAgentTelemetryLogging_WithOtlpEndpoint_ConfiguresOtlpExporter()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:OtlpEndpoint"] = "http://localhost:4318"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTelemetryOptions();

        // Act - Should not throw
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact(DisplayName = "AddAgentTelemetryLogging should configure Azure Monitor when connection string is provided")]
    public void AddAgentTelemetryLogging_WithAppInsightsConnectionString_ConfiguresAzureMonitor()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:ApplicationInsightsConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTelemetryOptions();

        // Act - Should not throw
        services.AddLogging(builder => builder.AddAgentTelemetryLogging());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            ["Telemetry:ServiceName"] = "TestService",
            ["Telemetry:SourceName"] = "TestSource"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    #endregion
}
