
using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AgentSdk in the host builder pipeline.
/// Provides automatic loading of .env files and YAML configuration.
/// </summary>
public static class AgentSdkHosingBuilderExtensions
{
    /// <summary>
    /// Configures the host builder to load AgentSdk configuration from .env and YAML files.
    /// Adds DotNetEnv for environment variable loading and YAML configuration file support.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="configFile">The YAML configuration file name. Defaults to "agent.config.yaml".</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseAgentSdk(
        this IHostBuilder hostBuilder,
        string configFile = "agent.config.yaml")
    {
        hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddDotNetEnv();
            config.AddYamlFile(configFile, optional: true, reloadOnChange: true);
        });

        return hostBuilder;
    }
}