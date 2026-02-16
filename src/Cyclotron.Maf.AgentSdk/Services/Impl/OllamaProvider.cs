using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Ollama local provider implementation for running models locally.
/// </summary>
/// <remarks>
/// Ollama runs models locally and doesn't use the Azure AI Agents API.
/// This provider is for configuration compatibility and future integration.
/// Direct integration with MAF workflows may require additional adapter layers.
/// </remarks>
public class OllamaProvider : IModelProvider
{
    private readonly ModelProviderDefinitionOptions _config;
    private readonly ILogger<OllamaProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaProvider"/> class.
    /// </summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public OllamaProvider(
        ModelProviderDefinitionOptions config,
        ILogger<OllamaProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string ProviderType => "ollama";

    /// <inheritdoc/>
    public string Endpoint => _config.Endpoint;

    /// <inheritdoc/>
    public string DeploymentName => _config.DeploymentName;

    /// <inheritdoc/>
    public bool IsValid()
    {
        // Ollama only requires endpoint and model name
        return !string.IsNullOrEmpty(_config.Endpoint) &&
               !string.IsNullOrEmpty(_config.DeploymentName);
    }

    /// <inheritdoc/>
    public object CreateClient()
    {
        if (!IsValid())
        {
            throw new InvalidOperationException(
                $"Ollama provider configuration is invalid. " +
                $"Endpoint: {_config.Endpoint}, Model: {_config.DeploymentName}");
        }

        _logger.LogInformation(
            "Creating Ollama client (Endpoint: {Endpoint}, Model: {Model})",
            _config.Endpoint,
            _config.DeploymentName);

        // For now, return an HttpClient configured for Ollama
        // Future: Integrate with OllamaSharp or direct HTTP client wrapper
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_config.Endpoint),
            Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
        };

        _logger.LogWarning(
            "Ollama provider returns HttpClient. Full MAF integration requires additional adapter layer.");

        return httpClient;
    }
}
