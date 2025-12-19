namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Defines the configuration options for an individual agent.
/// Maps to entries in the <c>agents:</c> section of agent.config.yaml.
/// </summary>
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
    /// Gets or sets the AI framework configuration for the agent.
    /// References a model provider defined in the <c>providers:</c> section.
    /// </summary>
    public AIFrameworkOptions AIFrameworkOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the desired agent version. Optional; if not set, the service version is used.
    /// </summary>
    public string? Version { get; set; }
}