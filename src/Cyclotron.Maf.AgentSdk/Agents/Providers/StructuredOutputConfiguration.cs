using System.Reflection;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Represents the configuration for structured output using a C# type.
/// Enables automatic JSON schema generation from CLR types for agent responses.
/// </summary>
/// <remarks>
/// This configuration allows agents to be constrained to produce responses that conform
/// to a specific C# type structure. The framework automatically generates JSON schemas
/// from the type definition using reflection and type annotations.
/// </remarks>
public sealed class StructuredOutputConfiguration
{
    /// <summary>
    /// Gets the CLR type that defines the structure of the agent's output.
    /// This type must be a public class or record with properly annotated properties.
    /// </summary>
    public Type OutputType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredOutputConfiguration"/> class.
    /// </summary>
    /// <param name="outputType">The CLR type defining the output structure.</param>
    /// <exception cref="ArgumentNullException">Thrown when outputType is null.</exception>
    public StructuredOutputConfiguration(Type outputType)
    {
        ArgumentNullException.ThrowIfNull(outputType, nameof(outputType));
        OutputType = outputType;
    }

    /// <summary>
    /// Creates a structured output configuration from a type name.
    /// Useful for loading from configuration files where only type names are available.
    /// </summary>
    /// <param name="typeName">
    /// The fully qualified type name (e.g., "MyNamespace.PersonInfo" or "MyProject.Models.ResponseDTO").
    /// </param>
    /// <param name="assemblies">
    /// Optional assemblies to search. If null or empty, searches all loaded AppDomain assemblies.
    /// </param>
    /// <returns>A new StructuredOutputConfiguration if type is found.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when typeName is null/empty or type cannot be resolved in the provided assemblies.
    /// </exception>
    public static StructuredOutputConfiguration FromTypeName(string typeName, params Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type name cannot be null or empty.", nameof(typeName));
        }

        var searchAssemblies = assemblies?.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        var type = searchAssemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    // Skip assemblies that fail to load
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(t => t.FullName == typeName);

        if (type == null)
        {
            throw new ArgumentException(
                $"Cannot resolve type '{typeName}'. Ensure the type is defined in a loaded assembly and the full type name is correct.",
                nameof(typeName));
        }

        return new StructuredOutputConfiguration(type);
    }
}
