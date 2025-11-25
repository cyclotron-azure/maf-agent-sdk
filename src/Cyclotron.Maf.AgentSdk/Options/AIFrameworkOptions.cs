using System.ComponentModel.DataAnnotations;

namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Defines the configuration options for the AI framework used by an agent.
/// References a model provider defined in the providers: section of agent.config.yaml.
/// </summary>
public class AIFrameworkOptions
{
    /// <summary>
    /// Gets or sets the model provider reference key (e.g., "azure_foundry_dev").
    /// Must match a provider key defined in the providers: section.
    /// </summary>
    [Required(ErrorMessage = "framework_config.provider is required and must reference a valid provider")]
    public string Provider { get; set; } = string.Empty;
}