using Cyclotron.Maf.AgentSdk.Common.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Provider implementation for Ollama local models.
/// </summary>
internal sealed class OllamaAgentProvider(
    IProviderClientFactory clientFactory,
    ILogger<OllamaAgentProvider> logger) : IAgentProvider
{
    private static readonly IReadOnlyCollection<string> SupportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ollama"
    };

    private readonly IProviderClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly ILogger<OllamaAgentProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedProviderTypes => SupportedTypes;

    /// <inheritdoc/>
    public AgentProviderCapabilities Capabilities { get; } = new(
        SupportsVectorStore: false,
        SupportsAgentDeletion: false,
        SupportsSessionDeletion: false,
        SupportsStructuredOutput: false);

    /// <inheritdoc/>
    public async Task<AgentProviderResult> CreateAgentAsync(
        AgentProviderCreationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        if (!string.IsNullOrWhiteSpace(request.VectorStoreId))
        {
            throw new InvalidOperationException(
                $"Provider '{request.ProviderName}' does not support vector store agents. " +
                $"Use CreateAgentAsync without a vector store for local providers.");
        }

        var modelName = request.Provider.GetEffectiveModel();
        var endpoint = request.Provider.Endpoint.TrimEnd('/');

        _logger.LogInformation(
            "Creating {AgentKey} agent for Ollama provider '{ProviderName}' (Endpoint: {Endpoint}, Model: {Model}, ReasoningMode: {ReasoningMode})",
            request.AgentKey,
            request.ProviderName,
            endpoint,
            modelName,
            request.Provider.EnableReasoningMode);

        var providerClient = _clientFactory.GetClient(request.ProviderName);
        if (!providerClient.TryGetOllamaClient(out var ollamaClient) || ollamaClient == null)
        {
            throw new InvalidOperationException(
                $"Provider '{request.ProviderName}' does not support Ollama agent operations.");
        }

        // Configure reasoning mode if enabled
        if (request.Provider.EnableReasoningMode)
        {
            var reasoningModel = request.Provider.GetReasoningModel();
            _logger.LogInformation(
                "Reasoning mode enabled for {AgentKey} agent using model '{ReasoningModel}'",
                request.AgentKey,
                reasoningModel);

            if (!string.IsNullOrEmpty(request.Provider.ReasoningModel))
            {
                ollamaClient.SelectedModel = reasoningModel;
            }
        }

        // Configure thermodynamic parameters if specified
        // Note: OllamaSharp's OllamaApiClient and ChatClientAgent don't expose direct properties for
        // Temperature and TopP at agent creation time. These are typically configured per-request in ChatOptions.
        // We log them here for reference and they can be used in the chat request options during execution.
        if (request.Temperature.HasValue || request.TopP.HasValue)
        {
            _logger.LogInformation(
                "Thermodynamic parameters configured for {AgentKey} agent: Temperature={Temperature}, TopP={TopP}. " +
                "Note: These should be applied in ChatOptions when making chat requests.",
                request.AgentKey,
                request.Temperature?.ToString("F2") ?? "null",
                request.TopP?.ToString("F2") ?? "null");
        }

        IChatClient chatClient = ollamaClient;
        var agentName = $"{request.NamePrefix}-ollama";
        AIAgent agent = new ChatClientAgent(
            chatClient,
            name: agentName,
            description: $"Ollama agent using model {modelName}",
            instructions: request.Instructions);

        _logger.LogInformation(
            "Created {AgentKey} agent for Ollama provider '{ProviderName}' (Model: {Model})",
            request.AgentKey,
            request.ProviderName,
            modelName);

        await Task.CompletedTask.ConfigureAwait(false);
        return new AgentProviderResult(agent, null, null);
    }

    /// <inheritdoc/>
    public Task DeleteAgentAsync(
        AgentProviderDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        _logger.LogDebug(
            "Skipping agent deletion for Ollama provider '{ProviderName}'",
            request.ProviderName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteSessionAsync(
        AgentProviderSessionDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        _logger.LogDebug(
            "Skipping session deletion for Ollama provider '{ProviderName}'",
            request.ProviderName);
        return Task.CompletedTask;
    }
}
