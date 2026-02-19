# Breaking Changes in v2.0.0

This document outlines all breaking changes introduced in version 2.0.0 of the Cyclotron.Maf.AgentSdk.

## Overview

Version 2.0.0 introduces significant architectural improvements based on patterns from the AgentFrameworkToolkit repository, including:
- Centralized middleware infrastructure
- Multi-provider support (Azure AI Foundry, Azure OpenAI, Ollama)
- Flattened configuration structure
- Improved naming consistency

## Migration Guide

### 1. Configuration Structure Changes

#### BREAKING: Flattened Agent Configuration

**Old Configuration (v1.x):**
```yaml
agents:
  my_agent:
    type: "custom"
    enabled: true
    framework_config:
      provider: "azure_foundry"
    system_prompt_template: |
      Instructions here
```

**New Configuration (v2.0.0):**
```yaml
agents:
  my_agent:
    type: "custom"
    enabled: true
    provider: "azure_foundry"  # Direct property, no nesting
    system_prompt_template: |
      Instructions here
```

**Migration Steps:**
1. Remove the `framework_config` wrapper
2. Move `provider` to be a direct property of the agent
3. Update all agent configurations in `agent.config.yaml`

---

### 2. Code Changes

#### BREAKING: AgentDefinitionOptions Property Changes

**Old Code (v1.x):**
```csharp
var providerName = agentDefinition.AIFrameworkOptions.Provider;
```

**New Code (v2.0.0):**
```csharp
var providerName = agentDefinition.Provider;
```

**Changes:**
- **Removed:** `AIFrameworkOptions` property
- **Added:** Direct `string Provider` property
- **Added:** `MiddlewareConfiguration? Middleware` property
- **Added:** `ExtensibilityOptions? Extensibility` property

**Migration Steps:**
1. Replace all `agentDefinition.AIFrameworkOptions.Provider` with `agentDefinition.Provider`
2. Replace all `agentDefinition.AIFrameworkOptions = new AIFrameworkOptions { Provider = "X" }` with `agentDefinition.Provider = "X"`

---

#### BREAKING: Interface Rename - IAIProjectClientFactory → IProviderClientFactory

**Old Code (v1.x):**
```csharp
using Cyclotron.Maf.AgentSdk.Services;

public class MyService
{
    private readonly IAIProjectClientFactory _clientFactory;

    public MyService(IAIProjectClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }
}
```

**New Code (v2.0.0):**
```csharp
using Cyclotron.Maf.AgentSdk.Services;

public class MyService
{
    private readonly IProviderClientFactory _clientFactory;

    public MyService(IProviderClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }
}
```

**Changes:**
- **Renamed Interface:** `IAIProjectClientFactory` → `IProviderClientFactory`
- **Renamed Implementation:** `AIProjectClientFactory` → `ProviderClientFactory`
- **Purpose:** Better reflects multi-provider support (Azure, Ollama, etc.)

**Migration Steps:**
1. Find and replace `IAIProjectClientFactory` with `IProviderClientFactory`
2. Find and replace `AIProjectClientFactory` with `ProviderClientFactory`
3. Update any test mocks or DI registrations

---

#### BREAKING: AgentFactory Constructor - Removed Legacy Constructor

**Old Code (v1.x):**
```csharp
using Cyclotron.Maf.AgentSdk.Services;

// Legacy constructor with explicit client factories
var factory = new AgentFactory(
    agentKey: "test",
    logger: logger,
    promptService: promptService,
    providerOptions: providerOptions,
    agentOptions: agentOptions,
    clientFactory: clientFactory,              // IProviderClientFactory
    httpClientFactory: httpClientFactory,      // IHttpClientFactory
    loggerFactory: loggerFactory,              // ILoggerFactory
    vectorStoreManager: vectorStoreManager,
    telemetryOptions: telemetryOptions);
```

**New Code (v2.0.0):**
```csharp
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;

// New constructor using IAgentProviderResolver
var factory = new AgentFactory(
    agentKey: "test",
    logger: logger,
    promptService: promptService,
    providerOptions: providerOptions,
    agentOptions: agentOptions,
    providerResolver: providerResolver,        // IAgentProviderResolver (NEW)
    vectorStoreManager: vectorStoreManager,
    telemetryOptions: telemetryOptions);
```

**Changes:**
- **Removed Parameters:** `IProviderClientFactory clientFactory`, `IHttpClientFactory httpClientFactory`, `ILoggerFactory loggerFactory`
- **Added Parameter:** `IAgentProviderResolver providerResolver`
- **Removed Method:** `BuildDefaultProviderResolver` (anti-pattern removed)

**Why This Change:**
The legacy constructor created a "mini-DI container" inside `BuildDefaultProviderResolver`, violating the Dependency Inversion Principle. The new design uses proper dependency injection through `IAgentProviderResolver`, which is registered via `AddAgentProviderResolver()` extension method.

**Migration Steps:**

1. **For Direct Instantiation (Not Recommended):**
   ```csharp
   // Create provider resolver manually
   var azureProvider = new AzureAgentProvider(clientFactory, loggerFactory);
   var ollamaProvider = new OllamaAgentProvider(httpClientFactory, loggerFactory);
   var providerResolver = new AgentProviderResolver(new[] { azureProvider, ollamaProvider });

   // Use new constructor
   var factory = new AgentFactory(
       agentKey: "test",
       logger: logger,
       promptService: promptService,
       providerOptions: providerOptions,
       agentOptions: agentOptions,
       providerResolver: providerResolver,
       vectorStoreManager: vectorStoreManager,
       telemetryOptions: telemetryOptions);
   ```

2. **For DI-Based Applications (Recommended):**
   ```csharp
   // In Startup.cs or Program.cs
   services.AddAgentSdkServices(configuration);  // Automatically registers IAgentProviderResolver

   // Or manually:
   services.AddAgentProviderResolver();  // Registers Azure and Ollama providers

   // Inject IAgentFactory with keyed services
   public class MyExecutor(
       [FromKeyedServices("classification")] IAgentFactory agentFactory)
   {
       // AgentFactory instances are created by DI with proper dependencies
   }
   ```

3. **For Workflow Applications:**
   ```csharp
   // Use AddDocumentWorkflowServices which includes provider resolver setup
   services.AddDocumentWorkflowServices(configuration);

   // Then use convenience method for keyed factories
   services.AddKeyedAgentFactories(["classification", "extraction"]);
   ```

**Test Migration Example:**

**Old Test (v1.x):**
```csharp
var mockClientFactory = new Mock<IProviderClientFactory>();
var mockHttpClientFactory = new Mock<IHttpClientFactory>();
var mockLoggerFactory = new Mock<ILoggerFactory>();

var factory = new AgentFactory(
    "test",
    mockLogger.Object,
    mockPromptService.Object,
    providerOptions,
    agentOptions,
    mockClientFactory.Object,
    mockHttpClientFactory.Object,
    mockLoggerFactory.Object,
    mockVectorStoreManager.Object,
    telemetryOptions);
```

**New Test (v2.0.0):**
```csharp
var mockProviderResolver = new Mock<IAgentProviderResolver>();
var mockProvider = new Mock<IAgentProvider>();
mockProvider.Setup(x => x.Capabilities).Returns(new AgentProviderCapabilities(
    SupportsVectorStore: true,
    SupportsAgentDeletion: true,
    SupportsSessionDeletion: false));
mockProviderResolver.Setup(x => x.Resolve(It.IsAny<ModelProviderDefinitionOptions>()))
    .Returns(mockProvider.Object);

var factory = new AgentFactory(
    "test",
    mockLogger.Object,
    mockPromptService.Object,
    providerOptions,
    agentOptions,
    mockProviderResolver.Object,
    mockVectorStoreManager.Object,
    telemetryOptions);
```

**Related New Types:**
- `IAgentProviderResolver` - Interface for resolving provider implementations
- `AgentProviderResolver` - Default provider resolution implementation
- `IAgentProvider` - Interface for provider-specific agent operations
- `AzureAgentProvider` - Azure AI Foundry provider implementation
- `OllamaAgentProvider` - Ollama provider implementation
- `AgentProviderCapabilities` - Provider capability descriptor

---

### 3. New Features (Breaking for Ollama Users)

#### Ollama Provider Configuration Support

**Configuration:**
```yaml
providers:
  ollama_local:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "llama3"
    model: "llama3:latest"  # Optional

agents:
  my_agent:
    provider: "ollama_local"
```

**Current Limitation (v2.0.0):**
Runtime execution throws `NotImplementedException`:
```
Local provider 'ollama' support is configured but not yet implemented in v2.0.0.
Ollama and other local providers will be fully supported in a future release.
```

**Non-Breaking:** Configuration is validated and ready for future releases.

See [OLLAMA-CONFIGURATION.md](./OLLAMA-CONFIGURATION.md) for details.

---

### 4. Middleware Infrastructure (New)

#### Centralized Middleware Configuration

New middleware system for agent customization:

```csharp
// In agent.config.yaml (optional)
agents:
  my_agent:
    middleware:
      raw_tool_call_details: null  # Handler for raw tool calls
      tool_calling_middleware: null  # Middleware for tool invocation
      opentelemetry:
        source: "MyApp"
      logging:
        logger_factory: null  # Uses default
```

**New Classes:**
- `AgentMiddlewareHelper` - Centralized middleware orchestration
- `MiddlewareConfiguration` - Configuration container
- `AgentOpenTelemetryOptions` - OpenTelemetry settings
- `AgentLoggingOptions` - Logging settings
- `ToolCallingMiddlewareDelegate` - Tool call interception
- `ExtensibilityOptions` - Advanced customization hooks

**Fixed Middleware Order:**
1. RawToolCallDetails handler
2. OpenTelemetry middleware
3. ToolCalling middleware
4. Logging middleware

**Non-Breaking:** Middleware is optional. Existing code works without changes.

---

## Test Updates Required

### Unit Test Changes

**Old Test Code (v1.x):**
```csharp
var agentDef = new AgentDefinitionOptions
{
    AIFrameworkOptions = new AIFrameworkOptions
    {
        Provider = "azure_foundry"
    }
};

agentDef.AIFrameworkOptions.Should().NotBeNull();
agentDef.AIFrameworkOptions.Provider.Should().Be("azure_foundry");
```

**New Test Code (v2.0.0):**
```csharp
var agentDef = new AgentDefinitionOptions
{
    Provider = "azure_foundry"
};

agentDef.Provider.Should().NotBeNullOrWhiteSpace();
agentDef.Provider.Should().Be("azure_foundry");
```

---

## Dependency Injection Changes

### Service Registration Updates

**Old Registration (v1.x):**
```csharp
services.AddScoped<IAIProjectClientFactory, AIProjectClientFactory>();
```

**New Registration (v2.0.0):**
```csharp
services.AddScoped<IProviderClientFactory, ProviderClientFactory>();
```

**Note:** `AddDocumentWorkflowServices()` automatically registers the correct services. Manual registration only needed for custom scenarios.

---

## Documentation Updates

### Updated Files

1. **`.github/copilot-instructions.md`**
   - Component table updated
   - YAML examples updated to flattened structure

2. **`src/Cyclotron.Maf.AgentSdk/README.md`**
   - Interface table updated
   - Examples updated

3. **Comments and XML Documentation**
   - All references to old names updated
   - Auto-generated XML documentation reflects new names

---

## Validation & Testing

### Build Verification

```bash
# Clean build
dotnet clean
dotnet build

# Expected: Build succeeded
```

### Test Verification

```bash
# Run all tests
dotnet test

# Expected: 608/618 tests passing
# Note: 10 expected failures for sealed framework types (Phase 1 limitation)
```

### Specific Test Suites

```bash
# Middleware tests
dotnet test --filter "FullyQualifiedName~Middleware"

# Ollama configuration tests
dotnet test --filter "FullyQualifiedName~Ollama"

# Provider factory tests
dotnet test --filter "FullyQualifiedName~ProviderClientFactoryTests"
```

---

## Rollback Instructions

If you need to rollback to v1.x:

1. **Revert Configuration:**
   ```yaml
   # Change back to nested structure
   agents:
     my_agent:
       framework_config:
         provider: "azure_foundry"
   ```

2. **Revert Code:**
   ```csharp
   // Change back to old interface/property names
   IAIProjectClientFactory clientFactory
   agentDefinition.AIFrameworkOptions.Provider
   ```

3. **Downgrade Package:**
   ```bash
   dotnet remove package Cyclotron.Maf.AgentSdk
   dotnet add package Cyclotron.Maf.AgentSdk --version 1.x.x
   ```

---

## Timeline & Support

- **v2.0.0 Release:** TBD
- **v1.x Support:** Security updates only after v2.0.0 release
- **Migration Window:** Recommended within 3 months of v2.0.0 release

---

## Additional Resources

- [Middleware Infrastructure Documentation](./TELEMETRY.md)
- [Ollama Configuration Guide](./OLLAMA-CONFIGURATION.md)
- [CI/CD Pipeline Details](./CICD.md)
- [Provider Configuration Examples](../samples/SpamDetection/agent.config.yaml)

---

## Questions & Support

For questions or issues during migration:
- GitHub Issues: [cyclotron-azure/maf-agent-sdk/issues](https://github.com/cyclotron-azure/maf-agent-sdk/issues)
- Discussions: [cyclotron-azure/maf-agent-sdk/discussions](https://github.com/cyclotron-azure/maf-agent-sdk/discussions)

---

## Summary of All Breaking Changes

| Change | Type | Impact | Migration Effort |
|--------|------|--------|------------------|
| Flattened agent configuration | Configuration | High | Low - Find/Replace |
| `AIFrameworkOptions` removed | Code | High | Low - Direct property access |
| `IAIProjectClientFactory` → `IProviderClientFactory` | Interface | Medium | Low - Rename |
| `AIProjectClientFactory` → `ProviderClientFactory` | Class | Medium | Low - Rename |
| `AgentFactory` constructor - removed legacy overload | Constructor | High | Medium - DI pattern change |
| `BuildDefaultProviderResolver` method removed | Code | Medium | Low - Use DI registration |
| Middleware infrastructure | New Feature | Low | None - Optional |
| Ollama configuration support | New Feature | Low | None - Additive |

**Total Breaking Changes:** 6 major, 2 additive

**Estimated Migration Time:** 2-4 hours for typical project (includes DI pattern updates)
