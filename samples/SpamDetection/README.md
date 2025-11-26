# Spam Detection Sample

This sample demonstrates how to use the **Cyclotron.Maf.AgentSdk** to build an AI-powered spam detection system using Azure AI Foundry and the Microsoft Agent Framework (MAF).

## Overview

The sample creates an AI agent that:

- Analyzes messages to classify them as **SPAM** or **NOT_SPAM**
- Uses a vector store with training examples for context
- Provides confidence levels and reasoning for each classification
- Demonstrates the full agent lifecycle (create, run, cleanup)

## Prerequisites

1. **.NET 8.0 SDK** or later
2. **Azure AI Foundry** project with a deployed model (e.g., GPT-4o, GPT-4o-mini)
3. **Azure CLI** authenticated (`az login`)

## Quick Start

### 1. Configure Environment Variables

Copy the example environment file and fill in your Azure AI Foundry details:

```bash
cp .env.example .env
```

Edit `.env` with your values:

```env
PROJECT_ENDPOINT=https://your-project.cognitiveservices.azure.com/
PROJECT_DEPLOYMENT_NAME=gpt-4o-mini
```

### 2. Build and Run

```bash
dotnet build
dotnet run
```

## How It Works

### Agent Configuration

The agent is configured in `agent.config.yaml`:

```yaml
providers:
  azure_foundry:
    type: "azure_foundry"
    endpoint: "{PROJECT_ENDPOINT}"        # Environment variable substitution
    deployment_name: "{PROJECT_DEPLOYMENT_NAME}"
    api_version: "2024-12-01-preview"

agents:
  spam_detector_agent:
    type: "spam_detector"
    enabled: true
    auto_delete: true                     # Delete agent after use
    auto_cleanup_resources: true          # Delete vector store after use

    metadata:
      description: "AI agent for detecting spam messages"
      tools:
        - "file_search"                   # Enables searching training documents

    framework_config:
      provider: "azure_foundry"           # Reference to providers section

    system_prompt_template: |
      You are an expert spam detection AI agent...

    user_prompt_template: |
      Please analyze the following message...
      {{message}}
```

### Configuring Agent Tools

The `tools` property in `metadata` controls which Azure AI Foundry tools the agent can use:

| Tool | Description |
|------|-------------|
| `file_search` | Enables searching documents in the vector store |
| `code_interpreter` | Enables Python code execution for data analysis |

Example with multiple tools:

```yaml
metadata:
  tools:
    - "file_search"
    - "code_interpreter"
```

> **Note:** If no tools are configured, `file_search` is enabled by default when creating an agent with a vector store.

### Keyed Dependency Injection

The agent factory is registered as a keyed service:

```csharp
services.AddKeyedSingleton<IAgentFactory>("spam_detector", (sp, key) =>
    new AgentFactory(key as string, ...));
```

And injected into the main class:

```csharp
public Main(
    [FromKeyedServices("spam_detector")] IAgentFactory spamDetectorFactory,
    ...)
```

### Agent Lifecycle

1. **Create Vector Store**: Upload training examples for the agent to reference
2. **Create Agent**: Initialize the ephemeral agent with configured tools (e.g., file search)
3. **Run Classification**: Send messages and receive classifications
4. **Cleanup**: Delete agent, thread, and vector store (based on `auto_delete` and `auto_cleanup_resources` settings)

## Sample Output

```text
================================================================================
SPAM DETECTION RESULTS
================================================================================

[✓] Message: "You have won $1,000,000! Click here to claim..."
    Predicted: spam | Expected: spam
    Confidence: 95%
    Reason: Financial scam with urgency tactics and suspicious link

[✓] Message: "Hi John, just wanted to follow up on our mee..."
    Predicted: not_spam | Expected: not_spam
    Confidence: 95%
    Reason: Normal business communication with professional context

================================================================================
ACCURACY: 10/10 (100%)
================================================================================
```

## Configuration Options

### Agent Definition Options

| Option | Description | Default |
|--------|-------------|---------|
| `type` | Agent type identifier | Required |
| `enabled` | Whether the agent is active | `true` |
| `auto_delete` | Delete agent/thread after use | `true` |
| `auto_cleanup_resources` | Delete vector store after use | `true` |

### Agent Metadata Options

| Option | Description | Default |
|--------|-------------|---------|
| `description` | Human-readable agent description | `""` |
| `tools` | List of tools: `file_search`, `code_interpreter` | `[]` (defaults to `file_search`) |

### Provider Options

| Option | Description |
|--------|-------------|
| `endpoint` | Azure AI Foundry endpoint URL |
| `deployment_name` | Model deployment name |
| `api_version` | API version (default: 2024-12-01-preview) |
| `timeout_seconds` | Request timeout (default: 300) |
| `max_retries` | Maximum retry attempts (default: 3) |

## Extending the Sample

### Add More Spam Categories

Modify the training document in `Main.cs`:

```csharp
private static string GenerateTrainingDocument()
{
    return """
        # Additional Spam Categories

        ## Cryptocurrency Scams
        - Promises of guaranteed returns
        - Unsolicited investment opportunities
        ...
        """;
}
```

### Customize Classification Response

Modify the agent's system prompt in `agent.config.yaml` to include additional fields:

```yaml
system_prompt_template: |
  Respond in JSON format:
  {
    "classification": "SPAM" | "NOT_SPAM",
    "confidence": 0.0-1.0,
    "category": "phishing" | "scam" | "promotional" | "legitimate",
    "indicators": ["list", "of", "indicators"]
  }
```

### Enable Code Interpreter

To enable Python code execution for more complex analysis:

```yaml
metadata:
  tools:
    - "file_search"
    - "code_interpreter"
```

### Enable Telemetry

Update the Telemetry section in `appsettings.json`:

```json
{
  "Telemetry": {
    "Enabled": true,
    "SourceName": "SpamDetection",
    "EnableSensitiveData": false
  }
}
```

## Troubleshooting

### Common Issues

1. **"Provider not found" error**
   - Ensure the provider name in `framework_config.provider` matches a key in `providers:`

2. **"Vector store file processing timeout"**
   - Increase timeout in vector store indexing options
   - Check Azure AI Foundry service health

3. **"Agent run completed with null response"**
   - Verify the model deployment is accessible
   - Check API rate limits

4. **"Unknown tool" warning**
   - Check that tools in `metadata.tools` are spelled correctly
   - Supported tools: `file_search`, `code_interpreter`

### Logging

Enable debug logging in `appsettings.Development.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

## Project Structure

```text
SpamDetection/
├── agent.config.yaml          # Agent and provider configuration
├── appsettings.json           # Application settings
├── appsettings.Development.json
├── .env                       # Environment variables (create from .env.example)
├── Program.cs                 # Application entry point
├── Main.cs                    # Spam detection logic
├── SpamDetection.csproj       # Project file
└── DependencyInjection/
    └── SpamDetectionServiceCollectionExtensions.cs
```

## Related Documentation

- [Cyclotron.Maf.AgentSdk](../../src/Cyclotron.Maf.AgentSdk/README.md)
- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-studio/)
- [Microsoft Agent Framework](https://github.com/microsoft/agents)
