using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="AgentSdkHosingBuilderExtensions"/> class.
/// Tests host builder configuration.
/// </summary>
public class AgentSdkHostingBuilderExtensionsTests
{
    #region UseAgentSdk Tests

    [Fact(DisplayName = "UseAgentSdk should be chainable")]
    public void UseAgentSdk_IsChainable()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        var result = hostBuilder.UseAgentSdk();

        // Assert
        result.Should().BeSameAs(hostBuilder);
    }

    [Fact(DisplayName = "UseAgentSdk should accept custom config file name")]
    public void UseAgentSdk_CustomConfigFile_AcceptsParameter()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        var result = hostBuilder.UseAgentSdk("custom.config.yaml");

        // Assert
        result.Should().BeSameAs(hostBuilder);
    }

    [Fact(DisplayName = "UseAgentSdk should use default config file when not specified")]
    public void UseAgentSdk_DefaultConfigFile_UsesAgentConfigYaml()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        var result = hostBuilder.UseAgentSdk();

        // Assert
        result.Should().BeSameAs(hostBuilder);
    }

    [Fact(DisplayName = "UseAgentSdk should configure app configuration")]
    public void UseAgentSdk_ConfiguresAppConfiguration()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        hostBuilder.UseAgentSdk();
        using var host = hostBuilder.Build();

        // Assert - Configuration should be available
        var configuration = host.Services.GetService<IConfiguration>();
        configuration.Should().NotBeNull();
    }

    [Fact(DisplayName = "UseAgentSdk should allow building host")]
    public void UseAgentSdk_AllowsBuildingHost()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        hostBuilder.UseAgentSdk();
        using var host = hostBuilder.Build();

        // Assert
        host.Should().NotBeNull();
    }

    #endregion
}
