# Cyclotron.Maf.AgentSdk.Vectors

Vector store management extensions for Cyclotron.Maf.AgentSdk. This package provides comprehensive vector store lifecycle management, file upload, and indexing operations for document processing workflows with Azure AI Foundry and OpenAI vector stores.

## Features

- **Vector Store Lifecycle Management**: Create, manage, and cleanup vector stores
- **File Upload & Indexing**: Upload single or multiple files with automatic indexing wait
- **Exponential Backoff**: Configurable polling with exponential backoff for indexing operations
- **Provider Abstraction**: Works with multiple AI providers through IProviderClientFactory
- **Optional Integration**: Can be added to existing AgentSdk applications without breaking changes

## Installation

```bash
dotnet add package AgentSdk.Vectors
```

## Quick Start

### 1. Register Vector Store Services

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register vector store services (optional - only if you need vector store functionality)
// Includes IVectorStoreManager and built-in chunkers by default.
builder.Services.AddVectorStoreServices();

// Register other AgentSdk services
builder.Services.AddDocumentWorkflowServices();
```

### 2. Optional: Customize Vector Store Registrations

```csharp
// Use a builder for optional registrations if you need a minimal surface.
builder.Services.AddVectorStoreServices(options =>
    options.AddManager()
        .AddChunking());
```

### 3. Configure Vector Store Indexing Options

**Configuration Path**:

```yaml
# appsettings.json or agent.config.yaml
VectorStoreIndexing:
    MaxWaitAttempts: 60
    InitialWaitDelayMs: 2000
    UseExponentialBackoff: true
    MaxWaitDelayMs: 30000
    TotalTimeoutMs: 0
```

### 4. Use Vector Store Manager

```csharp
public class DocumentProcessor
{
    private readonly IVectorStoreManager _vectorStoreManager;

    public DocumentProcessor(IVectorStoreManager vectorStoreManager)
    {
        _vectorStoreManager = vectorStoreManager;
    }

    public async Task ProcessDocumentsAsync(
        string providerName,
        IEnumerable<(Stream Content, string FileName)> files,
        CancellationToken cancellationToken)
    {
        // Create a vector store
        var vectorStoreId = await _vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
            providerName: providerName,
            key: "my-workflow",
            purpose: "Document classification",
            name: "My Documents Store",
            cancellationToken: cancellationToken);

        // Upload files and wait for indexing
        var fileIds = await _vectorStoreManager.AddFilesToVectorStoreAsync(
            providerName: providerName,
            vectorStoreId: vectorStoreId,
            files: files,
            cancellationToken: cancellationToken);

        // Use the vector store with your agents...

        // Cleanup when done
        await _vectorStoreManager.CleanupVectorStoreAsync(
            providerName: providerName,
            vectorStoreId: vectorStoreId,
            cancellationToken: cancellationToken);
    }
}
```

## Configuration Options

### VectorStoreIndexingOptions

Controls the polling behavior when waiting for files to be indexed in the vector store.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxWaitAttempts` | int | 60 | Maximum number of wait attempts before stopping |
| `InitialWaitDelayMs` | int | 2000 | Initial delay in milliseconds between checks |
| `UseExponentialBackoff` | bool | true | Enable exponential backoff for wait delays |
| `MaxWaitDelayMs` | int | 30000 | Maximum delay in milliseconds between checks |
| `TotalTimeoutMs` | int | 0 | Total timeout in milliseconds; 0 disables total timeout |

## API Reference

### IVectorStoreManager

#### GetOrCreateSharedVectorStoreAsync

Gets an existing shared vector store or creates a new one.

```csharp
Task<string> GetOrCreateSharedVectorStoreAsync(
    string providerName,
    string key,
    string purpose,
    string name,
    CancellationToken cancellationToken = default);
```

#### AddFileToVectorStoreAsync

Uploads a single file and waits for indexing to complete.

```csharp
Task<string> AddFileToVectorStoreAsync(
    string providerName,
    string vectorStoreId,
    Stream fileContent,
    string fileName,
    CancellationToken cancellationToken = default);
```

#### AddFilesToVectorStoreAsync

Uploads multiple files and waits for all to be indexed.

```csharp
Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
    string providerName,
    string vectorStoreId,
    IEnumerable<(Stream Content, string FileName)> files,
    CancellationToken cancellationToken = default);
```

#### CleanupVectorStoreAsync

Deletes a vector store and all associated files.

```csharp
Task CleanupVectorStoreAsync(
    string providerName,
    string vectorStoreId,
    CancellationToken cancellationToken = default);
```

## Migration from AgentSdk 2.x

If you're upgrading from AgentSdk 2.x where vector store management was included in the main package:

1. Add the `AgentSdk.Vectors` package reference
2. Add `services.AddVectorStoreServices()` to your DI registration
3. Ensure configuration uses the root-level `VectorStoreIndexing` section

See the [Migration Guide](../../docs/MIGRATION-GUIDE-v3.0.0.md) for detailed instructions.

## Requirements

- .NET 8.0 or later
- Cyclotron.Maf.AgentSdk 3.0.0 or later
- Azure AI Foundry or OpenAI API access

## License

See the [LICENSE](../../LICENSE) file in the root of the repository.
