using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Extensibility options for advanced agent configuration.
/// Provides hooks for customizing client and agent behavior beyond standard configuration.
/// </summary>
public class ExtensibilityOptions
{
    /// <summary>
    /// Gets or sets an action to configure provider-specific client options.
    /// The object type depends on the provider (e.g., AzureOpenAIClientOptions, OpenAIClientOptions).
    /// </summary>
    /// <remarks>
    /// Use this to set advanced provider-specific settings not exposed through standard configuration.
    /// Example: Custom retry policies, additional headers, or provider-specific features.
    /// </remarks>
    public Action<object>? AdditionalClientOptions { get; set; }

    /// <summary>
    /// Gets or sets a factory function to wrap or replace the underlying IChatClient.
    /// </summary>
    /// <remarks>
    /// Use this to add custom middleware, caching, or transformation layers
    /// around the chat client before it's used by the agent.
    /// </remarks>
    public Func<IChatClient, IChatClient>? ClientFactory { get; set; }

    /// <summary>
    /// Gets or sets an action to configure ChatClientAgentOptions.
    /// </summary>
    /// <remarks>
    /// Use this to set additional agent options not covered by standard configuration,
    /// such as custom context providers or advanced agent behaviors.
    /// </remarks>
    public Action<ChatClientAgentOptions>? AdditionalAgentOptions { get; set; }
}
