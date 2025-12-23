using Cyclotron.Maf.AgentSdk.Models.Workflow;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Workflows.Executors;
using AwesomeAssertions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Workflows.Executors;

/// <summary>
/// Unit tests for the <see cref="CleanupExecutor{TResult}"/> class.
/// Tests constructor validation, HandleAsync execution, and cleanup orchestration.
/// </summary>
public class CleanupExecutorTests
{
    private readonly Mock<IVectorStoreManager> _mockVectorStoreManager;
    private readonly Mock<IAIFoundryCleanupService> _mockCleanupService;
    private readonly Mock<ILogger<CleanupExecutor<TestCleanupableResult>>> _mockLogger;
    private readonly Mock<IWorkflowContext> _mockWorkflowContext;
    private readonly IOptions<ModelProviderOptions> _providerOptions;

    public CleanupExecutorTests()
    {
        _mockVectorStoreManager = new Mock<IVectorStoreManager>();
        _mockCleanupService = new Mock<IAIFoundryCleanupService>();
        _mockLogger = new Mock<ILogger<CleanupExecutor<TestCleanupableResult>>>();
        _mockWorkflowContext = new Mock<IWorkflowContext>();
        _providerOptions = MsOptions.Create(new ModelProviderOptions
        {
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["azure_foundry"] = new ModelProviderDefinitionOptions
                {
                    Type = "azure_foundry",
                    Endpoint = "https://test.azure.com",
                    DeploymentName = "gpt-4"
                }
            }
        });
    }

    private CleanupExecutor<TestCleanupableResult> CreateExecutor()
    {
        return new CleanupExecutor<TestCleanupableResult>(
            _mockVectorStoreManager.Object,
            _mockCleanupService.Object,
            _mockLogger.Object,
            _providerOptions);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when vectorStoreManager is null")]
    public void Constructor_NullVectorStoreManager_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CleanupExecutor<TestCleanupableResult>(
            null!,
            _mockCleanupService.Object,
            _mockLogger.Object,
            _providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("vectorStoreManager");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when cleanupService is null")]
    public void Constructor_NullCleanupService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CleanupExecutor<TestCleanupableResult>(
            _mockVectorStoreManager.Object,
            null!,
            _mockLogger.Object,
            _providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cleanupService");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CleanupExecutor<TestCleanupableResult>(
            _mockVectorStoreManager.Object,
            _mockCleanupService.Object,
            null!,
            _providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when providerOptions is null")]
    public void Constructor_NullProviderOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CleanupExecutor<TestCleanupableResult>(
            _mockVectorStoreManager.Object,
            _mockCleanupService.Object,
            _mockLogger.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerOptions");
    }

    [Fact(DisplayName = "Constructor should create instance with valid parameters")]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        var executor = CreateExecutor();

        // Assert
        executor.Should().NotBeNull();
    }

    #endregion

    #region HandleAsync Tests

    [Fact(DisplayName = "HandleAsync should return the same result unchanged")]
    public async Task HandleAsync_ValidResult_ReturnsSameResult()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(
            fileIds: ["file-1", "file-2"],
            vectorStoreIds: ["vs-1"],
            agentIds: ["agent-1"],
            action: "process_document");

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.Should().BeSameAs(result);
        actual.Action.Should().Be("process_document");
        actual.FileIds.Should().HaveCount(2);
        actual.VectorStoreIds.Should().HaveCount(1);
        actual.AgentIds.Should().HaveCount(1);
    }

    [Fact(DisplayName = "HandleAsync should cleanup all vector stores")]
    public async Task HandleAsync_MultipleVectorStores_CleansUpAll()
    {
        // Arrange
        var executor = CreateExecutor();
        var vectorStoreIds = new[] { "vs-1", "vs-2", "vs-3" };
        var result = new TestCleanupableResult(vectorStoreIds: vectorStoreIds);

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync("azure_foundry", "vs-1", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync("azure_foundry", "vs-2", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync("azure_foundry", "vs-3", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should handle empty vector store list")]
    public async Task HandleAsync_EmptyVectorStoreList_DoesNotCallCleanup()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(vectorStoreIds: []);

        // Act
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.Should().BeSameAs(result);
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should continue cleanup when one fails")]
    public async Task HandleAsync_OneCleanupFails_ContinuesWithRest()
    {
        // Arrange
        var executor = CreateExecutor();
        var vectorStoreIds = new[] { "vs-1", "vs-2", "vs-3" };
        var result = new TestCleanupableResult(vectorStoreIds: vectorStoreIds);

        // Setup first call to fail, rest to succeed
        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync("azure_foundry", "vs-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cleanup failed for vs-1"));
        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync("azure_foundry", "vs-2", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync("azure_foundry", "vs-3", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - should not throw
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.Should().BeSameAs(result);
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact(DisplayName = "HandleAsync should not throw when all cleanups fail")]
    public async Task HandleAsync_AllCleanupsFail_DoesNotThrow()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(vectorStoreIds: ["vs-1", "vs-2"]);

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cleanup failed"));

        // Act - should not throw
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.Should().BeSameAs(result);
    }

    [Fact(DisplayName = "HandleAsync should use default provider name from options")]
    public async Task HandleAsync_UsesDefaultProviderFromOptions()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(vectorStoreIds: ["vs-1"]);

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert - verifies that the provider name is correctly passed
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync("azure_foundry", "vs-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should support cancellation token")]
    public async Task HandleAsync_WithCancellationToken_PassesToCleanup()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(vectorStoreIds: ["vs-1"]);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), token))
            .Returns(Task.CompletedTask);

        // Act
        await executor.HandleAsync(result, _mockWorkflowContext.Object, token);

        // Assert
        _mockVectorStoreManager.Verify(
            m => m.CleanupVectorStoreAsync("azure_foundry", "vs-1", token),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should handle result with all empty collections")]
    public async Task HandleAsync_AllEmptyCollections_ReturnsResultUnchanged()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(
            fileIds: [],
            vectorStoreIds: [],
            agentIds: [],
            action: "no_op");

        // Act
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.Should().BeSameAs(result);
        actual.Action.Should().Be("no_op");
        actual.FileIds.Should().BeEmpty();
        actual.VectorStoreIds.Should().BeEmpty();
        actual.AgentIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "HandleAsync should preserve file IDs in result")]
    public async Task HandleAsync_WithFileIds_PreservesFileIds()
    {
        // Arrange
        var executor = CreateExecutor();
        var fileIds = new[] { "file-abc", "file-def", "file-ghi" };
        var result = new TestCleanupableResult(fileIds: fileIds);

        // Act
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.FileIds.Should().BeEquivalentTo(fileIds);
    }

    [Fact(DisplayName = "HandleAsync should preserve agent IDs in result")]
    public async Task HandleAsync_WithAgentIds_PreservesAgentIds()
    {
        // Arrange
        var executor = CreateExecutor();
        var agentIds = new[] { "agent-123", "agent-456" };
        var result = new TestCleanupableResult(agentIds: agentIds);

        // Act
        var actual = await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert
        actual.AgentIds.Should().BeEquivalentTo(agentIds);
    }

    [Fact(DisplayName = "HandleAsync should not modify cleanup service calls")]
    public async Task HandleAsync_DoesNotCallCleanupService()
    {
        // Arrange
        var executor = CreateExecutor();
        var result = new TestCleanupableResult(vectorStoreIds: ["vs-1"]);

        _mockVectorStoreManager
            .Setup(m => m.CleanupVectorStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await executor.HandleAsync(result, _mockWorkflowContext.Object);

        // Assert - cleanup service is injected but not used in HandleAsync
        // (it might be used for other methods or future functionality)
        _mockCleanupService.VerifyNoOtherCalls();
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test implementation of ICleanupableWorkflowResult for testing.
    /// </summary>
    public class TestCleanupableResult : ICleanupableWorkflowResult
    {
        private readonly List<string> _fileIds;
        private readonly List<string> _vectorStoreIds;
        private readonly List<string> _agentIds;

        public TestCleanupableResult(
            IEnumerable<string>? fileIds = null,
            IEnumerable<string>? vectorStoreIds = null,
            IEnumerable<string>? agentIds = null,
            string action = "test")
        {
            _fileIds = fileIds?.ToList() ?? [];
            _vectorStoreIds = vectorStoreIds?.ToList() ?? [];
            _agentIds = agentIds?.ToList() ?? [];
            Action = action;
        }

        public IReadOnlyList<string> FileIds => _fileIds;
        public IReadOnlyList<string> VectorStoreIds => _vectorStoreIds;
        public IReadOnlyList<string> AgentIds => _agentIds;
        public string Action { get; }
    }

    #endregion
}
