using Cyclotron.Maf.AgentSdk.Middleware;
using Microsoft.Agents.AI;
using Moq;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="AgentOpenTelemetryOptions"/>.
/// </summary>
public class AgentOpenTelemetryOptionsTests
{
    [Fact]
    public void Constructor_WithNullSource_SetsSourceToNull()
    {
        // Act
        var options = new AgentOpenTelemetryOptions(null, null);

        // Assert
        Assert.Null(options.Source);
        Assert.Null(options.Configure);
    }

    [Fact]
    public void Constructor_WithValidSource_SetsSourceCorrectly()
    {
        // Arrange
        var sourceName = "test-source";

        // Act
        var options = new AgentOpenTelemetryOptions(sourceName, null);

        // Assert
        Assert.Equal(sourceName, options.Source);
        Assert.Null(options.Configure);
    }

    [Fact]
    public void Constructor_WithConfigureAction_SetsConfigureCorrectly()
    {
        // Arrange
        var sourceName = "test-source";
        Action<OpenTelemetryAgent> configure = agent => { };

        // Act
        var options = new AgentOpenTelemetryOptions(sourceName, configure);

        // Assert
        Assert.Equal(sourceName, options.Source);
        Assert.Same(configure, options.Configure);
    }

    [Fact(Skip = "Requires mocking sealed type OpenTelemetryAgent - candidate for integration testing")]
    public void Constructor_WithBothParameters_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var sourceName = "test-source";
        var actionCalled = false;
        Action<OpenTelemetryAgent> configure = agent =>
        {
            actionCalled = true;
            agent.EnableSensitiveData = true;
        };

        // Act
        var options = new AgentOpenTelemetryOptions(sourceName, configure);

        // Assert
        Assert.Equal(sourceName, options.Source);
        Assert.NotNull(options.Configure);

        // Verify the action works
        var mockAgent = new Mock<OpenTelemetryAgent>();
        options.Configure?.Invoke(mockAgent.Object);
        Assert.True(actionCalled);
    }

    [Theory]
    [InlineData("source1")]
    [InlineData("source2")]
    [InlineData("my-custom-source")]
    public void Source_AcceptsDifferentValues(string sourceName)
    {
        // Act
        var options = new AgentOpenTelemetryOptions(sourceName, null);

        // Assert
        Assert.Equal(sourceName, options.Source);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var sourceName = "test-source";
        Action<OpenTelemetryAgent>? configure = null;

        var options1 = new AgentOpenTelemetryOptions(sourceName, configure);
        var options2 = new AgentOpenTelemetryOptions(sourceName, configure);

        // Assert
        Assert.Equal(options1, options2);
        Assert.True(options1 == options2);
    }

    [Fact]
    public void RecordEquality_WithDifferentSources_AreNotEqual()
    {
        // Arrange
        var options1 = new AgentOpenTelemetryOptions("source1", null);
        var options2 = new AgentOpenTelemetryOptions("source2", null);

        // Assert
        Assert.NotEqual(options1, options2);
        Assert.True(options1 != options2);
    }
}
