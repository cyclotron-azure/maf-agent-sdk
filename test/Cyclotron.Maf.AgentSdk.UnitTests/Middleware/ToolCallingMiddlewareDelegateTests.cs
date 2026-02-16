using Cyclotron.Maf.AgentSdk.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="ToolCallingMiddlewareDelegate"/> and <see cref="ToolCallsHandler"/>.
/// </summary>
public class ToolCallingMiddlewareDelegateTests
{
    [Fact]
    public void ToolCallsHandler_WithNullAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolCallsHandler(null!));
    }

    [Fact]
    public void ToolCallsHandler_WithValidAction_Constructs()
    {
        // Arrange
        Action<ToolCallingDetails> action = details => { };

        // Act
        var handler = new ToolCallsHandler(action);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void ToolCallingDetails_RecordProperties_SetCorrectly()
    {
        // Arrange
        var toolName = "test-tool";
        var arguments = new AIFunctionArguments();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var details = new ToolCallingDetails(toolName, arguments, timestamp);

        // Assert
        Assert.Equal(toolName, details.ToolName);
        Assert.Same(arguments, details.Arguments);
        Assert.Equal(timestamp, details.Timestamp);
    }

    [Fact]
    public void ToolCallingDetails_WithDifferentValues_NotEqual()
    {
        // Arrange
        var arguments1 = new AIFunctionArguments();
        var arguments2 = new AIFunctionArguments();
        var timestamp = DateTimeOffset.UtcNow;

        var details1 = new ToolCallingDetails("tool1", arguments1, timestamp);
        var details2 = new ToolCallingDetails("tool2", arguments2, timestamp);

        // Assert
        Assert.NotEqual(details1, details2);
    }

    [Fact]
    public void ToolCallingDetails_WithSameValues_Equal()
    {
        // Arrange
        var arguments = new AIFunctionArguments();
        var timestamp = DateTimeOffset.UtcNow;

        var details1 = new ToolCallingDetails("tool", arguments, timestamp);
        var details2 = new ToolCallingDetails("tool", arguments, timestamp);

        // Assert
        Assert.Equal(details1, details2);
    }

    [Fact]
    public async Task ToolCallingMiddlewareDelegate_CanBeInvoked()
    {
        // Arrange
        var delegateInvoked = false;

        ToolCallingMiddlewareDelegate middleware = async (agent, context, next, ct) =>
        {
            delegateInvoked = true;
            return await next(context, ct);
        };

        var mockAgent = new Mock<AIAgent>();
        var mockContext = new Mock<FunctionInvocationContext>();

        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next =
            (ctx, ct) => ValueTask.FromResult<object?>("result");

        // Act
        var result = await middleware(mockAgent.Object, mockContext.Object, next, CancellationToken.None);

        // Assert
        Assert.True(delegateInvoked);
        Assert.Equal("result", result);
    }

    [Fact]
    public async Task ToolCallingMiddlewareDelegate_CanModifyResult()
    {
        // Arrange
        ToolCallingMiddlewareDelegate middleware = async (agent, context, next, ct) =>
        {
            var result = await next(context, ct);
            return $"Modified: {result}";
        };

        var mockAgent = new Mock<AIAgent>();
        var mockContext = new Mock<FunctionInvocationContext>();

        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next =
            (ctx, ct) => ValueTask.FromResult<object?>("original");

        // Act
        var result = await middleware(mockAgent.Object, mockContext.Object, next, CancellationToken.None);

        // Assert
        Assert.Equal("Modified: original", result);
    }

    [Fact]
    public async Task ToolCallingMiddlewareDelegate_CanShortCircuit()
    {
        // Arrange
        var nextCalled = false;

        ToolCallingMiddlewareDelegate middleware = (agent, context, next, ct) =>
        {
            // Short-circuit without calling next
            return ValueTask.FromResult<object?>("short-circuit");
        };

        var mockAgent = new Mock<AIAgent>();
        var mockContext = new Mock<FunctionInvocationContext>();

        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next =
            (ctx, ct) =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("should-not-reach");
            };

        // Act
        var result = await middleware(mockAgent.Object, mockContext.Object, next, CancellationToken.None);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal("short-circuit", result);
    }

    [Fact]
    public async Task ToolCallingMiddlewareDelegate_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        ToolCallingMiddlewareDelegate middleware = (agent, context, next, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return next(context, ct);
        };

        var mockAgent = new Mock<AIAgent>();
        var mockContext = new Mock<FunctionInvocationContext>();

        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next =
            (ctx, ct) => ValueTask.FromResult<object?>(null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await middleware(mockAgent.Object, mockContext.Object, next, cts.Token);
        });
    }
}
