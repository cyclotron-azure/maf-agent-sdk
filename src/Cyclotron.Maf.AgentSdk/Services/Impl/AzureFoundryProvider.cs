using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Azure AI Foundry provider implementation using managed identity authentication.
/// </summary>
public class AzureFoundryProvider : IModelProvider
{
    private readonly ModelProviderDefinitionOptions _config;
    private readonly ILogger<AzureFoundryProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFoundryProvider"/> class.
    /// </summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public AzureFoundryProvider(
        ModelProviderDefinitionOptions config,
        ILogger<AzureFoundryProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string ProviderType => "azure_foundry";

    /// <inheritdoc/>
    public string Endpoint => _config.Endpoint;

    /// <inheritdoc/>
    public string DeploymentName => _config.DeploymentName;

    /// <inheritdoc/>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(_config.Endpoint) &&
               !string.IsNullOrEmpty(_config.DeploymentName);
    }

    /// <inheritdoc/>
    public object CreateClient()
    {
        if (!IsValid())
        {
            throw new InvalidOperationException(
                $"Azure Foundry provider configuration is invalid. " +
                $"Endpoint: {_config.Endpoint}, DeploymentName: {_config.DeploymentName}");
        }

        _logger.LogInformation(
            "Creating PersistentAgentsClient for Azure Foundry (Endpoint: {Endpoint}, Deployment: {Deployment})",
            _config.Endpoint,
            _config.DeploymentName);

        var credential = new DefaultAzureCredential();
        return new PersistentAgentsClient(_config.Endpoint, credential);
    }
}
