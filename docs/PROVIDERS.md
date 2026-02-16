# Multi-Provider Support

The MAF Agent SDK supports multiple AI model providers through a flexible provider architecture. This allows you to use Azure AI Foundry, Azure OpenAI, or local Ollama models with the same unified API.

## Supported Providers

| Provider Type | Description | Authentication |
|---------------|-------------|----------------|
| `azure_foundry` | Azure AI Foundry (AI Studio) | DefaultAzureCredential (Managed Identity) |
| `azure_openai` | Azure OpenAI Service | API Key |
| `ollama` | Local Ollama models | None (local endpoint) |

## Configuration

Configure providers in your `agent.config.yaml` file:

```yaml
providers:
  # Azure AI Foundry with managed identity
  azure_foundry_prod:
    type: "azure_foundry"
    endpoint: "${PROJECT_ENDPOINT}"
    deployment_name: "${PROJECT_DEPLOYMENT_NAME}"
    timeout_seconds: 300
    max_retries: 3

  # Azure OpenAI with API key
  azure_openai_dev:
    type: "azure_openai"
    endpoint: "${AZURE_OPENAI_ENDPOINT}"
    deployment_name: "gpt-4o"
    api_key: "${AZURE_OPENAI_API_KEY}"
    api_version: "2024-10-21"

  # Local Ollama for development
  ollama_local:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "llama2"
    timeout_seconds: 600
```

### Environment Variables

Set environment variables in your `.env` file:

```env
# Azure AI Foundry
PROJECT_ENDPOINT=https://your-project.api.azureml.ms
PROJECT_DEPLOYMENT_NAME=gpt-4o

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key-here
```

## Agent Configuration

Reference providers in your agent definitions:

```yaml
agents:
  classification_agent:
    type: "custom"
    enabled: true
    framework_config:
      provider: "azure_foundry_prod"  # References providers section
    system_prompt_template: |
      You are a classification agent...

  local_test_agent:
    type: "custom"
    enabled: true
    framework_config:
      provider: "ollama_local"  # Use local Ollama for development
    system_prompt_template: |
      You are a test agent...
```

## Provider-Specific Features

### Azure AI Foundry

- ✅ Managed identity authentication (no keys needed)
- ✅ Full Azure AI Agents API support
- ✅ Vector store integration
- ✅ Tool support (file_search, code_interpreter)
- ✅ Thread management

**Best for:** Production deployments with Azure infrastructure

### Azure OpenAI

- ✅ API key authentication
- ✅ Azure AI Agents API support via adapter
- ✅ Vector store integration
- ⚠️ Requires API key management

**Best for:** Existing Azure OpenAI customers

### Ollama

- ✅ Local model execution
- ✅ No cloud dependencies
- ✅ Fast development iterations
- ⚠️ Limited MAF integration (HttpClient wrapper)
- ⚠️ No built-in vector store support

**Best for:** Local development and testing

## Code Examples

### Using Provider Factory

```csharp
// Inject the provider factory
public class MyService
{
    private readonly IProviderClientFactory _providerFactory;

    public MyService(IProviderClientFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public void SwitchProviders()
    {
        // Get different providers dynamically
        var azureProvider = _providerFactory.GetProvider("azure_foundry_prod");
        var localProvider = _providerFactory.GetProvider("ollama_local");

        // Check if provider exists
        if (_providerFactory.HasProvider("custom_provider"))
        {
            // Use it
        }
    }
}
```

### Custom Provider Implementation

You can add new providers by implementing `IModelProvider`:

```csharp
public class CustomProvider : IModelProvider
{
    public string ProviderType => "custom";
    public string Endpoint { get; }
    public string DeploymentName { get; }

    public bool IsValid()
    {
        // Validate configuration
        return !string.IsNullOrEmpty(Endpoint);
    }

    public object CreateClient()
    {
        // Return your custom client
        return new MyCustomClient(Endpoint);
    }
}
```

Then register it in `ProviderClientFactory`:

```csharp
return config.Type.ToLowerInvariant() switch
{
    "azure_foundry" => new AzureFoundryProvider(config, logger),
    "azure_openai" => new AzureOpenAIProvider(config, logger),
    "ollama" => new OllamaProvider(config, logger),
    "custom" => new CustomProvider(config, logger),
    _ => throw new NotSupportedException($"Provider type '{config.Type}' is not supported.")
};
```

## Migration Guide

### From v0.1.0 to v0.2.0

The provider architecture was refactored in v0.2.0. Here's what changed:

#### Breaking Changes

1. **PersistentAgentsClientFactory Constructor**
   ```csharp
   // Old (v0.1.0)
   new PersistentAgentsClientFactory(logger, providerOptions)

   // New (v0.2.0)
   new PersistentAgentsClientFactory(logger, providerFactory)
   ```

2. **Dependency Injection**
   ```csharp
   // Old - No provider factory
   services.AddScoped<IPersistentAgentsClientFactory, PersistentAgentsClientFactory>();

   // New - Provider factory required
   services.AddSingleton<IProviderClientFactory, ProviderClientFactory>();
   services.AddScoped<IPersistentAgentsClientFactory, PersistentAgentsClientFactory>();
   ```

3. **Configuration stays the same** - Your `agent.config.yaml` files continue to work without changes.

## Best Practices

1. **Use managed identity in production** - Prefer `azure_foundry` provider for production deployments
2. **Keep API keys secure** - Use Azure Key Vault or environment variables for `azure_openai` API keys
3. **Test locally with Ollama** - Use `ollama` provider for fast development iterations
4. **Configure timeouts appropriately** - Local models may need longer timeout_seconds
5. **Monitor provider usage** - Log provider selection for debugging and cost tracking

## Troubleshooting

### Provider not found

```
InvalidOperationException: Provider 'my_provider' not found in configuration.
```

**Solution:** Check that the provider name in `framework_config.provider` matches a key in the `providers:` section.

### Ollama not compatible with Azure AI Agents API

```
InvalidOperationException: Provider 'ollama_local' (type: ollama) is not compatible with Azure AI Agents API.
```

**Solution:** Ollama provider returns `HttpClient` and doesn't support Azure AI Agents API directly. Use it only for custom integrations or testing.

### Authentication failed

```
Azure.RequestFailedException: Authentication failed
```

**Solution:** 
- For `azure_foundry`: Ensure your service has proper Azure RBAC roles
- For `azure_openai`: Verify API key is correct and not expired

## See Also

- [Telemetry Guide](TELEMETRY.md)
- [CI/CD Guide](CICD.md)
- [SDK Documentation](../src/Cyclotron.Maf.AgentSdk/README.md)
