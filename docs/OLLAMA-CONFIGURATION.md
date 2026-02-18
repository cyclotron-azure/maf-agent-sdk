# Ollama Provider Configuration

## Overview

As of **v2.0.0**, the SDK provides **full Ollama provider support** for local AI model execution using the [OllamaSharp](https://github.com/awaescher/OllamaSharp) SDK. This includes agent creation, vector store embeddings, reasoning mode, and multimodal capabilities.

## Status

- ✅ **Configuration Support**: Ollama provider type is recognized and validated
- ✅ **Validation Logic**: Proper validation for local providers (no API key required)
- ✅ **Runtime Support**: Full agent creation and execution via OllamaSharp
- ✅ **Vector Store Integration**: Native embedding generation for document processing
- ✅ **Reasoning Mode**: Support for advanced reasoning models (Qwen3, DeepSeek-R1)
- ✅ **Multimodal Support**: Image inputs via compatible models (LLaVA, Gemma3)

## Configuration Example

### Basic Configuration

Add Ollama providers to your `agent.config.yaml`:

```yaml
providers:
  ollama_local:
    type: "ollama"
    endpoint: "http://localhost:11434"  # Default Ollama endpoint
    deployment_name: "llama3"           # Model name in Ollama
    model: "llama3:latest"              # Optional: specific model tag
    timeout_seconds: 300
    max_retries: 3

  ollama_codellama:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "codellama"
    model: "codellama:13b"

agents:
  my_agent:
    type: "custom"
    enabled: true
    provider: "ollama_local"  # Reference the Ollama provider
    auto_delete: true
    system_prompt_template: |
      You are a helpful AI assistant.
    user_prompt_template: |
      {{input}}
```

### Reasoning Mode Configuration

Enable advanced reasoning capabilities with models like Qwen3 or DeepSeek-R1:

```yaml
providers:
  ollama_reasoning:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "llama3"                # Base model
    enable_reasoning_mode: true               # Enable reasoning mode
    reasoning_model: "qwen3:8b"               # Reasoning-capable model
    # Or use: reasoning_model: "deepseek-r1"
    timeout_seconds: 600                      # Longer timeout for reasoning

agents:
  reasoning_agent:
    type: "custom"
    enabled: true
    provider: "ollama_reasoning"
    system_prompt_template: |
      You are a logical reasoning assistant. Break down complex problems step-by-step.
```

**How Reasoning Mode Works:**
- When `enable_reasoning_mode: true`, the SDK automatically switches to the `reasoning_model`
- The reasoning model is used instead of `deployment_name` for agent creation
- Ideal for complex problem-solving, mathematics, code analysis, and logical deduction

### Multimodal Configuration

Configure Ollama for vision/multimodal capabilities:

```yaml
providers:
  ollama_vision:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "llava"         # Vision-capable model
    # Or use: "gemma3:4b"

agents:
  vision_agent:
    type: "custom"
    enabled: true
    provider: "ollama_vision"
    system_prompt_template: |
      You are a vision AI that can analyze images and answer questions about them.
```

**Supported Multimodal Models:**
- `llava` - Visual understanding and image Q&A
- `gemma3:4b` - Lightweight multimodal model
- Check [Ollama library](https://ollama.com/library) for more

## Validation Rules

### Required Fields
- `type`: Must be `"ollama"`
- `endpoint`: HTTP endpoint where Ollama is running (e.g., `http://localhost:11434`)
- `deployment_name`: Name of the model to use (e.g., `llama3`, `codellama`)

### Optional Fields
- `model`: Specific model tag (defaults to `deployment_name`)
- `timeout_seconds`: Request timeout (default: 300)
- `max_retries`: Maximum retry attempts (default: 3)
- `enable_reasoning_mode`: Enable reasoning mode (default: `false`)
- `reasoning_model`: Reasoning-capable model name (used when `enable_reasoning_mode: true`)

### Not Required
- `api_key`: Local providers don't require authentication
- `api_version`: Not applicable for Ollama

## Vector Store Support

Ollama providers can be used for vector store embeddings. The SDK uses OllamaSharp for generating embeddings:

```yaml
agents:
  document_processor:
    type: "custom"
    enabled: true
    provider: "ollama_local"
    vector_store:
      provider: "ollama"         # Use Ollama for embeddings
      deployment_name: "nomic-embed-text"  # Specialized embedding model
    system_prompt_template: |
      You process documents and answer questions based on their content.
```

**Recommended Embedding Models:**
- `nomic-embed-text` - High-quality text embeddings (default)
- `all-minilm` - Fast, lightweight embeddings
- `mxbai-embed-large` - Large, high-accuracy embeddings

## Methods

### `ModelProviderDefinitionOptions.IsLocalProvider()`

Determines if a provider is local (Ollama, etc.):

```csharp
var provider = new ModelProviderDefinitionOptions
{
    Type = "ollama",
    Endpoint = "http://localhost:11434",
    DeploymentName = "llama3"
};

bool isLocal = provider.IsLocalProvider();  // Returns true
```

### `ModelProviderDefinitionOptions.IsValid()`

Validates Ollama configuration:

```csharp
var provider = new ModelProviderDefinitionOptions
{
    Type = "ollama",
    Endpoint = "http://localhost:11434",
    DeploymentName = "llama3"
};

bool isValid = provider.IsValid();  // Returns true (no API key needed)
```

## Runtime Behavior

### Agent Creation

The SDK uses [OllamaSharp](https://github.com/awaescher/OllamaSharp) to create agents:

```csharp
// In AgentFactory.cs
var ollamaClient = new OllamaApiClient(new Uri(endpoint), modelName);
AIAgent agent = ollamaClient.AsAIAgent(
    instructions: instructions,
    name: agentKey,
    description: $"Ollama agent using model {modelName}");
```

### Reasoning Mode

When `enable_reasoning_mode` is enabled:

```csharp
if (provider.EnableReasoningMode)
{
    var reasoningModel = provider.GetReasoningModel();
    _logger.LogInformation(
        "Reasoning mode enabled for agent '{AgentKey}' - using model '{ReasoningModel}'",
        agentKey, reasoningModel);
    // SDK switches to reasoning_model automatically
}
```

### Vector Store Embeddings

OllamaVectorStoreManager uses OllamaSharp for embedding generation:

```csharp
var ollamaClient = new OllamaApiClient(new Uri(endpoint), embeddingModel);
var embedResponse = await ollamaClient.EmbedAsync(text, cancellationToken);
var embedding = embedResponse.Embeddings[0];  // float[]
```

## Installation

### Running Ollama Locally

1. **Install Ollama**:
   ```bash
   curl -fsSL https://ollama.com/install.sh | sh
   ```

2. **Pull a model**:
   ```bash
   ollama pull llama3
   ```

3. **Verify it's running**:
   ```bash
   ollama list
   ```

4. **Default endpoint**: `http://localhost:11434`

### Using Remote Ollama

Configure a remote Ollama instance:

```yaml
providers:
  ollama_remote:
    type: "ollama"
    endpoint: "http://my-server:11434"
    deployment_name: "llama3"
```

## Features Implemented

✅ **Agent Creation**: Full support for creating agents with Ollama models via OllamaSharp
✅ **OpenAI Compatibility**: Leverages Ollama's OpenAI-compatible API via OllamaSharp
✅ **Reasoning Mode**: Advanced reasoning with Qwen3, DeepSeek-R1
✅ **Multimodal Support**: Vision capabilities with LLaVA, Gemma3
✅ **Vector Store Integration**: Native embedding generation for document processing
✅ **Streaming Support**: Real-time response streaming via Microsoft.Extensions.AI

## Future Enhancements

- **Tool Integration**: Function calling support (depends on Ollama API evolution)
- **Model Management**: List, pull, and manage models programmatically via SDK
- **Advanced Telemetry**: Ollama-specific metrics and traces

## Architecture

### Dependencies

- **OllamaSharp v5.0.1**: Official .NET SDK for Ollama API
- **Microsoft.Extensions.AI**: Unified AI abstraction layer
- **Azure.AI.Projects**: Microsoft Agent Framework agent management

### Key Classes

- `AgentFactory.CreateOllamaAgentAsync()` - Creates Ollama agents using OllamaSharp
- `OllamaVectorStoreManager` - Manages vector stores with Ollama embeddings
- `ModelProviderDefinitionOptions` - Configuration model with reasoning mode support

## Related Files

- `src/Cyclotron.Maf.AgentSdk/Agents/AgentFactory.cs` - Ollama agent creation with reasoning mode
- `src/Cyclotron.Maf.AgentSdk.Vectors/Services/Impl/OllamaVectorStoreManager.cs` - Vector store with OllamaSharp
- `src/Cyclotron.Maf.AgentSdk.Common/Options/ModelProviderDefinitionOptions.cs` - Provider validation and reasoning config
- `test/Cyclotron.Maf.AgentSdk.UnitTests/Agents/AgentFactoryTests.cs` - Ollama agent tests
- `Directory.Packages.props` - OllamaSharp package reference

## See Also

- [Azure AI Foundry Configuration](../README.md#configuration)
- [Provider Configuration Reference](../samples/SpamDetection/agent.config.yaml)
- [Ollama Documentation](https://ollama.com/docs)
