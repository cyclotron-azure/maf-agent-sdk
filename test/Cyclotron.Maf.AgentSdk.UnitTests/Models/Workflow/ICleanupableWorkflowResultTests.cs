using Cyclotron.Maf.AgentSdk.Models.Workflow;
using AwesomeAssertions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Models.Workflow;

/// <summary>
/// Unit tests for the <see cref="ICleanupableWorkflowResult"/> interface.
/// Tests a concrete implementation to verify the contract.
/// </summary>
public class ICleanupableWorkflowResultTests
{
    /// <summary>
    /// Test implementation of ICleanupableWorkflowResult for testing purposes.
    /// </summary>
    private class TestCleanupableWorkflowResult : ICleanupableWorkflowResult
    {
        private readonly List<string> _fileIds;
        private readonly List<string> _vectorStoreIds;
        private readonly List<string> _agentIds;

        public TestCleanupableWorkflowResult(
            IEnumerable<string>? fileIds = null,
            IEnumerable<string>? vectorStoreIds = null,
            IEnumerable<string>? agentIds = null,
            string action = "default")
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

    #region FileIds Tests

    [Fact(DisplayName = "FileIds should return empty list when no files provided")]
    public void FileIds_NoFilesProvided_ReturnsEmptyList()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.FileIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "FileIds should return provided file IDs")]
    public void FileIds_WithFiles_ReturnsFileIds()
    {
        // Arrange
        var fileIds = new[] { "file-001", "file-002", "file-003" };
        var result = new TestCleanupableWorkflowResult(fileIds: fileIds);

        // Assert
        result.FileIds.Should().HaveCount(3);
        result.FileIds.Should().Contain("file-001");
        result.FileIds.Should().Contain("file-002");
        result.FileIds.Should().Contain("file-003");
    }

    [Fact(DisplayName = "FileIds should be read-only")]
    public void FileIds_IsReadOnly()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert - Should be IReadOnlyList
        result.FileIds.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    #endregion

    #region VectorStoreIds Tests

    [Fact(DisplayName = "VectorStoreIds should return empty list when no vector stores provided")]
    public void VectorStoreIds_NoVectorStoresProvided_ReturnsEmptyList()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.VectorStoreIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "VectorStoreIds should return provided vector store IDs")]
    public void VectorStoreIds_WithVectorStores_ReturnsVectorStoreIds()
    {
        // Arrange
        var vectorStoreIds = new[] { "vs-001", "vs-002" };
        var result = new TestCleanupableWorkflowResult(vectorStoreIds: vectorStoreIds);

        // Assert
        result.VectorStoreIds.Should().HaveCount(2);
        result.VectorStoreIds.Should().Contain("vs-001");
        result.VectorStoreIds.Should().Contain("vs-002");
    }

    [Fact(DisplayName = "VectorStoreIds should be read-only")]
    public void VectorStoreIds_IsReadOnly()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.VectorStoreIds.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    #endregion

    #region AgentIds Tests

    [Fact(DisplayName = "AgentIds should return empty list when no agents provided")]
    public void AgentIds_NoAgentsProvided_ReturnsEmptyList()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.AgentIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "AgentIds should return provided agent IDs")]
    public void AgentIds_WithAgents_ReturnsAgentIds()
    {
        // Arrange
        var agentIds = new[] { "agent-001" };
        var result = new TestCleanupableWorkflowResult(agentIds: agentIds);

        // Assert
        result.AgentIds.Should().HaveCount(1);
        result.AgentIds.Should().Contain("agent-001");
    }

    [Fact(DisplayName = "AgentIds should be read-only")]
    public void AgentIds_IsReadOnly()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.AgentIds.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    #endregion

    #region Action Tests

    [Fact(DisplayName = "Action should return default value when not specified")]
    public void Action_DefaultValue_ReturnsDefault()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult();

        // Assert
        result.Action.Should().Be("default");
    }

    [Fact(DisplayName = "Action should return provided action")]
    public void Action_WithValue_ReturnsAction()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult(action: "classify");

        // Assert
        result.Action.Should().Be("classify");
    }

    [Fact(DisplayName = "Action should handle empty string")]
    public void Action_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var result = new TestCleanupableWorkflowResult(action: "");

        // Assert
        result.Action.Should().BeEmpty();
    }

    #endregion

    #region Combined Tests

    [Fact(DisplayName = "Result should contain all resource IDs")]
    public void Result_AllResourceIds_ContainsAllIds()
    {
        // Arrange
        var fileIds = new[] { "file-1", "file-2" };
        var vectorStoreIds = new[] { "vs-1" };
        var agentIds = new[] { "agent-1", "agent-2", "agent-3" };

        var result = new TestCleanupableWorkflowResult(
            fileIds: fileIds,
            vectorStoreIds: vectorStoreIds,
            agentIds: agentIds,
            action: "process");

        // Assert
        result.FileIds.Should().HaveCount(2);
        result.VectorStoreIds.Should().HaveCount(1);
        result.AgentIds.Should().HaveCount(3);
        result.Action.Should().Be("process");
    }

    #endregion
}
