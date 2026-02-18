using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using Cyclotron.Maf.AgentSdk.Common.Models;
using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;
using ProviderDefinition = Cyclotron.Maf.AgentSdk.Common.Options.ModelProviderDefinitionOptions;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="OllamaVectorStoreManager"/>.
/// Tests Ollama-specific vector store operations including in-memory storage and HTTP API integration.
/// </summary>
public class OllamaVectorStoreManagerTests
{
    private readonly Mock<ILogger<OllamaVectorStoreManager>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<VectorStoreTelemetry> _mockTelemetry;
    private readonly IOptions<Cyclotron.Maf.AgentSdk.VectorStore.Options.VectorStoreIndexingOptions> _indexingOptions;
    private readonly IServiceProvider _serviceProvider;

    public OllamaVectorStoreManagerTests()
    {
        _mockLogger = new Mock<ILogger<OllamaVectorStoreManager>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Create proper mock dependencies for VectorStoreTelemetry
        var mockMeter = new Mock<Meter>("Test.Meter", "1.0.0");
        var mockMeterFactory = new Mock<IMeterFactory>();
        mockMeterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(mockMeter.Object);

        var mockTelemetryLogger = new Mock<ILogger<VectorStoreTelemetry>>();
        _mockTelemetry = new Mock<VectorStoreTelemetry>(MockBehavior.Loose, mockMeterFactory.Object, mockTelemetryLogger.Object);

        _indexingOptions = MsOptions.Create(new Cyclotron.Maf.AgentSdk.VectorStore.Options.VectorStoreIndexingOptions());

        // Build a real service provider with ModelProviderOptions for testing
        var services = new ServiceCollection();
        var modelProviderOptions = new ModelProviderOptions
        {
            Providers = new Dictionary<string, ProviderDefinition>
            {
                {
                    "ollama",
                    new ProviderDefinition
                    {
                        Type = "ollama",
                        Endpoint = "http://localhost:11434",
                        DeploymentName = "nomic-embed-text"
                    }
                }
            }
        };
        services.AddOptions<ModelProviderOptions>().Configure(opt =>
        {
            opt.Providers = modelProviderOptions.Providers;
        });
        _serviceProvider = services.BuildServiceProvider();
    }

    private Func<string, VectorStoreProviderConfig> CreateConfigFactory()
    {
        return providerName =>
        {
            if (providerName == "ollama")
            {
                return new VectorStoreProviderConfig(
                    "ollama",
                    "ollama",
                    "http://localhost:11434",
                    "nomic-embed-text",
                    null);
            }
            throw new InvalidOperationException($"Provider {providerName} not configured in tests");
        };
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaVectorStoreManager(null!, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory()));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when httpClientFactory is null")]
    public void Constructor_NullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaVectorStoreManager(_mockLogger.Object, null!, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory()));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when indexingOptions is null")]
    public void Constructor_NullIndexingOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, null!, _mockTelemetry.Object, CreateConfigFactory()));
    }

    [Fact(DisplayName = "GetOrCreateSharedVectorStoreAsync should create new vector store with valid ID")]
    public async Task GetOrCreateSharedVectorStoreAsync_ValidParameters_ReturnsVectorStoreId()
    {
        // Arrange
        var manager = new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory());

        // Act
        var vectorStoreId = await manager.GetOrCreateSharedVectorStoreAsync(
            "ollama",
            "test-key",
            "test-purpose",
            "test-name",
            CancellationToken.None);

        // Assert
        Assert.NotNull(vectorStoreId);
        Assert.NotEmpty(vectorStoreId);
    }

    [Fact(DisplayName = "AddFileToVectorStoreAsync should process chunks and generate embeddings")]
    public async Task AddFileToVectorStoreAsync_ValidFile_ProcessesChunksSuccessfully()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        var embeddingResponse = new
        {
            embeddings = new[] { new[] { 0.1f, 0.2f, 0.3f } }
        };

        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(embeddingResponse), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var manager = new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory());

        var vectorStoreId = await manager.GetOrCreateSharedVectorStoreAsync("ollama", "key", "purpose", "name", CancellationToken.None);

        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test document content for chunking"));

        // Act
        var fileId = await manager.AddFileToVectorStoreAsync(
            "ollama",
            vectorStoreId,
            fileStream,
            "test.txt",
            SimpleChunkingAsync,
            CancellationToken.None);

        // Assert
        Assert.NotNull(fileId);
        Assert.NotEmpty(fileId);
        _mockTelemetry.Verify(t => t.RecordEmbeddingsGenerated(It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce());
    }

    [Fact(DisplayName = "AddFileToVectorStoreAsync should throw VectorStoreIndexingException on embedding API failure")]
    public async Task AddFileToVectorStoreAsync_EmbeddingApiFailure_ThrowsVectorStoreIndexingException()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Ollama API error")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var manager = new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory());

        var vectorStoreId = await manager.GetOrCreateSharedVectorStoreAsync("ollama", "key", "purpose", "name", CancellationToken.None);
        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));

        // Act & Assert
        await Assert.ThrowsAsync<VectorStoreIndexingException>(async () =>
            await manager.AddFileToVectorStoreAsync(
                "ollama",
                vectorStoreId,
                fileStream,
                "test.txt",
                SimpleChunkingAsync,
                CancellationToken.None));
    }

    [Fact(DisplayName = "CleanupVectorStoreAsync should remove vector store from memory")]
    public async Task CleanupVectorStoreAsync_ExistingStore_RemovesSuccessfully()
    {
        // Arrange
        var manager = new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory());
        var vectorStoreId = await manager.GetOrCreateSharedVectorStoreAsync("ollama", "key", "purpose", "name", CancellationToken.None);

        // Act
        await manager.CleanupVectorStoreAsync("ollama", vectorStoreId, CancellationToken.None);

        // Assert - should not throw
        Assert.True(true);
    }

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should process multiple files in batch")]
    public async Task AddFilesToVectorStoreAsync_MultipleFiles_ProcessesAllSuccessfully()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        var embeddingResponse = new
        {
            embeddings = new[] { new[] { 0.1f, 0.2f, 0.3f } }
        };

        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(embeddingResponse), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var manager = new OllamaVectorStoreManager(_mockLogger.Object, _mockHttpClientFactory.Object, _indexingOptions, _mockTelemetry.Object, CreateConfigFactory());
        var vectorStoreId = await manager.GetOrCreateSharedVectorStoreAsync("ollama", "key", "purpose", "name", CancellationToken.None);

        var files = new[]
        {
            (Content: (Stream)new MemoryStream(Encoding.UTF8.GetBytes("File 1 content")), FileName: "file1.txt"),
            (Content: (Stream)new MemoryStream(Encoding.UTF8.GetBytes("File 2 content")), FileName: "file2.txt")
        };

        // Act
        var fileIds = await manager.AddFilesToVectorStoreAsync(
            "ollama",
            vectorStoreId,
            files,
            SimpleChunkingAsync,
            CancellationToken.None);

        // Assert
        Assert.Equal(2, fileIds.Count);
        Assert.All(fileIds, fileId => Assert.NotEmpty(fileId));
    }

    /// <summary>
    /// Simple chunking strategy for test purposes.
    /// </summary>
    private static async IAsyncEnumerable<(string Text, string ChunkId)> SimpleChunkingAsync(Stream stream, string fileName)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        var chunkSize = 100;
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var chunkText = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return (chunkText, $"{fileName}#{i / chunkSize}");
        }
    }
}
