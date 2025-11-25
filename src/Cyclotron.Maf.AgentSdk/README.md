# Cyclotron.Maf.AgentSdk

[![NuGet Version](https://img.shields.io/nuget/v/Cyclotron.Maf.AgentSdk.svg)](https://www.nuget.org/packages/Cyclotron.Maf.AgentSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cyclotron.Maf.AgentSdk.svg)](https://www.nuget.org/packages/Cyclotron.Maf.AgentSdk/)
[![Build Status](https://github.com/cyclotron-azure/maf-agent-sdk/actions/workflows/build.yml/badge.svg)](https://github.com/cyclotron-azure/maf-agent-sdk/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A .NET SDK for building AI agent workflows using Microsoft Agent Framework (MAF) and Azure AI Foundry. Provides workflow orchestration, agent factories, vector store management, PDF processing, and OpenTelemetry integration.

## Features

- **Workflow Orchestration** - Build sequential executor pipelines using MAF's `Executor<TInput, TOutput>` pattern
- **Agent Factory** - Create and manage ephemeral Azure AI Foundry agents with keyed DI support
- **Vector Store Management** - Lifecycle management for Azure AI Foundry vector stores with automatic indexing wait
- **PDF Processing** - Convert PDF documents to markdown using PdfPig for better text extraction
- **Prompt Rendering** - Handlebars-based template rendering for dynamic agent prompts
- **OpenTelemetry** - Built-in tracing, metrics, and logging with OTLP exporter support

## Installation

```bash
dotnet add package Cyclotron.Maf.AgentSdk
```

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
agents:
  my_agent:
    type: "custom"
    enabled: true
    auto_delete: true
    auto_cleanup_resources: false
    instructions: |
      You are a document processing specialist.
      Analyze documents and extract relevant information.
    metadata:
      description: "Document processing agent"
      tools: ["file_search"]
    framework_config:
      provider: "azure_foundry"
      model: "gpt-4o"
    system_prompt_template: |
      You are a helpful assistant.
    user_prompt_template: |
      Process the document: {{fileName}}
```

### 3. Create Workflow Executors

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
            // Run agent
            var response = await _agentFactory.RunAgentWithPollingAsync(
                messages: [_agentFactory.CreateUserMessage(promptContext)],
                cancellationToken: cancellationToken);

            // Process response...
            return result;
        }
        finally
        {
            await _agentFactory.CleanupAsync(cancellationToken);
        }
    }
}
```

### 4. Build and Execute Workflow

```csharp
using Microsoft.Agents.AI.Workflows;

var workflow = new WorkflowBuilder(executor1)
    .AddEdge(executor1, executor2)
    .AddEdge(executor2, executor3)
    .WithOutputFrom(executor3)
    .Build();

var result = await workflow.ExecuteAsync<OutputType>(input, cancellationToken);
```

## Configuration

### Environment Variables

```bash
PROJECT_ENDPOINT=https://your-project.api.azureml.ms
PROJECT_DEPLOYMENT_NAME=gpt-4o
```

### Model Provider Options

```json
{
  "ModelProviders": {
    "DefaultProvider": "azure_foundry",
    "Providers": {
      "azure_foundry": {
        "Endpoint": "https://your-project.api.azureml.ms",
        "ApiKey": null,
        "DeploymentName": "gpt-4o"
      }
    }
  }
}
```

### Telemetry Options

```json
{
  "Telemetry": {
    "Enabled": true,
    "ServiceName": "MyAgentService",
    "OtlpEndpoint": "http://localhost:4318"
  }
}
```

### Vector Store Indexing Options

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

### PDF Conversion Options

```json
{
  "PdfConversion": {
    "Enabled": true,
    "SaveMarkdownForDebug": false,
    "OutputDirectory": "./output"
  }
}
```

## Namespaces

The SDK uses the following namespace structure:

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
| `IPdfToMarkdownConverter` | Converts PDF documents to markdown |
| `IPromptRenderingService` | Renders Handlebars templates |
| `IPersistentAgentsClientFactory` | Creates Azure AI Foundry clients |
| `IAzureFoundryCleanupService` | Cleans up Azure AI Foundry resources |

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
| Azure.AI.Agents.Persistent | 1.2.0-beta.7 |
| Azure.AI.OpenAI | 2.5.0-beta.1 |
| Azure.Identity | 1.17.0 |
| Microsoft.Agents.AI.Workflows | 1.0.0-preview.251114.1 |
| Microsoft.Agents.AI.AzureAI | 1.0.0-preview.251114.1 |
| Microsoft.Extensions.AI | 10.0.0 |
| OpenTelemetry | 1.9.0 |
| PdfPig | 0.1.12 |
| Handlebars.Net | 2.1.6 |
| Polly.Core | 8.5.0 |

## Requirements

- .NET 8.0 or later
- Azure AI Foundry project endpoint
- Azure credentials (DefaultAzureCredential or API key)

## License

MIT
