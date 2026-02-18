# Cyclotron.Maf.AgentSdk

[![NuGet Version](https://img.shields.io/nuget/v/AgentSdk.svg)](https://www.nuget.org/packages/Cyclotron.Maf.AgentSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AgentSdk.svg)](https://www.nuget.org/packages/Cyclotron.Maf.AgentSdk/)
[![Build Status](https://github.com/cyclotron-azure/maf-agent-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/cyclotron-azure/maf-agent-sdk/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A .NET SDK for building AI agent workflows using Microsoft Agent Framework (MAF) and Azure AI Foundry. Provides workflow orchestration, agent factories, vector store management, and OpenTelemetry integration.

**For PDF processing features**, see [AgentSdk.Pdf](../Cyclotron.Maf.AgentSdk.Pdf/README.md).

## Features

- **Workflow Orchestration** - Build sequential executor pipelines using MAF's `Executor<TInput, TOutput>` pattern
- **Agent Factory** - Create and manage ephemeral Azure AI Foundry agents with keyed DI support
- **Vector Store Management** - Lifecycle management for Azure AI Foundry vector stores with automatic indexing wait
- **Prompt Rendering** - Handlebars-based template rendering for dynamic agent prompts
- **OpenTelemetry** - Built-in tracing, metrics, and logging with OTLP exporter support
- **Configurable Tools** - Enable `file_search` and/or `code_interpreter` via YAML configuration
- **Multi-Provider Support** - Azure AI Foundry and Ollama provider support

For PDF processing features (image extraction, content analysis, markdown conversion), see [AgentSdk.Pdf](../Cyclotron.Maf.AgentSdk.Pdf/README.md).

## Installation

```bash
dotnet add package AgentSdk

# For PDF processing features
dotnet add package AgentSdk.Pdf
```

## Session Management

Sessions maintain conversation context and state across multiple agent invocations. Each session is created per agent and managed throughout the workflow:

- **Session Creation**: Automatically created when `CreateAgentAsync()` is called
- **Session Reuse**: Persists across multiple `RunAgentWithPollingAsync()` calls
- **Session Cleanup**: Automatically deleted when `CleanupAsync()` is called (if `auto_delete` is true)

## Quick Start

### 1. Configure Services

```csharp
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Load agent configuration from agent.config.yaml and .env files
builder.UseAgentSdk();

// Register core services
builder.Services.AddAgentSdkServices();

// Add telemetry (optional)
builder.AddAgentSdkTelemetry();
```

### 2. Define Agent Configuration

Create `agent.config.yaml` in your project:

```yaml
# Model Provider Configuration
providers:
  azure_foundry:
    type: "azure_foundry"
    endpoint: "${PROJECT_ENDPOINT}"      # Environment variable substitution
    deployment_name: "${PROJECT_DEPLOYMENT_NAME}"
    api_version: "2024-12-01-preview"
    timeout_seconds: 300
    max_retries: 3

# Agent Configuration
agents:
  my_agent:
    type: "custom"
    enabled: true
    auto_delete: true              # Delete agent after workflow completes
    auto_cleanup_resources: true   # Delete vector store after workflow completes

    metadata:
      description: "Document processing agent"
      tools:                       # Configure which tools the agent can use
        - "file_search"            # Enable document search in vector stores
        - "code_interpreter"       # Enable Python code execution (optional)

    framework_config:
      provider: "azure_foundry"    # Reference to providers section

    system_prompt_template: |
      You are a helpful assistant specialized in document analysis.

    user_prompt_template: |
      Process the document: {{fileName}}
      Context: {{context}}
```

### 3. Set Environment Variables

Create a `.env` file in your project root:

```env
PROJECT_ENDPOINT=https://your-project.api.azureml.ms
PROJECT_DEPLOYMENT_NAME=gpt-4o
```

### 4. Create Workflow Executors

```csharp
using Cyclotron.Maf.AgentSdk.Agents;
using Microsoft.Agents.AI.Workflows.Executors;
using Microsoft.Extensions.DependencyInjection;

public class MyProcessingExecutor : Executor<InputType, OutputType>
{
    private readonly IAgentFactory _agentFactory;

    public MyProcessingExecutor(
        [FromKeyedServices("my_agent")] IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public override async ValueTask<OutputType> HandleAsync(
        InputType input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Create agent with vector store
        await _agentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);

        try
        {
            // Run agent with automatic retry and polling
            var response = await _agentFactory.RunAgentWithPollingAsync(
                messages: [_agentFactory.CreateUserMessage(promptContext)],
                cancellationToken: cancellationToken);

            // Process response...
            return result;
        }
        finally
        {
            // Cleanup respects auto_delete and auto_cleanup_resources settings
            // This deletes the agent, session, and optionally the vector store
            await _agentFactory.CleanupAsync(cancellationToken);
        }
    }
}
```

### 5. Build and Execute Workflow

```csharp
using Microsoft.Agents.AI.Workflows;

var workflow = new WorkflowBuilder(executor1)
    .AddEdge(executor1, executor2)
    .AddEdge(executor2, executor3)
    .WithOutputFrom(executor3)
    .Build();

var result = await workflow.ExecuteAsync<OutputType>(input, cancellationToken);
```

## Configuration Reference

### Agent Configuration (`agent.config.yaml`)

#### Provider Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `type` | string | Provider type (`azure_foundry`) | Required |
| `endpoint` | string | Azure AI Foundry endpoint URL | Required |
| `deployment_name` | string | Model deployment name | Required |
| `model` | string | Alternative to deployment_name | - |
| `api_version` | string | API version | `2024-12-01-preview` |
| `timeout_seconds` | int | Request timeout | `300` |
| `max_retries` | int | Maximum retry attempts | `3` |

#### Agent Definition Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `type` | string | Agent type identifier | Required |
| `enabled` | bool | Whether agent is active | `true` |
| `auto_delete` | bool | Delete agent and session after use | `true` |
| `auto_cleanup_resources` | bool | Delete vector store after use | `false` |
| `system_prompt_template` | string | Handlebars template for system prompt | - |
| `user_prompt_template` | string | Handlebars template for user prompt | - |

#### Agent Metadata Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `description` | string | Human-readable description | `""` |
| `tools` | string[] | Tools to enable: `file_search`, `code_interpreter` | `[]` |

> **Note:** If no tools are configured, `file_search` is enabled by default when creating an agent with a vector store.

#### Framework Config Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `provider` | string | Reference to a provider in the `providers` section | Required |

### Environment Variables

Values in `agent.config.yaml` support `${VAR_NAME}` syntax for environment variable substitution:

```yaml
endpoint: "${PROJECT_ENDPOINT}"        # Reads PROJECT_ENDPOINT from environment
deployment_name: "${PROJECT_DEPLOYMENT_NAME}"
```

### Application Settings (`appsettings.json`)

#### Telemetry Options

```json
{
  "Telemetry": {
    "Enabled": true,
    "SourceName": "MyAgentService",
    "EnableSensitiveData": false,
    "OtlpEndpoint": "http://localhost:4318"
  }
}
```

#### Vector Store Indexing Options

```json
{
  "VectorStoreIndexing": {
    "MaxWaitAttempts": 60,
    "InitialWaitDelayMs": 2000,
    "UseExponentialBackoff": true,
    "MaxWaitDelayMs": 30000
  }
}
```

For PDF-related configuration options, see [AgentSdk.Pdf README](../Cyclotron.Maf.AgentSdk.Pdf/README.md).

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `Cyclotron.Maf.AgentSdk` | Root namespace |
| `Cyclotron.Maf.AgentSdk.Agents` | Agent factory and related types |
| `Cyclotron.Maf.AgentSdk.Models` | Data models and DTOs |
| `Cyclotron.Maf.AgentSdk.Models.Workflow` | Workflow-specific models |
| `Cyclotron.Maf.AgentSdk.Options` | Configuration options classes |
| `Cyclotron.Maf.AgentSdk.Services` | Service interfaces |
| `Cyclotron.Maf.AgentSdk.Services.Impl` | Service implementations |
| `Cyclotron.Maf.AgentSdk.Workflows` | Workflow executors |

## Key Interfaces

| Interface | Description |
|-----------|-------------|
| `IAgentFactory` | Creates and manages Azure AI Foundry agents |
| `IVectorStoreManager` | Manages vector store lifecycle |
| `IPromptRenderingService` | Renders Handlebars templates |
| `IProviderClientFactory` | Creates AI provider clients (Azure, Ollama) |
| `IAzureFoundryCleanupService` | Cleans up Azure AI Foundry resources |

For PDF processing interfaces, see [AgentSdk.Pdf README](../Cyclotron.Maf.AgentSdk.Pdf/README.md).

## Microsoft Learn Documentation

For more information on Microsoft Agent Framework and session management, refer to these official resources:

- **[Microsoft Agent Framework Overview](https://learn.microsoft.com/agent-framework/)** - Core framework concepts and architecture
- **[Agent Framework Get Started](https://learn.microsoft.com/agent-framework/get-started/)** - Quick start guide
- **[Agent Framework: Sessions & Conversations](https://learn.microsoft.com/agent-framework/agents/conversations/session)** - Managing agent sessions for maintaining conversation context
- **[Agent Framework: Memory Management](https://learn.microsoft.com/agent-framework/get-started/memory/)** - How agents remember conversation history
- **[Azure AI Foundry Agents](https://learn.microsoft.com/azure/ai-foundry/agents/)** - Creating and managing agents in Azure AI Foundry
- **[Azure AI Foundry: Vector Stores](https://learn.microsoft.com/azure/ai-foundry/concepts/vector-stores)** - Vector store concepts and management
- **[OpenTelemetry Integration](https://learn.microsoft.com/dotnet/core/runtime-config/other)** - Observability and telemetry for .NET applications

## Workflow State Management

Use `WorkflowStateConstants` for common state keys:

```csharp
using Cyclotron.Maf.AgentSdk.Models.Workflow;

// Define your domain-specific scope constants
public static class MyWorkflowStateConstants
{
    public static readonly string MyDataScope = nameof(MyDataScope);
}

// Store data in workflow state
await context.QueueStateUpdateAsync(
    MyWorkflowStateConstants.MyDataScope,
    data,
    scopeName: WorkflowStateConstants.GetScopeName(workflowId, MyWorkflowStateConstants.MyDataScope),
    cancellationToken);

// Read data from workflow state
var data = await context.ReadStateAsync<MyType>(
    MyWorkflowStateConstants.MyDataScope,
    scopeName: WorkflowStateConstants.GetScopeName(workflowId, MyWorkflowStateConstants.MyDataScope),
    cancellationToken);
```

## Dependencies

| Package | Version |
|---------|---------|
| Azure.AI.Projects | 1.2.0-beta.1 |
| Azure.AI.OpenAI | 2.5.0-beta.1 |
| Azure.Identity | 1.17.0 |
| Microsoft.Agents.AI.Workflows | 1.0.0-preview.251114.1 |
| Microsoft.Agents.AI.AzureAI | 1.0.0-preview.251114.1 |
| Microsoft.Extensions.AI | 10.0.0 |
| OllamaSharp | 5.0.1 |
| OpenTelemetry | 1.9.0 |
| Handlebars.Net | 2.1.6 |
| Polly.Core | 8.5.0 |

For PDF processing dependencies (PdfPig, System.Drawing.Common), see [AgentSdk.Pdf](../Cyclotron.Maf.AgentSdk.Pdf/README.md).

For vector store dependencies (OllamaSharp), see [AgentSdk.Vectors](../Cyclotron.Maf.AgentSdk.Vectors/README.md).

## Requirements

- .NET 8.0 or later
- **For Azure providers**: Azure AI Foundry project endpoint
- **For Ollama providers**: Ollama installed and running locally or remotely
- Azure credentials (DefaultAzureCredential or API key)

## Samples

See the [SpamDetection sample](../../samples/SpamDetection/README.md) for a complete working example.

## License

MIT
