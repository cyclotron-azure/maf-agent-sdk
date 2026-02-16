# Ollama Provider Configuration

## Overview

As of **v2.0.0**, the SDK supports **Ollama provider configuration** for local AI model execution. The configuration is validated and ready, but runtime support is planned for a future release.

## Status

- ✅ **Configuration Support**: Ollama provider type is recognized and validated
- ✅ **Validation Logic**: Proper validation for local providers (no API key required)
- ⏳ **Runtime Support**: Coming in a future release (agent creation, execution, etc.)

## Configuration Example

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

## Validation Rules

### Required Fields
- `type`: Must be `"ollama"`
- `endpoint`: HTTP endpoint where Ollama is running (e.g., `http://localhost:11434`)
- `deployment_name`: Name of the model to use (e.g., `llama3`, `codellama`)

### Optional Fields
- `model`: Specific model tag (defaults to `deployment_name`)
- `timeout_seconds`: Request timeout (default: 300)
- `max_retries`: Maximum retry attempts (default: 3)

### Not Required
- `api_key`: Local providers don't require authentication
- `api_version`: Not applicable for Ollama

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

## Runtime Behavior (v2.0.0)

Currently, attempting to use an Ollama provider at runtime will throw:

```csharp
NotImplementedException: Local provider 'ollama' support is configured but not yet implemented in v2.0.0.
Ollama and other local providers will be fully supported in a future release.
The provider configuration is valid and ready for when implementation is complete.
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

## Roadmap

Future releases will add:

1. **Agent Creation**: Support for creating agents with Ollama models
2. **OpenAI Compatibility**: Leverage Ollama's OpenAI-compatible API
3. **Streaming Support**: Real-time response streaming
4. **Tool Integration**: Function calling support (if available in Ollama)
5. **Model Management**: List, pull, and manage models programmatically

## Migration Guide

When full Ollama support is released, no configuration changes will be required. Simply upgrade to the new version and your Ollama agents will work automatically.

## Related Files

- `src/Cyclotron.Maf.AgentSdk/Options/ModelProviderDefinitionOptions.cs` - Provider validation logic
- `src/Cyclotron.Maf.AgentSdk/Services/Impl/ProviderClientFactory.cs` - Client factory with Ollama detection
- `test/Cyclotron.Maf.AgentSdk.UnitTests/Services/ProviderClientFactoryTests.cs` - Ollama configuration tests

## See Also

- [Azure AI Foundry Configuration](../README.md#configuration)
- [Provider Configuration Reference](../samples/SpamDetection/agent.config.yaml)
- [Ollama Documentation](https://ollama.com/docs)
