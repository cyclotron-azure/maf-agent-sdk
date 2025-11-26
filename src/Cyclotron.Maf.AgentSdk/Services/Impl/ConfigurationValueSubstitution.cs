using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// Implementation of <see cref="IConfigurationValueSubstitution"/> that resolves
/// {VARIABLE_NAME} patterns from the IConfiguration hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// This service uses a regex pattern to find all {VARIABLE_NAME} references in a string
/// and replaces them with values from IConfiguration. Nested keys are supported using
/// the standard IConfiguration path syntax (e.g., "{Section:Key:Nested}").
/// </para>
/// <para>
/// If a variable is not found in configuration, it is left unchanged in the output
/// and a warning is logged.
/// </para>
/// </remarks>
public partial class ConfigurationValueSubstitution(
    IConfiguration configuration,
    ILogger<ConfigurationValueSubstitution> logger) : IConfigurationValueSubstitution
{

    // Regex pattern to match {VARIABLE_NAME} or {Section:Key:Nested}
    [GeneratedRegex(@"\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    /// <inheritdoc/>
    public string Substitute(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return VariablePattern().Replace(input, match =>
        {
            var variableName = match.Groups[1].Value;
            var value = configuration[variableName];

            if (value == null)
            {
                logger.LogWarning(
                    "Configuration variable '{VariableName}' not found in IConfiguration. Leaving unsubstituted.",
                    variableName);
                return match.Value; // Keep original {VARIABLE_NAME} if not found
            }

            logger.LogDebug(
                "Substituted configuration variable '{VariableName}' with value from IConfiguration",
                variableName);

            return value;
        });
    }

    /// <inheritdoc/>
    public string? SubstituteNullable(string? input)
    {
        if (input == null)
        {
            return null;
        }

        return Substitute(input);
    }
}
