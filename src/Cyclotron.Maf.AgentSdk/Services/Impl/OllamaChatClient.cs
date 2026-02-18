using System.Text;
using System.Text.Json;
using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Chat client for Ollama local models that implements <see cref="IChatClient"/>.
/// Provides OpenAI-compatible API access to locally running Ollama models.
/// </summary>
/// <remarks>
/// <para>
/// Ollama is a lightweight framework for running large language models locally.
/// This client uses Ollama's OpenAI-compatible API endpoint (typically at http://localhost:11434/v1/).
/// </para>
/// <para>
/// Supports both streaming and non-streaming chat completions.
/// Does not currently support function calling or tools.
/// </para>
/// </remarks>
public class OllamaChatClient : IChatClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;
    private readonly ILogger<OllamaChatClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaChatClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making requests to Ollama.</param>
    /// <param name="provider">The provider configuration containing endpoint and model info.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public OllamaChatClient(
        HttpClient httpClient,
        ModelProviderDefinitionOptions provider,
        ILogger<OllamaChatClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _modelId = provider.GetEffectiveModel();
        _endpoint = NormalizeEndpoint(provider.Endpoint);
        _timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds);

        _logger.LogInformation(
            "Initialized OllamaChatClient for model '{ModelId}' at endpoint '{Endpoint}' with timeout {TimeoutSeconds}s",
            _modelId,
            _endpoint,
            provider.TimeoutSeconds);
    }

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => new("Ollama", new Uri(_endpoint), _modelId);

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "GetResponseAsync: Preparing chat completion request for model '{ModelId}' with {MessageCount} messages",
            _modelId,
            messages.Count());

        try
        {
            var requestBody = BuildRequestPayload(messages, options, stream: false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                BuildChatCompletionUrl(),
                content,
                cancellationToken: cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Ollama API error (status {StatusCode}): {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                throw new InvalidOperationException(
                    $"Ollama API error: {response.StatusCode}. Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseChatResponse(responseContent);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "OllamaChatClient request timed out after {TimeoutSeconds}s", _timeout.TotalSeconds);
            throw new InvalidOperationException(
                $"Ollama request timed out after {_timeout.TotalSeconds} seconds", ex);
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "OllamaChatClient error in GetResponseAsync");
            throw;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in GetStreamingResponseAsync(messages, options, messageEdits: null, cancellationToken))
        {
            yield return update;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        System.Collections.Generic.IAsyncEnumerable<ChatMessage>? messageEdits = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "GetStreamingResponseAsync: Preparing streaming chat request for model '{ModelId}' with {MessageCount} messages",
            _modelId,
            messages.Count());

        if (messageEdits is not null)
        {
            _logger.LogWarning("OllamaChatClient: messageEdits are not supported");
        }

        var requestBody = BuildRequestPayload(messages, options, stream: true);
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.PostAsync(
                BuildChatCompletionUrl(),
                content,
                cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "OllamaChatClient streaming request timed out after {TimeoutSeconds}s", _timeout.TotalSeconds);
            throw new InvalidOperationException(
                $"Ollama streaming request timed out after {_timeout.TotalSeconds} seconds", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OllamaChatClient error in GetStreamingResponseAsync");
            throw;
        }

        if (response == null)
        {
            yield break;
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogError(
                    "Ollama streaming API error (status {StatusCode}): {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                throw new InvalidOperationException(
                    $"Ollama streaming API error: {response.StatusCode}. Response: {errorContent}");
            }

            await foreach (var update in ParseStreamingResponseAsync(response, cts.Token))
            {
                yield return update;
            }
        }
        finally
        {
            response?.Dispose();
        }
    }

    private string BuildChatCompletionUrl() => $"{_endpoint.TrimEnd('/')}/chat/completions";

    private Dictionary<string, object> BuildRequestPayload(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        bool stream)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = _modelId,
            ["messages"] = messages.Select(m => new
            {
                role = GetOllamaRole(m.Role),
                content = string.Join("\n", m.Contents.OfType<TextContent>().Select(c => c.Text))
            }).ToList(),
            ["stream"] = stream
        };

        // Add chat options if provided
        if (options is not null)
        {
            if (options.Temperature.HasValue)
            {
                payload["temperature"] = options.Temperature.Value;
            }

            if (options.TopP.HasValue)
            {
                payload["top_p"] = options.TopP.Value;
            }

            if (options.MaxOutputTokens.HasValue)
            {
                payload["num_predict"] = options.MaxOutputTokens.Value;
            }

            if (options.StopSequences is { Count: > 0 })
            {
                payload["stop"] = options.StopSequences;
            }

            if (options.Seed.HasValue)
            {
                payload["seed"] = options.Seed.Value;
            }
        }

        return payload;
    }

    private static string GetOllamaRole(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        if (role == ChatRole.Tool) return "tool";
        return "user";
    }

    private ChatResponse ParseChatResponse(string responseContent)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            // OpenAI-compatible format: message is in choices[0].message
            var contentText = string.Empty;
            if (root.TryGetProperty("choices", out var choicesArray) &&
                choicesArray.GetArrayLength() > 0)
            {
                var firstChoice = choicesArray[0];
                if (firstChoice.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentProp))
                {
                    contentText = contentProp.GetString() ?? string.Empty;
                }
            }

            var modelId = root.TryGetProperty("model", out var modelProp)
                ? modelProp.GetString() ?? _modelId
                : _modelId;

            // Parse usage if available - handle both formats
            var inputTokens = 0;
            var outputTokens = 0;

            if (root.TryGetProperty("usage", out var usageProp))
            {
                if (usageProp.TryGetProperty("prompt_tokens", out var promptTokensProp))
                {
                    inputTokens = promptTokensProp.GetInt32();
                }

                if (usageProp.TryGetProperty("completion_tokens", out var completionTokensProp))
                {
                    outputTokens = completionTokensProp.GetInt32();
                }
            }

            // Fallback to Ollama's native format
            if (inputTokens == 0 && root.TryGetProperty("prompt_eval_count", out var promptEvalTokens))
            {
                inputTokens = promptEvalTokens.GetInt32();
            }

            if (outputTokens == 0 && root.TryGetProperty("eval_count", out var evalCountTokens))
            {
                outputTokens = evalCountTokens.GetInt32();
            }

            var assistantMessage = new ChatMessage(ChatRole.Assistant, [new TextContent(contentText)]);

            return new ChatResponse(new[] { assistantMessage })
            {
                ModelId = modelId,
                FinishReason = ChatFinishReason.Stop,
                Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Ollama response: {ResponseContent}", responseContent);
            throw new InvalidOperationException("Failed to parse Ollama response", ex);
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> ParseStreamingResponseAsync(
        HttpResponseMessage response,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var fullContent = new StringBuilder();
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var (hasContent, contentText, inputTokens, outputTokens) = ParseStreamLine(line);

            if (hasContent && !string.IsNullOrEmpty(contentText))
            {
                fullContent.Append(contentText);

                yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(contentText)])
                {
                    ModelId = _modelId,
                    FinishReason = null
                };
            }

            // Track token usage from last chunk
            if (inputTokens > 0)
            {
                totalInputTokens = inputTokens;
            }

            if (outputTokens > 0)
            {
                totalOutputTokens = outputTokens;
            }
        }

        // Yield final update with finish reason
        yield return new ChatResponseUpdate(ChatRole.Assistant, [])
        {
            ModelId = _modelId,
            FinishReason = ChatFinishReason.Stop
        };
    }

    private (bool HasContent, string ContentText, int InputTokens, int OutputTokens) ParseStreamLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            var contentText = string.Empty;

            // Try OpenAI-compatible format first (choices[0].message)
            if (root.TryGetProperty("choices", out var choicesArray) &&
                choicesArray.GetArrayLength() > 0)
            {
                var firstChoice = choicesArray[0];
                if (firstChoice.TryGetProperty("message", out var choiceMessage) &&
                    choiceMessage.TryGetProperty("content", out var choiceContent))
                {
                    contentText = choiceContent.GetString() ?? string.Empty;
                }
            }

            // Fallback to direct message format
            if (string.IsNullOrEmpty(contentText) &&
                root.TryGetProperty("message", out var directMessage) &&
                directMessage.TryGetProperty("content", out var directContent))
            {
                contentText = directContent.GetString() ?? string.Empty;
            }

            // Parse usage - try OpenAI format first
            var inputTokens = 0;
            var outputTokens = 0;

            if (root.TryGetProperty("usage", out var usageProp))
            {
                if (usageProp.TryGetProperty("prompt_tokens", out var promptTokensProp))
                {
                    inputTokens = promptTokensProp.GetInt32();
                }

                if (usageProp.TryGetProperty("completion_tokens", out var completionTokensProp))
                {
                    outputTokens = completionTokensProp.GetInt32();
                }
            }

            // Fallback to Ollama's native format
            if (inputTokens == 0 && root.TryGetProperty("prompt_eval_count", out var promptTokens))
            {
                inputTokens = promptTokens.GetInt32();
            }

            if (outputTokens == 0 && root.TryGetProperty("eval_count", out var outputTokensProp))
            {
                outputTokens = outputTokensProp.GetInt32();
            }

            return (!string.IsNullOrEmpty(contentText), contentText, inputTokens, outputTokens);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse streaming response line: {Line}", line);
            return (false, string.Empty, 0, 0);
        }
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        // Ensure endpoint has the /v1 path for OpenAI-compatible API
        endpoint = endpoint.TrimEnd('/');

        if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            if (endpoint.EndsWith("/v1/", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint[..^1];
            }
            else
            {
                endpoint = endpoint + "/v1";
            }
        }

        return endpoint;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null)
    {
        // Ollama client doesn't provide additional services
        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // HttpClient is managed by the factory, no cleanup needed here
        GC.SuppressFinalize(this);
    }
}
