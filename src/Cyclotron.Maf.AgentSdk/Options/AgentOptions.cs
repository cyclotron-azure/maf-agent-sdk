namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Configuration options for the agent system.
/// Contains the collection of agent definitions loaded from agent.config.yaml.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Gets or sets the dictionary of agent definitions keyed by agent name.
    /// Keys typically follow the pattern "{agent_type}_agent" (e.g., "classification_agent").
    /// </summary>
    public Dictionary<string, AgentDefinitionOptions> Agents { get; set; } = [];
}
