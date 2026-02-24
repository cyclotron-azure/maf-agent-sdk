using AwesomeAssertions;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using Microsoft.Extensions.Logging;
using Moq;
using SpamDetection.Services.Impl;

namespace SpamDetection.UnitTests.Services;

public sealed class SpamProviderStrategyTests
{
    [Fact]
    public async Task PrepareVectorStoreAsync_WithAzureStrategy_ReturnsVectorStoreId()
    {
        var logger = Mock.Of<ILogger<AzureSpamProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();
        var vectorStoreManager = new Mock<IVectorStoreManager>();

        agentFactory.SetupGet(factory => factory.AgentDefinition)
            .Returns(new AgentDefinitionOptions { Provider = "azure_foundry" });

        vectorStoreManager
            .Setup(manager => manager.GetOrCreateSharedVectorStoreAsync(
                "azure_foundry",
                "spam-detection-examples",
                "Spam detection training examples",
                "SpamDetectionExamples",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("vector-store-1");

        vectorStoreManager
            .Setup(manager => manager.AddFileToVectorStoreAsync(
                "azure_foundry",
                "vector-store-1",
                It.IsAny<Stream>(),
                "spam_training_examples.md",
                It.IsAny<Func<Stream, string, IAsyncEnumerable<(string Text, string ChunkId)>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-1");

        var strategy = new AzureSpamProviderStrategy(logger, agentFactory.Object, vectorStoreManager.Object);

        var result = await strategy.PrepareVectorStoreAsync(CancellationToken.None);

        result.Should().Be("vector-store-1");
        vectorStoreManager.VerifyAll();
    }

    [Fact]
    public async Task PrepareVectorStoreAsync_WithOllamaStrategy_ReturnsNull()
    {
        var logger = Mock.Of<ILogger<OllamaSpamProviderStrategy>>();
        var agentFactory = new Mock<IAgentFactory>();

        var strategy = new OllamaSpamProviderStrategy(logger, agentFactory.Object);

        var result = await strategy.PrepareVectorStoreAsync(CancellationToken.None);

        result.Should().BeNull();
    }
}
