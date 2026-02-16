using Cyclotron.Maf.AgentSdk.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="AgentLoggingOptions"/>.
/// </summary>
public class AgentLoggingOptionsTests
{
    [Fact]
    public void Constructor_WithNullLoggerFactory_SetsLoggerFactoryToNull()
    {
        // Act
        var options = new AgentLoggingOptions(null, null);

        // Assert
        Assert.Null(options.LoggerFactory);
        Assert.Null(options.Configure);
    }

    [Fact]
    public void Constructor_WithValidLoggerFactory_SetsLoggerFactoryCorrectly()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();

        // Act
        var options = new AgentLoggingOptions(mockLoggerFactory.Object, null);

        // Assert
        Assert.Same(mockLoggerFactory.Object, options.LoggerFactory);
        Assert.Null(options.Configure);
    }

    [Fact]
    public void Constructor_WithConfigureAction_SetsConfigureCorrectly()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        Action<LoggingAgent> configure = agent => { };

        // Act
        var options = new AgentLoggingOptions(mockLoggerFactory.Object, configure);

        // Assert
        Assert.Same(mockLoggerFactory.Object, options.LoggerFactory);
        Assert.Same(configure, options.Configure);
    }

    [Fact(Skip = "Requires mocking sealed type LoggingAgent - candidate for integration testing")]
    public void Constructor_WithBothParameters_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var actionCalled = false;
        Action<LoggingAgent> configure = agent =>
        {
            actionCalled = true;
        };

        // Act
        var options = new AgentLoggingOptions(mockLoggerFactory.Object, configure);

        // Assert
        Assert.Same(mockLoggerFactory.Object, options.LoggerFactory);
        Assert.NotNull(options.Configure);

        // Verify the action works
        var mockAgent = new Mock<LoggingAgent>();
        options.Configure?.Invoke(mockAgent.Object);
        Assert.True(actionCalled);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        Action<LoggingAgent>? configure = null;

        var options1 = new AgentLoggingOptions(mockLoggerFactory.Object, configure);
        var options2 = new AgentLoggingOptions(mockLoggerFactory.Object, configure);

        // Assert
        Assert.Equal(options1, options2);
        Assert.True(options1 == options2);
    }

    [Fact]
    public void RecordEquality_WithDifferentLoggerFactories_AreNotEqual()
    {
        // Arrange
        var mockLoggerFactory1 = new Mock<ILoggerFactory>();
        var mockLoggerFactory2 = new Mock<ILoggerFactory>();

        var options1 = new AgentLoggingOptions(mockLoggerFactory1.Object, null);
        var options2 = new AgentLoggingOptions(mockLoggerFactory2.Object, null);

        // Assert
        Assert.NotEqual(options1, options2);
        Assert.True(options1 != options2);
    }

    [Fact]
    public void RecordEquality_WithNullValues_AreEqual()
    {
        // Arrange
        var options1 = new AgentLoggingOptions(null, null);
        var options2 = new AgentLoggingOptions(null, null);

        // Assert
        Assert.Equal(options1, options2);
        Assert.True(options1 == options2);
    }
}
