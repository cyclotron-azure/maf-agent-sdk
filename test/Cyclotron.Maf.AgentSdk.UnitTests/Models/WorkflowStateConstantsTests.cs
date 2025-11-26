using Cyclotron.Maf.AgentSdk.Models.Workflow;
using AwesomeAssertions;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Models;

/// <summary>
/// Unit tests for the <see cref="WorkflowStateConstants"/> class.
/// Tests constant values and scope name generation.
/// </summary>
public class WorkflowStateConstantsTests
{
    #region Constant Value Tests

    [Fact(DisplayName = "WorkflowInstanceIdKey should be 'WorkflowInstanceId'")]
    public void WorkflowInstanceIdKey_Value_IsCorrect()
    {
        // Assert
        WorkflowStateConstants.WorkflowInstanceIdKey.Should().Be("WorkflowInstanceId");
    }

    [Fact(DisplayName = "FileContentStateScope should be 'FileContentStateScope'")]
    public void FileContentStateScope_Value_IsCorrect()
    {
        // Assert
        WorkflowStateConstants.FileContentStateScope.Should().Be("FileContentStateScope");
    }

    [Fact(DisplayName = "VectorStorageStateScope should be 'VectorStorageStateScope'")]
    public void VectorStorageStateScope_Value_IsCorrect()
    {
        // Assert
        WorkflowStateConstants.VectorStorageStateScope.Should().Be("VectorStorageStateScope");
    }

    #endregion

    #region GetScopeName Tests

    [Fact(DisplayName = "GetScopeName should combine base scope with workflow instance ID")]
    public void GetScopeName_ValidInputs_ReturnsCombinedName()
    {
        // Arrange
        const string workflowInstanceId = "abc123";
        const string baseScopeName = "TestScope";

        // Act
        var result = WorkflowStateConstants.GetScopeName(workflowInstanceId, baseScopeName);

        // Assert
        result.Should().Be("TestScope_abc123");
    }

    [Fact(DisplayName = "GetScopeName should work with FileContentStateScope")]
    public void GetScopeName_WithFileContentStateScope_ReturnsCorrectName()
    {
        // Arrange
        const string workflowInstanceId = "workflow-123";

        // Act
        var result = WorkflowStateConstants.GetScopeName(
            workflowInstanceId,
            WorkflowStateConstants.FileContentStateScope);

        // Assert
        result.Should().Be("FileContentStateScope_workflow-123");
    }

    [Fact(DisplayName = "GetScopeName should work with VectorStorageStateScope")]
    public void GetScopeName_WithVectorStorageStateScope_ReturnsCorrectName()
    {
        // Arrange
        const string workflowInstanceId = "workflow-456";

        // Act
        var result = WorkflowStateConstants.GetScopeName(
            workflowInstanceId,
            WorkflowStateConstants.VectorStorageStateScope);

        // Assert
        result.Should().Be("VectorStorageStateScope_workflow-456");
    }

    [Fact(DisplayName = "GetScopeName should handle empty workflow instance ID")]
    public void GetScopeName_EmptyWorkflowInstanceId_ReturnsBaseScopeWithUnderscore()
    {
        // Arrange
        const string baseScopeName = "TestScope";

        // Act
        var result = WorkflowStateConstants.GetScopeName(string.Empty, baseScopeName);

        // Assert
        result.Should().Be("TestScope_");
    }

    [Fact(DisplayName = "GetScopeName should handle empty base scope name")]
    public void GetScopeName_EmptyBaseScopeName_ReturnsUnderscoreWithId()
    {
        // Arrange
        const string workflowInstanceId = "workflow-789";

        // Act
        var result = WorkflowStateConstants.GetScopeName(workflowInstanceId, string.Empty);

        // Assert
        result.Should().Be("_workflow-789");
    }

    [Fact(DisplayName = "GetScopeName should handle GUID-formatted workflow instance ID")]
    public void GetScopeName_GuidWorkflowInstanceId_ReturnsCorrectName()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid().ToString();
        const string baseScopeName = "WorkflowScope";

        // Act
        var result = WorkflowStateConstants.GetScopeName(workflowInstanceId, baseScopeName);

        // Assert
        result.Should().StartWith("WorkflowScope_");
        result.Should().EndWith(workflowInstanceId);
    }

    #endregion
}
