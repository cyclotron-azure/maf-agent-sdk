namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Contains the collection of model provider configurations.
/// </summary>
public class ModelProviderOptions
{
    /// <summary>
    /// Dictionary of provider configurations keyed by provider name.
    /// </summary>
    public Dictionary<string, ModelProviderDefinitionOptions> Providers { get; set; } = [];
    /// <summary>
    /// Default provider name to use when not specified.
    /// If not set, uses the first provider in the dictionary.
    /// </summary>
    public string? DefaultProviderName { get; set; }

    /// <summary>
    /// Configuration for vector store indexing behavior.
    /// </summary>
    public VectorStoreIndexingOptions VectorStoreIndexing { get; set; } = new();

    /// <summary>
    /// Gets the default provider name (configured or first available).
    /// </summary>
    public string GetDefaultProviderName()
    {
        if (!string.IsNullOrEmpty(DefaultProviderName) && Providers.ContainsKey(DefaultProviderName))
        {
            return DefaultProviderName;
        }

        if (Providers.Count == 0)
        {
            throw new InvalidOperationException("No providers configured in ModelProviderOptions");
        }

        return Providers.Keys.First();
    }}
