using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Hosted service that validates all required agent prompt templates are loaded from YAML configuration.
/// Runs at application startup to ensure fail-fast behavior if templates are missing.
/// </summary>
public class AgentTemplateValidationService(
    IPromptRenderingService promptService,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentTemplateValidationService> logger) : IHostedService
{
    private readonly IPromptRenderingService _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
    private readonly AgentOptions _agentOptions = agentOptions?.Value ?? throw new ArgumentNullException(nameof(agentOptions));
    private readonly ILogger<AgentTemplateValidationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Validates that all enabled agents have required prompt templates configured.
    /// Throws <see cref="InvalidOperationException"/> if any templates are missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task if validation succeeds.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required prompt templates are missing.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating agent prompt templates from agent.config.yaml...");

        var missingTemplates = new List<string>();

        // Validate all agents defined in configuration instead of hard-coded keys
        foreach (var (agentKey, agentDefinition) in _agentOptions.Agents)
        {
            // Skip disabled agents
            if (!agentDefinition.Enabled)
            {
                _logger.LogDebug("Skipping validation for disabled agent '{AgentKey}'", agentKey);
                continue;
            }

            var hasSystemTemplate = _promptService.HasSystemPromptTemplate(agentKey);
            var hasUserTemplate = _promptService.HasUserPromptTemplate(agentKey);

            if (!hasSystemTemplate)
            {
                missingTemplates.Add($"Agent '{agentKey}': missing 'system_prompt_template' in agent.config.yaml");
            }

            if (!hasUserTemplate)
            {
                missingTemplates.Add($"Agent '{agentKey}': missing 'user_prompt_template' in agent.config.yaml");
            }

            if (hasSystemTemplate && hasUserTemplate)
            {
                _logger.LogInformation("✓ Agent '{AgentKey}': Both system_prompt_template and user_prompt_template loaded", agentKey);
            }
        }

        if (missingTemplates.Count > 0)
        {
            var errorMessage = "CONFIGURATION ERROR: Required agent prompt templates are missing from agent.config.yaml:\n"
                + string.Join("\n", missingTemplates)
                + "\n\nPlease ensure all agents have both 'system_prompt_template' and 'user_prompt_template' configured.";

            _logger.LogError("{ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("✓ All agent prompt templates validated successfully");
        return Task.CompletedTask;
    }

    /// <summary>
    /// No cleanup required on stop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
