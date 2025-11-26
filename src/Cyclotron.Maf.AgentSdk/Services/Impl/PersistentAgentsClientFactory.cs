using Cyclotron.Maf.AgentSdk.Options;
using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Adapter to use Azure API Key with <see cref="TokenCredential"/> interface.
/// </summary>
/// <remarks>
/// This is a workaround since <see cref="PersistentAgentsClient"/> only accepts <see cref="TokenCredential"/>.
/// For production use with API keys, consider using Azure.AI.OpenAI.OpenAIClient instead.
/// </remarks>
internal class AzureKeyCredentialAdapter(string apiKey) : TokenCredential
{
    private readonly string _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

    /// <inheritdoc/>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Return API key as bearer token (not standard OAuth flow)
        return new AccessToken(_apiKey, DateTimeOffset.MaxValue);
    }

    /// <inheritdoc/>
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
    }
}

/// <summary>
/// Factory for creating <see cref="PersistentAgentsClient"/> instances with provider-specific authentication.
/// Supports multiple providers with different endpoints and authentication methods.
/// Creates new client instances per scope to avoid state sharing in parallel processing.
/// </summary>
/// <remarks>
/// <para>
/// Supported provider types:
/// <list type="bullet">
/// <item><description><c>azure_foundry</c>: Uses <see cref="DefaultAzureCredential"/> for authentication.</description></item>
/// <item><description><c>azure_openai</c>: Uses API key authentication via <see cref="AzureKeyCredentialAdapter"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
public class PersistentAgentsClientFactory : IPersistentAgentsClientFactory
{
    private readonly ILogger<PersistentAgentsClientFactory> _logger;
    private readonly ModelProviderOptions _providerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentAgentsClientFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="providerOptions">The model provider configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no providers are configured.</exception>
    public PersistentAgentsClientFactory(
        ILogger<PersistentAgentsClientFactory> logger,
        IOptions<ModelProviderOptions> providerOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(providerOptions, nameof(providerOptions));

        _providerOptions = providerOptions.Value;

        if (_providerOptions.Providers.Count == 0)
        {
            throw new InvalidOperationException(
                "No providers configured. Add a 'providers:' section to agent.config.yaml");
        }
    }

    /// <inheritdoc/>
    public PersistentAgentsClient GetClient(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        // Create new client instance per request to avoid state sharing across parallel processing
        return CreateClient(providerName);
    }

    private PersistentAgentsClient CreateClient(string providerName)
    {
        if (!_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}");
        }

        if (!provider.IsValid())
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' configuration is invalid. " +
                $"Type: {provider.Type}, Endpoint: {provider.Endpoint}, DeploymentName: {provider.DeploymentName}");
        }

        _logger.LogInformation(
            "Creating PersistentAgentsClient for provider '{ProviderName}' (Type: {ProviderType}, Endpoint: {Endpoint})",
            providerName,
            provider.Type,
            provider.Endpoint);

        // Create credential based on provider type and configuration
        TokenCredential credential = CreateCredential(provider);

        return new PersistentAgentsClient(provider.Endpoint, credential);
    }

    private TokenCredential CreateCredential(ModelProviderDefinitionOptions provider)
    {
        // azure_openai with API key: Use AzureKeyCredentialAdapter
        if (provider.Type.Equals("azure_openai", StringComparison.OrdinalIgnoreCase) && provider.UsesApiKey())
        {
            _logger.LogDebug("Using API Key authentication for provider type: {ProviderType}", provider.Type);
            _logger.LogWarning(
                "Using API Key with PersistentAgentsClient via adapter. " +
                "For production Azure OpenAI usage, consider using Azure.AI.OpenAI.OpenAIClient or DefaultAzureCredential.");

            return new AzureKeyCredentialAdapter(provider.ApiKey!);
        }

        // azure_foundry or azure_openai without API key: Use DefaultAzureCredential
        _logger.LogDebug(
            "Using DefaultAzureCredential for provider type: {ProviderType}",
            provider.Type);

        return new DefaultAzureCredential();
    }
}
