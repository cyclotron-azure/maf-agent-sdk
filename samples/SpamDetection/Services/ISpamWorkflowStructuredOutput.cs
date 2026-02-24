using Cyclotron.Maf.AgentSdk.Models;
using SpamDetection.Models;

namespace SpamDetection.Services;

/// <summary>
/// Structured output spam detection workflow interface.
/// Demonstrates using the AgentFactory's generic RunAgentWithPollingAsync generic method
/// for type-safe structured responses from AI agents.
/// </summary>
/// <remarks>
/// This service extends the standard spam detection workflow by using structured output,
/// which constrains the agent to respond with JSON conforming to a specific C# type.
/// This provides both type safety and better control over agent responses.
/// </remarks>
public interface ISpamWorkflowStructuredOutput
{
    /// <summary>
    /// Executes the spam detection workflow using structured output.
    /// Returns strongly-typed structured results instead of parsed text.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the exit code.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Classifies a message as spam or not spam using structured output.
    /// Returns a structured output response with confidence scores and detailed classification information.
    /// </summary>
    /// <param name="messageContent">The message content to classify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a structured output response
    /// containing the classification result and original agent response metadata.
    /// </returns>
    Task<IStructuredOutputAgentResponse<SpamClassificationReadyForStructuredOutput>> ClassifyMessageStructuredAsync(
        string messageContent,
        CancellationToken cancellationToken);
}
