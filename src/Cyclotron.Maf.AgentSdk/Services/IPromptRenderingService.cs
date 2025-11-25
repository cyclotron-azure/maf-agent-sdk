namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Unified service for rendering both system and user prompts using Handlebars templates.
/// Consolidates prompt management for agent instructions (system) and task requests (user).
/// Provides dynamic prompt generation with context variable substitution from YAML configuration.
/// </summary>
/// <remarks>
/// <para>
/// System prompts define agent behavior, role, and rules, while user prompts define
/// specific task requests with dynamic context variables.
/// </para>
/// <para>
/// Templates are loaded from agent.config.yaml and compiled at startup for performance.
/// When context is null, the raw template is returned without Handlebars rendering.
/// </para>
/// </remarks>
public interface IPromptRenderingService
{
    /// <summary>
    /// Renders the system prompt (instructions) for a specific agent.
    /// System prompts define agent behavior, role, and rules.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier (e.g., "classification", "address_extraction").</param>
    /// <param name="context">Optional context object for template variable substitution. If null, the raw template is returned without Handlebars rendering.</param>
    /// <returns>The rendered system prompt/instructions string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="agentKey"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no system_prompt_template is configured for the agent.</exception>
    string RenderSystemPrompt(string agentKey, object? context = null);

    /// <summary>
    /// Renders the user prompt template for a specific agent with the given context.
    /// User prompts define specific task requests with dynamic context.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier (e.g., "classification", "address_extraction").</param>
    /// <param name="context">Optional context object for template variable substitution. If null, the raw template is returned without Handlebars rendering.</param>
    /// <returns>The rendered user prompt string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="agentKey"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no user_prompt_template is configured for the agent.</exception>
    string RenderUserPrompt(string agentKey, object? context = null);

    /// <summary>
    /// Gets the agent name prefix for generating unique agent names.
    /// Converts agent keys to PascalCase and appends "Agent" suffix if not present.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier (e.g., "classification" becomes "ClassificationAgent").</param>
    /// <returns>The agent name prefix (e.g., "ClassificationAgent", "AddressExtractionAgent").</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="agentKey"/> is null or empty.</exception>
    string GetAgentNamePrefix(string agentKey);

    /// <summary>
    /// Checks if a configuration exists for the given agent key.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier to check.</param>
    /// <returns><c>true</c> if agent configuration exists; otherwise, <c>false</c>.</returns>
    bool HasConfiguration(string agentKey);

    /// <summary>
    /// Checks if a system prompt template exists for the given agent key.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier to check.</param>
    /// <returns><c>true</c> if a system prompt template exists; otherwise, <c>false</c>.</returns>
    bool HasSystemPromptTemplate(string agentKey);

    /// <summary>
    /// Checks if a user prompt template exists for the given agent key.
    /// </summary>
    /// <param name="agentKey">The agent key/identifier to check.</param>
    /// <returns><c>true</c> if a user prompt template exists; otherwise, <c>false</c>.</returns>
    bool HasUserPromptTemplate(string agentKey);
}
