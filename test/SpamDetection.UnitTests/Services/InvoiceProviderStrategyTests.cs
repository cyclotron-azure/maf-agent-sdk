using AwesomeAssertions;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Microsoft.Extensions.Logging;
using Moq;
using SpamDetection.Services.Impl;

namespace SpamDetection.UnitTests.Services;

public sealed class InvoiceProviderStrategyTests
{
    [Fact]
    public void AzureStrategy_HasCorrectProviderKey()
    {
        var logger = Mock.Of<ILogger<AzureInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "azure_foundry" });

        var strategy = new AzureInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.ProviderKey.Should().Be("azure");
    }

    [Fact]
    public void AzureStrategy_UsesVectorStore()
    {
        var logger = Mock.Of<ILogger<AzureInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "azure_foundry" });

        var strategy = new AzureInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.UsesVectorStore.Should().BeTrue();
    }

    [Fact]
    public void AzureStrategy_ExposesCorrectAgentFactory()
    {
        var logger = Mock.Of<ILogger<AzureInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "azure_foundry" });

        var strategy = new AzureInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.AgentFactory.Should().Be(agentFactory.Object);
    }

    [Fact]
    public async Task OllamaStrategy_HasCorrectProviderKey()
    {
        var logger = Mock.Of<ILogger<OllamaInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "ollama" });

        var strategy = new OllamaInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.ProviderKey.Should().Be("ollama");
    }

    [Fact]
    public void OllamaStrategy_DoesNotUseVectorStore()
    {
        var logger = Mock.Of<ILogger<OllamaInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "ollama" });

        var strategy = new OllamaInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.UsesVectorStore.Should().BeFalse();
    }

    [Fact]
    public async Task OllamaStrategy_PrepareVectorStoreAsync_ReturnsNull()
    {
        var logger = Mock.Of<ILogger<OllamaInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "ollama" });

        var strategy = new OllamaInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        var result = await strategy.PrepareVectorStoreAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public void OllamaStrategy_ExposesCorrectAgentFactory()
    {
        var logger = Mock.Of<ILogger<OllamaInvoiceProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "ollama" });

        var strategy = new OllamaInvoiceProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        strategy.AgentFactory.Should().Be(agentFactory.Object);
    }
}
