using Cyclotron.Maf.AgentSdk.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.UnitTests.Options;

/// <summary>
/// Unit tests for <see cref="ExtensibilityOptions"/>.
/// Tests advanced customization hooks for client and agent configuration.
/// </summary>
public class ExtensibilityOptionsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultNullValues()
    {
        // Act
        var options = new ExtensibilityOptions();

        // Assert
        Assert.Null(options.AdditionalClientOptions);
        Assert.Null(options.ClientFactory);
        Assert.Null(options.AdditionalAgentOptions);
    }

    [Fact]
    public void AdditionalClientOptions_CanBeSet()
    {
        // Arrange
        var options = new ExtensibilityOptions();
        Action<object> clientOptionsAction = opts => { };

        // Act
        options.AdditionalClientOptions = clientOptionsAction;

        // Assert
        Assert.NotNull(options.AdditionalClientOptions);
        Assert.Same(clientOptionsAction, options.AdditionalClientOptions);
    }

    [Fact]
    public void ClientFactory_CanBeSet()
    {
        // Arrange
        var options = new ExtensibilityOptions();
        var factoryCalled = false;
        Func<IChatClient, IChatClient> factory = client =>
        {
            factoryCalled = true;
            return client; // Pass through
        };

        // Act
        options.ClientFactory = factory;

        // Assert
        Assert.NotNull(options.ClientFactory);
        Assert.Same(factory, options.ClientFactory);

        // Verify factory can be invoked
        var mockClient = new Mock<IChatClient>();
        var result = options.ClientFactory.Invoke(mockClient.Object);
        Assert.True(factoryCalled);
        Assert.NotNull(result);
    }

    [Fact]
    public void AdditionalAgentOptions_CanBeSet()
    {
        // Arrange
        var options = new ExtensibilityOptions();
        var actionCalled = false;
        Action<ChatClientAgentOptions> action = agentOptions =>
        {
            actionCalled = true;
            agentOptions.Name = "custom-agent";
        };

        // Act
        options.AdditionalAgentOptions = action;

        // Assert
        Assert.NotNull(options.AdditionalAgentOptions);
        Assert.Same(action, options.AdditionalAgentOptions);

        // Verify action can be invoked (with a real instance)
        var agentOptions = new ChatClientAgentOptions();
        options.AdditionalAgentOptions.Invoke(agentOptions);
        Assert.True(actionCalled);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        Action<object> clientOptionsAction = opts => { };
        Func<IChatClient, IChatClient> factory = client => client;
        Action<ChatClientAgentOptions> agentAction = opts => opts.Name = "test";

        // Act
        var options = new ExtensibilityOptions
        {
            AdditionalClientOptions = clientOptionsAction,
            ClientFactory = factory,
            AdditionalAgentOptions = agentAction
        };

        // Assert
        Assert.NotNull(options.AdditionalClientOptions);
        Assert.NotNull(options.ClientFactory);
        Assert.NotNull(options.AdditionalAgentOptions);
        Assert.Same(clientOptionsAction, options.AdditionalClientOptions);
        Assert.Same(factory, options.ClientFactory);
        Assert.Same(agentAction, options.AdditionalAgentOptions);
    }

    [Fact]
    public void AdditionalClientOptions_CanConfigureProviderOptions()
    {
        // Arrange
        var options = new ExtensibilityOptions();
        var configured = false;

        options.AdditionalClientOptions = clientOptions =>
        {
            configured = true;
            // In real scenario, this would be provider-specific options
            Assert.NotNull(clientOptions);
        };

        // Act
        var testOptions = new object();
        options.AdditionalClientOptions.Invoke(testOptions);

        // Assert
        Assert.True(configured);
    }

    [Fact]
    public void ClientFactory_CanWrapClient()
    {
        // Arrange
        var options = new ExtensibilityOptions();
        var wrapperCalled = false;

        options.ClientFactory = innerClient =>
        {
            wrapperCalled = true;
            // In real scenario, this could wrap with caching, logging, etc.
            return innerClient;
        };

        // Act
        var mockClient = new Mock<IChatClient>();
        var wrappedClient = options.ClientFactory.Invoke(mockClient.Object);

        // Assert
        Assert.True(wrapperCalled);
        Assert.NotNull(wrappedClient);
    }

    [Fact]
    public void AdditionalAgentOptions_ModifiesAgentOptions()
    {
        // Arrange
        var options = new ExtensibilityOptions
        {
            AdditionalAgentOptions = agentOptions =>
            {
                agentOptions.Name = "custom-name";
                agentOptions.Description = "custom-description";
            }
        };

        var agentOptions = new ChatClientAgentOptions();

        // Act
        options.AdditionalAgentOptions?.Invoke(agentOptions);

        // Assert (verify properties were set)
        Assert.Equal("custom-name", agentOptions.Name);
        Assert.Equal("custom-description", agentOptions.Description);
    }

    [Fact]
    public void ExtensibilityOptions_SupportsComplexScenario()
    {
        // Arrange
        var clientConfigured = false;
        var factoryCalled = false;
        var agentConfigured = false;

        var options = new ExtensibilityOptions
        {
            AdditionalClientOptions = clientOpts =>
            {
                clientConfigured = true;
            },
            ClientFactory = client =>
            {
                factoryCalled = true;
                return client;
            },
            AdditionalAgentOptions = agentOpts =>
            {
                agentConfigured = true;
                agentOpts.Name = "production-agent";
            }
        };

        // Act
        var testOptions = new object();
        options.AdditionalClientOptions?.Invoke(testOptions);

        var mockClient = new Mock<IChatClient>();
        var wrappedClient = options.ClientFactory?.Invoke(mockClient.Object);

        var agentOptions = new ChatClientAgentOptions();
        options.AdditionalAgentOptions?.Invoke(agentOptions);

        // Assert
        Assert.True(clientConfigured);
        Assert.True(factoryCalled);
        Assert.True(agentConfigured);
        Assert.NotNull(wrappedClient);
        Assert.Equal("production-agent", agentOptions.Name);
    }
}
