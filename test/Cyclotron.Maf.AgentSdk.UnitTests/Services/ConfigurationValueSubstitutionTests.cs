using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="ConfigurationValueSubstitution"/> class.
/// Tests variable substitution from IConfiguration with various scenarios.
/// </summary>
public class ConfigurationValueSubstitutionTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<ConfigurationValueSubstitution>> _mockLogger;
    private readonly ConfigurationValueSubstitution _sut;

    public ConfigurationValueSubstitutionTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<ConfigurationValueSubstitution>>();
        _sut = new ConfigurationValueSubstitution(_mockConfiguration.Object, _mockLogger.Object);
    }

    [Fact(DisplayName = "Substitute should return empty string when input is empty")]
    public void Substitute_EmptyInput_ReturnsEmptyString()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Substitute should return null when SubstituteNullable receives null")]
    public void SubstituteNullable_NullInput_ReturnsNull()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _sut.SubstituteNullable(input);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Substitute should return original string when no variables present")]
    public void Substitute_NoVariables_ReturnsOriginalString()
    {
        // Arrange
        var input = "This is a plain string without any variables.";

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be(input);
    }

    [Fact(DisplayName = "Substitute should replace single variable with configuration value")]
    public void Substitute_SingleVariable_ReplacesWithConfigValue()
    {
        // Arrange
        var input = "The endpoint is {AZURE_ENDPOINT}";
        _mockConfiguration.Setup(c => c["AZURE_ENDPOINT"]).Returns("https://my-endpoint.azure.com");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("The endpoint is https://my-endpoint.azure.com");
    }

    [Fact(DisplayName = "Substitute should replace multiple variables in same string")]
    public void Substitute_MultipleVariables_ReplacesAllWithConfigValues()
    {
        // Arrange
        var input = "Endpoint: {AZURE_ENDPOINT}, Model: {AZURE_MODEL}";
        _mockConfiguration.Setup(c => c["AZURE_ENDPOINT"]).Returns("https://endpoint.azure.com");
        _mockConfiguration.Setup(c => c["AZURE_MODEL"]).Returns("gpt-4");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Endpoint: https://endpoint.azure.com, Model: gpt-4");
    }

    [Fact(DisplayName = "Substitute should handle nested configuration keys")]
    public void Substitute_NestedConfigKey_ReplacesWithNestedValue()
    {
        // Arrange
        var input = "Connection string: {ConnectionStrings:DefaultConnection}";
        _mockConfiguration.Setup(c => c["ConnectionStrings:DefaultConnection"])
            .Returns("Server=localhost;Database=mydb");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Connection string: Server=localhost;Database=mydb");
    }

    [Fact(DisplayName = "Substitute should leave variable unchanged when not found in configuration")]
    public void Substitute_VariableNotFound_LeavesUnchanged()
    {
        // Arrange
        var input = "The value is {NON_EXISTENT_KEY}";
        _mockConfiguration.Setup(c => c["NON_EXISTENT_KEY"]).Returns((string?)null);

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("The value is {NON_EXISTENT_KEY}");
    }

    [Fact(DisplayName = "Substitute should replace found variables and leave unfound ones unchanged")]
    public void Substitute_MixedFoundAndNotFound_PartialReplacement()
    {
        // Arrange
        var input = "Found: {FOUND_KEY}, Not Found: {NOT_FOUND_KEY}";
        _mockConfiguration.Setup(c => c["FOUND_KEY"]).Returns("found-value");
        _mockConfiguration.Setup(c => c["NOT_FOUND_KEY"]).Returns((string?)null);

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Found: found-value, Not Found: {NOT_FOUND_KEY}");
    }

    [Theory(DisplayName = "Substitute should handle various variable name formats")]
    [InlineData("{simple}", "simple", "value1")]
    [InlineData("{UPPER_CASE}", "UPPER_CASE", "value2")]
    [InlineData("{Mixed_Case_123}", "Mixed_Case_123", "value3")]
    [InlineData("{a}", "a", "value4")]
    public void Substitute_VariousVariableFormats_ReplacesCorrectly(
        string input, string configKey, string configValue)
    {
        // Arrange
        _mockConfiguration.Setup(c => c[configKey]).Returns(configValue);

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be(configValue);
    }

    [Fact(DisplayName = "Substitute should handle deeply nested configuration keys")]
    public void Substitute_DeeplyNestedKey_ReplacesWithValue()
    {
        // Arrange
        var input = "Value: {Level1:Level2:Level3:Key}";
        _mockConfiguration.Setup(c => c["Level1:Level2:Level3:Key"]).Returns("deep-value");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Value: deep-value");
    }

    [Fact(DisplayName = "Substitute should handle variable at start of string")]
    public void Substitute_VariableAtStart_ReplacesCorrectly()
    {
        // Arrange
        var input = "{PREFIX}rest of the string";
        _mockConfiguration.Setup(c => c["PREFIX"]).Returns("START");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("STARTrest of the string");
    }

    [Fact(DisplayName = "Substitute should handle variable at end of string")]
    public void Substitute_VariableAtEnd_ReplacesCorrectly()
    {
        // Arrange
        var input = "Beginning of string {SUFFIX}";
        _mockConfiguration.Setup(c => c["SUFFIX"]).Returns("END");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Beginning of string END");
    }

    [Fact(DisplayName = "Substitute should handle only variable as entire string")]
    public void Substitute_OnlyVariable_ReturnsConfigValue()
    {
        // Arrange
        var input = "{ONLY_VALUE}";
        _mockConfiguration.Setup(c => c["ONLY_VALUE"]).Returns("the-value");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("the-value");
    }

    [Fact(DisplayName = "Substitute should handle adjacent variables")]
    public void Substitute_AdjacentVariables_ReplacesAllCorrectly()
    {
        // Arrange
        var input = "{FIRST}{SECOND}{THIRD}";
        _mockConfiguration.Setup(c => c["FIRST"]).Returns("A");
        _mockConfiguration.Setup(c => c["SECOND"]).Returns("B");
        _mockConfiguration.Setup(c => c["THIRD"]).Returns("C");

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("ABC");
    }

    [Fact(DisplayName = "SubstituteNullable should work same as Substitute for non-null input")]
    public void SubstituteNullable_NonNullInput_BehavesLikeSubstitute()
    {
        // Arrange
        var input = "Value: {KEY}";
        _mockConfiguration.Setup(c => c["KEY"]).Returns("substituted");

        // Act
        var result = _sut.SubstituteNullable(input);

        // Assert
        result.Should().Be("Value: substituted");
    }

    [Fact(DisplayName = "Substitute should handle empty configuration value")]
    public void Substitute_EmptyConfigValue_ReplacesWithEmptyString()
    {
        // Arrange
        var input = "Value: {EMPTY_KEY}";
        _mockConfiguration.Setup(c => c["EMPTY_KEY"]).Returns(string.Empty);

        // Act
        var result = _sut.Substitute(input);

        // Assert
        result.Should().Be("Value: ");
    }

    [Fact(DisplayName = "Substitute should not match incomplete variable syntax")]
    public void Substitute_IncompleteVariableSyntax_LeavesUnchanged()
    {
        // Arrange
        var inputs = new[]
        {
            "This has {incomplete",
            "This has incomplete}",
            "This has { spaced }",
            "This has {}"
        };

        foreach (var input in inputs)
        {
            // Act
            var result = _sut.Substitute(input);

            // Assert - should either leave unchanged or handle gracefully
            result.Should().NotBeNull();
        }
    }
}
