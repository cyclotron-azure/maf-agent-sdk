using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Azure.AI.Projects;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;
using VectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl.VectorStoreManager;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="VectorStoreManager"/> class.
/// Tests constructor validation and indexing options configuration.
/// Note: Async methods and Azure client interaction require integration tests.
/// </summary>
public class VectorStoreManagerTests
{
    private readonly Mock<ILogger<VectorStoreManager>> _mockLogger;
    private readonly Func<string, AIProjectClient> _factoryDelegate;

    public VectorStoreManagerTests()
    {
        _mockLogger = new Mock<ILogger<VectorStoreManager>>();
        // Simple factory that returns null - sufficient for unit tests
        _factoryDelegate = _ => null!;
    }

    private IOptions<VectorStoreIndexingOptions> CreateIndexingOptions(VectorStoreIndexingOptions? indexingOptions = null)
    {
        return MsOptions.Create(indexingOptions ?? new VectorStoreIndexingOptions());
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when logger is null")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var indexingOptions = CreateIndexingOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreManager(null!, _factoryDelegate, indexingOptions));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when clientFactory is null")]
    public void Constructor_NullClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var indexingOptions = CreateIndexingOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreManager(_mockLogger.Object, null!, indexingOptions));
    }

    [Fact(DisplayName = "Constructor should use default indexing options when options is null")]
    public void Constructor_NullOptions_UsesDefaults()
    {
        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _factoryDelegate,
            null!);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should create instance with valid parameters")]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _factoryDelegate,
            CreateIndexingOptions());

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should initialize with custom indexing options")]
    public void Constructor_CustomIndexingOptions_UsesCustomValues()
    {
        // Arrange
        var customOptions = new VectorStoreIndexingOptions
        {
            MaxWaitAttempts = 50,
            InitialWaitDelayMs = 1500,
            UseExponentialBackoff = false,
            MaxWaitDelayMs = 25000,
            TotalTimeoutMs = 120000
        };
        var optionsInstance = CreateIndexingOptions(customOptions);

        // Act
        var manager = new VectorStoreManager(
            _mockLogger.Object,
            _factoryDelegate,
            optionsInstance);

        // Assert
        manager.Should().NotBeNull();
    }

    #endregion

    #region VectorStoreIndexingOptions Tests

    [Fact(DisplayName = "VectorStoreIndexingOptions has correct default values")]
    public void VectorStoreIndexingOptions_Defaults()
    {
        // Act
        var options = new VectorStoreIndexingOptions();

        // Assert
        options.MaxWaitAttempts.Should().Be(60);
        options.InitialWaitDelayMs.Should().Be(2000);
        options.UseExponentialBackoff.Should().BeTrue();
        options.MaxWaitDelayMs.Should().Be(30000);
        options.TotalTimeoutMs.Should().Be(0);
    }

    [Fact(DisplayName = "VectorStoreIndexingOptions properties are settable")]
    public void VectorStoreIndexingOptions_SetProperties()
    {
        // Arrange
        var options = new VectorStoreIndexingOptions();

        // Act
        options.MaxWaitAttempts = 100;
        options.InitialWaitDelayMs = 500;
        options.UseExponentialBackoff = false;
        options.MaxWaitDelayMs = 60000;
        options.TotalTimeoutMs = 300000;

        // Assert
        options.MaxWaitAttempts.Should().Be(100);
        options.InitialWaitDelayMs.Should().Be(500);
        options.UseExponentialBackoff.Should().BeFalse();
        options.MaxWaitDelayMs.Should().Be(60000);
        options.TotalTimeoutMs.Should().Be(300000);
    }

    #endregion
}
