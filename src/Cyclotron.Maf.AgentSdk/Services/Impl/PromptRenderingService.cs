using Cyclotron.Maf.AgentSdk.Options;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Unified implementation for rendering both system and user prompts using Handlebars.Net.
/// Consolidates agent prompt management with dynamic context variable substitution.
/// </summary>
/// <remarks>
/// <para>
/// Templates are loaded from agent.config.yaml and compiled at startup for performance.
/// The service supports both system prompts (agent instructions) and user prompts (task requests).
/// </para>
/// <para>
/// Agent keys can be specified with or without the "_agent" suffix. The service automatically
/// tries both patterns when looking up configurations.
/// </para>
/// </remarks>
public class PromptRenderingService : IPromptRenderingService
{
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<PromptRenderingService> _logger;
    private readonly IHandlebars _handlebars;
    private readonly Dictionary<string, HandlebarsTemplate<object, object>> _compiledSystemTemplates;
    private readonly Dictionary<string, HandlebarsTemplate<object, object>> _compiledUserTemplates;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRenderingService"/> class.
    /// Compiles all templates at startup for optimal runtime performance.
    /// </summary>
    /// <param name="agentOptions">The agent configuration options containing prompt templates.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public PromptRenderingService(
        IOptions<AgentOptions> agentOptions,
        ILogger<PromptRenderingService> logger)
    {
        _agentOptions = agentOptions?.Value ?? throw new ArgumentNullException(nameof(agentOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlebars = Handlebars.Create();
        _compiledSystemTemplates = [];
        _compiledUserTemplates = [];

        // Pre-compile all templates at startup
        CompileTemplates();
    }

    /// <inheritdoc/>
    public string RenderSystemPrompt(string agentKey, object? context = null)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new ArgumentException("Agent key cannot be null or empty", nameof(agentKey));
        }

        // If context is null, return the raw template without Handlebars rendering
        if (context == null)
        {
            var agentConfig = GetAgentConfig(agentKey);
            if (agentConfig == null || string.IsNullOrWhiteSpace(agentConfig.SystemPromptTemplate))
            {
                _logger.LogError("No system prompt template found for agent key: {AgentKey}", agentKey);
                throw new InvalidOperationException(
                    $"No system_prompt_template configured for agent: {agentKey}. "
                    + "Please add a system_prompt_template field in agent.config.yaml.");
            }

            _logger.LogDebug("Returning raw system prompt template for agent: {AgentKey} (no context provided)", agentKey);
            return agentConfig.SystemPromptTemplate;
        }

        // Context provided - use Handlebars rendering
        var template = GetCompiledSystemTemplate(agentKey);

        if (template == null)
        {
            _logger.LogError("No system prompt template found for agent key: {AgentKey}", agentKey);
            throw new InvalidOperationException(
                $"No system_prompt_template configured for agent: {agentKey}. "
                + "Please add a system_prompt_template field in agent.config.yaml.");
        }

        try
        {
            var renderedPrompt = template(context);
            _logger.LogDebug("Rendered system prompt for agent: {AgentKey} with context", agentKey);
            return renderedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render system prompt template for agent: {AgentKey}", agentKey);
            throw new InvalidOperationException($"Failed to render system prompt for agent: {agentKey}", ex);
        }
    }

    /// <inheritdoc/>
    public string RenderUserPrompt(string agentKey, object? context = null)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new ArgumentException("Agent key cannot be null or empty", nameof(agentKey));
        }

        // If context is null, return the raw template without Handlebars rendering
        if (context == null)
        {
            var agentConfig = GetAgentConfig(agentKey);
            if (agentConfig == null || string.IsNullOrWhiteSpace(agentConfig.UserPromptTemplate))
            {
                _logger.LogError("No user prompt template found for agent key: {AgentKey}", agentKey);
                throw new InvalidOperationException(
                    $"No user_prompt_template configured for agent: {agentKey}. "
                    + "Please add a user_prompt_template field in agent.config.yaml.");
            }

            _logger.LogDebug("Returning raw user prompt template for agent: {AgentKey} (no context provided)", agentKey);
            return agentConfig.UserPromptTemplate;
        }

        // Context provided - use Handlebars rendering
        var template = GetCompiledUserTemplate(agentKey);

        if (template == null)
        {
            _logger.LogError("No user prompt template found for agent key: {AgentKey}", agentKey);
            throw new InvalidOperationException(
                $"No user_prompt_template configured for agent: {agentKey}. "
                + "Please add a user_prompt_template field in agent.config.yaml.");
        }

        try
        {
            var renderedPrompt = template(context);
            _logger.LogDebug("Rendered user prompt for agent: {AgentKey} with context", agentKey);
            return renderedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render user prompt template for agent: {AgentKey}", agentKey);
            throw new InvalidOperationException($"Failed to render user prompt for agent: {agentKey}", ex);
        }
    }

    /// <inheritdoc/>
    public string GetAgentNamePrefix(string agentKey)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new ArgumentException("Agent key cannot be null or empty", nameof(agentKey));
        }

        return ConvertToPascalCase(agentKey);
    }

    /// <inheritdoc/>
    public bool HasConfiguration(string agentKey)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            return false;
        }

        return GetAgentConfig(agentKey) != null;
    }

    /// <inheritdoc/>
    public bool HasSystemPromptTemplate(string agentKey)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            return false;
        }

        return GetCompiledSystemTemplate(agentKey) != null;
    }

    /// <inheritdoc/>
    public bool HasUserPromptTemplate(string agentKey)
    {
        if (string.IsNullOrWhiteSpace(agentKey))
        {
            return false;
        }

        return GetCompiledUserTemplate(agentKey) != null;
    }

    private void CompileTemplates()
    {
        foreach (var (key, agentDef) in _agentOptions.Agents)
        {
            // Compile system prompt template
            if (!string.IsNullOrWhiteSpace(agentDef.SystemPromptTemplate))
            {
                try
                {
                    var template = _handlebars.Compile(agentDef.SystemPromptTemplate);
                    _compiledSystemTemplates[key] = template;
                    _logger.LogInformation("Compiled system prompt template for agent: {AgentKey}", key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile system prompt template for agent: {AgentKey}", key);
                }
            }

            // Compile user prompt template
            if (!string.IsNullOrWhiteSpace(agentDef.UserPromptTemplate))
            {
                try
                {
                    var template = _handlebars.Compile(agentDef.UserPromptTemplate);
                    _compiledUserTemplates[key] = template;
                    _logger.LogInformation("Compiled user prompt template for agent: {AgentKey}", key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile user prompt template for agent: {AgentKey}", key);
                }
            }
        }
    }

    private AgentDefinitionOptions? GetAgentConfig(string agentKey)
    {
        // Try exact match: classification -> classification_agent
        var agentConfigKey = $"{agentKey}_agent";
        if (_agentOptions.Agents.TryGetValue(agentConfigKey, out var agentDef))
        {
            return agentDef;
        }

        // Try without _agent suffix
        if (_agentOptions.Agents.TryGetValue(agentKey, out agentDef))
        {
            return agentDef;
        }

        return null;
    }

    private HandlebarsTemplate<object, object>? GetCompiledSystemTemplate(string agentKey)
    {
        // Try exact match: classification -> classification_agent
        var agentConfigKey = $"{agentKey}_agent";
        if (_compiledSystemTemplates.TryGetValue(agentConfigKey, out var template))
        {
            return template;
        }

        // Try without _agent suffix
        if (_compiledSystemTemplates.TryGetValue(agentKey, out template))
        {
            return template;
        }

        return null;
    }

    private HandlebarsTemplate<object, object>? GetCompiledUserTemplate(string agentKey)
    {
        // Try exact match: classification -> classification_agent
        var agentConfigKey = $"{agentKey}_agent";
        if (_compiledUserTemplates.TryGetValue(agentConfigKey, out var template))
        {
            return template;
        }

        // Try without _agent suffix
        if (_compiledUserTemplates.TryGetValue(agentKey, out template))
        {
            return template;
        }

        return null;
    }

    private string ConvertToPascalCase(string agentKey)
    {
        var parts = agentKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));

        return result.EndsWith("Agent") ? result : result + "Agent";
    }
}
