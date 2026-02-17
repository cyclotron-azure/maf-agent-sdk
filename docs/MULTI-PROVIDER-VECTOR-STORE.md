# Multi-Provider Vector Store Implementation

This document describes the multi-provider vector store architecture that supports both Azure AI Foundry (with server-side chunking) and local Ollama backends (with client-side chunking).

## Architecture Overview

The vector store implementation follows a **provider dispatch pattern** with different chunking strategies per provider:

- **Azure AI Foundry**: Server-side chunking using native `FileChunkingStrategy`
- **Ollama**: Client-side chunking using delegate-based document processing

```
┌─────────────────────────────────────────────────────────────┐
│                    IVectorStoreManager                      │
│  (Unified interface with optional chunking delegate)       │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              VectorStoreManagerAdapter                      │
│  (Delegates to factory based on provider type)             │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│            VectorStoreManagerFactory                        │
│  (Dispatches by provider.Type field)                       │
└─────────────────────────────────────────────────────────────┘
             │                            │
             ▼                            ▼
┌───────────────────────┐    ┌───────────────────────────┐
│ AzureVectorStore      │    │ OllamaVectorStore         │
│ Manager               │    │ Manager                   │
│                       │    │                           │
│ ✓ Server-side chunking│    │ ✓ Client-side chunking    │
│ ✓ FileChunkingStrategy│    │ ✓ Chunking delegate used  │
│ ✓ Full file upload    │    │ ✓ In-memory storage       │
│ ✓ Azure native indexing│    │ ✓ Ollama /api/embed       │
│ ✓ Exponential backoff │    │ ✓ Thread-safe Dictionary  │
└───────────────────────┘    └───────────────────────────┘
```

## Key Components

### 1. Chunking Strategies (Provider-Specific)

#### ⚠️ Important: Chunking Responsibility Varies by Provider

**Azure AI Foundry** - **SERVER-SIDE CHUNKING**:
- The chunking delegate parameter is **IGNORED**
- Azure handles all chunking internally using `FileChunkingStrategy`
- Complete files are uploaded to Azure (not pre-chunked)
- Default strategy: Auto (800 tokens/chunk, 400 token overlap)
- Future support planned for configuring `FileChunkingStrategy.Static`

**Ollama** - **CLIENT-SIDE CHUNKING**:
- The chunking delegate parameter is **REQUIRED**
- Documents must be chunked before embedding generation
- Ollama has no native file management or chunking APIs
- Use built-in chunkers or provide custom logic

#### The Chunking Delegate

For **Ollama only**, the interface accepts a chunking delegate:

```csharp
Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate
```

**Built-in Chunkers (for Ollama)**:

- **SemanticDocumentChunker**: Intelligent sentence-boundary aware chunking adapted from [SemanticChunker.NET](https://github.com/GregorBiswanger/SemanticChunker.NET)
  - Configurable target/min chunk sizes
  - Overlap preservation for context
  - Paragraph awareness

- **SimpleDocumentChunker**: Fixed-size chunking with natural break-point detection
  - Breaks at word boundaries when possible
  - Predictable chunk sizes

**Usage (Ollama with custom chunking):**
```csharp
// Chunking delegate IS USED for Ollama
await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "ollama",
    vectorStoreId: vectorStoreId,
    fileContent: pdfStream,
    fileName: "invoice.pdf",
    chunkingDelegate: async (stream, fileName) =>
    {
        // Custom chunking logic for Ollama
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        // ... chunk the text
        yield return (chunkText, chunkId);
    },
    cancellationToken: cancellationToken);
```

**Usage (Azure with server-side chunking):**
```csharp
// Chunking delegate is IGNORED for Azure (pass any value or null-returning delegate)
await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "azure_foundry",
    vectorStoreId: vectorStoreId,
    fileContent: pdfStream,
    fileName: "invoice.pdf",
    chunkingDelegate: (stream, fileName) => AsyncEnumerable.Empty<(string, string)>(),  // Ignored
    cancellationToken: cancellationToken);
// Azure handles chunking server-side using FileChunkingStrategy.Auto
```

### 2. Provider Implementations

#### Azure AI Foundry (`AzureVectorStoreManager`)

**Server-Side Chunking Approach:**
- Uses Azure AI Projects V2 API with native `OpenAIFileClient` and `VectorStoreClient`
- **Uploads complete files** (not chunks) via `UploadFileAsync(stream, fileName, purpose)`
- **Azure performs chunking server-side** using `FileChunkingStrategy.Auto` (default: 800 tokens, 400 overlap)
- Ephemeral vector stores (created per workflow)
- Exponential backoff polling for indexing completion
- Automatic cleanup based on `auto_delete` configuration

**Key Implementation Details:**
```csharp
// 1. Upload complete file to Azure
var uploadedFile = await fileClient.UploadFileAsync(
    fileContent,      // Complete file stream
    fileName,
    FileUploadPurpose.Assistants);

// 2. Add file to vector store (Azure chunks it server-side)
await vectorStoreClient.AddFileToVectorStoreAsync(
    vectorStoreId,
    uploadedFile.Value.Id,
    cancellationToken);

// 3. Wait for Azure to finish chunking and indexing
await WaitForFileProcessingAsync(providerName, vectorStoreId, fileId);
```

#### Ollama (`OllamaVectorStoreManager`)

**Client-Side Chunking Approach:**
- In-memory vector storage using thread-safe `Dictionary<string, VectorStoreData>`
- **Requires client-side chunking** via the chunking delegate (Ollama has no file/chunking APIs)
- Direct HTTP calls to Ollama `/api/embed` endpoint for embedding generation
- Synchronous embedding generation per chunk
- No persistent storage (ephemeral like Azure)
- Automatic cleanup removes from memory

### 3. Universal Error Handling

All vector store exceptions inherit from `VectorStoreException`:

```csharp
VectorStoreException (base)
├── VectorStoreIndexingException     // Chunking/embedding/indexing failures
├── VectorStoreCleanupException      // Deletion/cleanup failures
└── VectorStoreConfigurationException // Invalid provider config
```

Each exception captures:

- Provider name (for context)
- Original exception (inner exception)
- Additional context (e.g., VectorStoreId for cleanup)

### 4. Comprehensive Telemetry

OpenTelemetry metrics with structured tagging:

| Metric | Type | Tags | Description |
|--------|------|------|-------------|
| `vectorstore.documents.indexed` | Counter | provider, format | Documents indexed count |
| `vectorstore.embeddings.generated` | Counter | provider | Embeddings created count |
| `vectorstore.indexing.duration` | Histogram | provider, chunker_type | Indexing time (ms) |
| `vectorstore.chunks.per_document` | Histogram | provider, format | Chunks per document |
| `vectorstore.errors` | Counter | provider, error_type | Error occurrences |

## Configuration

### agent.config.yaml

```yaml
providers:
  azure_foundry:
    type: "azure_foundry"
    endpoint: "${PROJECT_ENDPOINT}"
    deployment_name: "${PROJECT_DEPLOYMENT_NAME}"

  ollama:
    type: "ollama"
    endpoint: "http://localhost:11434"
    deployment_name: "nomic-embed-text"

# Optional chunking configuration
SemanticChunking:
  TargetChunkSize: 800
  MinChunkSize: 200
  OverlapSize: 100
  PreserveFormatting: true
```

### Dependency Injection Setup

```csharp
services.AddVectorStoreServices(
    // Azure client factory
    sp => providerName => sp.GetRequiredService<IProviderClientFactory>()
                            .GetClient(providerName),
    // Provider config factory
    sp =>
    {
        var modelProviderOptions = sp.GetRequiredService<IOptions<ModelProviderOptions>>();
        return providerName =>
        {
            var providerDef = modelProviderOptions.Value.Providers[providerName];
            return new VectorStoreProviderConfig(
                providerName,
                providerDef.Type,
                providerDef.Endpoint,
                providerDef.DeploymentName);
        };
    });
```

## Usage Patterns

### Azure Document Indexing (Server-Side Chunking)

```csharp
// Create vector store
var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
    providerName: "azure_foundry",
    key: "my-documents",
    purpose: "Document retrieval",
    name: "My Document Store",
    cancellationToken);

// Add document - Azure chunks server-side (delegate ignored)
using var docStream = File.OpenRead("document.pdf");
var fileId = await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "azure_foundry",
    vectorStoreId: vectorStoreId,
    fileContent: docStream,
    fileName: "document.pdf",
    chunkingDelegate: (_, _) => AsyncEnumerable.Empty<(string, string)>(),  // Ignored by Azure
    cancellationToken);

// Azure automatically chunks using FileChunkingStrategy.Auto
// Default: 800 tokens per chunk, 400 token overlap
```

### Ollama Document Indexing (Client-Side Chunking)

```csharp
// Create vector store
var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
    providerName: "ollama",
    key: "my-documents",
    purpose: "Document retrieval",
    name: "My Document Store",
    cancellationToken);

// Add document with semantic chunking (delegate IS USED)
using var docStream = File.OpenRead("document.pdf");
var fileId = await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "ollama",
    vectorStoreId: vectorStoreId,
    fileContent: docStream,
    fileName: "document.pdf",
    chunkingDelegate: semanticChunker.ChunkAsync,  // Actually used by Ollama
    cancellationToken);
```

### Batch Document Upload

```csharp
var files = Directory.GetFiles("./pdfs")
    .Select(path => (
        Content: (Stream)File.OpenRead(path),
        FileName: Path.GetFileName(path)));

var fileIds = await vectorStoreManager.AddFilesToVectorStoreAsync(
    providerName: "ollama",
    vectorStoreId: vectorStoreId,
    files: files,
    chunkingDelegate: simpleChunker.ChunkAsync,
    cancellationToken);
```

### Cleanup

```csharp
await vectorStoreManager.CleanupVectorStoreAsync(
    providerName: "azure_foundry",
    vectorStoreId: vectorStoreId,
    cancellationToken);
```

## Chunking Strategy Decision Guide

### When Does Chunking Happen?

| Provider | Chunking Location | Chunking Delegate | Configuration |
|----------|-------------------|-------------------|---------------|
| **Azure AI Foundry** | Server-side (Azure) | Ignored | `FileChunkingStrategy` (future) |
| **Ollama** | Client-side | Required & Used | Built-in chunkers or custom |

### Why the Difference?

**Azure AI Foundry**:
- Azure provides a complete file management and chunking platform
- `FileChunkingStrategy.Auto` automatically determines optimal chunk sizes
- `FileChunkingStrategy.Static` allows custom token sizes (800 default, 400 overlap)
- Server-side chunking is more efficient and consistent
- Eliminates unnecessary client-side processing and file operations

**Ollama**:
- Ollama is a model inference engine only (no file or vector store APIs)
- Client must handle all document processing and chunking
- Embeddings are generated per chunk via `/api/embed` endpoint
- In-memory storage managed by SDK (not Ollama)

### Recommended Patterns

**For Azure Projects**:
```csharp
// Provide a no-op delegate (required by interface signature)
chunkingDelegate: (_, _) => AsyncEnumerable.Empty<(string, string)>()

// Or use a helper method
private static IAsyncEnumerable<(string, string)> NoOpChunking(Stream _, string __)
    => AsyncEnumerable.Empty<(string, string)>();
```

**For Ollama Projects**:
```csharp
// Use semantic chunking for better context preservation
chunkingDelegate: semanticChunker.ChunkAsync

// Or simple fixed-size chunking
chunkingDelegate: simpleChunker.ChunkAsync

// Or custom logic
chunkingDelegate: async (stream, fileName) =>
{
    // Your chunking logic
    yield return (chunkText, chunkId);
}
```

## Testing

### Unit Tests Created

- `OllamaVectorStoreManagerTests` - Ollama backend operations
- `SemanticDocumentChunkerTests` - Intelligent chunking logic
- `SimpleDocumentChunkerTests` - Fixed-size chunking
- `VectorStoreExceptionTests` - Exception hierarchy

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~.Vectors.UnitTests"
```

## Migration from v1.x

**Breaking Changes:**

1. **Method signatures changed** - all `AddFileToVectorStoreAsync` methods now require `chunkingDelegate` parameter
   - **Azure**: Parameter is ignored (server-side chunking used instead)
   - **Ollama**: Parameter is required and used for client-side chunking
2. **VectorStoreManager removed** - use `AzureVectorStoreManager` directly or via factory
3. **DI registration updated** - `AddVectorStoreServices` now requires both factories
4. **Azure chunking behavior changed** - Previously used client-side chunking (incorrect), now uses Azure's native server-side chunking

**Migration steps:**

```csharp
// OLD (v1.x) - Azure with incorrect client-side chunking
await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName, vectorStoreId, fileStream, fileName, cancellationToken);

// NEW (v2.0) - Azure with correct server-side chunking
await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "azure_foundry",
    vectorStoreId: vectorStoreId,
    fileContent: fileStream,
    fileName: fileName,
    chunkingDelegate: (_, _) => AsyncEnumerable.Empty<(string, string)>(),  // Ignored
    cancellationToken: cancellationToken);

// NEW (v2.0) - Ollama with client-side chunking
await vectorStoreManager.AddFileToVectorStoreAsync(
    providerName: "ollama",
    vectorStoreId: vectorStoreId,
    fileContent: fileStream,
    fileName: fileName,
    chunkingDelegate: semanticChunker.ChunkAsync,  // Actually used
    cancellationToken: cancellationToken);
```

## Performance Considerations

### Azure Backend

- **Single file upload per document** (not multiple chunks) - more efficient than v1.x
- **Server-side chunking** eliminates client-side processing overhead
- **Native Azure indexing** with built-in optimization
- Parallel file uploads supported via `AddFilesToVectorStoreAsync`
- Exponential backoff prevents API throttling
- Polling interval: 1s → 2s → 4s → 8s (configurable via `VectorStoreIndexingOptions`)
- **No temporary file creation** (streams directly uploaded)

### Ollama Backend

- **Client-side chunking required** (Ollama has no file/chunking APIs)
- Sequential embedding generation (one chunk at a time)
- In-memory store eliminates disk I/O
- Thread-safe for concurrent access
- No persistent storage = fast cleanup

## Security Notes

- **API Keys**: Never commit credentials; use environment variables (`${VAR_NAME}` in config)
- **Provider Endpoints**: Validate endpoint URLs before use
- **Chunk Size**: Limit chunk sizes to prevent memory exhaustion
- **Rate Limiting**: Azure has built-in rate limits; Ollama may require manual throttling

## Future Enhancements

- [ ] Add PostgreSQL pgvector backend
- [ ] Implement Redis vector store provider
- [ ] Support incremental indexing (update existing chunks)
- [ ] Add vector similarity search API
- [ ] Implement chunk deduplication
- [ ] Add compression for stored embeddings

## References

- [Azure AI Projects SDK](https://learn.microsoft.com/en-us/azure/ai-services/agents/)
- [Ollama API Documentation](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [SemanticChunker.NET](https://github.com/GregorBiswanger/SemanticChunker.NET)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
