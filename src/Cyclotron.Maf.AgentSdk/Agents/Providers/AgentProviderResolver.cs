using Cyclotron.Maf.AgentSdk.Common.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Cyclotron.Maf.AgentSdk.Agents.Providers;

/// <summary>
/// Default resolver for provider implementations based on provider type.
/// Creates a service scope when resolving providers to allow dependency on scoped services like IProviderClientFactory.
/// </summary>
internal sealed class AgentProviderResolver(IServiceProvider serviceProvider) : IAgentProviderResolver
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc/>
    public IAgentProvider Resolve(ModelProviderDefinitionOptions provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));

        if (string.IsNullOrWhiteSpace(provider.Type))
        {
            throw new InvalidOperationException("Provider type is required for agent creation.");
        }

        // Create a scope to resolve scoped dependencies (like IProviderClientFactory)
        // This allows transient providers to safely depend on scoped services
        using var scope = _serviceProvider.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IAgentProvider>();
        var match = providers.FirstOrDefault(
            candidate => candidate.SupportedProviderTypes.Contains(provider.Type, StringComparer.OrdinalIgnoreCase));

        if (match != null)
        {
            return match;
        }

        var supportedTypes = providers
            .SelectMany(candidate => candidate.SupportedProviderTypes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type)
            .ToArray();

        throw new InvalidOperationException(
            $"No agent provider is registered for type '{provider.Type}'. " +
            $"Supported provider types: {string.Join(", ", supportedTypes)}");
    }
}
