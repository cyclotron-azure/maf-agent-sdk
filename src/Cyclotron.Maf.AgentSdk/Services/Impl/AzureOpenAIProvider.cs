using Azure.AI.Agents.Persistent;
using Azure.Core;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;

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
/// Azure OpenAI provider implementation using API key authentication.
/// </summary>
public class AzureOpenAIProvider : IModelProvider
{
    private readonly ModelProviderDefinitionOptions _config;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIProvider"/> class.
    /// </summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public AzureOpenAIProvider(
        ModelProviderDefinitionOptions config,
        ILogger<AzureOpenAIProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string ProviderType => "azure_openai";

    /// <inheritdoc/>
    public string Endpoint => _config.Endpoint;

    /// <inheritdoc/>
    public string DeploymentName => _config.DeploymentName;

    /// <inheritdoc/>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(_config.Endpoint) &&
               !string.IsNullOrEmpty(_config.DeploymentName) &&
               !string.IsNullOrEmpty(_config.ApiKey);
    }

    /// <inheritdoc/>
    public object CreateClient()
    {
        if (!IsValid())
        {
            throw new InvalidOperationException(
                $"Azure OpenAI provider configuration is invalid. " +
                $"Endpoint: {_config.Endpoint}, DeploymentName: {_config.DeploymentName}, " +
                $"ApiKey: {(_config.ApiKey != null ? "[SET]" : "[MISSING]")}");
        }

        _logger.LogInformation(
            "Creating PersistentAgentsClient for Azure OpenAI (Endpoint: {Endpoint}, Deployment: {Deployment})",
            _config.Endpoint,
            _config.DeploymentName);

        _logger.LogWarning(
            "Using API Key with PersistentAgentsClient via adapter. " +
            "For production Azure OpenAI usage, consider using Azure.AI.OpenAI.OpenAIClient or DefaultAzureCredential.");

        TokenCredential credential = new AzureKeyCredentialAdapter(_config.ApiKey!);
        return new PersistentAgentsClient(_config.Endpoint, credential);
    }
}
