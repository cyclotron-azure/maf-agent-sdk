using System.ComponentModel.DataAnnotations;

namespace Cyclotron.Maf.AgentSdk.Common.Options;

/// <summary>
/// Defines the configuration for a model provider (Azure OpenAI, Azure AI Foundry, etc.).
/// This is a shared configuration type used across multiple AgentSdk packages.
/// </summary>
public class ModelProviderDefinitionOptions
{
    /// <summary>
    /// Provider type (e.g., "azure_foundry", "azure_openai", "ollama").
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
    /// Enables reasoning mode for Ollama models that support it (e.g., qwen3:8b, deepseek-r1).
    /// When enabled, the model will provide step-by-step reasoning before generating responses.
    /// Only applicable for provider type "ollama".
    /// </summary>
    public bool EnableReasoningMode { get; set; } = false;

    /// <summary>
    /// Optional reasoning model identifier to use when EnableReasoningMode is true.
    /// If not specified, uses the primary DeploymentName/Model.
    /// Only applicable for provider type "ollama".
    /// Supports IConfiguration variable substitution: {VARIABLE_NAME}.
    /// </summary>
    public string? ReasoningModel { get; set; }

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
    /// Supported provider types: azure_foundry, azure_openai, ollama.
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

        // ollama is a local provider - minimal validation required
        if (Type.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            // Ollama requires endpoint and model name, no authentication
            return !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(DeploymentName);
        }

        return true;
    }

    /// <summary>
    /// Determines if this is a local provider (Ollama, etc.) that doesn't use cloud authentication.
    /// </summary>
    public bool IsLocalProvider() => Type.Equals("ollama", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the model to use for reasoning operations when reasoning mode is enabled.
    /// Returns ReasoningModel if specified, otherwise returns the effective model.
    /// </summary>
    public string GetReasoningModel() => ReasoningModel ?? GetEffectiveModel();

    /// <summary>
    /// Gets or sets the sampling temperature for model responses (controls randomness).
    /// Valid range: 0.0 to 2.0. Lower values (e.g., 0.2) produce more deterministic responses.
    /// Higher values (e.g., 0.8) produce more creative/random responses.
    /// Null means use the provider's default.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the nucleus sampling parameter (Top P).
    /// Valid range: 0.0 to 1.0. Controls diversity of responses by limiting token selection to top-probability tokens.
    /// Typically used as an alternative to Temperature.
    /// Null means use the provider's default.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Validates that Temperature and TopP parameters are within acceptable ranges.
    /// Throws ArgumentException if values are out of range.
    /// </summary>
    /// <remarks>
    /// Temperature: 0.0 to 2.0
    /// TopP: 0.0 to 1.0
    /// </remarks>
    public void ValidateThermodynamicParameters()
    {
        if (Temperature.HasValue)
        {
            if (Temperature < 0.0f || Temperature > 2.0f)
            {
                throw new ArgumentException(
                    $"Temperature must be between 0.0 and 2.0, but got {Temperature}",
                    nameof(Temperature));
            }
        }

        if (TopP.HasValue)
        {
            if (TopP < 0.0f || TopP > 1.0f)
            {
                throw new ArgumentException(
                    $"TopP must be between 0.0 and 1.0, but got {TopP}",
                    nameof(TopP));
            }
        }
    }
}
