using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using Microsoft.Extensions.AI;

namespace SpamDetection;

/// <summary>
/// Main entry point for the spam detection sample.
/// Demonstrates using the AgentSdk to classify messages as spam or not spam.
/// </summary>
public class Main : IMain
{
    private readonly ILogger<Main> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IAgentFactory _spamDetectorFactory;
    private readonly IVectorStoreManager _vectorStoreManager;

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

    public Main(
        IHostApplicationLifetime applicationLifetime,
        IConfiguration configuration,
        ILogger<Main> logger,
        [FromKeyedServices("spam_detector")] IAgentFactory spamDetectorFactory,
        IVectorStoreManager vectorStoreManager)
    {
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _spamDetectorFactory = spamDetectorFactory ?? throw new ArgumentNullException(nameof(spamDetectorFactory));
        _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));
    }

    public IConfiguration Configuration { get; set; }

    public async Task<int> RunAsync()
    {
        _logger.LogInformation("Starting Spam Detection Agent...");

        var cancellationToken = _applicationLifetime.ApplicationStopping;

        try
        {
            // Create a vector store with spam detection examples
            var vectorStoreId = await CreateSpamExamplesVectorStoreAsync(cancellationToken);

            // Create the spam detection agent
            await _spamDetectorFactory.CreateAgentAsync(vectorStoreId, cancellationToken);

            _logger.LogInformation("Spam Detection Agent created successfully");
            _logger.LogInformation("Testing {Count} sample messages...", TestMessages.Count);

            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("SPAM DETECTION RESULTS");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

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
                var statusColor = isCorrect ? ConsoleColor.Green : ConsoleColor.Red;

                Console.ForegroundColor = statusColor;
                Console.Write($"[{statusIcon}] ");
                Console.ResetColor();

                Console.WriteLine($"Message: \"{Truncate(message.Content, 50)}\"");
                Console.WriteLine($"    Predicted: {result.Classification} | Expected: {message.ExpectedLabel}");
                Console.WriteLine($"    Confidence: {result.Confidence:P0}");
                Console.WriteLine($"    Reason: {result.Reason}");
                Console.WriteLine();
            }

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"ACCURACY: {correctPredictions}/{totalPredictions} ({(double)correctPredictions / totalPredictions:P0})");
            Console.WriteLine("=".PadRight(80, '='));

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
            // Cleanup agent resources
            await _spamDetectorFactory.CleanupAsync(cancellationToken);
            _logger.LogInformation("Cleanup completed");
        }
    }

    private async Task<string> CreateSpamExamplesVectorStoreAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating vector store with spam examples...");

        var providerName = _spamDetectorFactory.AgentDefinition.AIFrameworkOptions.Provider;

        // Get or create a shared vector store for spam detection training data
        var vectorStoreId = await _vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
            providerName,
            key: "spam-detection-examples",
            purpose: "Spam detection training examples",
            name: "SpamDetectionExamples",
            cancellationToken);

        // Create a training document with spam examples
        var trainingContent = GenerateTrainingDocument();

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trainingContent));

        await _vectorStoreManager.AddFileToVectorStoreAsync(
            providerName,
            vectorStoreId,
            stream,
            "spam_training_examples.md",
            cancellationToken);

        _logger.LogInformation("Vector store created with ID: {VectorStoreId}", vectorStoreId);

        return vectorStoreId;
    }

    private static string GenerateTrainingDocument()
    {
        return """
            # Spam Detection Training Examples

            ## Common Spam Indicators

            ### Financial Scams
            - Messages promising large sums of money
            - "You've won" or "Congratulations" with no context
            - Requests for personal financial information
            - Urgent calls to action regarding money

            ### Phishing Attempts
            - Suspicious links (shortened URLs, misspelled domains)
            - Urgent account verification requests
            - Messages impersonating known companies
            - Threats of account suspension

            ### Marketing Spam
            - Unsolicited product promotions
            - "Limited time offers" with excessive urgency
            - Work-from-home schemes
            - Weight loss or health product promotions

            ## Examples of Spam Messages

            1. "Congratulations! You've won $1,000,000 in our lottery!"
            2. "URGENT: Verify your account now or face suspension!"
            3. "Make money fast! $5000/day working from home!"
            4. "Click here for exclusive deals you won't believe!"
            5. "Your package delivery failed. Click to reschedule."

            ## Examples of Legitimate Messages

            1. "Hi, can we schedule a meeting for next week?"
            2. "Please review the attached document when you have time."
            3. "Thanks for your help with the project yesterday."
            4. "The code review looks good, approved!"
            5. "Reminder: Team standup at 10am tomorrow."

            ## Classification Guidelines

            - **SPAM**: Messages with deceptive intent, unsolicited promotions, or phishing attempts
            - **NOT_SPAM**: Legitimate business communications, personal messages, or expected notifications
            """;
    }

    private async Task<SpamClassificationResult> ClassifyMessageAsync(string messageContent, CancellationToken cancellationToken)
    {
        var context = new { message = messageContent };

        var userMessage = _spamDetectorFactory.CreateUserMessage(context);

        var response = await _spamDetectorFactory.RunAgentWithPollingAsync(
            messages: [userMessage],
            cancellationToken: cancellationToken);

        // Parse the response
        var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;

        return ParseClassificationResponse(responseText);
    }

    private static SpamClassificationResult ParseClassificationResponse(string response)
    {
        // Simple parsing - in production, you'd want structured output
        var lowerResponse = response.ToLowerInvariant();

        var classification = lowerResponse.Contains("spam") && !lowerResponse.Contains("not spam")
            ? "spam"
            : "not_spam";

        // Try to extract confidence (default to 0.8 if not found)
        var confidence = 0.8;
        if (lowerResponse.Contains("high confidence") || lowerResponse.Contains("confident"))
        {
            confidence = 0.95;
        }
        else if (lowerResponse.Contains("low confidence") || lowerResponse.Contains("uncertain"))
        {
            confidence = 0.6;
        }

        // Extract reason from response
        var reason = response.Length > 100 ? response[..100] + "..." : response;

        return new SpamClassificationResult(classification, confidence, reason);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Represents a sample message for testing.
    /// </summary>
    private sealed record SampleMessage(string Content, string ExpectedLabel);

    /// <summary>
    /// Represents the result of spam classification.
    /// </summary>
    private sealed record SpamClassificationResult(string Classification, double Confidence, string Reason);
}
