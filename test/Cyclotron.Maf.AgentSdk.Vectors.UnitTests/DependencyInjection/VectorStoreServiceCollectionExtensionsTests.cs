using Cyclotron.Maf.AgentSdk.Common.Options;
using System.Diagnostics.Metrics;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Services.Chunking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProviderDefinition = Cyclotron.Maf.AgentSdk.Common.Options.ModelProviderDefinitionOptions;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests.DependencyInjection;

public class VectorStoreServiceCollectionExtensionsTests
{
    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options)
        {
            return new Meter(options.Name, options.Version);
        }

        public void Dispose()
        {
        }
    }

    private static IMeterFactory CreateMeterFactory()
    {
        return new TestMeterFactory();
    }

    [Fact(DisplayName = "AddVectorStoreServices_DefaultRegistration_ResolvesManagerAndChunkers")]
    public void AddVectorStoreServices_DefaultRegistration_ResolvesManagerAndChunkers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateMeterFactory());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddOptions<ModelProviderOptions>().Configure(options =>
        {
            options.Providers = new Dictionary<string, ProviderDefinition>
            {
                {
                    "azure_foundry",
                    new ProviderDefinition
                    {
                        Type = "azure_foundry",
                        Endpoint = "https://example.test",
                        DeploymentName = "deployment"
                    }
                },
                {
                    "ollama",
                    new ProviderDefinition
                    {
                        Type = "ollama",
                        Endpoint = "http://localhost:11434",
                        DeploymentName = "nomic-embed-text"
                    }
                }
            };
        });

        services.AddVectorStoreServices();

        var provider = services.BuildServiceProvider();

        var manager = provider.GetService<IVectorStoreManager>();
        var chunkers = provider.GetServices<IDocumentChunker>().ToList();

        Assert.NotNull(manager);
        Assert.Contains(chunkers, chunker => chunker is SemanticDocumentChunker);
        Assert.Contains(chunkers, chunker => chunker is SimpleDocumentChunker);
    }

    [Fact(DisplayName = "AddVectorStoreServices_NoBuilderOptions_DoesNotRegisterManagerOrChunkers")]
    public void AddVectorStoreServices_NoBuilderOptions_DoesNotRegisterManagerOrChunkers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateMeterFactory());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddOptions<ModelProviderOptions>().Configure(options =>
        {
            options.Providers = new Dictionary<string, ProviderDefinition>();
        });

        services.AddVectorStoreServices(_ => { });

        var provider = services.BuildServiceProvider();

        var manager = provider.GetService<IVectorStoreManager>();
        var chunkers = provider.GetServices<IDocumentChunker>().ToList();

        Assert.Null(manager);
        Assert.Empty(chunkers);
    }
}
