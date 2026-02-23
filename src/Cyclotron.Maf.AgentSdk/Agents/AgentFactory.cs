using Cyclotron.Maf.AgentSdk.Agents.Providers;
using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.Common.Services;
using Cyclotron.Maf.AgentSdk.Middleware;
using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Text.Json;
using VectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace Cyclotron.Maf.AgentSdk.Agents;

/// <summary>
/// Generic agent factory implementation that creates AI agents using <see cref="IPromptRenderingService"/> for instructions.
/// Registered as a keyed service with different agent keys.
/// Resolves model provider configuration from the agent's provider reference.
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
    private readonly IAgentProviderResolver _providerResolver;
    private readonly VectorStoreManager? _vectorStoreManager;
    private readonly ModelProviderOptions _providerOptions;
    private readonly string _agentKey;
    private readonly AgentDefinitionOptions _agentDefinition;
    private readonly TelemetryOptions _telemetryOptions;
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
    /// <param name="providerResolver">Resolver for provider-specific agent factories.</param>
    /// <param name="vectorStoreManager">Optional manager for vector store operations. If null, vector store functionality will be disabled.</param>
    /// <param name="telemetryOptions">The telemetry configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public AgentFactory(
        string agentKey,
        ILogger<AgentFactory> logger,
        IPromptRenderingService promptService,
        IOptions<ModelProviderOptions> providerOptions,
        IOptions<AgentOptions> agentOptions,
        IAgentProviderResolver providerResolver,
        VectorStoreManager? vectorStoreManager,
        IOptions<TelemetryOptions> telemetryOptions)
    {
        _agentKey = agentKey ?? throw new ArgumentNullException(nameof(agentKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        _vectorStoreManager = vectorStoreManager; // Optional - can be null

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
            #pragma warning disable MEAI001
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
            #pragma warning restore MEAI001

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
        #pragma warning disable MEAI001
        if (response.ContinuationToken != null)
        {
            return false;
        }
        #pragma warning restore MEAI001

        // Check if response has no messages or all messages have empty text
        return response.Messages != null &&
               response.Messages.Count > 0 &&
               response.Messages.All(m => string.IsNullOrWhiteSpace(m.Text));
    }

    /// <inheritdoc/>
    public async Task<IStructuredOutputAgentResponse<T>> RunAgentWithPollingAsync<T>(
        IList<ChatMessage> messages,
        int pollingIntervalSeconds = 2,
        int maxRetries = 10,
        int retryDelaySeconds = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Running {AgentKey} agent with structured output deserialization to type '{TargetType}'",
            _agentKey,
            typeof(T).FullName);

        // Run the agent and get the base response
        var response = await RunAgentWithPollingAsync(messages, pollingIntervalSeconds, maxRetries, retryDelaySeconds, cancellationToken);

        // Deserialize and return the typed result wrapped with original response
        return DeserializeStructuredResponse<T>(response);
    }

    /// <inheritdoc/>
    public async Task<IStructuredOutputAgentResponse<T>> RunAgentWithPollingAsync<T>(
        string userPrompt,
        int pollingIntervalSeconds = 2,
        int maxRetries = 10,
        int retryDelaySeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("User prompt cannot be null or empty", nameof(userPrompt));
        }

        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };
        return await RunAgentWithPollingAsync<T>(messages, pollingIntervalSeconds, maxRetries, retryDelaySeconds, cancellationToken);
    }

    /// <summary>
    /// Deserializes the agent response text into a structured output response wrapper.
    /// Preserves the original response while also providing the deserialized typed result.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="response">The base agent response with JSON text.</param>
    /// <returns>A structured output response containing both the deserialized result and original response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    private IStructuredOutputAgentResponse<T> DeserializeStructuredResponse<T>(AgentResponse response)
    {
        try
        {
            var responseText = response.Text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException(
                    $"Agent '{_agentKey}' returned an empty response. Cannot deserialize to {typeof(T).Name}.");
            }

            // Strip markdown code fences if present (e.g., ```json ... ```)
            responseText = StripMarkdownCodeFences(responseText);

            _logger.LogDebug(
                "Deserializing structured response for {AgentKey} into type '{TargetType}'",
                _agentKey,
                typeof(T).FullName);

            var result = JsonSerializer.Deserialize<T>(responseText, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException(
                    $"Deserialization of response into {typeof(T).Name} resulted in null.");

            _logger.LogInformation(
                "Successfully deserialized structured response for {AgentKey} into type '{TargetType}'",
                _agentKey,
                typeof(T).FullName);

            return new StructuredOutputAgentResponse<T>(result, response);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize structured response for {AgentKey} into type '{TargetType}'. Response text: {ResponseText}",
                _agentKey,
                typeof(T).FullName,
                response.Text);
            throw new InvalidOperationException(
                $"Failed to deserialize agent response into {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Strips markdown code fences from text if present.
    /// Handles both ```json and ``` fences.
    /// </summary>
    /// <param name="text">The text that may contain markdown code fences.</param>
    /// <returns>The text with code fences removed.</returns>
    private static string StripMarkdownCodeFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        text = text.Trim();

        // Check if text starts with ``` (with optional language identifier like json, xml, etc.)
        if (text.StartsWith("```"))
        {
            // Find the end of the first line (which contains the opening fence and optional language)
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd > 0)
            {
                // Remove the first line
                text = text.Substring(firstLineEnd + 1);
            }

            // Remove trailing ``` if present
            if (text.TrimEnd().EndsWith("```"))
            {
                var lastFenceIndex = text.LastIndexOf("```");
                text = text.Substring(0, lastFenceIndex);
            }

            text = text.Trim();
        }

        return text;
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

        var providerName = _agentDefinition.Provider;
        var provider = GetProviderDefinition(providerName);
        var providerImplementation = _providerResolver.Resolve(provider);
        if (!providerImplementation.Capabilities.SupportsVectorStore)
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' does not support vector store agents. " +
                "Use CreateAgentAsync without a vector store for local providers.");
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

        // Resolve structured output configuration if specified
        var structuredOutput = ResolveStructuredOutput();

        // Resolve temperature and top_p parameters (agent-level overrides provider-level)
        var (temperature, topP) = ResolveThermodynamicParameters();

        var creationRequest = new AgentProviderCreationRequest(
            _agentKey,
            providerName,
            provider,
            vectorStoreId,
            tools,
            instructions,
            _promptService.GetAgentNamePrefix(_agentKey),
            _agentDefinition.Version,
            structuredOutput,
            temperature,
            topP);

        var providerResult = await providerImplementation
            .CreateAgentAsync(creationRequest, cancellationToken)
            .ConfigureAwait(false);

        _createdAgentName = providerResult.CreatedAgentName;
        _createdAgentVersion = providerResult.CreatedAgentVersion;

        var agent = ApplyMiddleware(providerResult.Agent);
        Agent = agent;
        Session = await agent.CreateSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        VectorStoreId = vectorStoreId;
        _logger.LogDebug("Created session for {AgentKey} agent: {AgentId}", _agentKey, agent.Id);

        return agent;
    }

    /// <inheritdoc/>
    public async Task<AIAgent> CreateAgentAsync(CancellationToken cancellationToken = default)
    {
        var providerName = _agentDefinition.Provider;
        var provider = GetProviderDefinition(providerName);
        var providerImplementation = _providerResolver.Resolve(provider);

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

        // Resolve structured output configuration if specified
        var structuredOutput = ResolveStructuredOutput();

        // Resolve temperature and top_p parameters (agent-level overrides provider-level)
        var (temperature, topP) = ResolveThermodynamicParameters();

        var creationRequest = new AgentProviderCreationRequest(
            _agentKey,
            providerName,
            provider,
            null,
            [],
            instructions,
            _promptService.GetAgentNamePrefix(_agentKey),
            _agentDefinition.Version,
            structuredOutput,
            temperature,
            topP);

        var providerResult = await providerImplementation
            .CreateAgentAsync(creationRequest, cancellationToken)
            .ConfigureAwait(false);

        _createdAgentName = providerResult.CreatedAgentName;
        _createdAgentVersion = providerResult.CreatedAgentVersion;

        var agent = ApplyMiddleware(providerResult.Agent);
        Agent = agent;
        Session = await agent.CreateSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        VectorStoreId = null; // No vector store for this agent
        _logger.LogDebug("Created session for {AgentKey} agent: {AgentId} (no vector store)", _agentKey, agent.Id);

        return agent;
    }

    /// <inheritdoc/>
    public async Task DeleteAgentAsync(CancellationToken cancellationToken = default)
    {
        if (Agent == null)
        {
            _logger.LogDebug("No agent to delete");
            return;
        }

        try
        {
            var providerName = _agentDefinition.Provider;
            var provider = GetProviderDefinition(providerName);
            var providerImplementation = _providerResolver.Resolve(provider);

            if (!providerImplementation.Capabilities.SupportsAgentDeletion)
            {
                _logger.LogDebug("Skipping agent deletion for provider '{ProviderName}'", providerName);
                return;
            }

            var deletionRequest = new AgentProviderDeletionRequest(
                _agentKey,
                providerName,
                provider,
                Agent,
                _createdAgentName,
                _createdAgentVersion);

            await providerImplementation
                .DeleteAgentAsync(deletionRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {AgentKey} agent: {AgentId}", _agentKey, Agent.Id);
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
            var provider = GetProviderDefinition(providerName);
            var providerImplementation = _providerResolver.Resolve(provider);

            if (!providerImplementation.Capabilities.SupportsSessionDeletion)
            {
                _logger.LogDebug("Skipping session deletion for provider '{ProviderName}'", providerName);
                return;
            }

            var deletionRequest = new AgentProviderSessionDeletionRequest(
                _agentKey,
                providerName,
                provider,
                Session);

            await providerImplementation
                .DeleteSessionAsync(deletionRequest, cancellationToken)
                .ConfigureAwait(false);
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

    private StructuredOutputConfiguration? ResolveStructuredOutput()
    {
        if (string.IsNullOrWhiteSpace(_agentDefinition.StructuredOutputType))
        {
            return null;
        }

        try
        {
            _logger.LogDebug(
                "Resolving structured output type '{StructuredOutputType}' for {AgentKey}",
                _agentDefinition.StructuredOutputType,
                _agentKey);

            var config = StructuredOutputConfiguration.FromTypeName(_agentDefinition.StructuredOutputType);

            _logger.LogInformation(
                "Resolved structured output type '{StructuredOutputType}' (CLR Type: {ClrType}) for {AgentKey}",
                _agentDefinition.StructuredOutputType,
                config.OutputType.FullName,
                _agentKey);

            return config;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(
                ex,
                "Failed to resolve structured output type '{StructuredOutputType}' for {AgentKey}",
                _agentDefinition.StructuredOutputType,
                _agentKey);
            throw;
        }
    }

    private (float? Temperature, float? TopP) ResolveThermodynamicParameters()
    {
        // Agent-level settings override provider-level settings
        var temperature = _agentDefinition.Temperature ?? _providerOptions.Providers[_agentDefinition.Provider].Temperature;
        var topP = _agentDefinition.TopP ?? _providerOptions.Providers[_agentDefinition.Provider].TopP;

        if (temperature.HasValue || topP.HasValue)
        {
            _logger.LogDebug(
                "Resolved thermodynamic parameters for {AgentKey}: Temperature={Temperature}, TopP={TopP}",
                _agentKey,
                temperature?.ToString("F2") ?? "null",
                topP?.ToString("F2") ?? "null");
        }

        return (temperature, topP);
    }

    private void ValidateProviderReference()
    {
        var providerName = _agentDefinition.Provider;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException(
                $"Agent '{_agentKey}' does not have a provider configured. " +
                $"Please specify a provider reference in agent.config.yaml using the 'provider' property.");
        }

        if (!_providerOptions.Providers.ContainsKey(providerName))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}. " +
                $"Please add '{providerName}' to the providers: section in agent.config.yaml.");
        }
    }

    private ModelProviderDefinitionOptions GetProviderDefinition(string providerName)
    {
        if (!_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            throw new InvalidOperationException(
                $"Provider '{providerName}' referenced by agent '{_agentKey}' not found in configuration. " +
                $"Available providers: {string.Join(", ", _providerOptions.Providers.Keys)}");
        }

        return provider;
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

}
