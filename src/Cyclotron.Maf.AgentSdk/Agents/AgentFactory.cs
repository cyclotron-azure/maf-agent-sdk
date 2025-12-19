using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Cyclotron.Maf.AgentSdk.Agents;

/// <summary>
/// Generic agent factory implementation that creates AI agents using <see cref="IPromptRenderingService"/> for instructions.
/// Registered as a keyed service with different agent keys.
/// Resolves model provider configuration from the agent's framework_config.provider reference.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates ephemeral agents with unique names and stores them for the duration
/// of a workflow execution. Agents can be automatically deleted after use when AutoDelete is enabled.
/// </para>
/// <para>
/// The factory uses Polly for retry logic with exponential backoff when agent responses are empty.
/// </para>
/// </remarks>
public class AgentFactory : IAgentFactory
{
    private readonly ILogger<AgentFactory> _logger;
    private readonly IPromptRenderingService _promptService;
    private readonly IPersistentAgentsClientFactory _clientFactory;
    private readonly IVectorStoreManager _vectorStoreManager;
    private readonly ModelProviderOptions _providerOptions;
    private readonly string _agentKey;
    private readonly AgentDefinitionOptions _agentDefinition;
    private readonly TelemetryOptions _telemetryOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFactory"/> class.
    /// </summary>
    /// <param name="agentKey">The unique key identifying this agent type.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="promptService">The service for rendering agent prompts.</param>
    /// <param name="providerOptions">The model provider configuration options.</param>
    /// <param name="agentOptions">The agent configuration options.</param>
    /// <param name="clientFactory">The factory for creating Azure AI Foundry clients.</param>
    /// <param name="vectorStoreManager">The manager for vector store operations.</param>
    /// <param name="telemetryOptions">The telemetry configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public AgentFactory(
        string agentKey,
        ILogger<AgentFactory> logger,
        IPromptRenderingService promptService,
        IOptions<ModelProviderOptions> providerOptions,
        IOptions<AgentOptions> agentOptions,
        IPersistentAgentsClientFactory clientFactory,
        IVectorStoreManager vectorStoreManager,
        IOptions<TelemetryOptions> telemetryOptions)
    {
        _agentKey = agentKey ?? throw new ArgumentNullException(nameof(agentKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _vectorStoreManager = vectorStoreManager ?? throw new ArgumentNullException(nameof(vectorStoreManager));

        ArgumentNullException.ThrowIfNull(providerOptions, nameof(providerOptions));
        ArgumentNullException.ThrowIfNull(agentOptions, nameof(agentOptions));
        ArgumentNullException.ThrowIfNull(telemetryOptions, nameof(telemetryOptions));

        _providerOptions = providerOptions.Value;
        _telemetryOptions = telemetryOptions.Value;

        // Validate that configuration exists for this agent key
        if (!_promptService.HasConfiguration(_agentKey))
        {
            _logger.LogWarning(
                "No configuration found for agent key '{AgentKey}'. Using default instructions.",
                _agentKey);
        }

        // Get agent definition from configuration (creates default if not found)
        _agentDefinition = GetAgentDefinition(agentOptions.Value, _agentKey);

        // Validate provider reference
        ValidateProviderReference();
    }

    /// <summary>
    /// Gets the agent key identifying this factory's agent type.
    /// </summary>
    public string AgentKey => _agentKey;

    /// <summary>
    /// Gets the agent definition options loaded from configuration.
    /// </summary>
    public AgentDefinitionOptions AgentDefinition => _agentDefinition;

    /// <inheritdoc/>
    public AIAgent? Agent { get; private set; }

    /// <inheritdoc/>
    public AgentThread? Thread { get; private set; }

    /// <inheritdoc/>
    public string? VectorStoreId { get; private set; }

    /// <inheritdoc/>
    public ChatMessage CreateUserMessage(object? context = null)
    {
        var prompt = _promptService.RenderUserPrompt(_agentKey, context);
        return new ChatMessage(ChatRole.User, [new TextContent(prompt)]);
    }

    /// <inheritdoc/>
    public async Task<AgentRunResponse> RunAgentWithPollingAsync(
        IList<ChatMessage> messages,
        int pollingIntervalSeconds = 2,
        int maxRetries = 10,
        int retryDelaySeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (Agent == null)
        {
            throw new InvalidOperationException("Agent must be created before running. Call CreateAgentAsync first.");
        }

        if (Thread == null)
        {
            throw new InvalidOperationException("Thread must be created before running. Call CreateAgentAsync first.");
        }

        _logger.LogDebug(
            "Running {AgentKey} agent with {MessageCount} messages (polling interval: {PollingInterval}s, max retries: {MaxRetries})",
            _agentKey,
            messages.Count,
            pollingIntervalSeconds,
            maxRetries);

        // Configure Polly retry pipeline with exponential backoff
        var retryPipeline = new ResiliencePipelineBuilder<AgentRunResponse>()
            .AddRetry(new RetryStrategyOptions<AgentRunResponse>
            {
                ShouldHandle = new PredicateBuilder<AgentRunResponse>()
                    .HandleResult(response => IsEmptyResponse(response)),
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(retryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Empty response from {AgentKey} agent (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}...",
                        _agentKey,
                        args.AttemptNumber + 1,
                        maxRetries + 1,
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        // Execute agent run with retry policy
        var response = await retryPipeline.ExecuteAsync(async ct =>
        {
            // Enable background responses (only supported by OpenAI Responses at this time)
            AgentRunOptions options = new() { AllowBackgroundResponses = true };

            // Initial agent run
            var agentResponse = await Agent.RunAsync(
                messages,
                thread: Thread,
                options: options,
                cancellationToken: ct);

            // Poll until the response is complete
            while (agentResponse.ContinuationToken is { } token)
            {
                // Wait before polling again
                await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), ct);

                // Continue with the token
                options.ContinuationToken = token;
                _logger.LogDebug(
                    "Polling {AgentKey} agent for continuation with token: {ContinuationToken}",
                    _agentKey,
                    token);

                agentResponse = await Agent.RunAsync(Thread, options, cancellationToken: ct);
            }

            return agentResponse;
        }, cancellationToken);

        _logger.LogDebug(
            "{AgentKey} agent run completed - Messages: {MessageCount}",
            _agentKey,
            response.Messages?.Count ?? 0);

        return response ?? throw new InvalidOperationException("Agent run completed with null response");
    }

    /// <summary>
    /// Determines if an agent response is considered empty.
    /// A response is empty if no continuation occurred and all message texts are empty or whitespace.
    /// </summary>
    private static bool IsEmptyResponse(AgentRunResponse response)
    {
        // If there's a continuation token, the response is not considered empty (still processing)
        if (response.ContinuationToken != null)
        {
            return false;
        }

        // Check if response has no messages or all messages have empty text
        return response.Messages != null &&
               response.Messages.Count > 0 &&
               response.Messages.All(m => string.IsNullOrWhiteSpace(m.Text));
    }

    /// <inheritdoc/>
    public async Task<AIAgent> CreateAgentAsync(
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vectorStoreId))
        {
            throw new ArgumentException("Vector store ID cannot be null or empty", nameof(vectorStoreId));
        }

        // Get provider configuration from agent's framework_config
        var providerName = _agentDefinition.AIFrameworkOptions.Provider;
        if (!_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}");
        }

        _logger.LogInformation(
            "Creating {AgentKey} agent with provider '{ProviderName}' (Endpoint: {Endpoint}, Model: {Model})",
            _agentKey,
            providerName,
            provider.Endpoint,
            provider.GetEffectiveModel());

        // Get system prompt (instructions) from prompt rendering service
        var instructions = _promptService.RenderSystemPrompt(_agentKey);

        // Configure tools based on agent metadata configuration
        var tools = BuildToolConfiguration(vectorStoreId);

        // Create ephemeral agent with unique name
        var namePrefix = _promptService.GetAgentNamePrefix(_agentKey);
        var agentName = $"{namePrefix}-{Guid.NewGuid().ToString("N")[..8]}";

        try
        {
            _logger.LogDebug(
                "Creating {AgentKey} agent with name: {AgentName}, provider: {ProviderName}, model: {Model}, tools: [{Tools}]",
                _agentKey,
                agentName,
                providerName,
                provider.GetEffectiveModel(),
                string.Join(", ", _agentDefinition.Metadata.Tools));

            // Get provider-specific client
            var projectClient = _clientFactory.GetClient(providerName);

            // Create agent using V2 API
            AIAgent agent = await projectClient.CreateAIAgentAsync(
                model: provider.GetEffectiveModel(),
                name: agentName,
                instructions: instructions,
                tools: tools,
                cancellationToken: cancellationToken);

            var agentId = agent.Id;

            _logger.LogInformation(
                "Created {AgentKey} agent: {AgentId} (Name: {AgentName}, Provider: {ProviderName})",
                _agentKey,
                agentId,
                agentName,
                providerName);

            if (_telemetryOptions.Enabled && !string.IsNullOrWhiteSpace(_telemetryOptions.SourceName))
            {
                agent = agent.AsBuilder()
                    .UseOpenTelemetry(
                        _telemetryOptions.SourceName,
                        configure =>
                        {
                            configure.EnableSensitiveData = _telemetryOptions.EnableSensitiveData;
                        })
                    .Build();
            }

            // Store agent and create thread automatically
            Agent = agent;
            Thread = agent.GetNewThread();
            VectorStoreId = vectorStoreId;
            _logger.LogDebug("Created thread for {AgentKey} agent: {AgentId}", _agentKey, agentId);

            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create {AgentKey} agent with provider '{ProviderName}'",
                _agentKey,
                providerName);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAgentAsync(CancellationToken cancellationToken = default)
    {
        if (Agent == null)
        {
            _logger.LogDebug("No agent to delete");
            return;
        }

        var agentId = Agent.Id;

        try
        {
            var providerName = _agentDefinition.AIFrameworkOptions.Provider;
            var projectClient = _clientFactory.GetClient(providerName);

            // Agent IDs in V2 API are formatted as "name:version"
            // Extract both parts for DeleteAgentVersionAsync
            var parts = agentId.Split(':');
            if (parts.Length == 2)
            {
                var agentName = parts[0];
                var agentVersion = parts[1];
                await projectClient.Agents.DeleteAgentVersionAsync(agentName, agentVersion, cancellationToken);
                _logger.LogDebug("Deleted {AgentKey} agent version {AgentVersion}: {AgentName}", _agentKey, agentVersion, agentName);
            }
            else
            {
                _logger.LogWarning("Agent ID format unexpected: {AgentId}. Expected 'name:version' format", agentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {AgentKey} agent: {AgentId}", _agentKey, agentId);
        }
        finally
        {
            Agent = null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteThreadAsync(CancellationToken cancellationToken = default)
    {
        if (Thread == null)
        {
            _logger.LogDebug("No thread to delete");
            return;
        }

        try
        {
            // Cast to ChatClientAgentThread to access ConversationId
            var typedThread = Thread as ChatClientAgentThread;
            if (typedThread?.ConversationId == null)
            {
                _logger.LogWarning("Thread does not have a ConversationId, cannot delete");
                return;
            }

            var providerName = _agentDefinition.AIFrameworkOptions.Provider;
            var projectClient = _clientFactory.GetClient(providerName);

            // V2 API: Thread/conversation deletion is not directly supported via AIProjectClient
            // Threads are managed through agent lifecycle and are automatically cleaned up
            _logger.LogDebug("Thread {ThreadId} for {AgentKey} - deletion not directly supported in V2 API, will be cleaned up automatically", typedThread.ConversationId, _agentKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {AgentKey} thread", _agentKey);
        }
        finally
        {
            Thread = null;
        }
    }

    /// <inheritdoc/>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        // Cleanup agent and thread if AutoDelete is enabled
        if (_agentDefinition.AutoDelete)
        {
            await DeleteThreadAsync(cancellationToken);
            await DeleteAgentAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {AgentKey} agent and thread", _agentKey);
        }
        else
        {
            _logger.LogInformation("Skipping {AgentKey} agent cleanup (AutoDelete=false)", _agentKey);
        }

        // Cleanup vector store if AutoCleanupResources is enabled
        if (_agentDefinition.AutoCleanupResources && !string.IsNullOrWhiteSpace(VectorStoreId))
        {
            var providerName = _agentDefinition.AIFrameworkOptions.Provider;
            _logger.LogInformation(
                "Cleaning up vector store for {AgentKey}: {VectorStoreId}",
                _agentKey,
                VectorStoreId);

            try
            {
                await _vectorStoreManager.CleanupVectorStoreAsync(
                    providerName,
                    VectorStoreId,
                    cancellationToken);

                _logger.LogInformation(
                    "Successfully deleted vector store: {VectorStoreId}",
                    VectorStoreId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete vector store {VectorStoreId} during cleanup",
                    VectorStoreId);
            }
            finally
            {
                VectorStoreId = null;
            }
        }
        else if (_agentDefinition.AutoCleanupResources)
        {
            _logger.LogDebug("No vector store to clean up for {AgentKey}", _agentKey);
        }
        else
        {
            _logger.LogInformation(
                "Skipping {AgentKey} vector store cleanup (AutoCleanupResources=false)",
                _agentKey);
        }
    }

    /// <summary>
    /// Builds the tool configuration based on the agent's metadata.tools configuration.
    /// Supports "file_search" and "code_interpreter" tools using V2 API patterns.
    /// </summary>
    /// <param name="vectorStoreId">The vector store ID to associate with file search tool.</param>
    /// <returns>A list of tools for the agent.</returns>
    private List<AITool> BuildToolConfiguration(string vectorStoreId)
    {
        var tools = new List<AITool>();
        var configuredTools = _agentDefinition.Metadata.Tools;

        // Default to file_search if no tools are configured
        if (configuredTools.Count == 0)
        {
            _logger.LogDebug(
                "No tools configured for {AgentKey}, defaulting to file_search",
                _agentKey);
            configuredTools = ["file_search"];
        }

        foreach (var tool in configuredTools)
        {
            switch (tool.ToLowerInvariant())
            {
                case "file_search":
                    var fileSearchTool = new HostedFileSearchTool();
                    if (fileSearchTool.Inputs == null)
                    {
                        fileSearchTool.Inputs = new List<AIContent>();
                    }
                    fileSearchTool.Inputs.Add(new HostedVectorStoreContent(vectorStoreId));
                    tools.Add(fileSearchTool);
                    _logger.LogDebug("Configured file_search tool for {AgentKey} with vector store {VectorStoreId}", _agentKey, vectorStoreId);
                    break;

                case "code_interpreter":
                    tools.Add(new HostedCodeInterpreterTool());
                    _logger.LogDebug("Configured code_interpreter tool for {AgentKey}", _agentKey);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown tool '{Tool}' configured for {AgentKey}. Supported tools: file_search, code_interpreter",
                        tool,
                        _agentKey);
                    break;
            }
        }

        // Ensure at least one tool is configured
        if (tools.Count == 0)
        {
            _logger.LogWarning(
                "No valid tools configured for {AgentKey}, defaulting to file_search",
                _agentKey);
            var fileSearchTool = new HostedFileSearchTool();
            if (fileSearchTool.Inputs == null)
            {
                fileSearchTool.Inputs = new List<AIContent>();
            }
            fileSearchTool.Inputs.Add(new HostedVectorStoreContent(vectorStoreId));
            tools.Add(fileSearchTool);
        }

        return tools;
    }

    private void ValidateProviderReference()
    {
        var providerName = _agentDefinition.AIFrameworkOptions.Provider;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException(
                $"Agent '{_agentKey}' does not have a provider configured in framework_config.provider. " +
                $"Please specify a provider reference in agent.config.yaml.");
        }

        if (!_providerOptions.Providers.ContainsKey(providerName))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}. " +
                $"Please add '{providerName}' to the providers: section in agent.config.yaml.");
        }
    }

    private AgentDefinitionOptions GetAgentDefinition(AgentOptions agentOptions, string agentKey)
    {
        // Try exact match: classification -> classification_agent
        var agentConfigKey = $"{agentKey}_agent";
        if (agentOptions.Agents.TryGetValue(agentConfigKey, out var agentDef))
        {
            return agentDef;
        }

        // Try without _agent suffix
        if (agentOptions.Agents.TryGetValue(agentKey, out agentDef))
        {
            return agentDef;
        }

        // Return default configuration if not found
        _logger.LogWarning(
            "No configuration found for agent key: {AgentKey}, using default configuration",
            agentKey);
        return new AgentDefinitionOptions
        {
            Type = agentKey,
            Enabled = true,
            AutoDelete = true
        };
    }
}
