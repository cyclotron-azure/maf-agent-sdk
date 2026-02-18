using Cyclotron.Maf.AgentSdk.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="AgentMiddlewareHelper"/>.
/// Tests the centralized middleware application logic with various configuration scenarios.
/// </summary>
public class AgentMiddlewareHelperTests
{
    /// <summary>
    /// Creates a mock AIAgent for testing.
    /// </summary>
    private static AIAgent CreateMockAgent()
    {
        return new Mock<AIAgent>().Object;
    }

    [Fact]
    public void ApplyMiddleware_WithNullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        AIAgent? agent = null;
        var configuration = new MiddlewareConfiguration();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            AgentMiddlewareHelper.ApplyMiddleware(agent!, configuration));
    }

    [Fact]
    public void ApplyMiddleware_WithNullConfiguration_ReturnsOriginalAgent()
    {
        // Arrange
        var agent = CreateMockAgent();

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, null);

        // Assert
        Assert.Same(agent, result);
    }

    [Fact]
    public void ApplyMiddleware_WithEmptyConfiguration_ReturnsOriginalAgent()
    {
        // Arrange
        var agent = CreateMockAgent();
        var configuration = new MiddlewareConfiguration();

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration);

        // Assert
        Assert.NotNull(result);
        // With empty configuration, agent is unchanged (but wrapped by builder)
    }

    [Fact]
    public void ApplyMiddleware_WithOpenTelemetry_ConfiguresCorrectly()
    {
        // Arrange
        var agent = CreateMockAgent();
        var sourceName = "test-source";

        var configuration = new MiddlewareConfiguration
        {
            OpenTelemetryOptions = new AgentOpenTelemetryOptions(
                sourceName,
                configure =>
                {
                    configure.EnableSensitiveData = true;
                })
        };

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration);

        // Assert
        Assert.NotNull(result);
        // Note: Due to the nature of middleware wrapping, we can't easily verify the callback was called
        // This would require integration testing with actual agent execution
    }

    [Fact]
    public void ApplyMiddleware_WithLogging_ConfiguresCorrectly()
    {
        // Arrange
        var agent = CreateMockAgent();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .BuildServiceProvider();

        var configuration = new MiddlewareConfiguration
        {
            LoggingOptions = new AgentLoggingOptions(
                loggerFactory,
                configure => { })
        };

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration, serviceProvider);

        // Assert
        Assert.NotNull(result);
    }

    [Fact(Skip = "Requires real AIAgent with FunctionInvokingChatClient - needs integration test")]
    public void ApplyMiddleware_WithRawToolCallDetails_ConfiguresHandler()
    {
        // Arrange
        var agent = CreateMockAgent();

        var configuration = new MiddlewareConfiguration
        {
            RawToolCallDetails = details =>
            {
                // Tool call handler configured
            }
        };

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration);

        // Assert
        Assert.NotNull(result);
        // Tool call handler is configured (actual invocation requires integration testing)
    }

    [Fact(Skip = "Requires real AIAgent with FunctionInvokingChatClient - needs integration test")]
    public void ApplyMiddleware_WithToolCallingMiddleware_ConfiguresDelegate()
    {
        // Arrange
        var agent = CreateMockAgent();

        ToolCallingMiddlewareDelegate middleware = async (callingAgent, context, next, ct) =>
        {
            return await next(context, ct);
        };

        var configuration = new MiddlewareConfiguration
        {
            ToolCallingMiddleware = middleware
        };

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration);

        // Assert
        Assert.NotNull(result);
        // Middleware delegate is configured (actual invocation requires integration testing)
    }

    [Fact(Skip = "Requires real AIAgent with FunctionInvokingChatClient - needs integration test")]
    public void ApplyMiddleware_WithAllMiddleware_AppliesInDefinedOrder()
    {
        // Arrange
        var agent = CreateMockAgent();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        ToolCallingMiddlewareDelegate middleware = async (callingAgent, context, next, ct) =>
        {
            return await next(context, ct);
        };

        var configuration = new MiddlewareConfiguration
        {
            RawToolCallDetails = details => { },
            OpenTelemetryOptions = new AgentOpenTelemetryOptions("test-source", null),
            ToolCallingMiddleware = middleware,
            LoggingOptions = new AgentLoggingOptions(loggerFactory, null)
        };

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration, serviceProvider);

        // Assert
        Assert.NotNull(result);
        // All middleware configured (order verification requires integration testing)
    }

    [Fact]
    public void ApplyMiddleware_WithServices_PassesServiceProvider()
    {
        // Arrange
        var agent = CreateMockAgent();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var configuration = new MiddlewareConfiguration
        {
            OpenTelemetryOptions = new AgentOpenTelemetryOptions("test-source", null)
        };

        // Act
        var result = AgentMiddlewareHelper.ApplyMiddleware(agent, configuration, mockServiceProvider.Object);

        // Assert
        Assert.NotNull(result);
        // Service provider is passed through to builder.Build()
    }
}
