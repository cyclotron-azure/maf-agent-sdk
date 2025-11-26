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
| `IPersistentAgentsClientFactory` | `src/.../Services/` | Creates Azure AI Foundry clients per provider |

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
    framework_config:
      provider: "azure_foundry"  # References providers: section
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
| `Handlebars.Net` | Template rendering |
| `Polly.Core` | Retry policies with exponential backoff |
| `OpenTelemetry` | Distributed tracing and metrics |

## Important Patterns

### Environment Variable Substitution

Values in `agent.config.yaml` support `${VAR_NAME}` syntax for environment variables, resolved by `IConfigurationValueSubstitution`.

### Exponential Backoff for Indexing

`VectorStoreManager.WaitForFileProcessingAsync` uses configurable polling with exponential backoff controlled by `VectorStoreIndexingOptions`.

### OpenTelemetry Integration

Enable via `builder.AddAgentSdkTelemetry()` - supports OTLP export and Azure Application Insights. Configure sensitive data logging with `Telemetry:EnableSensitiveData`.

## Files to Review First

- `src/.../DependencyInjection/AgentSdkServiceCollectionExtensions.cs` - DI registration entry point
- `src/.../Agents/AgentFactory.cs` - Core agent lifecycle implementation
- `src/.../Services/Impl/PromptRenderingService.cs` - Template handling
- `docs/CICD.md` - Pipeline and versioning details
- `docs/TELEMETRY.md` - Observability setup
