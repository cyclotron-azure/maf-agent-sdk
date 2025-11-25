using System.ComponentModel.DataAnnotations;

namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Defines the configuration for a model provider (Azure OpenAI, Azure AI Foundry, etc.).
/// </summary>
public class ModelProviderDefinitionOptions
{
    /// <summary>
    /// Provider type (e.g., "azure_foundry", "azure_openai").
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Provider endpoint URL. Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Model deployment name. Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    [Required]
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Optional model identifier that can override deployment_name for specific use cases.
    /// Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// API version for the provider (e.g., "2024-10-21").
    /// Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// API key for authentication (required for azure_openai type).
    /// Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Request timeout in seconds. Defaults to 300 (5 minutes).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of retry attempts. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets the effective model name to use (Model if specified, otherwise DeploymentName).
    /// </summary>
    public string GetEffectiveModel() => Model ?? DeploymentName;

    /// <summary>
    /// Determines if this provider uses API key authentication.
    /// </summary>
    public bool UsesApiKey() => !string.IsNullOrEmpty(ApiKey);

    /// <summary>
    /// Validates that the provider configuration is complete based on type.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(Type) || string.IsNullOrEmpty(Endpoint) || string.IsNullOrEmpty(DeploymentName))
        {
            return false;
        }

        // azure_openai requires API key
        if (Type.Equals("azure_openai", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(ApiKey))
        {
            return false;
        }

        return true;
    }
}
