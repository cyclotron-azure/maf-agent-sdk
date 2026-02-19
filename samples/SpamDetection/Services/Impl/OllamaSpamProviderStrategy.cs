using Cyclotron.Maf.AgentSdk.Agents;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Ollama spam provider strategy using local models without vector stores.
/// </summary>
public sealed class OllamaSpamProviderStrategy(
    ILogger<OllamaSpamProviderStrategy> logger,
    [FromKeyedServices("spam_detector_ollama")] IAgentFactory spamDetectorFactory) : ISpamProviderStrategy
{
    private readonly ILogger<OllamaSpamProviderStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAgentFactory _spamDetectorFactory = spamDetectorFactory ?? throw new ArgumentNullException(nameof(spamDetectorFactory));

    public string ProviderKey => "ollama";

    public IAgentFactory AgentFactory => _spamDetectorFactory;

    public Task<string?> PrepareVectorStoreAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ollama provider selected; no vector store will be created.");
        return Task.FromResult<string?>(null);
    }
}
