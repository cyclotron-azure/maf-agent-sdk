using SpamDetection.Models;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Implementation of the structured output spam detection workflow.
/// Demonstrates using the AgentFactory's generic RunAgentWithPollingAsync generic method
/// for type-safe structured responses from AI agents.
/// </summary>
/// <remarks>
/// This implementation extends the standard spam detection by using structured output,
/// which constrains the agent to respond with JSON conforming to SpamClassificationReadyForStructuredOutput.
/// This provides type safety and better control over agent responses compared to parsing text.
/// </remarks>
public sealed class SpamWorkflowStructuredOutput(
    ILogger<SpamWorkflowStructuredOutput> logger,
    IConfiguration configuration,
    IEnumerable<ISpamProviderStrategy> providerStrategies) : ISpamWorkflowStructuredOutput
{
    private readonly ILogger<SpamWorkflowStructuredOutput> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IReadOnlyDictionary<string, ISpamProviderStrategy> _providerStrategies =
        BuildProviderStrategies(providerStrategies);

    /// <summary>
    /// Sample messages to test spam detection.
    /// </summary>
    private static readonly List<SampleMessage> TestMessages =
    [
        new("You have won $1,000,000! Click here to claim your prize NOW!", "spam"),
        new("Hi John, just wanted to follow up on our meeting yesterday about the Q3 budget.", "not_spam"),
        new("URGENT: Your account will be suspended unless you verify your details immediately!", "spam"),
        new("Thanks for the code review feedback. I'll address the comments by EOD.", "not_spam"),
        new("Congratulations! You've been selected for an exclusive offer. Act now!", "spam"),
        new("Can we reschedule our 1:1 to Thursday at 2pm?", "not_spam"),
        new("Make $5000 per day working from home! No experience needed!", "spam"),
        new("Please find attached the quarterly report as discussed.", "not_spam"),
        new("Your package could not be delivered. Click to reschedule: bit.ly/abc123", "spam"),
        new("The pull request is ready for review when you have a moment.", "not_spam")
    ];

    /// <summary>
    /// Executes the complete spam detection workflow using structured output.
    /// Demonstrates the new structured output capability of the AgentFactory.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Spam Detection Workflow with Structured Output...");
        ISpamProviderStrategy? strategy = null;

        try
        {
            strategy = ResolveProviderStrategy();
            _logger.LogInformation("Spam detection provider: {Provider}", strategy.ProviderKey);

            var vectorStoreId = await strategy.PrepareVectorStoreAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(vectorStoreId))
            {
                await strategy.AgentFactory.CreateAgentAsync(cancellationToken);
            }
            else
            {
                await strategy.AgentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            }

            _logger.LogInformation("Spam Detection Agent created successfully");
            _logger.LogInformation("Testing {Count} sample messages with structured output...", TestMessages.Count);
            _logger.LogInformation(new string('=', 80));
            _logger.LogInformation("STRUCTURED SPAM DETECTION RESULTS");
            _logger.LogInformation(new string('=', 80));

            var correctPredictions = 0;
            var totalPredictions = 0;

            foreach (var message in TestMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ClassifyMessageStructuredAsync(message.Content, cancellationToken);

                // Map structured output IsSpam to expected label
                var predicted = result.IsSpam ? "spam" : "not_spam";
                var isCorrect = predicted.Equals(message.ExpectedLabel, StringComparison.OrdinalIgnoreCase);

                if (isCorrect)
                {
                    correctPredictions++;
                }
                totalPredictions++;

                // Display results
                var statusIcon = isCorrect ? "✓" : "✗";

                _logger.LogInformation(
                    "[{StatusIcon}] Message: \"{TruncatedMessage}\" | Predicted: {Predicted} | Expected: {Expected} | Confidence: {Confidence:P0} | Indicators: {Indicators}",
                    statusIcon,
                    Truncate(message.Content, 50),
                    predicted,
                    message.ExpectedLabel,
                    result.Confidence,
                    Truncate(result.SpamIndicators ?? string.Empty, 40));
            }

            _logger.LogInformation(new string('=', 80));
            _logger.LogInformation("ACCURACY: {Correct}/{Total} ({Percentage:P0})", correctPredictions, totalPredictions, (double)correctPredictions / totalPredictions);
            _logger.LogInformation(new string('=', 80));

            _logger.LogInformation(
                "Structured spam detection completed. Accuracy: {Correct}/{Total}",
                correctPredictions,
                totalPredictions);

            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation was cancelled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during structured spam detection");
            return 1;
        }
        finally
        {
            if (strategy is not null)
            {
                await strategy.AgentFactory.CleanupAsync(cancellationToken);
                _logger.LogInformation("Cleanup completed");
            }
        }
    }

    /// <summary>
    /// Classifies a message as spam or not spam using structured output.
    /// Returns a strongly-typed result instead of parsing text responses.
    /// </summary>
    /// <remarks>
    /// This method demonstrates the new generic RunAgentWithPollingAsync method capability,
    /// which automatically deserializes the agent response into the structured output type
    /// defined in the agent.config.yaml (SpamClassificationReadyForStructuredOutput).
    /// This eliminates the need for manual response parsing and provides type safety.
    /// </remarks>
    public async Task<SpamClassificationReadyForStructuredOutput> ClassifyMessageStructuredAsync(
        string messageContent,
        CancellationToken cancellationToken)
    {
        var context = new { message = messageContent };
        var strategy = ResolveProviderStrategy();

        var userMessage = strategy.AgentFactory.CreateUserMessage(context);

        // Use the new generic method for structured output
        // The agent factory automatically deserializes the response into the configured type
        var result = await strategy.AgentFactory.RunAgentWithPollingAsync<SpamClassificationReadyForStructuredOutput>(
            messages: [userMessage],
            cancellationToken: cancellationToken);

        return result;
    }

    /// <summary>
    /// Truncates a string to a maximum length.
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Represents a sample message for testing.
    /// </summary>
    private sealed record SampleMessage(string Content, string ExpectedLabel);

    private ISpamProviderStrategy ResolveProviderStrategy()
    {
        var provider = _configuration["Workflow:SpamProvider"]?.ToLowerInvariant() ?? "azure";

        if (_providerStrategies.TryGetValue(provider, out var strategy))
        {
            return strategy;
        }

        if (_providerStrategies.TryGetValue("azure", out var azureStrategy))
        {
            return azureStrategy;
        }

        return _providerStrategies.Values.First();
    }

    private static IReadOnlyDictionary<string, ISpamProviderStrategy> BuildProviderStrategies(
        IEnumerable<ISpamProviderStrategy> providerStrategies)
    {
        if (providerStrategies is null)
        {
            throw new ArgumentNullException(nameof(providerStrategies));
        }

        var strategies = providerStrategies.ToList();
        if (strategies.Count == 0)
        {
            throw new ArgumentException("At least one spam provider strategy must be registered.", nameof(providerStrategies));
        }

        return strategies.ToDictionary(
            strategy => strategy.ProviderKey,
            StringComparer.OrdinalIgnoreCase);
    }
}
