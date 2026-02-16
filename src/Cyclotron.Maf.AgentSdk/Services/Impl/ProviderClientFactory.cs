using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Factory for creating model provider instances based on configuration.
/// Supports Azure Foundry, Azure OpenAI, and Ollama providers.
/// </summary>
public class ProviderClientFactory : IProviderClientFactory
{
    private readonly ILogger<ProviderClientFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ModelProviderOptions _providerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderClientFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory for creating provider-specific loggers.</param>
    /// <param name="providerOptions">The model provider configuration options.</param>
    public ProviderClientFactory(
        ILogger<ProviderClientFactory> logger,
        ILoggerFactory loggerFactory,
        IOptions<ModelProviderOptions> providerOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        ArgumentNullException.ThrowIfNull(providerOptions, nameof(providerOptions));

        _providerOptions = providerOptions.Value;

        if (_providerOptions.Providers.Count == 0)
        {
            throw new InvalidOperationException(
                "No providers configured. Add a 'providers:' section to agent.config.yaml");
        }
    }

    /// <inheritdoc/>
    public IModelProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        if (!_providerOptions.Providers.TryGetValue(providerName, out var config))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}");
        }

        return CreateProvider(providerName, config);
    }

    /// <inheritdoc/>
    public bool HasProvider(string providerName)
    {
        return !string.IsNullOrWhiteSpace(providerName) &&
               _providerOptions.Providers.ContainsKey(providerName);
    }

    private IModelProvider CreateProvider(string providerName, ModelProviderDefinitionOptions config)
    {
        _logger.LogInformation(
            "Creating provider '{ProviderName}' of type '{ProviderType}'",
            providerName,
            config.Type);

        return config.Type.ToLowerInvariant() switch
        {
            "azure_foundry" => new AzureFoundryProvider(
                config,
                _loggerFactory.CreateLogger<AzureFoundryProvider>()),

            "azure_openai" => new AzureOpenAIProvider(
                config,
                _loggerFactory.CreateLogger<AzureOpenAIProvider>()),

            "ollama" => new OllamaProvider(
                config,
                _loggerFactory.CreateLogger<OllamaProvider>()),

            _ => throw new NotSupportedException(
                $"Provider type '{config.Type}' is not supported. " +
                $"Supported types: azure_foundry, azure_openai, ollama")
        };
    }
}
