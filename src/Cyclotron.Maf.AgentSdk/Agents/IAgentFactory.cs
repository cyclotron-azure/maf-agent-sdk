using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Cyclotron.Maf.AgentSdk.Agents;

/// <summary>
/// Simplified factory interface for creating AI agents.
/// Uses keyed services pattern to eliminate type-specific interfaces.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Gets the currently created agent instance.
    /// Null if no agent has been created or after disposal.
    /// </summary>
    AIAgent? Agent { get; }

    /// <summary>
    /// Gets the currently created thread instance.
    /// Null if no thread has been created or after disposal.
    /// </summary>
    AgentThread? Thread { get; }

    /// <summary>
    /// Gets the vector store ID associated with the current agent.
    /// Null if no agent has been created or after disposal.
    /// </summary>
    string? VectorStoreId { get; }

    /// <summary>
    /// Creates a specialized AI agent configured for a specific task.
    /// Agent and thread are stored in properties for reuse.
    /// </summary>
    /// <param name="vectorStoreId">The IDs of the vector stores containing the knowledge base</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A configured AIAgent ready for task execution</returns>
    Task<AIAgent> CreateAgentAsync(
        string vectorStoreId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a user message with the given context using Handlebars templates.
    /// Uses the agent key to retrieve the appropriate template and renders it as a ChatMessage.
    /// </summary>
    /// <param name="context">Optional context object for template variable substitution</param>
    /// <returns>A ChatMessage ready to send to the agent</returns>
    ChatMessage CreateUserMessage(object? context = null);

    /// <summary>
    /// Runs the agent with automatic continuation token polling.
    /// Handles background response polling until completion.
    /// Includes retry logic for empty responses when no continuation token is present.
    /// </summary>
    /// <param name="messages">Initial messages to send to the agent</param>
    /// <param name="pollingIntervalSeconds">Interval between polling attempts (default: 2 seconds)</param>
    /// <param name="maxRetries">Maximum number of retries for empty responses (default: 3)</param>
    /// <param name="retryDelaySeconds">Initial delay between retries in seconds (default: 5, uses exponential backoff)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final agent response after all continuations are complete (type inferred from agent.RunAsync)</returns>
    Task<AgentRunResponse> RunAgentWithPollingAsync(
        IList<ChatMessage> messages,
        int pollingIntervalSeconds = 2,
        int maxRetries = 10,
        int retryDelaySeconds = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stored agent if it exists.
    /// Clears the Agent property after deletion.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAgentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stored thread if it exists.
    /// Clears the Thread property after deletion.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteThreadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup of agent, thread, and vector store resources based on configuration.
    /// If AutoDelete is true, deletes both thread and agent.
    /// If AutoCleanupResources is true, deletes the associated vector store.
    /// If either is false, logs and skips that cleanup step.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the agent key/identifier for this factory.
    /// </summary>
    string AgentKey { get; }

    /// <summary>
    /// Gets the agent definition options for this factory.
    /// </summary>
    AgentDefinitionOptions AgentDefinition { get; }
}