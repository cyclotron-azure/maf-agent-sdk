using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Microsoft.Extensions.Options;

namespace Cyclotron.Maf.AgentSdk.Vectors.Internal;


/// <summary>
/// Adapter that wraps VectorStoreManagerFactory to provide direct IVectorStoreManager interface.
/// Delegates all operations to the factory-selected implementation based on provider name.
/// </summary>
internal class VectorStoreManagerAdapter(
    VectorStoreManagerFactory factory,
    IOptions<ModelProviderOptions> modelProviderOptions) : IVectorStoreManager
{
    private readonly VectorStoreManagerFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly IOptions<ModelProviderOptions> _modelProviderOptions = modelProviderOptions ?? throw new ArgumentNullException(nameof(modelProviderOptions));

    private IVectorStoreManager GetManager(string providerName)
    {
        try
        {
            var options = _modelProviderOptions.Value;
            if (!options.Providers.TryGetValue(providerName, out var providerDef))
            {
                var availableProviders = string.Join(", ", options.Providers.Keys);
                throw new InvalidOperationException(
                    $"Provider '{providerName}' not found in ModelProviderOptions configuration. " +
                    $"Available providers: {(string.IsNullOrEmpty(availableProviders) ? "none" : availableProviders)}");
            }

            var providerType = providerDef.Type;
            if (string.IsNullOrWhiteSpace(providerType))
            {
                throw new InvalidOperationException($"Provider type for '{providerName}' is not configured");
            }

            return _factory.GetManager(providerName, providerType);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get manager for provider '{providerName}'", ex);
        }
    }

    /// <inheritdoc/>
    public Task<string> GetOrCreateSharedVectorStoreAsync(
        string providerName,
        string key,
        string purpose,
        string name,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .GetOrCreateSharedVectorStoreAsync(providerName, key, purpose, name, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CleanupVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .CleanupVectorStoreAsync(providerName, vectorStoreId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> AddFileToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        Stream fileContent,
        string fileName,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .AddFileToVectorStoreAsync(providerName, vectorStoreId, fileContent, fileName, chunkingDelegate, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> AddFilesToVectorStoreAsync(
        string providerName,
        string vectorStoreId,
        IEnumerable<(Stream Content, string FileName)> files,
        Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>> chunkingDelegate,
        CancellationToken cancellationToken = default)
    {
        return GetManager(providerName)
            .AddFilesToVectorStoreAsync(providerName, vectorStoreId, files, chunkingDelegate, cancellationToken);
    }
}
