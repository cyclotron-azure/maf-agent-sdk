using Cyclotron.Maf.AgentSdk.Middleware;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Polly;
using Polly.Retry;
using VectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

#pragma warning disable CS0618 // Type or member is obsolete

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
    private readonly IProviderClientFactory _clientFactory;
    private readonly VectorStoreManager? _vectorStoreManager;
    private readonly ModelProviderOptions _providerOptions;
    private readonly string _agentKey;
    private readonly AgentDefinitionOptions _agentDefinition;
    private readonly TelemetryOptions _telemetryOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private string? _createdAgentName;
    private string? _createdAgentVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFactory"/> class.
    /// </summary>
    /// <param name="agentKey">The unique key identifying this agent type.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="promptService">The service for rendering agent prompts.</param>
    /// <param name="providerOptions">The model provider configuration options.</param>
    /// <param name="agentOptions">The agent configuration options.</param>
    /// <param name="clientFactory">The factory for creating Azure AI Foundry clients.</param>
    /// <param name="vectorStoreManager">Optional manager for vector store operations. If null, vector store functionality will be disabled.</param>
    /// <param name="telemetryOptions">The telemetry configuration options.</param>
    /// <param name="httpClientFactory">The HTTP client factory for creating clients to Ollama.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public AgentFactory(
        string agentKey,
        ILogger<AgentFactory> logger,
        IPromptRenderingService promptService,
        IOptions<ModelProviderOptions> providerOptions,
        IOptions<AgentOptions> agentOptions,
        IProviderClientFactory clientFactory,
        VectorStoreManager? vectorStoreManager,
        IOptions<TelemetryOptions> telemetryOptions,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _agentKey = agentKey ?? throw new ArgumentNullException(nameof(agentKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _vectorStoreManager = vectorStoreManager; // Optional - can be null
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

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
    public AgentSession? Session { get; private set; }

    /// <inheritdoc/>
    public string? VectorStoreId { get; private set; }

    /// <inheritdoc/>
    public ChatMessage CreateUserMessage(object? context = null)
    {
        var prompt = _promptService.RenderUserPrompt(_agentKey, context);
        return new ChatMessage(ChatRole.User, [new TextContent(prompt)]);
    }

    /// <inheritdoc/>
    public async Task<AgentResponse> RunAgentWithPollingAsync(
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

        if (Session == null)
        {
            throw new InvalidOperationException("Session must be created before running. Call CreateAgentAsync first.");
        }

        _logger.LogDebug(
            "Running {AgentKey} agent with {MessageCount} messages (polling interval: {PollingInterval}s, max retries: {MaxRetries})",
            _agentKey,
            messages.Count,
            pollingIntervalSeconds,
            maxRetries);

        // Configure Polly retry pipeline with exponential backoff
        var retryPipeline = new ResiliencePipelineBuilder<AgentResponse>()
            .AddRetry(new RetryStrategyOptions<AgentResponse>()
            {
                ShouldHandle = new PredicateBuilder<AgentResponse>()
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
                Session,
                options,
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

                agentResponse = await Agent.RunAsync(Session, options, cancellationToken: ct);
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
    private static bool IsEmptyResponse(AgentResponse response)
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

        if (_vectorStoreManager == null)
        {
            throw new InvalidOperationException(
                "IVectorStoreManager is not registered. Vector store functionality requires the AgentSdk.Vectors package. " +
                "Add a package reference to AgentSdk.Vectors and call AddVectorStoreServices() in your startup configuration.");
        }

        // Get provider configuration from agent's provider property
        var providerName = _agentDefinition.Provider;
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

            _logger.LogDebug(
                "Creating {AgentKey} agent with configured version: {ConfiguredVersion}",
                _agentKey,
                _agentDefinition.Version ?? "(auto-generated)");

            // Create agent using V2 versioned API with PromptAgentDefinition
            var promptDefinition = new PromptAgentDefinition(model: provider.GetEffectiveModel())
            {
                Instructions = instructions,
            };

            if (tools.Count > 0)
            {
                var agentTools = tools
                    .Select(t => t.AsOpenAIResponseTool())
                    .Where(t => t is not null)
                    .Select(t => t!.AsAgentTool())
                    .ToList();

                foreach (var tool in agentTools)
                {
                    promptDefinition.Tools.Add(tool);
                }
            }

            var versionOptions = new AgentVersionCreationOptions(promptDefinition);
            AgentVersion createdAgentVersion = await projectClient.Agents.CreateAgentVersionAsync(
                agentName: agentName,
                options: versionOptions,
                cancellationToken: cancellationToken);

            _createdAgentName = agentName;
            _createdAgentVersion = createdAgentVersion.Version;

            // Get the AIAgent from the created version
            var agentRecord = projectClient.Agents.GetAgent(agentName).Value;
            var agentReference = new AgentReference(agentRecord.Id);
            AIAgent agent = projectClient.AsAIAgent(agentReference);
            var agentId = agent.Id;

            _logger.LogInformation(
                "Created {AgentKey} agent: {AgentId} (Name: {AgentName}, Provider: {ProviderName})",
                _agentKey,
                agentId,
                agentName,
                providerName);

            // Apply middleware using centralized helper
            agent = ApplyMiddleware(agent);

            // Store agent and create session automatically
            Agent = agent;
            Session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
            VectorStoreId = vectorStoreId;
            _logger.LogDebug("Created session for {AgentKey} agent: {AgentId}", _agentKey, agentId);

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
    public async Task<AIAgent> CreateAgentAsync(CancellationToken cancellationToken = default)
    {
        // Get provider configuration from agent's provider property
        var providerName = _agentDefinition.Provider;
        if (!_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}");
        }

        // Check if using local provider (Ollama) - create using ChatClientAgent pattern
        if (provider.IsLocalProvider())
        {
            return await CreateOllamaAgentAsync(provider, cancellationToken);
        }

        _logger.LogInformation(
            "Creating {AgentKey} agent WITHOUT vector store with provider '{ProviderName}' (Endpoint: {Endpoint}, Model: {Model})",
            _agentKey,
            providerName,
            provider.Endpoint,
            provider.GetEffectiveModel());

        // Get system prompt (instructions) from prompt rendering service
        var instructions = _promptService.RenderSystemPrompt(_agentKey);

        // No tools configuration for agents without vector stores (e.g., Ollama)
        // (BuildToolConfiguration returns List<AITool>, but we don't call it here)

        // Create ephemeral agent with unique name
        var namePrefix = _promptService.GetAgentNamePrefix(_agentKey);
        var agentName = $"{namePrefix}-{Guid.NewGuid().ToString("N")[..8]}";

        try
        {
            _logger.LogDebug(
                "Creating {AgentKey} agent with name: {AgentName}, provider: {ProviderName}, model: {Model} (no tools configured)",
                _agentKey,
                agentName,
                providerName,
                provider.GetEffectiveModel());

            // Get provider-specific client
            var projectClient = _clientFactory.GetClient(providerName);

            _logger.LogDebug(
                "Creating {AgentKey} agent with configured version: {ConfiguredVersion}",
                _agentKey,
                _agentDefinition.Version ?? "(auto-generated)");

            // Create agent using V2 versioned API with PromptAgentDefinition
            var promptDefinition = new PromptAgentDefinition(model: provider.GetEffectiveModel())
            {
                Instructions = instructions,
            };

            var versionOptions = new AgentVersionCreationOptions(promptDefinition);
            AgentVersion createdAgentVersion = await projectClient.Agents.CreateAgentVersionAsync(
                agentName: agentName,
                options: versionOptions,
                cancellationToken: cancellationToken);

            _createdAgentName = agentName;
            _createdAgentVersion = createdAgentVersion.Version;

            // Get the AIAgent from the created version
            var agentRecord = projectClient.Agents.GetAgent(agentName).Value;
            var agentReference = new AgentReference(agentRecord.Id);
            AIAgent agent = projectClient.AsAIAgent(agentReference);
            var agentId = agent.Id;

            _logger.LogInformation(
                "Created {AgentKey} agent WITHOUT vector store: {AgentId} (Name: {AgentName}, Provider: {ProviderName})",
                _agentKey,
                agentId,
                agentName,
                providerName);

            // Apply middleware using centralized helper
            agent = ApplyMiddleware(agent);

            // Store agent and create session automatically
            Agent = agent;
            Session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
            VectorStoreId = null; // No vector store for this agent
            _logger.LogDebug("Created session for {AgentKey} agent: {AgentId} (no vector store)", _agentKey, agentId);

            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create {AgentKey} agent WITHOUT vector store with provider '{ProviderName}'",
                _agentKey,
                providerName);

            throw;
        }
    }

    /// <summary>
    /// Creates an AI agent for Ollama local models using OpenAI-compatible API.
    /// </summary>
    private async Task<AIAgent> CreateOllamaAgentAsync(
        ModelProviderDefinitionOptions provider,
        CancellationToken cancellationToken)
    {
        var providerName = _agentDefinition.Provider;

        _logger.LogInformation(
            "Creating {AgentKey} agent for Ollama provider '{ProviderName}' (Endpoint: {Endpoint}, Model: {Model})",
            _agentKey,
            providerName,
            provider.Endpoint,
            provider.GetEffectiveModel());

        try
        {
            // Get system prompt (instructions) from prompt rendering service
            var instructions = _promptService.RenderSystemPrompt(_agentKey);

            // Create Ollama chat client using OllamaChatClient
            var httpClient = _httpClientFactory.CreateClient("ollama");
            var ollamaLogger = _loggerFactory.CreateLogger<OllamaChatClient>();
            var chatClient = new OllamaChatClient(httpClient, provider, ollamaLogger);

            // Wrap chat client in ChatClientAgent to get full AIAgent capabilities
            var chatClientLogger = _loggerFactory.CreateLogger<ChatClientAgent>();
            var chatOptions = new ChatOptions { Instructions = instructions };
            AIAgent agent = new ChatClientAgent(
                chatClient,
                instructions: instructions,
                name: $"{_promptService.GetAgentNamePrefix(_agentKey)}-ollama",
                loggerFactory: _loggerFactory);

            _logger.LogInformation(
                "Created {AgentKey} agent for Ollama provider '{ProviderName}' (Model: {Model})",
                _agentKey,
                providerName,
                provider.GetEffectiveModel());

            // Apply middleware using centralized helper
            agent = ApplyMiddleware(agent);

            // Store agent and create session automatically
            Agent = agent;
            Session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
            VectorStoreId = null; // No vector store for Ollama agents
            _logger.LogDebug("Created session for {AgentKey} Ollama agent", _agentKey);

            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create {AgentKey} agent for Ollama provider '{ProviderName}'",
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
            var providerName = _agentDefinition.Provider;

            // Skip cleanup for Ollama local providers - they don't support agent deletion
            if (IsOllamaProvider(providerName))
            {
                _logger.LogDebug("Skipping agent deletion for Ollama provider '{ProviderName}'", providerName);
                return;
            }

            var projectClient = _clientFactory.GetClient(providerName);
            if (TryParseAgentId(agentId, out var agentName, out var agentVersion))
            {
                await projectClient.Agents.DeleteAgentVersionAsync(agentName, agentVersion, cancellationToken);
                _logger.LogDebug("Deleted {AgentKey} agent version {AgentVersion}: {AgentName}", _agentKey, agentVersion, agentName);
            }
            else if (!string.IsNullOrWhiteSpace(_createdAgentName) && !string.IsNullOrWhiteSpace(_createdAgentVersion))
            {
                await projectClient.Agents.DeleteAgentVersionAsync(_createdAgentName, _createdAgentVersion, cancellationToken);
                _logger.LogDebug(
                    "Deleted {AgentKey} agent version {AgentVersion}: {AgentName} (fallback)",
                    _agentKey,
                    _createdAgentVersion,
                    _createdAgentName);
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
            _createdAgentName = null;
            _createdAgentVersion = null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteSessionAsync(CancellationToken cancellationToken = default)
    {
        if (Session == null)
        {
            _logger.LogDebug("No session to delete");
            return;
        }

        try
        {
            var providerName = _agentDefinition.Provider;

            // Skip cleanup for Ollama local providers - they don't support session management
            if (IsOllamaProvider(providerName))
            {
                _logger.LogDebug("Skipping session deletion for Ollama provider '{ProviderName}'", providerName);
                return;
            }

            var projectClient = _clientFactory.GetClient(providerName);

            // V2 API: Session/conversation deletion is not directly supported via AIProjectClient
            // Sessions are managed through agent lifecycle and are automatically cleaned up
            _logger.LogDebug("Session for {AgentKey} - deletion not directly supported in V2 API, will be cleaned up automatically", _agentKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {AgentKey} session", _agentKey);
        }
        finally
        {
            Session = null;
        }
    }

    /// <inheritdoc/>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        // Cleanup agent and session if AutoDelete is enabled
        if (_agentDefinition.AutoDelete)
        {
            await DeleteSessionAsync(cancellationToken);
            await DeleteAgentAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {AgentKey} agent and session", _agentKey);
        }
        else
        {
            _logger.LogInformation("Skipping {AgentKey} agent cleanup (AutoDelete=false)", _agentKey);
        }

        // Cleanup vector store if AutoCleanupResources is enabled
        if (_agentDefinition.AutoCleanupResources && !string.IsNullOrWhiteSpace(VectorStoreId) && _vectorStoreManager != null)
        {
            var providerName = _agentDefinition.Provider;
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
        else if (_agentDefinition.AutoCleanupResources && _vectorStoreManager == null)
        {
            _logger.LogWarning(
                "Vector store cleanup requested but IVectorStoreManager is not registered. Add AgentSdk.Vectors package and call AddVectorStoreServices() to enable vector store support.");
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

        // Default to file_search if no tools are configured and vector store manager is available
        if (configuredTools.Count == 0 && _vectorStoreManager != null)
        {
            _logger.LogDebug(
                "No tools configured for {AgentKey}, defaulting to file_search",
                _agentKey);
            configuredTools = ["file_search"];
        }
        else if (configuredTools.Count == 0)
        {
            _logger.LogDebug(
                "No tools configured for {AgentKey} and vector store manager not available - no tools will be added",
                _agentKey);
            return tools;
        }

        foreach (var tool in configuredTools)
        {
            switch (tool.ToLowerInvariant())
            {
                case "file_search":
                    if (_vectorStoreManager == null)
                    {
                        _logger.LogWarning(
                            "file_search tool requested for {AgentKey} but IVectorStoreManager is not registered. " +
                            "Add AgentSdk.Vectors package and call AddVectorStoreServices() to enable vector store support. Skipping file_search tool.",
                            _agentKey);
                        continue;
                    }
                    var fileSearchTool = new HostedFileSearchTool();
                    fileSearchTool.Inputs ??= [];
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

        // Only ensure at least one tool if vector store manager is available
        if (tools.Count == 0 && _vectorStoreManager != null && configuredTools.Any(t => t.Equals("file_search", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "No valid tools configured for {AgentKey}, defaulting to file_search",
                _agentKey);
            var fileSearchTool = new HostedFileSearchTool();
            if (fileSearchTool.Inputs == null)
            {
                fileSearchTool.Inputs = [];
            }
            fileSearchTool.Inputs.Add(new HostedVectorStoreContent(vectorStoreId));
            tools.Add(fileSearchTool);
        }

        return tools;
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

    private void ValidateProviderReference()
    {
        var providerName = _agentDefinition.Provider;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException(
                $"Agent '{_agentKey}' does not have a provider configured. " +
                $"Please specify a provider reference in agent.config.yaml (v2.0: use 'provider' property, not 'framework_config.provider').");
        }

        if (!_providerOptions.Providers.ContainsKey(providerName))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}. " +
                $"Please add '{providerName}' to the providers: section in agent.config.yaml.");
        }
    }

    /// <summary>
    /// Applies middleware to the agent using centralized middleware helper.
    /// Combines legacy TelemetryOptions with new MiddlewareConfiguration.
    /// </summary>
    private AIAgent ApplyMiddleware(AIAgent agent)
    {
        // Build middleware configuration from agent definition and telemetry options
        var middlewareConfig = _agentDefinition.Middleware ?? new MiddlewareConfiguration();

        // If TelemetryOptions are enabled, add/override OpenTelemetry configuration
        if (_telemetryOptions.Enabled && !string.IsNullOrWhiteSpace(_telemetryOptions.SourceName))
        {
            middlewareConfig.OpenTelemetryOptions = new AgentOpenTelemetryOptions(
                _telemetryOptions.SourceName,
                configure => configure.EnableSensitiveData = _telemetryOptions.EnableSensitiveData);
        }

        // Apply middleware using centralized helper
        return AgentMiddlewareHelper.ApplyMiddleware(agent, middlewareConfig, services: null);
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

    private bool IsOllamaProvider(string providerName)
    {
        // Check if the provider is for Ollama (local models that don't need cleanup)
        return !string.IsNullOrWhiteSpace(providerName) &&
               (providerName.Equals("ollama_local", StringComparison.OrdinalIgnoreCase) ||
                providerName.Equals("ollama", StringComparison.OrdinalIgnoreCase) ||
                providerName.StartsWith("ollama_", StringComparison.OrdinalIgnoreCase));
    }
}
