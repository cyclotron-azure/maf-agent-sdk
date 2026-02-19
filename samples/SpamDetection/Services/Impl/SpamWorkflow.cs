using SpamDetection.Models;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Implementation of the spam detection workflow.
/// Orchestrates vector store creation, agent setup, and message classification.
/// </summary>
public sealed class SpamWorkflow(
    ILogger<SpamWorkflow> logger,
    IConfiguration configuration,
    IEnumerable<ISpamProviderStrategy> providerStrategies) : ISpamWorkflow
{
    private readonly ILogger<SpamWorkflow> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Executes the complete spam detection workflow.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Spam Detection Workflow...");
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
            _logger.LogInformation("Testing {Count} sample messages...", TestMessages.Count);
            _logger.LogInformation(new string('=', 80));
            _logger.LogInformation("SPAM DETECTION RESULTS");
            _logger.LogInformation(new string('=', 80));

            var correctPredictions = 0;
            var totalPredictions = 0;

            foreach (var message in TestMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ClassifyMessageAsync(message.Content, cancellationToken);

                var isCorrect = result.Classification.Equals(message.ExpectedLabel, StringComparison.OrdinalIgnoreCase);
                if (isCorrect)
                {
                    correctPredictions++;
                }
                totalPredictions++;

                // Display results
                var statusIcon = isCorrect ? "✓" : "✗";

                _logger.LogInformation(
                    "[{StatusIcon}] Message: \"{TruncatedMessage}\" | Predicted: {Predicted} | Expected: {Expected} | Confidence: {Confidence:P0} | Reason: {Reason}",
                    statusIcon,
                    Truncate(message.Content, 50),
                    result.Classification,
                    message.ExpectedLabel,
                    result.Confidence,
                    result.Reason);
            }

            _logger.LogInformation(new string('=', 80));
            _logger.LogInformation("ACCURACY: {Correct}/{Total} ({Percentage:P0})", correctPredictions, totalPredictions, (double)correctPredictions / totalPredictions);
            _logger.LogInformation(new string('=', 80));

            _logger.LogInformation(
                "Spam detection completed. Accuracy: {Correct}/{Total}",
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
            _logger.LogError(ex, "Error during spam detection");
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
    /// Classifies a message as spam or not spam.
    /// </summary>
    public async Task<SpamClassificationResult> ClassifyMessageAsync(string messageContent, CancellationToken cancellationToken)
    {
        var context = new { message = messageContent };
        var strategy = ResolveProviderStrategy();

        var userMessage = strategy.AgentFactory.CreateUserMessage(context);

        var response = await strategy.AgentFactory.RunAgentWithPollingAsync(
            messages: [userMessage],
            cancellationToken: cancellationToken);

        // Parse the response
        var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;

        return ParseClassificationResponse(responseText);
    }

    /// <summary>
    /// Parses the classification response from the agent.
    /// </summary>
    private static SpamClassificationResult ParseClassificationResponse(string response)
    {
        // Simple parsing - in production, you'd want structured output
        var lowerResponse = response.ToLowerInvariant();

        // Check for NOT_SPAM first (covers "not_spam", "not spam", "NOT_SPAM")
        var isNotSpam = lowerResponse.Contains("not_spam") ||
                        lowerResponse.Contains("not spam") ||
                        lowerResponse.Contains("classification: not");

        var classification = isNotSpam ? "not_spam" : "spam";

        // Try to extract confidence (default to 0.8 if not found)
        var confidence = 0.8;
        if (lowerResponse.Contains("high confidence") || lowerResponse.Contains("confidence: high"))
        {
            confidence = 0.95;
        }
        else if (lowerResponse.Contains("low confidence") || lowerResponse.Contains("confidence: low") || lowerResponse.Contains("uncertain"))
        {
            confidence = 0.6;
        }
        else if (lowerResponse.Contains("medium confidence") || lowerResponse.Contains("confidence: medium"))
        {
            confidence = 0.8;
        }

        // Extract reason from response
        var reason = response.Length > 100 ? response[..100] + "..." : response;

        return new SpamClassificationResult(classification, confidence, reason);
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
