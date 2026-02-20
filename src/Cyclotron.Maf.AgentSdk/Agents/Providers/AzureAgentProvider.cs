using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Cyclotron.Maf.AgentSdk.Common.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using System.Reflection;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Provider implementation for Azure AI Foundry and Azure OpenAI.
/// </summary>
internal sealed class AzureAgentProvider(
    IProviderClientFactory clientFactory,
    ILogger<AzureAgentProvider> logger) : IAgentProvider
{
    private static readonly IReadOnlyCollection<string> SupportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "azure_foundry",
        "azure_openai"
    };

    private readonly IProviderClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly ILogger<AzureAgentProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedProviderTypes => SupportedTypes;

    /// <inheritdoc/>
    public AgentProviderCapabilities Capabilities { get; } = new(
        SupportsVectorStore: true,
        SupportsAgentDeletion: true,
        SupportsSessionDeletion: false,
        SupportsStructuredOutput: true);

    /// <inheritdoc/>
    public async Task<AgentProviderResult> CreateAgentAsync(
        AgentProviderCreationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        _logger.LogInformation(
            "Creating {AgentKey} agent with provider '{ProviderName}' (Endpoint: {Endpoint}, Model: {Model})",
            request.AgentKey,
            request.ProviderName,
            request.Provider.Endpoint,
            request.Provider.GetEffectiveModel());

        var agentName = $"{request.NamePrefix}-{Guid.NewGuid().ToString("N")[..8]}";

        try
        {
            _logger.LogDebug(
                "Creating {AgentKey} agent with name: {AgentName}, provider: {ProviderName}, model: {Model}",
                request.AgentKey,
                agentName,
                request.ProviderName,
                request.Provider.GetEffectiveModel());

            var projectClient = _clientFactory.GetClient(request.ProviderName);

            _logger.LogDebug(
                "Creating {AgentKey} agent with configured version: {ConfiguredVersion}",
                request.AgentKey,
                request.Version ?? "(auto-generated)");

            var promptDefinition = new PromptAgentDefinition(model: request.Provider.GetEffectiveModel())
            {
                Instructions = request.Instructions,
            };

            if (request.Tools.Count > 0)
            {
                var agentTools = request.Tools
                    .Select(tool => tool.AsOpenAIResponseTool())
                    .Where(tool => tool is not null)
                    .Select(tool => tool!.AsAgentTool())
                    .ToList();

                foreach (var tool in agentTools)
                {
                    promptDefinition.Tools.Add(tool);
                }
            }

            // Configure structured output if specified
            if (request.StructuredOutput != null)
            {
                ConfigureStructuredOutput(promptDefinition, request);
            }

            var versionOptions = new AgentVersionCreationOptions(promptDefinition);
            AgentVersion createdAgentVersion = await projectClient.Agents
                .CreateAgentVersionAsync(agentName, versionOptions, cancellationToken)
                .ConfigureAwait(false);

            var agentRecord = projectClient.Agents.GetAgent(agentName).Value;
            var agentReference = new AgentReference(agentRecord.Id);
            AIAgent agent = projectClient.AsAIAgent(agentReference);

            _logger.LogInformation(
                "Created {AgentKey} agent: {AgentId} (Name: {AgentName}, Provider: {ProviderName})",
                request.AgentKey,
                agent.Id,
                agentName,
                request.ProviderName);

            return new AgentProviderResult(agent, agentName, createdAgentVersion.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create {AgentKey} agent with provider '{ProviderName}'",
                request.AgentKey,
                request.ProviderName);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAgentAsync(
        AgentProviderDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var projectClient = _clientFactory.GetClient(request.ProviderName);

        if (TryParseAgentId(request.Agent.Id, out var agentName, out var agentVersion))
        {
            await projectClient.Agents
                .DeleteAgentVersionAsync(agentName, agentVersion, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Deleted {AgentKey} agent version {AgentVersion}: {AgentName}",
                request.AgentKey,
                agentVersion,
                agentName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.CreatedAgentName) &&
            !string.IsNullOrWhiteSpace(request.CreatedAgentVersion))
        {
            await projectClient.Agents
                .DeleteAgentVersionAsync(request.CreatedAgentName, request.CreatedAgentVersion, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Deleted {AgentKey} agent version {AgentVersion}: {AgentName} (fallback)",
                request.AgentKey,
                request.CreatedAgentVersion,
                request.CreatedAgentName);
            return;
        }

        _logger.LogWarning(
            "Agent ID format unexpected: {AgentId}. Expected 'name:version' format",
            request.Agent.Id);
    }

    /// <inheritdoc/>
    public Task DeleteSessionAsync(
        AgentProviderSessionDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        _logger.LogDebug(
            "Session for {AgentKey} - deletion not directly supported in V2 API, will be cleaned up automatically",
            request.AgentKey);

        return Task.CompletedTask;
    }

    private static bool TryParseAgentId(string agentId, out string agentName, out string agentVersion)
    {
        agentName = string.Empty;
        agentVersion = string.Empty;

        if (string.IsNullOrWhiteSpace(agentId))
        {
            return false;
        }

        var parts = agentId.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        agentName = parts[0];
        agentVersion = parts[1];
        return !string.IsNullOrWhiteSpace(agentName) && !string.IsNullOrWhiteSpace(agentVersion);
    }

    /// <summary>
    /// Configures structured output for the agent by setting up the response format
    /// to match the specified C# type.
    /// </summary>
    /// <param name="promptDefinition">The prompt agent definition to configure.</param>
    /// <param name="request">The agent creation request containing structured output configuration.</param>
    private void ConfigureStructuredOutput(
        PromptAgentDefinition promptDefinition,
        AgentProviderCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.StructuredOutput, nameof(request.StructuredOutput));

        try
        {
            var outputType = request.StructuredOutput.OutputType;

            _logger.LogDebug(
                "Configuring structured output for {AgentKey} agent with type '{OutputType}'",
                request.AgentKey,
                outputType.FullName);

            // Use reflection to call ChatResponseFormat.ForJsonSchema<T> with the dynamic type
            // Try the parameterless signature first, then try with parameters if available
            var methodInfo = typeof(ChatResponseFormat)
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "ForJsonSchema" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1);

            if (methodInfo == null)
            {
                _logger.LogWarning(
                    "Cannot find ChatResponseFormat.ForJsonSchema<T> method. Ensure Microsoft.Extensions.AI is properly referenced. " +
                    "Structured output configuration for type '{OutputType}' will not be applied to {AgentKey}.",
                    outputType.FullName,
                    request.AgentKey);
                return;
            }

            var genericMethod = methodInfo.MakeGenericMethod(outputType);
            var parameters = genericMethod.GetParameters();
            object?[] args;

            if (parameters.Length == 0)
            {
                args = [];
            }
            else if (parameters.All(parameter => parameter.IsOptional))
            {
                args = parameters.Select(_ => Type.Missing).ToArray();
            }
            else
            {
                _logger.LogWarning(
                    "ChatResponseFormat.ForJsonSchema<{OutputType}> requires non-optional parameters. " +
                    "Structured output configuration for type '{OutputType}' will not be applied to {AgentKey}.",
                    outputType.FullName,
                    outputType.FullName,
                    request.AgentKey);
                return;
            }

            var responseFormat = genericMethod.Invoke(null, args) as ChatResponseFormat;

            if (responseFormat == null)
            {
                _logger.LogWarning(
                    "Could not invoke ChatResponseFormat.ForJsonSchema<{OutputType}>(). " +
                    "The method signature may have changed in the latest Microsoft.Extensions.AI version. " +
                    "Structured output configuration for type '{OutputType}' will not be applied to {AgentKey}.",
                    outputType.FullName,
                    outputType.FullName,
                    request.AgentKey);
                return;
            }

            // Attempt to set ResponseFormat property via reflection
            // This supports various SDK versions that may have this property
            var property = typeof(PromptAgentDefinition).GetProperty("ResponseFormat");
            if (property != null && property.CanWrite)
            {
                property.SetValue(promptDefinition, responseFormat);

                _logger.LogInformation(
                    "Configured structured output for {AgentKey} agent with response format for type '{OutputType}'",
                    request.AgentKey,
                    outputType.FullName);
            }
            else
            {
                _logger.LogWarning(
                    "PromptAgentDefinition does not expose a ResponseFormat property. " +
                    "Structured output configuration for type '{OutputType}' may not be applied to {AgentKey}. " +
                    "Ensure the Azure.AI.Projects SDK version supports structured output.",
                    outputType.FullName,
                    request.AgentKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to configure structured output for {AgentKey} agent with type '{OutputType}'. " +
                "This is often due to SDK version compatibility. Continuing without structured output configuration.",
                request.AgentKey,
                request.StructuredOutput?.OutputType?.FullName ?? "(unknown)");
            // Don't rethrow - allow agent creation to continue without structured output
        }
    }
}
