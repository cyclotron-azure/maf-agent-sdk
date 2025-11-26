
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
    /// <summary>
    /// Registers core AgentSdk services including configuration value substitution,
    /// model provider options, agent options, telemetry, and PDF conversion.
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
        services.AddPdfConversionOptions();

        // Register PDF to Markdown converter
        services.AddSingleton<IPdfToMarkdownConverter, PdfPigMarkdownConverter>();

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
                            AutoDelete = agentSection.GetValue<bool>("auto_delete", true),
                            AutoCleanupResources = agentSection.GetValue<bool>("auto_cleanup_resources", true),
                            Enabled = agentSection.GetValue<bool>("enabled", true),
                            SystemPromptTemplate = agentSection.GetValue<string>("system_prompt_template"),
                            UserPromptTemplate = agentSection.GetValue<string>("user_prompt_template")
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

                        // Bind AIFrameworkOptions section (maps from framework_config)
                        var frameworkSection = agentSection.GetSection("framework_config");
                        if (frameworkSection.Exists())
                        {
                            agentDef.AIFrameworkOptions = new AIFrameworkOptions
                            {
                                Provider = frameworkSection.GetValue<string>("provider") ?? string.Empty
                            };
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
                            Type = substitution.Substitute(providerSection.GetValue<string>("type") ?? string.Empty),
                            Endpoint = substitution.Substitute(providerSection.GetValue<string>("endpoint") ?? string.Empty),
                            DeploymentName = substitution.Substitute(providerSection.GetValue<string>("deployment_name") ?? string.Empty),
                            Model = substitution.SubstituteNullable(providerSection.GetValue<string>("model")),
                            ApiVersion = substitution.SubstituteNullable(providerSection.GetValue<string>("api_version")),
                            ApiKey = substitution.SubstituteNullable(providerSection.GetValue<string>("api_key")),
                            TimeoutSeconds = providerSection.GetValue<int>("timeout_seconds", 300),
                            MaxRetries = providerSection.GetValue<int>("max_retries", 3)
                        };

                        // Validate provider configuration
                        if (!providerDef.IsValid())
                        {
                            throw new InvalidOperationException(
                                $"Provider '{providerSection.Key}' is not properly configured. " +
                                $"Type: {providerDef.Type}, Endpoint: {providerDef.Endpoint}, DeploymentName: {providerDef.DeploymentName}. " +
                                $"Ensure all required fields are present and correctly formatted.");
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
    /// Registers and configures <see cref="PdfConversionOptions"/> from the <c>PdfConversion:</c> section in configuration.
    /// Controls PDF to Markdown conversion behavior and debug output settings.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfConversionOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<PdfConversionOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var pdfSection = configuration.GetSection(PdfConversionOptions.SectionName);
                if (pdfSection.Exists())
                {
                    pdfSection.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}