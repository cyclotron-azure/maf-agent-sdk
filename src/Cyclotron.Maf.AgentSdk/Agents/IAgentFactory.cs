namespace Cyclotron.Maf.AgentSdk.Agents;

/// <summary>
/// Simplified factory interface for creating AI agents.
/// Uses keyed services pattern to eliminate type-specific interfaces.
/// </summary>
public interface IAgentFactory : IAgentOrchestrator
{
}