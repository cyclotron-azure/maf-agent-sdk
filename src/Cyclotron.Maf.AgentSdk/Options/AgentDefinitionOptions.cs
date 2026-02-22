using System.ComponentModel.DataAnnotations;
using Cyclotron.Maf.AgentSdk.Middleware;

namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Defines the configuration options for an individual agent.
/// Maps to entries in the <c>agents:</c> section of agent.config.yaml.
/// </summary>
/// <remarks>
/// BREAKING CHANGE in 1.0: Configuration structure has been flattened.
/// - Removed nested AIFrameworkOptions - use direct Provider property
/// - Added Middleware configuration for centralized middleware management
/// - Added Extensibility options for advanced customization
/// </remarks>
public class AgentDefinitionOptions
{
    /// <summary>
    /// Gets or sets the type identifier for the agent (e.g., "classification", "address_extraction").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the agent is enabled for use.
    /// Disabled agents are skipped during workflow execution.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the model provider reference key (e.g., "azure_foundry", "ollama_local").
    /// Must match a provider key defined in the providers: section of agent.config.yaml.
    /// </summary>
    /// <remarks>
    /// BREAKING CHANGE in 1.0: This replaces the nested AIFrameworkOptions.Provider property.
    /// </remarks>
    [Required(ErrorMessage = "provider is required and must reference a valid provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the agent should be automatically deleted after execution.
    /// Defaults to <c>true</c> for ephemeral agents. Set to <c>false</c> to keep agents for debugging or reuse.
    /// </summary>
    public bool AutoDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether vector stores and files should be automatically cleaned up after agent execution.
    /// When enabled, the executor will delete the vector store used by this agent (which cascades to delete contained files).
    /// Defaults to <c>false</c> to preserve resources for debugging. Set to <c>true</c> for production to avoid orphaned resources.
    /// </summary>
    /// <remarks>
    /// Only affects vector stores created specifically for this agent's execution.
    /// Shared or protected vector stores are not affected.
    /// </remarks>
    public bool AutoCleanupResources { get; set; } = false;

    /// <summary>
    /// Gets or sets the system prompt template using Handlebars syntax for agent instructions.
    /// Defines agent behavior, role, rules, and expected output schema.
    /// If not provided, default prompts will be used based on agent type.
    /// </summary>
    public string? SystemPromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the user prompt template using Handlebars syntax for dynamic context injection.
    /// Defines specific task requests with runtime variables.
    /// If not provided, a default prompt will be used based on agent type.
    /// </summary>
    public string? UserPromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the metadata configuration for the agent.
    /// Contains description and tool definitions.
    /// </summary>
    public AgentMetadataOptions Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the middleware configuration for this agent.
    /// Configures OpenTelemetry, logging, and tool calling middleware.
    /// </summary>
    /// <remarks>
    /// NEW in 1.0: Centralized middleware configuration with fixed application order.
    /// </remarks>
    public MiddlewareConfiguration? Middleware { get; set; }

    /// <summary>
    /// Gets or sets extensibility options for advanced agent customization.
    /// </summary>
    /// <remarks>
    /// NEW in 1.0: Provides hooks for custom client factories and advanced configuration.
    /// </remarks>
    public ExtensibilityOptions? Extensibility { get; set; }

    /// <summary>
    /// Gets or sets the desired agent version. Optional; if not set, the service version is used.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified type name for structured output configuration.
    /// When specified, the agent will be configured to produce responses conforming to this C# type.
    /// Example: "MyNamespace.PersonInfo" or "MyProject.Models.ResponseDTO"
    /// Only one output type per agent is supported.
    /// </summary>
    /// <remarks>
    /// The type must be a public class or record with public properties. The framework will
    /// automatically generate a JSON schema from this type and configure the agent's response
    /// format accordingly.
    ///
    /// Example C# type definition:
    /// <code>
    /// using System.ComponentModel;
    /// using System.Text.Json.Serialization;
    ///
    /// [Description("Information about a person")]
    /// public class PersonInfo
    /// {
    ///     [JsonPropertyName("name")]
    ///     [Description("Full name")]
    ///     public string? Name { get; set; }
    ///
    ///     [JsonPropertyName("age")]
    ///     [Description("Age in years")]
    ///     public int? Age { get; set; }
    /// }
    /// </code>
    ///
    /// Example YAML configuration:
    /// <code>
    /// agents:
    ///   person_info_extractor:
    ///     type: "extraction"
    ///     provider: "azure_foundry"
    ///     structured_output_type: "MyProject.Models.PersonInfo"
    ///     system_prompt_template: "Extract person information..."
    /// </code>
    /// </remarks>
    public string? StructuredOutputType { get; set; }

    /// <summary>
    /// Gets or sets the sampling temperature for model responses (controls randomness).
    /// Valid range: 0.0 to 2.0. Lower values (e.g., 0.2) produce more deterministic responses.
    /// Higher values (e.g., 0.8) produce more creative/random responses.
    /// When specified, overrides the provider-level temperature setting for this agent.
    /// Null means use the provider's temperature setting or the provider's default.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the nucleus sampling parameter (Top P).
    /// Valid range: 0.0 to 1.0. Controls diversity of responses by limiting token selection to top-probability tokens.
    /// Typically used as an alternative to Temperature.
    /// When specified, overrides the provider-level TopP setting for this agent.
    /// Null means use the provider's TopP setting or the provider's default.
    /// </summary>
    public float? TopP { get; set; }
}