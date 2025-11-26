namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Defines the metadata configuration options for an agent.
/// Maps to the <c>metadata:</c> section within an agent definition in agent.config.yaml.
/// </summary>
public class AgentMetadataOptions
{
    /// <summary>
    /// Gets or sets the human-readable description of the agent.
    /// Used for documentation and logging purposes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of tools that the agent can utilize.
    /// Common tools include: "file_search", "code_interpreter".
    /// </summary>
    public List<string> Tools { get; set; } = [];
}