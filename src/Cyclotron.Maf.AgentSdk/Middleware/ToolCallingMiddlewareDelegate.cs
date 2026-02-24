using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Cyclotron.Maf.AgentSdk.Middleware;

/// <summary>
/// Delegate for tool calling middleware that allows interception and modification of tool calls.
/// </summary>
/// <param name="callingAgent">The agent that is calling the tool.</param>
/// <param name="context">The function invocation context containing tool call details.</param>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The result of the tool invocation.</returns>
public delegate ValueTask<object?> ToolCallingMiddlewareDelegate(
    AIAgent callingAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken);

/// <summary>
/// Details about a tool call for inspection and logging.
/// </summary>
/// <param name="ToolName">The name of the tool being called.</param>
/// <param name="Arguments">The arguments passed to the tool.</param>
/// <param name="Timestamp">When the tool call occurred.</param>
public record ToolCallingDetails(
    string ToolName,
    AIFunctionArguments Arguments,
    DateTimeOffset Timestamp);

/// <summary>
/// Simple handler that wraps an inspection action into a middleware delegate.
/// </summary>
public class ToolCallsHandler
{
    private readonly Action<ToolCallingDetails> _inspectionAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallsHandler"/> class.
    /// </summary>
    /// <param name="inspectionAction">Action to call for each tool invocation.</param>
    public ToolCallsHandler(Action<ToolCallingDetails> inspectionAction)
    {
        _inspectionAction = inspectionAction ?? throw new ArgumentNullException(nameof(inspectionAction));
    }

    /// <summary>
    /// Middleware method that logs tool calls and passes execution to the next handler.
    /// </summary>
    public async ValueTask<object?> ToolCallingMiddlewareAsync(
        AIAgent callingAgent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        // Capture tool call details
        var details = new ToolCallingDetails(
            context.Function?.Name ?? "unknown",
            context.Arguments,
            DateTimeOffset.UtcNow);

        // Invoke inspection action
        _inspectionAction(details);

        // Continue pipeline
        return await next(context, cancellationToken);
    }
}
