using Cyclotron.Maf.AgentSdk.Agents;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Azure AI Foundry spam provider strategy using vector stores.
/// </summary>
public sealed class AzureSpamProviderStrategy(
    ILogger<AzureSpamProviderStrategy> logger,
    [FromKeyedServices("spam_detector")] IAgentFactory spamDetectorFactory,
    IVectorStoreManager vectorStoreManager) : ISpamProviderStrategy
{
    private readonly ILogger<AzureSpamProviderStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAgentFactory _spamDetectorFactory = spamDetectorFactory ?? throw new ArgumentNullException(nameof(spamDetectorFactory));
    private readonly IVectorStoreManager _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));

    public string ProviderKey => "azure";

    public IAgentFactory AgentFactory => _spamDetectorFactory;

    public async Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating vector store with spam examples...");

        var providerName = _spamDetectorFactory.AgentDefinition.Provider;

        var vectorStoreId = await _vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
            providerName,
            key: "spam-detection-examples",
            purpose: "Spam detection training examples",
            name: "SpamDetectionExamples",
            cancellationToken);

        var trainingContent = GenerateTrainingDocument();

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trainingContent));

        await _vectorStoreManager.AddFileToVectorStoreAsync(
            providerName,
            vectorStoreId,
            stream,
            "spam_training_examples.md",
            SimpleChunkingAsync,
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
            - \"You've won\" or \"Congratulations\" with no context
            - Requests for personal financial information
            - Urgent calls to action regarding money

            ### Phishing Attempts
            - Suspicious links (shortened URLs, misspelled domains)
            - Urgent account verification requests
            - Messages impersonating known companies
            - Threats of account suspension

            ### Marketing Spam
            - Unsolicited product promotions
            - \"Limited time offers\" with excessive urgency
            - Work-from-home schemes
            - Weight loss or health product promotions

            ## Examples of Spam Messages

            1. \"Congratulations! You've won $1,000,000 in our lottery!\"
            2. \"URGENT: Verify your account now or face suspension!\"
            3. \"Make money fast! $5000/day working from home!\"
            4. \"Click here for exclusive deals you won't believe!\"
            5. \"Your package delivery failed. Click to reschedule.\"

            ## Examples of Legitimate Messages

            1. \"Hi, can we schedule a meeting for next week?\"
            2. \"Please review the attached document when you have time.\"
            3. \"Thanks for your help with the project yesterday.\"
            4. \"The code review looks good, approved!\"
            5. \"Reminder: Team standup at 10am tomorrow.\"

            ## Classification Guidelines

            - **SPAM**: Messages with deceptive intent, unsolicited promotions, or phishing attempts
            - **NOT_SPAM**: Legitimate business communications, personal messages, or expected notifications
            """;
    }

    private static async IAsyncEnumerable<(string Text, string ChunkId)> SimpleChunkingAsync(
        Stream stream,
        string fileName)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        const int chunkSize = 1000;
        var buffer = new char[chunkSize];
        var chunkIndex = 0;

        while (true)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, chunkSize).ConfigureAwait(false);
            if (charsRead == 0)
            {
                yield break;
            }

            var chunkText = new string(buffer, 0, charsRead);
            yield return (chunkText, $"{fileName}#{chunkIndex}");
            chunkIndex++;
        }
    }
}
