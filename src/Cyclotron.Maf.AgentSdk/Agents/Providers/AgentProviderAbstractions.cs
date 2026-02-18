using Cyclotron.Maf.AgentSdk.Common.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Describes provider capabilities that influence agent orchestration behavior.
/// </summary>
public sealed record AgentProviderCapabilities(
    bool SupportsVectorStore,
    bool SupportsAgentDeletion,
    bool SupportsSessionDeletion);

/// <summary>
/// Input required to create an agent through a provider.
/// </summary>
public sealed record AgentProviderCreationRequest(
    string AgentKey,
    string ProviderName,
    ModelProviderDefinitionOptions Provider,
    string? VectorStoreId,
    IReadOnlyList<AITool> Tools,
    string Instructions,
    string NamePrefix,
    string? Version);

/// <summary>
/// Result of creating an agent through a provider.
/// </summary>
public sealed record AgentProviderResult(
    AIAgent Agent,
    string? CreatedAgentName,
    string? CreatedAgentVersion);

/// <summary>
/// Input required to delete a provider-managed agent.
/// </summary>
public sealed record AgentProviderDeletionRequest(
    string AgentKey,
    string ProviderName,
    ModelProviderDefinitionOptions Provider,
    AIAgent Agent,
    string? CreatedAgentName,
    string? CreatedAgentVersion);

/// <summary>
/// Input required to delete a provider-managed session.
/// </summary>
public sealed record AgentProviderSessionDeletionRequest(
    string AgentKey,
    string ProviderName,
    ModelProviderDefinitionOptions Provider,
    AgentSession Session);

/// <summary>
/// Provider abstraction for creating and cleaning up agents.
/// </summary>
public interface IAgentProvider
{
    /// <summary>
    /// Gets the provider types supported by this implementation.
    /// </summary>
    IReadOnlyCollection<string> SupportedProviderTypes { get; }

    /// <summary>
    /// Gets the provider capabilities.
    /// </summary>
    AgentProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Creates an agent for the specified provider.
    /// </summary>
    /// <param name="request">The provider creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider-specific agent creation result.</returns>
    Task<AgentProviderResult> CreateAgentAsync(
        AgentProviderCreationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the provider-managed agent if supported.
    /// </summary>
    /// <param name="request">The provider deletion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAgentAsync(
        AgentProviderDeletionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the provider-managed session if supported.
    /// </summary>
    /// <param name="request">The provider session deletion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSessionAsync(
        AgentProviderSessionDeletionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves a provider implementation for a given provider definition.
/// </summary>
public interface IAgentProviderResolver
{
    /// <summary>
    /// Resolves the provider implementation for the specified provider definition.
    /// </summary>
    /// <param name="provider">The model provider definition.</param>
    /// <returns>The matching provider implementation.</returns>
    IAgentProvider Resolve(ModelProviderDefinitionOptions provider);
}
