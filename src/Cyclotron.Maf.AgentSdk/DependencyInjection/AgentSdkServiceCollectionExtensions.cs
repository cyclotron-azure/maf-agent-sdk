
using Cyclotron.Maf.AgentSdk.Agents.Providers;
using Cyclotron.Maf.AgentSdk.Common.Options;
using Cyclotron.Maf.AgentSdk.Common.Services;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AgentSdk services in the dependency injection container.
/// Provides configuration binding for agent options, provider options, and telemetry.
/// </summary>
public static class AgentSdkServiceCollectionExtensions
{
    private static string NormalizeProviderType(string? providerType)
    {
        return string.IsNullOrWhiteSpace(providerType)
            ? string.Empty
            : providerType.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Registers core AgentSdk services including configuration value substitution,
    /// model provider options, agent options, telemetry, provider resolver, and PDF processing.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentSdkServices(this IServiceCollection services)
    {
        // Register configuration value substitution service
        services.AddSingleton<IConfigurationValueSubstitution, ConfigurationValueSubstitution>();

        // Add Model Provider Options
        services.AddModelProviderOptions();

        services.AddAgentOptions();
        services.AddTelemetryOptions();

        // Register agent provider resolver and provider implementations
        services.AddAgentProviderResolver();

        // Register PDF services from AgentSdk.Pdf package
        services.AddPdfServices();

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="AgentOptions"/> from the <c>agents:</c> section in configuration.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentOptions(this IServiceCollection services, string? name = null)
    {
        services.AddOptions<AgentOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                Dictionary<string, AgentDefinitionOptions> agents = [];

                var agentsSection = configuration.GetSection("agents");
                if (agentsSection.Exists())
                {
                    foreach (var agentSection in agentsSection.GetChildren())
                    {
                        var agentDef = new AgentDefinitionOptions
                        {
                            Type = agentSection.GetValue<string>("type") ?? string.Empty,
                            Provider = agentSection.GetValue<string>("provider") ?? string.Empty,
                            AutoDelete = agentSection.GetValue("auto_delete", true),
                            AutoCleanupResources = agentSection.GetValue("auto_cleanup_resources", true),
                            Enabled = agentSection.GetValue("enabled", true),
                            Version = agentSection.GetValue<string?>("version"),
                            SystemPromptTemplate = agentSection.GetValue<string>("system_prompt_template"),
                            UserPromptTemplate = agentSection.GetValue<string>("user_prompt_template"),
                            StructuredOutputType = agentSection.GetValue<string?>("structured_output_type"),
                            Temperature = agentSection.GetValue<float?>("temperature"),
                            TopP = agentSection.GetValue<float?>("top_p")
                        };

                        // Bind Metadata section
                        var metadataSection = agentSection.GetSection("metadata");
                        if (metadataSection.Exists())
                        {
                            agentDef.Metadata = new AgentMetadataOptions
                            {
                                Description = metadataSection.GetValue<string>("description") ?? string.Empty,
                                Tools = metadataSection.GetSection("tools").Get<List<string>>() ?? []
                            };
                        }

                        // Validate temperature and top_p parameters
                        try
                        {
                            if (agentDef.Temperature.HasValue)
                            {
                                if (agentDef.Temperature < 0.0f || agentDef.Temperature > 2.0f)
                                {
                                    throw new ArgumentException(
                                        $"Temperature must be between 0.0 and 2.0, but got {agentDef.Temperature}",
                                        nameof(agentDef.Temperature));
                                }
                            }

                            if (agentDef.TopP.HasValue)
                            {
                                if (agentDef.TopP < 0.0f || agentDef.TopP > 1.0f)
                                {
                                    throw new ArgumentException(
                                        $"TopP must be between 0.0 and 1.0, but got {agentDef.TopP}",
                                        nameof(agentDef.TopP));
                                }
                            }
                        }
                        catch (ArgumentException ex)
                        {
                            throw new InvalidOperationException(
                                $"Agent '{agentSection.Key}' has invalid thermodynamic parameters. {ex.Message}",
                                ex);
                        }

                        agents[agentSection.Key] = agentDef;
                    }
                }

                options.Agents = agents;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="TelemetryOptions"/> from the <c>Telemetry:</c> section in configuration.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTelemetryOptions(this IServiceCollection services, string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<TelemetryOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var telemetrySection = configuration.GetSection(TelemetryOptions.SectionName);
                if (telemetrySection.Exists())
                {
                    telemetrySection.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="ModelProviderOptions"/> from the <c>providers:</c> section in configuration.
    /// Performs IConfiguration variable substitution for endpoint URLs, API keys, and other values.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a provider configuration is invalid.</exception>
    public static IServiceCollection AddModelProviderOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<ModelProviderOptions>(name)
            .Configure<IConfiguration, IConfigurationValueSubstitution>((options, configuration, substitution) =>
            {
                Dictionary<string, ModelProviderDefinitionOptions> providers = [];

                var providersSection = configuration.GetSection("providers");
                if (providersSection.Exists())
                {
                    foreach (var providerSection in providersSection.GetChildren())
                    {
                        var providerDef = new ModelProviderDefinitionOptions
                        {
                            Type = NormalizeProviderType(
                                substitution.Substitute(providerSection.GetValue<string>("type") ?? string.Empty)),
                            Endpoint = substitution.Substitute(providerSection.GetValue<string>("endpoint") ?? string.Empty),
                            DeploymentName = substitution.Substitute(providerSection.GetValue<string>("deployment_name") ?? string.Empty),
                            Model = substitution.SubstituteNullable(providerSection.GetValue<string>("model")),
                            ApiVersion = substitution.SubstituteNullable(providerSection.GetValue<string>("api_version")),
                            ApiKey = substitution.SubstituteNullable(providerSection.GetValue<string>("api_key")),
                            TimeoutSeconds = providerSection.GetValue("timeout_seconds", 300),
                            MaxRetries = providerSection.GetValue("max_retries", 3),
                            Temperature = providerSection.GetValue<float?>("temperature"),
                            TopP = providerSection.GetValue<float?>("top_p")
                        };

                        // Validate provider configuration
                        if (!providerDef.IsValid())
                        {
                            throw new InvalidOperationException(
                                $"Provider '{providerSection.Key}' is not properly configured. " +
                                $"Type: {providerDef.Type}, Endpoint: {providerDef.Endpoint}, DeploymentName: {providerDef.DeploymentName}. " +
                                $"Ensure all required fields are present and correctly formatted.");
                        }

                        // Validate temperature and top_p parameters
                        try
                        {
                            providerDef.ValidateThermodynamicParameters();
                        }
                        catch (ArgumentException ex)
                        {
                            throw new InvalidOperationException(
                                $"Provider '{providerSection.Key}' has invalid thermodynamic parameters. {ex.Message}",
                                ex);
                        }

                        providers[providerSection.Key] = providerDef;
                    }
                }

                options.Providers = providers;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers agent provider resolver and provider implementations.
    /// Providers are registered as Scoped to safely maintain references to scoped <see cref="IProviderClientFactory"/>.
    /// The resolver is registered as Scoped to ensure scope-safe resolution and access to its dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers:
    /// <list type="bullet">
    /// <item><description><see cref="IAgentProvider"/> implementations (Azure, Ollama) as Scoped services</description></item>
    /// <item><description><see cref="IAgentProviderResolver"/> as a Scoped service</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The Scoped lifetime for providers ensures they live in the same scope as their scoped <see cref="IProviderClientFactory"/> dependency
    /// and can safely maintain references to it throughout their lifetime (e.g., used in CreateAgentAsync, DeleteAgentAsync).
    /// The Scoped lifetime for the resolver ensures it can safely access scoped dependencies like <see cref="IProviderClientFactory"/>
    /// without violating DI scope rules. Since AgentFactory (the consumer) is also scoped, this creates a consistent
    /// scope-safe hierarchy where the resolver, providers, and their dependencies all live in the same scope.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAgentProviderResolver(this IServiceCollection services)
    {
        // Register provider implementations as Scoped
        // Scoped lifetime ensures they live in the same scope as their scoped IProviderClientFactory dependency.
        // Providers store the factory reference and use it throughout their lifetime (e.g., in CreateAgentAsync, DeleteAgentAsync).
        services.AddScoped<IAgentProvider, AzureAgentProvider>();
        services.AddScoped<IAgentProvider, OllamaAgentProvider>();

        // Register provider resolver as Scoped
        // Scoped lifetime ensures providers can safely access scoped dependencies like IProviderClientFactory
        // AgentFactory (the consumer) is also scoped, creating a scope-safe hierarchy
        services.AddScoped<IAgentProviderResolver, AgentProviderResolver>();

        return services;
    }
}