# Cyclotron.Maf.AgentSdk

[![NuGet Version](https://img.shields.io/nuget/v/AgentSdk.svg)](https://www.nuget.org/packages/AgentSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AgentSdk.svg)](https://www.nuget.org/packages/AgentSdk/)
[![Build Status](https://github.com/cyclotron-azure/maf-agent-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/cyclotron-azure/maf-agent-sdk/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A .NET SDK for building AI agent workflows using **Microsoft Agent Framework (MAF)** and **Azure AI Foundry**. This SDK simplifies creating, orchestrating, and managing AI agents with built-in support for vector stores, document processing, and observability.

## 🚀 Quick Start

### Installation

```bash
dotnet add package Cyclotron.Maf.AgentSdk
```

### Minimal Setup

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Load configuration from agent.config.yaml and .env files
builder.UseAgentSdk();

// Register core services
builder.Services.AddAgentSdkServices();

// Optional: Add telemetry
builder.AddAgentSdkTelemetry();
```

### Create Your Agent Configuration

Create `agent.config.yaml` in your project root:

```yaml
providers:
  azure_foundry:
    type: "azure_foundry"
    endpoint: "${PROJECT_ENDPOINT}"
    deployment_name: "${PROJECT_DEPLOYMENT_NAME}"

agents:
  my_agent:
    type: "custom"
    enabled: true
    auto_delete: true
    auto_cleanup_resources: true
    metadata:
      description: "My AI agent"
      tools:
        - "file_search"        # Enable document search
        - "code_interpreter"   # Enable code execution (optional)
    framework_config:
      provider: "azure_foundry"
    system_prompt_template: |
      You are a helpful assistant.
    user_prompt_template: |
      Process: {{input}}
```

### Set Environment Variables

Create a `.env` file:

```env
PROJECT_ENDPOINT=https://your-project.api.azureml.ms
PROJECT_DEPLOYMENT_NAME=gpt-4o
```

## ✨ Features

| Feature | Description |
|---------|-------------|
| **Workflow Orchestration** | Build sequential executor pipelines using MAF's `Executor<TInput, TOutput>` pattern |
| **Agent Factory** | Create and manage ephemeral Azure AI Foundry agents with keyed DI support |
| **Vector Store Management** | Lifecycle management with automatic indexing wait and exponential backoff |
| **PDF Processing** | Convert PDF documents to markdown using PdfPig for better text extraction |
| **Prompt Rendering** | Handlebars-based template rendering for dynamic agent prompts |
| **OpenTelemetry** | Built-in tracing, metrics, and logging with OTLP exporter support |
| **Configurable Tools** | Enable `file_search` and/or `code_interpreter` via YAML configuration |

## 📁 Project Structure

```text
maf-agent-sdk/
├── src/
│   └── Cyclotron.Maf.AgentSdk/     # Main SDK library
├── samples/
│   └── SpamDetection/              # Complete working example
├── test/
│   └── Cyclotron.Maf.AgentSdk.UnitTests/
└── docs/
    ├── CICD.md                     # CI/CD and versioning
    └── TELEMETRY.md                # Observability setup
```

## 📖 Documentation

- **[SDK Documentation](src/Cyclotron.Maf.AgentSdk/README.md)** - Detailed API reference and configuration options
- **[Spam Detection Sample](samples/SpamDetection/README.md)** - Complete working example
- **[Telemetry Guide](docs/TELEMETRY.md)** - OpenTelemetry setup and configuration
- **[CI/CD Guide](docs/CICD.md)** - Build pipeline and versioning

## 📌 Versioning

This project uses **GitVersion** with semantic versioning following the GitFlow workflow. Versions are automatically bumped based on commit message keywords.

### Version Bumping via Commit Messages

Add `+semver:` to your commit messages to control version increments:

| Keyword | Version Bump | Example |
| --- | --- | --- |
| `+semver: breaking` or `+semver: major` | Major (X.0.0) | `BREAKING CHANGE: +semver: major Refactor API` |
| `+semver: feature` or `+semver: minor` | Minor (0.X.0) | `CHANGE: +semver: feature Add new endpoint` |
| `+semver: fix` or `+semver: patch` | Patch (0.0.X) | `FIX: +semver: patch Resolve dependency issue` |
| `+semver: skip` or `+semver: none` | No bump | `CHORE: +semver: skip Update docs` |

### Branch Versioning Strategy

- **main** - Stable releases (1.0.0, 1.0.1, 2.0.0, etc.)
- **dev** - Alpha pre-releases (1.1.0-alpha.1, 1.1.0-alpha.2, etc.)
- **release/*** - Release candidates (1.0.0-rc.1, 1.0.0-rc.2, etc.)
- **feature/*** - Feature branches with pre-release labels
- **hotfix/*** - Patch releases for critical fixes

For more details, see [GitVersion.yaml](GitVersion.yaml)

## 🔧 Prerequisites

- **.NET 8.0** or later
- **Azure AI Foundry** project with a deployed model (GPT-4o, GPT-4o-mini, etc.)
- **Azure CLI** authenticated (`az login`) or service principal credentials

## 📦 Samples

### [Spam Detection](samples/SpamDetection/)

A complete example demonstrating:

- Agent configuration via YAML
- Vector store creation with training data
- Message classification with confidence scores
- Full agent lifecycle management

```bash
cd samples/SpamDetection
dotnet run
```

## 🏗️ Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                    Your Application                          │
├─────────────────────────────────────────────────────────────┤
│                  Cyclotron.Maf.AgentSdk                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │AgentFactory │  │VectorStore  │  │PromptRendering      │  │
│  │             │  │Manager      │  │Service              │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
├─────────┼────────────────┼────────────────────┼─────────────┤
│         │    Microsoft Agent Framework (MAF)   │             │
│         │    ┌─────────────────────────────┐  │             │
│         │    │  Workflow Executors         │  │             │
│         │    │  AIAgent / AgentThread      │  │             │
│         └────┴─────────────────────────────┴──┘             │
├─────────────────────────────────────────────────────────────┤
│                   Azure AI Foundry                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Agents API  │  │Vector Stores│  │ Model Deployments   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## 🤝 Contributing

Contributions are welcome! Please see our contributing guidelines.

### Development Setup

```bash
# Clone the repository
git clone https://github.com/cyclotron-azure/maf-agent-sdk.git
cd maf-agent-sdk

# Build
dotnet build

# Run tests
dotnet test

# Run sample
cd samples/SpamDetection
cp .env.example .env
# Edit .env with your Azure AI Foundry credentials
dotnet run
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🏢 About Cyclotron

Cyclotron is an award-winning technology consultancy specializing in Microsoft solutions, AI innovation, and enterprise security. Founded in 2014 and headquartered in San Francisco, Cyclotron helps organizations accelerate digital transformation through services spanning Azure cloud, modern work, compliance, and custom AI platforms. As a multi-year Microsoft Partner of the Year and member of the Copilot Early Access Program, Cyclotron delivers integrated solutions that drive real business impact—empowering clients to work smarter, scale faster, and innovate securely.

Learn more at [cyclotron.com](https://cyclotron.com).
