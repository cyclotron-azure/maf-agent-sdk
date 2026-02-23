# Spam Detection Sample

This sample demonstrates how to use the **Cyclotron.Maf.AgentSdk** to build an AI-powered spam detection system with support for both:

- **Azure AI Foundry** (cloud-based, production-ready)
- **Ollama** (local open-source models, privacy-first)

## Features

✨ **Multi-Provider Support**: Switch between Azure AI and local Ollama models
🛡️ **Privacy-First Option**: Run entirely offline with Ollama
📊 **Spam Classification**: Analyzes messages with confidence levels and reasoning
📄 **Invoice Extraction**: PDF processing with text and image analysis
🔧 **Full Agent Lifecycle**: Create, run, and cleanup with automatic resource management
💾 **Vector Store Integration**: Context-aware classification using training examples

## Quick Start Guide

| Provider | Setup Time | Cost | Privacy | Command |
|----------|------------|------|---------|---------|
| **Azure AI** | 5 min | Pay-per-use | Cloud | [Setup Guide](#quick-start) |
| **Ollama** | 10 min | Free | Local-only | [Ollama Guide](#using-ollama-local-models) |

## Overview

The sample creates AI agents that:

- Analyzes messages to classify them as **SPAM** or **NOT_SPAM**
- Uses a vector store with training examples for context
- Provides confidence levels and reasoning for each classification
- Demonstrates the full agent lifecycle (create, run, cleanup)
- Includes an invoice extraction workflow for PDF processing

## Prerequisites

### For Azure AI Foundry (Default)

1. **.NET 8.0 SDK** or later
2. **Azure AI Foundry** project with a deployed model (e.g., GPT-4o, GPT-4o-mini)
3. **Azure CLI** authenticated (`az login`)

### For Ollama (Local, Optional)

1. **.NET 8.0 SDK** or later
2. **Ollama** installed and running ([ollama.ai](https://ollama.ai))
3. A downloaded model (e.g., `ollama pull llama3.2`)

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
dotnet run --Workflow:Mode=both
```

To run a single workflow, set `Workflow:Mode`:

```bash
dotnet run --Workflow:Mode=spam
dotnet run --Workflow:Mode=invoice
```

**To use Ollama instead of Azure for spam detection:**

```bash
dotnet run --Workflow:Mode=spam --Workflow:SpamProvider=ollama
```

Place PDF files in the `pdfs/` folder next to the sample, or set a custom folder:

```bash
dotnet run --Workflow:Mode=invoice --Workflow:InvoicePdfDirectory=/path/to/pdfs
```

## Using Ollama (Local Models)

This sample includes an example Ollama-based spam detector agent for **privacy-first, local spam detection**. No cloud API calls required!

### Why Use Ollama?

- **Privacy**: All data stays on your machine - no cloud API calls
- **Cost**: Free to use - no API charges
- **Offline**: Works without internet connection
- **Experimentation**: Try different open-source models easily

### ⚠️ Important Limitations

**Ollama agents do NOT support Azure AI Foundry features:**

| Feature | Azure AI Foundry | Ollama |
|---------|------------------|--------|
| Vector Stores (file_search) | ✅ Yes | ❌ Not Available |
| Code Interpreter | ✅ Yes | ❌ Not Available |
| Training Documents | ✅ Upload & search | ❌ N/A |
| Custom Knowledge Base | ✅ Dynamic context | ❌ Model knowledge only |
| Resource Cleanup | ✅ Delete vector stores | N/A |

**What this means:**

- **Azure agent**: Uses vector store with training examples for context-aware classification
- **Ollama agent**: Relies solely on the model's built-in knowledge from pre-training
- **No document uploads**: Ollama agents can't search uploaded training documents
- **No resource cleanup**: `auto_cleanup_resources: false` (no Azure resources exist)

**When to use which:**

- ✅ **Use Azure** when you need custom document search or training examples
- ✅ **Use Ollama** for general classification using model's pre-trained knowledge

### Quick Start with Ollama

1. **Install Ollama** (if not already installed):

   ```bash
   # macOS/Linux
   curl -fsSL https://ollama.ai/install.sh | sh

   # Windows: Download from https://ollama.ai
   ```

2. **Pull a model** (recommended models for spam detection):

   ```bash
   # Llama 3.2 (4GB) - Fast, good for classification
   ollama pull llama3.2:latest

   # Or try other models:
   ollama pull llama3.1:8b    # Better reasoning (4.7GB)
   ollama pull mistral:latest # Fast and efficient (4.1GB)
   ollama pull phi3:latest    # Lightweight option (2.3GB)
   ```

3. **Verify Ollama is running**:

   ```bash
   curl http://localhost:11434/api/tags
   ```

4. **Configure environment** (in `.env`):

   ```env
   OLLAMA_ENDPOINT=http://localhost:11434
   OLLAMA_MODEL=llama3.2:latest
   ```

5. **Enable the Ollama agent** in `agent.config.yaml`:

   ```yaml
   agents:
     spam_detector_ollama:
       enabled: true  # Change from false to true
   ```

6. **Run the sample with Ollama**:

   ```bash
   dotnet run --Workflow:Mode=spam --Workflow:SpamProvider=ollama
   ```

   Or set it in `appsettings.json`:

   ```json
   {
     "Workflow": {
       "Mode": "spam",
       "SpamProvider": "ollama"
     }
   }
   ```

### Ollama Configuration

The Ollama provider is configured in `agent.config.yaml`:

```yaml
providers:
  ollama_local:
    type: "ollama"              # Identifies local provider
    endpoint: "{OLLAMA_ENDPOINT}"
    deployment_name: "{OLLAMA_MODEL}"
    timeout_seconds: 120        # Local inference may be slower
    max_retries: 2

agents:
  spam_detector_ollama:
    type: "spam_detector_local"
    enabled: false              # Set to true to use
    auto_cleanup_resources: false  # NOTE: No Azure resources for local models

    metadata:
      # NOTE: No tools section - Ollama doesn't support Azure AI Foundry tools
      # Vector stores (file_search) and code_interpreter are Azure-only

    provider: "ollama_local"  # References the Ollama provider
```

**Key Differences from Azure:**

- ❌ No `file_search` tool (vector stores are Azure-only)
- ❌ No `code_interpreter` tool (Azure-only)
- ✅ `auto_cleanup_resources: false` (no cloud resources to clean up)
- ✅ Agent uses only the model's pre-trained knowledge
- ✅ No vector store creation or document uploads

### Model Recommendations

| Model | Size | Speed | Quality | Best For |
|-------|------|-------|---------|----------|
| `llama3.2:latest` | 4GB | Fast | Good | Quick classification tasks |
| `llama3.1:8b` | 4.7GB | Medium | Better | More accurate spam detection |
| `mistral:latest` | 4.1GB | Fast | Good | Balanced performance |
| `phi3:latest` | 2.3GB | Very Fast | Fair | Resource-constrained environments |

### Switching Between Azure and Ollama

You can easily switch between providers using configuration:

**Option 1: Command Line** (Recommended)

```bash
# Use Azure (default):
dotnet run --Workflow:Mode=spam

# Use Ollama:
dotnet run --Workflow:Mode=spam --Workflow:SpamProvider=ollama
```

**Option 2: Configuration File**

In `appsettings.json` or `appsettings.Development.json`:

```json
{
  "Workflow": {
    "Mode": "spam",
    "SpamProvider": "azure"  // or "ollama"
  }
}
```

**Option 3: Environment Variable**

```bash
export Workflow__SpamProvider=ollama
dotnet run --Workflow:Mode=spam
```

**Note:** Both agents must have `enabled: true` in `agent.config.yaml` for their respective provider to work.

### Ollama Troubleshooting

1. **"Connection refused" error**:
   - Ensure Ollama is running: `ollama serve` (runs automatically on macOS/Windows)
   - Check the endpoint: `curl http://localhost:11434`

2. **"Model not found" error**:
   - Pull the model first: `ollama pull llama3.2`
   - List available models: `ollama list`

3. **Slow response times**:
   - Ollama runs on CPU by default - inference will be slower than cloud APIs
   - Use a smaller model like `phi3:latest` for faster responses
   - Consider GPU acceleration if available

4. **Out of memory errors**:
   - Use a smaller model (e.g., `phi3:latest` instead of `llama3.1:8b`)
   - Close other applications to free up RAM
   - Reduce context window in prompts

For more details, see [OLLAMA-CONFIGURATION.md](../../docs/OLLAMA-CONFIGURATION.md)

## How It Works

### Azure vs Ollama Feature Comparison

Understanding the differences helps you choose the right provider for your use case:

| Feature | Azure AI Foundry | Ollama (Local) |
|---------|------------------|----------------|
| **Vector Stores** | ✅ Supported | ❌ Not Available |
| **File Search Tool** | ✅ Yes | ❌ No |
| **Code Interpreter** | ✅ Yes | ❌ No |
| **Training Documents** | ✅ Upload & search | ❌ N/A |
| **Custom Knowledge** | ✅ Dynamic context | ❌ Model training only |
| **Agent Creation** | With vector store | Standalone only |
| **Resource Cleanup** | ✅ Delete vector stores | N/A (no resources) |
| **Privacy** | Cloud (data sent to Azure) | ✅ 100% Local |
| **Cost** | Pay-per-token | ✅ Free |
| **Performance** | 200-500ms | 0.5-2s (after load) |
| **Scaling** | Automatic | Limited by hardware |
| **Models** | Latest OpenAI (GPT-4o, etc.) | Open-source (Llama, Mistral) |
| **Internet Required** | Yes | ✅ No (offline capable) |
| **Best For** | Production, custom knowledge | Development, privacy, cost |

**Key Takeaway:**

- **Azure**: Best for production workloads needing custom document search and dynamic context
- **Ollama**: Best for development, privacy-sensitive data, and cost-free experimentation

### Workflow Selection

`Workflow:Mode` controls which workflows run:

- `spam` runs only the spam detection workflow
- `invoice` runs only the invoice extraction workflow
- `both` runs spam detection then invoice extraction
- Aliases: `all`, `inv`, `invoices`, `spam-only`, `invoice-only`

`Workflow:InvoicePdfDirectory` controls where the invoice workflow looks for PDFs
(default: `pdfs`).

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

    provider: "azure_foundry"             # Reference to providers section

    system_prompt_template: |
      You are an expert spam detection AI agent...

    user_prompt_template: |
      Please analyze the following message...
      {{message}}
```

### Configuring Agent Tools

**Azure AI Foundry Only:** The `tools` property in `metadata` controls which Azure AI Foundry tools the agent can use. Ollama agents do not support these tools.

| Tool | Azure | Ollama | Description |
|------|-------|--------|-------------|
| `file_search` | ✅ Yes | ❌ No | Enables searching documents in the vector store |
| `code_interpreter` | ✅ Yes | ❌ No | Enables Python code execution for data analysis |

Example with multiple tools (Azure only):

```yaml
agents:
  spam_detector_agent:  # Azure agent
    metadata:
      tools:
        - "file_search"
        - "code_interpreter"

  spam_detector_ollama:  # Ollama agent
    metadata:
      # No tools section - not supported
```

> **Important:**
>
> - Azure agents: Tools are supported. If no tools are configured, `file_search` is enabled by default when creating an agent with a vector store.
> - Ollama agents: Do not configure tools - they are not supported and will be ignored.

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

**For Azure AI Foundry agents:**

1. **Create Vector Store**: Upload training examples for the agent to reference
2. **Create Agent**: Initialize the ephemeral agent with configured tools (e.g., file search). A session is automatically created to maintain conversation context.
3. **Run Classification**: Send messages through the session and receive classifications
4. **Cleanup**: Delete agent, session, and vector store (based on `auto_delete` and `auto_cleanup_resources` settings)

**For Ollama agents:**

1. **Create Agent**: Initialize the agent (no vector store needed). A session is automatically created.
2. **Run Classification**: Send messages through the session and receive classifications
3. **Cleanup**: Delete agent and session (based on `auto_delete` setting). No Azure resources to clean up.

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
| `auto_delete` | Delete agent and session after use | `true` |
| `auto_cleanup_resources` | Delete vector store after use | `true` |
| `temperature` | Overrides provider temperature (0.0-2.0) | - |
| `top_p` | Overrides provider Top P (0.0-1.0) | - |

### Agent Metadata Options

| Option | Description | Default |
|--------|-------------|---------|
| `description` | Human-readable agent description | `""` |
| `tools` | List of tools: `file_search`, `code_interpreter` | `[]` (defaults to `file_search`) |

### Provider Options

**Azure AI Foundry:**

| Option | Description |
|--------|-------------|
| `endpoint` | Azure AI Foundry endpoint URL |
| `deployment_name` | Model deployment name |
| `api_version` | API version (default: 2024-12-01-preview) |
| `timeout_seconds` | Request timeout (default: 300) |
| `max_retries` | Maximum retry attempts (default: 3) |
| `temperature` | Sampling temperature (0.0-2.0) |
| `top_p` | Nucleus sampling (0.0-1.0) |

**Ollama (Local):**

| Option | Description |
|--------|-------------|
| `endpoint` | Ollama API endpoint (default: <http://localhost:11434>) |
| `deployment_name` | Model name (e.g., llama3.2:latest, mistral:latest) |
| `timeout_seconds` | Request timeout (default: 120 for local inference) |
| `max_retries` | Maximum retry attempts (default: 2) |
| `temperature` | Sampling temperature (0.0-2.0) |
| `top_p` | Nucleus sampling (0.0-1.0) |

## Microsoft Learn Resources

For more information on Agent Framework, sessions, and Azure AI Foundry:

- **[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/)** - Official framework documentation
- **[Agent Sessions & Conversations](https://learn.microsoft.com/agent-framework/agents/conversations/session)** - Maintaining conversation context with sessions
- **[Multi-turn Conversations](https://learn.microsoft.com/agent-framework/get-started/multi-turn/)** - Building multi-turn agent interactions
- **[Azure AI Foundry Agents](https://learn.microsoft.com/azure/ai-foundry/agents/)** - Creating agents in Azure AI Foundry
- **[Azure AI Foundry File Search](https://learn.microsoft.com/azure/ai-foundry/how-to/file-search)** - Using file search tool with agents

## Extending the Sample

### Compare Azure vs Ollama Performance

Run both agents and compare results:

```csharp
// In Main.cs, create both factories and compare classifications
var azureFactory = serviceProvider.GetKeyedService<IAgentFactory>("spam_detector_agent");
var ollamaFactory = serviceProvider.GetKeyedService<IAgentFactory>("spam_detector_ollama");

// Run classification with both
var azureResult = await ClassifyWithAgent(azureFactory, message);
var ollamaResult = await ClassifyWithAgent(ollamaFactory, message);

// Compare accuracy, speed, and consistency
```

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


Telemetry works with both Azure and Ollama providers, allowing you to monitor:
- Agent response times (compare Azure vs Ollama)
- Token usage (Azure) vs inference time (Ollama)
- Error rates and retry patterns
- Classification accuracy over time - "file_search"
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

- Ensure the provider name in `provider` matches a key in `providers:`

1. **"Vector store file processing timeout"**
   - Increase timeout in vector store indexing options
   - Check Azure AI Foundry service health

2. **"Agent run completed with null response"**
   - Verify the model deployment is accessible
   - Check API rate limits

3. **"Unknown tool" warning**
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

- [Cyclotron.Maf.AgentSdk](../../src/Cyclotron.Maf.AgentSdk/README.md) - SDK documentation
- [Ollama Configuration Guide](../../docs/OLLAMA-CONFIGURATION.md) - Detailed Ollama setup
- [Changelog](../../docs/CHANGELOG.md) - Version history and migration guide
- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-studio/) - Cloud provider documentation
- [Microsoft Agent Framework](https://github.com/microsoft/agents) - Framework documentation
- [Ollama](https://ollama.ai) - Local model runtime
