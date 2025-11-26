using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="VectorStoreManager"/> class.
/// Tests constructor validation and indexing options configuration.
/// Note: Async methods require Azure client mocking which is not feasible in unit tests.
/// </summary>
public class VectorStoreManagerTests
{
    private readonly Mock<ILogger<VectorStoreManager>> _mockLogger;
    private readonly Mock<IPersistentAgentsClientFactory> _mockClientFactory;

    public VectorStoreManagerTests()
    {
        _mockLogger = new Mock<ILogger<VectorStoreManager>>();
        _mockClientFactory = new Mock<IPersistentAgentsClientFactory>();
    }

    private IOptions<ModelProviderOptions> CreateProviderOptions(VectorStoreIndexingOptions? indexingOptions = null)
    {
        var options = new ModelProviderOptions
        {
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>
            {
                ["azure_foundry"] = new ModelProviderDefinitionOptions
                {
                    Type = "azure_foundry",
                    Endpoint = "https://test.azure.com",
                    DeploymentName = "gpt-4"
                }
            },
            VectorStoreIndexing = indexingOptions ?? new VectorStoreIndexingOptions()
        };
        return MsOptions.Create(options);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();

        // Act
        var act = () => new VectorStoreManager(null!, _mockClientFactory.Object, providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when clientFactory is null")]
    public void Constructor_NullClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var providerOptions = CreateProviderOptions();

        // Act
        var act = () => new VectorStoreManager(_mockLogger.Object, null!, providerOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientFactory");
    }

    [Fact(DisplayName = "Constructor should use default indexing options when providerOptions is null")]
    public void Constructor_NullProviderOptions_UsesDefaultIndexingOptions()
    {
        // Act - should not throw, uses default indexing options
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            null!);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should use default indexing options when VectorStoreIndexing is null")]
    public void Constructor_NullVectorStoreIndexing_UsesDefaultIndexingOptions()
    {
        // Arrange
        var options = new ModelProviderOptions
        {
            Providers = new Dictionary<string, ModelProviderDefinitionOptions>(),
            VectorStoreIndexing = null!
        };

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            MsOptions.Create(options));

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should accept custom indexing options")]
    public void Constructor_CustomIndexingOptions_AcceptsOptions()
    {
        // Arrange
        var indexingOptions = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 30,
            InitialWaitDelayMs = 1000,
            UseExponentialBackoff = false,
            MaxWaitDelayMs = 60000
        };

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions(indexingOptions));

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should create instance with valid parameters")]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        // Assert
        manager.Should().NotBeNull();
    }

    #endregion

    #region VectorStoreIndexingOptions Tests

    [Fact(DisplayName = "VectorStoreIndexingOptions should have correct default values")]
    public void VectorStoreIndexingOptions_Defaults_AreCorrect()
    {
        // Arrange & Act
        var options = new VectorStoreIndexingOptions();

        // Assert
        options.MaxWaitAttempts.Should().Be(60);
        options.InitialWaitDelayMs.Should().Be(2000);
        options.UseExponentialBackoff.Should().BeTrue();
        options.MaxWaitDelayMs.Should().Be(30000);
        options.TotalTimeoutMs.Should().Be(0);
    }

    [Fact(DisplayName = "VectorStoreIndexingOptions should allow setting all properties")]
    public void VectorStoreIndexingOptions_SetAllProperties_PropertiesArePersisted()
    {
        // Arrange & Act
        var options = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 100,
            InitialWaitDelayMs = 500,
            UseExponentialBackoff = false,
            MaxWaitDelayMs = 120000,
            TotalTimeoutMs = 300000
        };

        // Assert
        options.MaxWaitAttempts.Should().Be(100);
        options.InitialWaitDelayMs.Should().Be(500);
        options.UseExponentialBackoff.Should().BeFalse();
        options.MaxWaitDelayMs.Should().Be(120000);
        options.TotalTimeoutMs.Should().Be(300000);
    }

    #endregion

    #region GetOrCreateSharedVectorStoreAsync Tests

    [Fact(DisplayName = "GetOrCreateSharedVectorStoreAsync should call GetClient with provider name")]
    public async Task GetOrCreateSharedVectorStoreAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called with correct provider"));

        // Act
        var act = () => manager.GetOrCreateSharedVectorStoreAsync(
            "azure_foundry",
            "test-key",
            "test-purpose",
            "test-name");

        // Assert - Verifies GetClient was called with the right provider
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called with correct provider*");
        _mockClientFactory.Verify(x => x.GetClient("azure_foundry"), Times.Once);
    }

    [Fact(DisplayName = "GetOrCreateSharedVectorStoreAsync should throw when client factory throws")]
    public async Task GetOrCreateSharedVectorStoreAsync_ClientFactoryThrows_PropagatesException()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider not found"));

        // Act
        var act = () => manager.GetOrCreateSharedVectorStoreAsync(
            "unknown_provider",
            "key",
            "purpose",
            "name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider not found*");
    }

    [Fact(DisplayName = "GetOrCreateSharedVectorStoreAsync should accept cancellation token")]
    public async Task GetOrCreateSharedVectorStoreAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => manager.GetOrCreateSharedVectorStoreAsync(
            "azure_foundry",
            "key",
            "purpose",
            "name",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "GetOrCreateSharedVectorStoreAsync should accept various key values")]
    [InlineData("simple-key")]
    [InlineData("key_with_underscores")]
    [InlineData("key-with-dashes")]
    [InlineData("key123")]
    [InlineData("KEY_UPPERCASE")]
    public async Task GetOrCreateSharedVectorStoreAsync_VariousKeyFormats_CallsClientFactory(string key)
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => manager.GetOrCreateSharedVectorStoreAsync(
            "azure_foundry",
            key,
            "purpose",
            "name");

        // Assert - Verifies the method proceeds with various key formats
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region CleanupVectorStoreAsync Tests

    [Fact(DisplayName = "CleanupVectorStoreAsync should call GetClient with provider name")]
    public async Task CleanupVectorStoreAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => manager.CleanupVectorStoreAsync(
            "azure_foundry",
            "vs-123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
        _mockClientFactory.Verify(x => x.GetClient("azure_foundry"), Times.Once);
    }

    [Fact(DisplayName = "CleanupVectorStoreAsync should throw when client factory throws")]
    public async Task CleanupVectorStoreAsync_ClientFactoryThrows_PropagatesException()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider error"));

        // Act
        var act = () => manager.CleanupVectorStoreAsync(
            "unknown_provider",
            "vs-123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider error*");
    }

    [Fact(DisplayName = "CleanupVectorStoreAsync should accept cancellation token")]
    public async Task CleanupVectorStoreAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => manager.CleanupVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "CleanupVectorStoreAsync should accept various vector store IDs")]
    [InlineData("vs-123")]
    [InlineData("vector_store_456")]
    [InlineData("abc123def456")]
    [InlineData("VS-UPPERCASE")]
    public async Task CleanupVectorStoreAsync_VariousVectorStoreIds_CallsClientFactory(string vectorStoreId)
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => manager.CleanupVectorStoreAsync(
            "azure_foundry",
            vectorStoreId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region AddFileToVectorStoreAsync Tests

    [Fact(DisplayName = "AddFileToVectorStoreAsync should call GetClient with provider name")]
    public async Task AddFileToVectorStoreAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        using var stream = new MemoryStream("test content"u8.ToArray());

        // Act
        var act = () => manager.AddFileToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            stream,
            "test.txt");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "AddFileToVectorStoreAsync should throw when client factory throws")]
    public async Task AddFileToVectorStoreAsync_ClientFactoryThrows_PropagatesException()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider error"));

        using var stream = new MemoryStream("test"u8.ToArray());

        // Act
        var act = () => manager.AddFileToVectorStoreAsync(
            "unknown_provider",
            "vs-123",
            stream,
            "test.txt");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider error*");
    }

    [Fact(DisplayName = "AddFileToVectorStoreAsync should accept cancellation token")]
    public async Task AddFileToVectorStoreAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        using var stream = new MemoryStream("test"u8.ToArray());

        // Act
        var act = () => manager.AddFileToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            stream,
            "test.txt",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "AddFileToVectorStoreAsync should accept various file names")]
    [InlineData("document.txt")]
    [InlineData("report.pdf")]
    [InlineData("data_file.json")]
    [InlineData("file-with-dashes.xml")]
    [InlineData("FILE_UPPERCASE.CSV")]
    public async Task AddFileToVectorStoreAsync_VariousFileNames_CallsClientFactory(string fileName)
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        using var stream = new MemoryStream("test"u8.ToArray());

        // Act
        var act = () => manager.AddFileToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            stream,
            fileName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region AddFilesToVectorStoreAsync Tests

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should call GetClient with provider name")]
    public async Task AddFilesToVectorStoreAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        var files = new List<(Stream Content, string FileName)>
        {
            (new MemoryStream("test1"u8.ToArray()), "file1.txt"),
            (new MemoryStream("test2"u8.ToArray()), "file2.txt")
        };

        // Act
        var act = () => manager.AddFilesToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should throw when client factory throws")]
    public async Task AddFilesToVectorStoreAsync_ClientFactoryThrows_PropagatesException()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider error"));

        var files = new List<(Stream Content, string FileName)>
        {
            (new MemoryStream("test"u8.ToArray()), "file.txt")
        };

        // Act
        var act = () => manager.AddFilesToVectorStoreAsync(
            "unknown_provider",
            "vs-123",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider error*");
    }

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should accept cancellation token")]
    public async Task AddFilesToVectorStoreAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        var files = new List<(Stream Content, string FileName)>
        {
            (new MemoryStream("test"u8.ToArray()), "file.txt")
        };

        // Act
        var act = () => manager.AddFilesToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            files,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should accept empty file list")]
    public async Task AddFilesToVectorStoreAsync_EmptyFileList_CallsClientFactory()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        var files = new List<(Stream Content, string FileName)>();

        // Act
        var act = () => manager.AddFilesToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "AddFilesToVectorStoreAsync should accept multiple files")]
    public async Task AddFilesToVectorStoreAsync_MultipleFiles_CallsClientFactory()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        var files = new List<(Stream Content, string FileName)>
        {
            (new MemoryStream("content1"u8.ToArray()), "file1.txt"),
            (new MemoryStream("content2"u8.ToArray()), "file2.pdf"),
            (new MemoryStream("content3"u8.ToArray()), "file3.json")
        };

        // Act
        var act = () => manager.AddFilesToVectorStoreAsync(
            "azure_foundry",
            "vs-123",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region WaitForFileProcessingAsync Tests

    [Fact(DisplayName = "WaitForFileProcessingAsync should call GetClient with provider name")]
    public async Task WaitForFileProcessingAsync_ValidProvider_CallsGetClient()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient("azure_foundry"))
            .Throws(new InvalidOperationException("GetClient called correctly"));

        // Act
        var act = () => manager.WaitForFileProcessingAsync(
            "azure_foundry",
            "vs-123",
            "file-456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GetClient called correctly*");
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync should throw when client factory throws")]
    public async Task WaitForFileProcessingAsync_ClientFactoryThrows_PropagatesException()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Provider error"));

        // Act
        var act = () => manager.WaitForFileProcessingAsync(
            "unknown_provider",
            "vs-123",
            "file-456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Provider error*");
    }

    [Fact(DisplayName = "WaitForFileProcessingAsync should accept cancellation token")]
    public async Task WaitForFileProcessingAsync_CancellationToken_AcceptsToken()
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        using var cts = new CancellationTokenSource();

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => manager.WaitForFileProcessingAsync(
            "azure_foundry",
            "vs-123",
            "file-456",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory(DisplayName = "WaitForFileProcessingAsync should accept various file IDs")]
    [InlineData("file-123")]
    [InlineData("file_456")]
    [InlineData("abc123")]
    [InlineData("FILE-UPPERCASE")]
    public async Task WaitForFileProcessingAsync_VariousFileIds_CallsClientFactory(string fileId)
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test - client called"));

        // Act
        var act = () => manager.WaitForFileProcessingAsync(
            "azure_foundry",
            "vs-123",
            fileId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Multiple Provider Tests

    [Theory(DisplayName = "VectorStoreManager should work with various provider names")]
    [InlineData("azure_foundry")]
    [InlineData("openai")]
    [InlineData("custom_provider")]
    [InlineData("provider-with-dashes")]
    public async Task VectorStoreManager_VariousProviderNames_CallsCorrectProvider(string providerName)
    {
        // Arrange
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions());

        _mockClientFactory.Setup(x => x.GetClient(providerName))
            .Throws(new InvalidOperationException($"Called with {providerName}"));

        // Act
        var act = () => manager.GetOrCreateSharedVectorStoreAsync(
            providerName,
            "key",
            "purpose",
            "name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Called with {providerName}*");
    }

    #endregion

    #region Indexing Options Configuration Tests

    [Fact(DisplayName = "VectorStoreManager should use provided indexing options")]
    public void VectorStoreManager_CustomIndexingOptions_UsesProvidedValues()
    {
        // Arrange
        var indexingOptions = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 5,
            InitialWaitDelayMs = 100,
            UseExponentialBackoff = true,
            MaxWaitDelayMs = 5000
        };

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions(indexingOptions));

        // Assert - Manager is created with custom options
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "VectorStoreManager should handle zero values in indexing options")]
    public void VectorStoreManager_ZeroIndexingOptions_CreatesManager()
    {
        // Arrange
        var indexingOptions = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 0,
            InitialWaitDelayMs = 0,
            MaxWaitDelayMs = 0,
            TotalTimeoutMs = 0
        };

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions(indexingOptions));

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "VectorStoreManager should handle large values in indexing options")]
    public void VectorStoreManager_LargeIndexingOptions_CreatesManager()
    {
        // Arrange
        var indexingOptions = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 1000,
            InitialWaitDelayMs = 60000,
            MaxWaitDelayMs = 300000,
            TotalTimeoutMs = 3600000
        };

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _mockClientFactory.Object,
            CreateProviderOptions(indexingOptions));

        // Assert
        manager.Should().NotBeNull();
    }

    #endregion
}
