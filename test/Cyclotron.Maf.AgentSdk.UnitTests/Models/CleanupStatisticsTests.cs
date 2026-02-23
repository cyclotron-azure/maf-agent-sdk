using Cyclotron.Maf.AgentSdk.Models;
using AwesomeAssertions;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Models;

/// <summary>
/// Unit tests for the <see cref="CleanupStatistics"/> record.
/// Tests computed properties TotalDeleted and TotalFailed across various scenarios.
/// </summary>
public class CleanupStatisticsTests
{
    [Fact(DisplayName = "TotalDeleted should sum all deleted counts when all are positive")]
    public void TotalDeleted_AllPositiveCounts_ReturnsSum()
    {
        // Arrange
        var statistics = new CleanupStatistics
        {
            FilesDeleted = 5,
            FilesFailedToDelete = 0,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 0,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        // Act
        var result = statistics.TotalDeleted;

        // Assert
        result.Should().Be(10);
    }

    [Fact(DisplayName = "TotalFailed should sum all failed counts when all are positive")]
    public void TotalFailed_AllPositiveCounts_ReturnsSum()
    {
        // Arrange
        var statistics = new CleanupStatistics
        {
            FilesDeleted = 0,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 0,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 0,
            AgentsFailedToDelete = 4
        };

        // Act
        var result = statistics.TotalFailed;

        // Assert
        result.Should().Be(7);
    }

    [Fact(DisplayName = "TotalDeleted should return zero when all counts are zero")]
    public void TotalDeleted_AllZeroCounts_ReturnsZero()
    {
        // Arrange
        var statistics = new CleanupStatistics();

        // Act
        var result = statistics.TotalDeleted;

        // Assert
        result.Should().Be(0);
    }

    [Fact(DisplayName = "TotalFailed should return zero when all counts are zero")]
    public void TotalFailed_AllZeroCounts_ReturnsZero()
    {
        // Arrange
        var statistics = new CleanupStatistics();

        // Act
        var result = statistics.TotalFailed;

        // Assert
        result.Should().Be(0);
    }

    [Theory(DisplayName = "TotalDeleted should correctly sum individual delete counts")]
    [InlineData(1, 0,  0, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 0,  0, 0)]
    [InlineData(0, 0,  1, 1)]
    [InlineData(10, 20,  40, 70)]
    public void TotalDeleted_VariousCombinations_ReturnsCorrectSum(
        int files, int vectorStores, int agents, int expected)
    {
        // Arrange
        var statistics = new CleanupStatistics
        {
            FilesDeleted = files,
            VectorStoresDeleted = vectorStores,
            AgentsDeleted = agents
        };

        // Act
        var result = statistics.TotalDeleted;

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName = "TotalFailed should correctly sum individual failed counts")]
    [InlineData(1, 0, 0, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(5, 10, 15, 30)]
    public void TotalFailed_VariousCombinations_ReturnsCorrectSum(
        int files, int vectorStores, int agents, int expected)
    {
        // Arrange
        var statistics = new CleanupStatistics
        {
            FilesFailedToDelete = files,
            VectorStoresFailedToDelete = vectorStores,
            AgentsFailedToDelete = agents
        };

        // Act
        var result = statistics.TotalFailed;

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "Record should be immutable and support with expressions")]
    public void WithExpression_ModifyProperty_CreatesNewInstanceWithModifiedValue()
    {
        // Arrange
        var original = new CleanupStatistics
        {
            FilesDeleted = 5,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        // Act
        var modified = original with { FilesDeleted = 100 };

        // Assert
        modified.FilesDeleted.Should().Be(100);
        original.FilesDeleted.Should().Be(5);
        modified.Should().NotBeSameAs(original);
    }

    [Fact(DisplayName = "Records with same values should be equal")]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var statistics1 = new CleanupStatistics
        {
            FilesDeleted = 5,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        var statistics2 = new CleanupStatistics
        {
            FilesDeleted = 5,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        // Act & Assert
        statistics1.Should().Be(statistics2);
        statistics1.GetHashCode().Should().Be(statistics2.GetHashCode());
    }

    [Fact(DisplayName = "Records with different values should not be equal")]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var statistics1 = new CleanupStatistics
        {
            FilesDeleted = 5,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        var statistics2 = new CleanupStatistics
        {
            FilesDeleted = 100,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 3,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 2,
            AgentsFailedToDelete = 0
        };

        // Act & Assert
        statistics1.Should().NotBe(statistics2);
    }

    [Fact(DisplayName = "Computed properties should reflect mixed deleted and failed operations")]
    public void ComputedProperties_MixedOperations_ReturnsCorrectTotals()
    {
        // Arrange - realistic scenario with partial successes
        var statistics = new CleanupStatistics
        {
            FilesDeleted = 8,
            FilesFailedToDelete = 2,
            VectorStoresDeleted = 4,
            VectorStoresFailedToDelete = 1,
            AgentsDeleted = 3,
            AgentsFailedToDelete = 2
        };

        // Act
        var totalDeleted = statistics.TotalDeleted;
        var totalFailed = statistics.TotalFailed;

        // Assert
        totalDeleted.Should().Be(15); // 8 + 4 + 3
        totalFailed.Should().Be(5);  // 2 + 1 + 2
    }

    [Fact(DisplayName = "Default record should have all properties as zero")]
    public void DefaultRecord_AllPropertiesZero()
    {
        // Arrange
        var statistics = new CleanupStatistics();

        // Assert
        statistics.FilesDeleted.Should().Be(0);
        statistics.FilesFailedToDelete.Should().Be(0);
        statistics.VectorStoresDeleted.Should().Be(0);
        statistics.VectorStoresFailedToDelete.Should().Be(0);
        statistics.AgentsDeleted.Should().Be(0);
        statistics.AgentsFailedToDelete.Should().Be(0);
    }
}
