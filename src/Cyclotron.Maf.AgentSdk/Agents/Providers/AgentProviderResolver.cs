using Cyclotron.Maf.AgentSdk.Common.Options;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Default resolver for provider implementations based on provider type.
/// </summary>
internal sealed class AgentProviderResolver(IEnumerable<IAgentProvider> providers) : IAgentProviderResolver
{
    private readonly IReadOnlyList<IAgentProvider> _providers = (providers ?? throw new ArgumentNullException(nameof(providers)))
        .ToList();

    /// <inheritdoc/>
    public IAgentProvider Resolve(ModelProviderDefinitionOptions provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));

        if (string.IsNullOrWhiteSpace(provider.Type))
        {
            throw new InvalidOperationException("Provider type is required for agent creation.");
        }

        var match = _providers.FirstOrDefault(
            candidate => candidate.SupportedProviderTypes.Contains(provider.Type, StringComparer.OrdinalIgnoreCase));

        if (match != null)
        {
            return match;
        }

        var supportedTypes = _providers
            .SelectMany(candidate => candidate.SupportedProviderTypes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type)
            .ToArray();

        throw new InvalidOperationException(
            $"No agent provider is registered for type '{provider.Type}'. " +
            $"Supported provider types: {string.Join(", ", supportedTypes)}");
    }
}
