using Microsoft.Agents.AI;

namespace Cyclotron.Maf.AgentSdk.Models;

/// <summary>
/// Implementation of <see cref="IStructuredOutputAgentResponse{T}"/> that holds both
/// structured output and the original agent response.
/// </summary>
/// <typeparam name="T">The type of the structured output.</typeparam>
public sealed class StructuredOutputAgentResponse<T> : IStructuredOutputAgentResponse<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredOutputAgentResponse{T}"/> class.
    /// </summary>
    /// <param name="structuredOutput">The deserialized structured output of type T.</param>
    /// <param name="originalResponse">The original agent response containing message history and metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null.</exception>
    public StructuredOutputAgentResponse(T structuredOutput, AgentResponse originalResponse)
    {
        ArgumentNullException.ThrowIfNull(structuredOutput, nameof(structuredOutput));
        ArgumentNullException.ThrowIfNull(originalResponse, nameof(originalResponse));

        StructuredOutput = structuredOutput;
        OriginalResponse = originalResponse;
    }

    /// <inheritdoc/>
    public T StructuredOutput { get; }

    /// <inheritdoc/>
    public AgentResponse OriginalResponse { get; }
}
