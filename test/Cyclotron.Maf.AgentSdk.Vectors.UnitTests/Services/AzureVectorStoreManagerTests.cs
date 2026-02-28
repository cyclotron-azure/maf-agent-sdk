using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics.Metrics;

#pragma warning disable OPENAI001
using Cyclotron.Maf.AgentSdk.VectorStore.Exceptions;
using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.VectorStores;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="AzureVectorStoreManager.WaitForFileProcessingAsync"/>.
/// Tests the status-handling logic for Azure vector store file indexing using a testable subclass.
/// </summary>
public class AzureVectorStoreManagerTests
{
    private readonly Mock<ILogger<AzureVectorStoreManager>> _mockLogger;
    private readonly Mock<VectorStoreTelemetry> _mockTelemetry;

    public AzureVectorStoreManagerTests()
    {
        _mockLogger = new Mock<ILogger<AzureVectorStoreManager>>();

        var mockMeterFactory = new Mock<IMeterFactory>();
        mockMeterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(new Mock<Meter>("Test.Meter", "1.0.0").Object);
        var mockTelemetryLogger = new Mock<ILogger<VectorStoreTelemetry>>();
        _mockTelemetry = new Mock<VectorStoreTelemetry>(MockBehavior.Loose, mockMeterFactory.Object, mockTelemetryLogger.Object);
    }

    private AzureVectorStoreManager CreateManager(VectorStoreIndexingOptions? options = null)
    {
        var indexingOptions = MsOptions.Create(options ?? new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 3,
            InitialWaitDelayMs = 0,
            UseExponentialBackoff = false,
        });

        return new AzureVectorStoreManager(
            _mockLogger.Object,
            indexingOptions,
            _mockTelemetry.Object,
            _ => null!,
            _ => null!);
    }

    /// <summary>
    /// Creates a <see cref="ClientResult{VectorStoreFileAssociation}"/> from an OpenAI JSON response payload.
    /// </summary>
    private static ClientResult<VectorStoreFile> CreateFileResult(
        string status,
        string? errorCode = null,
        string? errorMessage = null)
    {
        var lastErrorJson = errorCode is not null
            ? $@",""last_error"":{{""code"":""{errorCode}"",""message"":""{errorMessage}""}}"
            : string.Empty;

        var json = $$"""{"id":"file-test","object":"vector_store.file","created_at":1698107661,"vector_store_id":"vs-test","status":"{{status}}"{{lastErrorJson}}}""";

        var association = ModelReaderWriter.Read<VectorStoreFile>(
            BinaryData.FromString(json),
            ModelReaderWriterOptions.Json);

        var mockResponse = new Mock<PipelineResponse>(MockBehavior.Loose);
        mockResponse.Setup(r => r.Status).Returns(200);
        mockResponse.Setup(r => r.Content).Returns(BinaryData.FromString(json));

        return ClientResult.FromValue(association!, mockResponse.Object);
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync: Completed status completes without throwing")]
    public async Task WaitForFileProcessingAsync_StatusCompleted_CompletesSuccessfully()
    {
        var mockVsClient = new Mock<VectorStoreClient>();
        mockVsClient
            .Setup(c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileResult("completed"));

        var manager = CreateManager();

        // Should not throw
        await manager.WaitForFileProcessingAsync("azure", "vs-test", "file-test", mockVsClient.Object);

        mockVsClient.Verify(
            c => c.GetVectorStoreFileAsync("vs-test", "file-test", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync: Failed status throws VectorStoreIndexingException with Azure error details")]
    public async Task WaitForFileProcessingAsync_StatusFailed_ThrowsVectorStoreIndexingExceptionWithErrorDetails()
    {
        var mockVsClient = new Mock<VectorStoreClient>();
        mockVsClient
            .Setup(c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileResult("failed", "internal_error", "File processing failed"));

        var manager = CreateManager();

        var ex = await Assert.ThrowsAsync<VectorStoreIndexingException>(
            () => manager.WaitForFileProcessingAsync("azure", "vs-test", "file-test", mockVsClient.Object));

        Assert.Contains("internal_error", ex.Message);
        Assert.Contains("File processing failed", ex.Message);
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync: Cancelled status throws VectorStoreIndexingException (not OperationCanceledException)")]
    public async Task WaitForFileProcessingAsync_StatusCancelled_ThrowsVectorStoreIndexingExceptionNotOperationCanceledException()
    {
        var mockVsClient = new Mock<VectorStoreClient>();
        mockVsClient
            .Setup(c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileResult("cancelled"));

        var manager = CreateManager();

        // Azure server-side cancellation must be VectorStoreIndexingException, NOT OperationCanceledException
        await Assert.ThrowsAsync<VectorStoreIndexingException>(
            () => manager.WaitForFileProcessingAsync("azure", "vs-test", "file-test", mockVsClient.Object));
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync: All polling attempts exhausted throws TimeoutException")]
    public async Task WaitForFileProcessingAsync_AllAttemptsExhausted_ThrowsTimeoutException()
    {
        var mockVsClient = new Mock<VectorStoreClient>();
        mockVsClient
            .Setup(c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileResult("in_progress"));

        var manager = CreateManager(new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 2,
            InitialWaitDelayMs = 0,
            UseExponentialBackoff = false,
        });

        await Assert.ThrowsAsync<TimeoutException>(
            () => manager.WaitForFileProcessingAsync("azure", "vs-test", "file-test", mockVsClient.Object));

        mockVsClient.Verify(
            c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync: CancellationToken cancellation propagates as OperationCanceledException")]
    public async Task WaitForFileProcessingAsync_CancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var mockVsClient = new Mock<VectorStoreClient>();
        mockVsClient
            .Setup(c => c.GetVectorStoreFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(CreateFileResult("in_progress"));

        var manager = CreateManager(new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 5,
            InitialWaitDelayMs = 0,
            UseExponentialBackoff = false,
        });

        // Token is cancelled inside the mock callback; the subsequent Task.Delay(0, cancelledToken) will throw
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.WaitForFileProcessingAsync("azure", "vs-test", "file-test", mockVsClient.Object, cts.Token));
    }

}
