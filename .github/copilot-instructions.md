# Copilot Instructions for Cyclotron.Maf.AgentSdk

## Project Overview

This is a .NET 8.0 SDK for building AI agent workflows using Microsoft Agent Framework (MAF) and Azure AI Foundry. The SDK provides workflow orchestration, agent factories, vector store management, PDF processing, and OpenTelemetry integration.

## Architecture

### Core Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IAgentFactory` | `src/.../Agents/` | Creates ephemeral Azure AI Foundry agents with keyed DI |
| `IVectorStoreManager` | `src/.../Services/` | Manages vector store lifecycle with indexing wait |
| `IPromptRenderingService` | `src/.../Services/` | Handlebars template rendering for prompts |
| `IProviderClientFactory` | `src/.../Services/` | Creates AI provider clients (Azure, Ollama) per provider |

### Data Flow

1. Configuration loaded from `agent.config.yaml` + `.env` files via `UseAgentSdk()`
2. Agents registered as **keyed services** (e.g., `[FromKeyedServices("my_agent")]`)
3. `AgentFactory` creates ephemeral agents with vector stores for document processing
4. Agents auto-cleanup based on `auto_delete` and `auto_cleanup_resources` flags

### Namespace Structure

```
Cyclotron.Maf.AgentSdk
├── Agents/           # IAgentFactory, AgentFactory
├── DependencyInjection/  # Extension methods (in Microsoft.Extensions.DependencyInjection namespace)
├── Models/           # DTOs and workflow models
├── Options/          # Configuration classes (*Options.cs)
├── Services/         # Service interfaces
│   └── Impl/        # Service implementations
└── Workflows/        # Workflow executors
```

## Development Patterns

### Configuration via YAML

Agents are configured in `agent.config.yaml`:
```yaml
agents:
  my_agent:
    type: "custom"
    enabled: true
    auto_delete: true
    auto_cleanup_resources: false
    provider: "azure_foundry"  # References providers: section (flattened in 1.0.0)
    system_prompt_template: |
      Your instructions here with {{variables}}
    user_prompt_template: |
      Process: {{input}}

providers:
  azure_foundry:
    type: "azure_foundry"
    endpoint: "${PROJECT_ENDPOINT}"  # Environment variable substitution
    deployment_name: "${PROJECT_DEPLOYMENT_NAME}"
```

### Keyed Dependency Injection

Register agents dynamically and inject via keyed services:
```csharp
// In DI setup
services.AddKeyedSingleton<IAgentFactory>("classification", (sp, key) =>
    new AgentFactory(key as string, ...));

// In executors
public class MyExecutor([FromKeyedServices("classification")] IAgentFactory agentFactory)
```

### Agent Lifecycle Pattern

```csharp
await _agentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
try
{
    var response = await _agentFactory.RunAgentWithPollingAsync(
        messages: [_agentFactory.CreateUserMessage(context)],
        cancellationToken: cancellationToken);
    // Process response
}
finally
{
    await _agentFactory.CleanupAsync(cancellationToken);  // Respects AutoDelete/AutoCleanupResources
}
```

## Build & Test Commands

```bash
# Build
dotnet build

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Pack NuGet (version from GitVersion)
dotnet pack -c Release
```

## Testing Conventions

- **Framework**: xUnit with Moq and AwesomeAssertions
- **Naming**: `MethodName_Condition_ExpectedResult()` pattern
- **Location**: Mirror source structure in `test/Cyclotron.Maf.AgentSdk.UnitTests/`

Example:
```csharp
[Fact]
public void RenderSystemPrompt_WithValidAgentKey_ReturnsRenderedTemplate()
```

## Version Control

- **Branching**: GitFlow (`main` → stable, `dev` → alpha, `feature/*` → development)
- **Versioning**: GitVersion automatic (use `+semver: major|minor|patch` in commits)
- **Commit format**: `CHORE:|FIX:|CHANGE:|BREAKING CHANGE:|TESTS:|SECURITY:` prefix

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.AI.Workflows` | MAF workflow orchestration |
| `Azure.AI.Agents.Persistent` | Azure AI Foundry agent APIs |
| `OllamaSharp` | Ollama provider integration for agents and embeddings |
| `Handlebars.Net` | Template rendering |
| `Polly.Core` | Retry policies with exponential backoff |
| `OpenTelemetry` | Distributed tracing and metrics |

## Important Patterns

### Dependency Injection Lifetime Strategy

**Provider Scope Safety**:
- `IAgentProvider` implementations (Azure, Ollama) are registered as **Scoped**
- `IAgentProviderResolver` is registered as **Scoped**
- `IProviderClientFactory` (their dependency) is registered as **Scoped**

**Why Scoped?**
Providers store and use `IProviderClientFactory` throughout their lifetime (e.g., in `CreateAgentAsync()`, `DeleteAgentAsync()`).
Making them scoped ensures they live in the same scope as their factory dependency, preventing scope lifetime violations.

**How It Works**:
```
AgentFactory (Scoped)
  → IAgentProviderResolver.Resolve() (Scoped)
    → Creates nested scope to instantiate scoped providers
    → Provider maintains reference to its scoped factory
    → Provider is immediately used and remains valid
    → Scope disposed after provider creation, but factory reference lives on
```

**Key Point**: The nested scope in `AgentProviderResolver.Resolve()` is intentional. It's required to resolve scoped services
from the root provider, but safe because the returned provider instance keeps its scoped factory alive for its entire usage lifetime.

**For Consumers**: No action needed—`AgentFactory` is already scoped via keyed DI (`AddKeyedScoped<IAgentFactory>`),
so scope hierarchy is automatically correct.

### Environment Variable Substitution

Values in `agent.config.yaml` support `${VAR_NAME}` syntax for environment variables, resolved by `IConfigurationValueSubstitution`.

### Exponential Backoff for Indexing

`AzureVectorStoreManager.WaitForFileProcessingAsync` uses configurable polling with exponential backoff controlled by `VectorStoreIndexingOptions`.

### OpenTelemetry Integration

Enable via `builder.AddAgentSdkTelemetry()` - supports OTLP export and Azure Application Insights. Configure sensitive data logging with `Telemetry:EnableSensitiveData`.

## Files to Review First

- `src/.../DependencyInjection/AgentSdkServiceCollectionExtensions.cs` - DI registration entry point
- `src/.../Agents/AgentFactory.cs` - Core agent lifecycle implementation
- `src/.../Services/Impl/PromptRenderingService.cs` - Template handling
- `docs/CICD.md` - Pipeline and versioning details
- `docs/TELEMETRY.md` - Observability setup
