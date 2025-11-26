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

## Setup

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

### 2. Build the Solution

```bash
dotnet build
```

### 3. Run the Sample

```bash
dotnet run
```

## How It Works

### Agent Configuration

The agent is configured in `agent.config.yaml`:

```yaml
agents:
  spam_detector_agent:
    type: "spam_detector"
    enabled: true
    auto_delete: true
    auto_cleanup_resources: true

    framework_config:
      provider: "azure_foundry"

    system_prompt_template: |
      You are an expert spam detection AI agent...

    user_prompt_template: |
      Please analyze the following message...
      {{message}}
```

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
2. **Create Agent**: Initialize the ephemeral agent with file search capabilities
3. **Run Classification**: Send messages and receive classifications
4. **Cleanup**: Delete agent, thread, and vector store (configurable via `auto_delete` and `auto_cleanup_resources`)

## Sample Output

```
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

### Enable Telemetry

Update the Telemetry section in `agent.config.yaml`:

```yaml
Telemetry:
  Enabled: true
  SourceName: "SpamDetection"
  EnableSensitiveData: false  # Set to true for debugging (logs message content)
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

## Related Documentation

- [Cyclotron.Maf.AgentSdk](../../src/Cyclotron.Maf.AgentSdk/README.md)
- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-studio/)
- [Microsoft Agent Framework](https://github.com/microsoft/agents)
