namespace SpamDetection.Services;

/// <summary>
/// Defines the spam detection workflow interface.
/// Orchestrates the creation of vector stores, agent setup, and message classification.
/// </summary>
public interface ISpamWorkflow
{
    /// <summary>
    /// Executes the complete spam detection workflow.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the exit code.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Classifies a message as spam or not spam.
    /// </summary>
    /// <param name="messageContent">The message content to classify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the classification result.</returns>
    Task<SpamClassificationResult> ClassifyMessageAsync(string messageContent, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the result of spam classification.
/// </summary>
public sealed record SpamClassificationResult(string Classification, double Confidence, string Reason);
