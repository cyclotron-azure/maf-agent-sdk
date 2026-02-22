using Cyclotron.Maf.AgentSdk.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;
using AwesomeAssertions;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Models;

/// <summary>
/// Unit tests for the <see cref="StructuredOutputAgentResponse{T}"/> class.
/// Tests instantiation, property access, and proper preservation of both structured output and original response.
/// </summary>
public class StructuredOutputAgentResponseTests
{
    [Fact(DisplayName = "Constructor should initialize with structured output and original response")]
    public void Constructor_ValidParameters_InitializesProperties()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Test", Value = 42 };
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(structuredOutput, originalResponse);

        // Assert
        response.StructuredOutput.Should().Be(structuredOutput);
        response.OriginalResponse.Should().BeSameAs(originalResponse);
    }

    [Fact(DisplayName = "StructuredOutput property should return the provided structured output")]
    public void StructuredOutputProperty_ValidData_ReturnsStructuredOutput()
    {
        // Arrange
        var expectedOutput = new TestData { Name = "Alice", Value = 100 };
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(expectedOutput, originalResponse);

        // Assert
        response.StructuredOutput.Should().Be(expectedOutput);
        response.StructuredOutput.Name.Should().Be("Alice");
        response.StructuredOutput.Value.Should().Be(100);
    }

    [Fact(DisplayName = "OriginalResponse property should return the provided agent response")]
    public void OriginalResponseProperty_ValidResponse_ReturnsOriginalResponse()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Bob", Value = 50 };
        var expectedResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(structuredOutput, expectedResponse);

        // Assert
        response.OriginalResponse.Should().BeSameAs(expectedResponse);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when structured output is null")]
    public void Constructor_NullStructuredOutput_ThrowsArgumentNullException()
    {
        // Arrange
        var originalResponse = CreateMockResponse();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new StructuredOutputAgentResponse<TestData>(null!, originalResponse));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException when original response is null")]
    public void Constructor_NullOriginalResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Charlie", Value = 75 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new StructuredOutputAgentResponse<TestData>(structuredOutput, null!));
    }

    [Fact(DisplayName = "Should support different structured output types")]
    public void Constructor_DifferentTypes_SupportsStringOutput()
    {
        // Arrange
        var stringOutput = "Test output string";
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<string>(stringOutput, originalResponse);

        // Assert
        response.StructuredOutput.Should().Be("Test output string");
        response.OriginalResponse.Should().BeSameAs(originalResponse);
    }

    [Fact(DisplayName = "Should support complex nested types")]
    public void Constructor_ComplexType_SupportsNestedStructures()
    {
        // Arrange
        var complexOutput = new ComplexTestData
        {
            Id = 123,
            Nested = new TestData { Name = "Nested", Value = 999 },
            Items = new[] { "item1", "item2", "item3" }
        };
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<ComplexTestData>(complexOutput, originalResponse);

        // Assert
        response.StructuredOutput.Id.Should().Be(123);
        response.StructuredOutput.Nested.Should().NotBeNull();
        response.StructuredOutput.Nested!.Name.Should().Be("Nested");
        response.StructuredOutput.Nested!.Value.Should().Be(999);
        response.StructuredOutput.Items.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Should implement IStructuredOutputAgentResponse<T> interface")]
    public void InterfaceImplementation_ShouldImplementInterface()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Test", Value = 42 };
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(structuredOutput, originalResponse);

        // Assert
        response.Should().BeAssignableTo<IStructuredOutputAgentResponse<TestData>>();
    }

    [Fact(DisplayName = "Should preserve original response metadata from agent")]
    public void PreservesOriginalResponseMetadata_MultipleMessages_PreservesHistory()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Test", Value = 42 };
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "test message 1"),
            new ChatMessage(ChatRole.Assistant, "test response 1")
        };

        // Create AgentResponse with messages (using constructor if available)
        var originalResponse = new AgentResponse();
        originalResponse.GetType().GetProperty("Messages")?.SetValue(originalResponse, messages);

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(structuredOutput, originalResponse);

        // Assert
        response.OriginalResponse.Messages.Should().HaveCount(2);
        response.OriginalResponse.Messages[0].Role.Should().Be(ChatRole.User);
        response.OriginalResponse.Messages[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact(DisplayName = "Should allow access to both structured and raw response simultaneously")]
    public void BothPropertiesAccessible_StructuredAndRaw_CanAccessBoth()
    {
        // Arrange
        var structuredOutput = new TestData { Name = "Combined", Value = 555 };
        var originalResponse = CreateMockResponse();

        // Act
        var response = new StructuredOutputAgentResponse<TestData>(structuredOutput, originalResponse);

        // Assert
        var structured = response.StructuredOutput;
        var raw = response.OriginalResponse;

        structured.Name.Should().Be("Combined");
        raw.Should().NotBeNull();

        // Verify they're both accessible and refer to same wrapper
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Test helper class for structured output.
    /// </summary>
    private record TestData
    {
        public string? Name { get; init; }
        public int Value { get; init; }
    }

    /// <summary>
    /// Complex test data for nested structure testing.
    /// </summary>
    private record ComplexTestData
    {
        public int Id { get; init; }
        public TestData? Nested { get; init; }
        public string[]? Items { get; init; }
    }

    /// <summary>
    /// Helper method to create a mock AgentResponse.
    /// </summary>
    private static AgentResponse CreateMockResponse()
    {
        return new AgentResponse();
    }
}
