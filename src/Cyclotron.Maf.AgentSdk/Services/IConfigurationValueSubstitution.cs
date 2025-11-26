namespace Cyclotron.Maf.AgentSdk.Services;

/// <summary>
/// Service for substituting IConfiguration variable references in strings.
/// Supports syntax: {VARIABLE_NAME} which resolves from the IConfiguration hierarchy.
/// Used to dynamically resolve configuration values in agent configurations and templates.
/// </summary>
public interface IConfigurationValueSubstitution
{
    /// <summary>
    /// Substitutes all {VARIABLE_NAME} references in the input string with values from IConfiguration.
    /// Variables not found in configuration are left unchanged.
    /// </summary>
    /// <param name="input">String containing variable references (e.g., "{PROJECT_ENDPOINT}" or "{AzureAI:Endpoint}").</param>
    /// <returns>String with all found variables substituted from IConfiguration. Unfound variables remain as-is.</returns>
    /// <example>
    /// <code>
    /// // Configuration contains: PROJECT_ENDPOINT = "https://example.openai.azure.com"
    /// var result = substitution.Substitute("Endpoint: {PROJECT_ENDPOINT}");
    /// // Result: "Endpoint: https://example.openai.azure.com"
    /// </code>
    /// </example>
    string Substitute(string input);

    /// <summary>
    /// Substitutes variables in the input string, returning null if input is null.
    /// Provides null-safe variable substitution for optional configuration values.
    /// </summary>
    /// <param name="input">String containing variable references, or null.</param>
    /// <returns>String with variables substituted, or null if input was null.</returns>
    string? SubstituteNullable(string? input);
}
