using Microsoft.Agents.AI;

namespace Cyclotron.Maf.AgentSdk.Models;

/// <summary>
/// Represents an agent response containing both structured output and the original agent response.
/// Allows consumers to access both the parsed strongly-typed result and the underlying response metadata.
/// </summary>
/// <typeparam name="T">The type of the structured output.</typeparam>
public interface IStructuredOutputAgentResponse<T>
{
    /// <summary>
    /// Gets the structured output deserialized to type T.
    /// </summary>
    T StructuredOutput { get; }

    /// <summary>
    /// Gets the original agent response from which the structured output was generated.
    /// Contains message history, continuation tokens, and other response metadata.
    /// </summary>
    AgentResponse OriginalResponse { get; }
}
