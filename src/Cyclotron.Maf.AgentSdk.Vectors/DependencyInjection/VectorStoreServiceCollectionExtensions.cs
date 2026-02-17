using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;
using Azure.AI.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering vector store services in the dependency injection container.
/// Provides vector store lifecycle management, file upload, and indexing capabilities for AI agents.
/// </summary>
public static class VectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all vector store services including store manager and configuration options.
    /// This is required to enable vector store functionality in AgentSdk applications.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="providerClientFactory">Factory delegate for creating AIProjectClient instances by provider name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreServices(
        this IServiceCollection services,
        Func<IServiceProvider, Func<string, AIProjectClient>> providerClientFactory)
    {
        ArgumentNullException.ThrowIfNull(providerClientFactory, nameof(providerClientFactory));

        // Register vector store indexing options with backward compatibility
        services.AddVectorStoreIndexingOptions();

        // Register vector store manager as scoped service
        services.AddScoped<IVectorStoreManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VectorStoreManager>>();
            var options = sp.GetRequiredService<IOptions<VectorStoreIndexingOptions>>();
            var clientFactory = providerClientFactory(sp);

            return new VectorStoreManager(logger, clientFactory, options);
        });

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="VectorStoreIndexingOptions"/> with backward-compatible configuration binding.
    /// Supports both the new path (<c>VectorStoreIndexing:</c>) and legacy path (<c>ModelProvider:VectorStoreIndexing:</c>).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreIndexingOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<VectorStoreIndexingOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                // Try new configuration path first (recommended)
                var newSection = configuration.GetSection(VectorStoreIndexingOptions.SectionName);
                if (newSection.Exists())
                {
                    newSection.Bind(options);
                    return;
                }

                // Fall back to legacy path for backward compatibility
                var legacySection = configuration.GetSection("ModelProvider:VectorStoreIndexing");
                if (legacySection.Exists())
                {
                    legacySection.Bind(options);

                    // Note: We can't log directly here as ILogger isn't available in Configure
                    // The deprecation will be logged when the service is first used
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add post-configuration to log deprecation warning
        services.AddSingleton<IConfigureOptions<VectorStoreIndexingOptions>>(sp =>
            new ConfigureNamedOptions<VectorStoreIndexingOptions>(name, options =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var legacySection = configuration.GetSection("ModelProvider:VectorStoreIndexing");
                var newSection = configuration.GetSection(VectorStoreIndexingOptions.SectionName);

                if (legacySection.Exists() && !newSection.Exists())
                {
                    var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("VectorStoreConfiguration");
                    logger?.LogWarning(
                        "Configuration path 'ModelProvider:VectorStoreIndexing' is deprecated. " +
                        "Please migrate to '{NewPath}' in your configuration. " +
                        "The legacy path will be removed in a future version.",
                        VectorStoreIndexingOptions.SectionName);
                }
            }));

        return services;
    }
}
