using Cyclotron.Maf.AgentSdk.Middleware;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="MiddlewareConfiguration"/>.
/// Tests the middleware configuration container and property assignments.
/// </summary>
public class MiddlewareConfigurationTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultNullValues()
    {
        // Act
        var configuration = new MiddlewareConfiguration();

        // Assert
        Assert.Null(configuration.RawToolCallDetails);
        Assert.Null(configuration.ToolCallingMiddleware);
        Assert.Null(configuration.OpenTelemetryOptions);
        Assert.Null(configuration.LoggingOptions);
    }

    [Fact]
    public void RawToolCallDetails_CanBeSet()
    {
        // Arrange
        var configuration = new MiddlewareConfiguration();
        Action<ToolCallingDetails> handler = details => { };

        // Act
        configuration.RawToolCallDetails = handler;

        // Assert
        Assert.NotNull(configuration.RawToolCallDetails);
        Assert.Same(handler, configuration.RawToolCallDetails);
    }

    [Fact]
    public void ToolCallingMiddleware_CanBeSet()
    {
        // Arrange
        var configuration = new MiddlewareConfiguration();
        ToolCallingMiddlewareDelegate middleware = async (agent, context, next, ct) =>
            await next(context, ct);

        // Act
        configuration.ToolCallingMiddleware = middleware;

        // Assert
        Assert.NotNull(configuration.ToolCallingMiddleware);
        Assert.Same(middleware, configuration.ToolCallingMiddleware);
    }

    [Fact]
    public void OpenTelemetryOptions_CanBeSet()
    {
        // Arrange
        var configuration = new MiddlewareConfiguration();
        var sourceName = "test-source";
        var options = new AgentOpenTelemetryOptions(sourceName, null);

        // Act
        configuration.OpenTelemetryOptions = options;

        // Assert
        Assert.NotNull(configuration.OpenTelemetryOptions);
        Assert.Same(options, configuration.OpenTelemetryOptions);
        Assert.Equal(sourceName, configuration.OpenTelemetryOptions.Source);
    }

    [Fact]
    public void LoggingOptions_CanBeSet()
    {
        // Arrange
        var configuration = new MiddlewareConfiguration();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var options = new AgentLoggingOptions(mockLoggerFactory.Object, null);

        // Act
        configuration.LoggingOptions = options;

        // Assert
        Assert.NotNull(configuration.LoggingOptions);
        Assert.Same(options, configuration.LoggingOptions);
        Assert.Same(mockLoggerFactory.Object, configuration.LoggingOptions.LoggerFactory);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        Action<ToolCallingDetails> rawHandler = details => { };
        ToolCallingMiddlewareDelegate middleware = async (agent, context, next, ct) =>
            await next(context, ct);
        var telemetryOptions = new AgentOpenTelemetryOptions("source", null);
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var loggingOptions = new AgentLoggingOptions(mockLoggerFactory.Object, null);

        // Act
        var configuration = new MiddlewareConfiguration
        {
            RawToolCallDetails = rawHandler,
            ToolCallingMiddleware = middleware,
            OpenTelemetryOptions = telemetryOptions,
            LoggingOptions = loggingOptions
        };

        // Assert
        Assert.NotNull(configuration.RawToolCallDetails);
        Assert.NotNull(configuration.ToolCallingMiddleware);
        Assert.NotNull(configuration.OpenTelemetryOptions);
        Assert.NotNull(configuration.LoggingOptions);
        Assert.Same(rawHandler, configuration.RawToolCallDetails);
        Assert.Same(middleware, configuration.ToolCallingMiddleware);
        Assert.Same(telemetryOptions, configuration.OpenTelemetryOptions);
        Assert.Same(loggingOptions, configuration.LoggingOptions);
    }
}
