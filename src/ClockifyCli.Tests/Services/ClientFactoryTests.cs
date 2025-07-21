using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using Moq;
using RichardSzalay.MockHttp;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class ClientFactoryTests
{
    private ClientFactory clientFactory;
    private string tempConfigDirectory;

    [SetUp]
    public void SetUp()
    {
        // Create a temporary directory for test config
        tempConfigDirectory = Path.Combine(Path.GetTempPath(), "ClockifyCliTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempConfigDirectory);
        
        // Create ClientFactory with isolated ConfigurationService
        var configurationService = new ConfigurationService(tempConfigDirectory);
        clientFactory = new ClientFactory(configurationService);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temp directory
        if (Directory.Exists(tempConfigDirectory))
        {
            Directory.Delete(tempConfigDirectory, true);
        }
    }

    [Test]
    public void Constructor_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new ClientFactory());
    }

    [Test]
    public void CreateClockifyClientAsync_WithIncompleteConfiguration_ShouldThrowInvalidOperationException()
    {
        // This test assumes no valid configuration exists
        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await clientFactory.CreateClockifyClientAsync());
        
        Assert.That(ex.Message, Does.Contain("Configuration is incomplete"));
    }

    [Test]
    public void CreateJiraClientAsync_WithIncompleteConfiguration_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await clientFactory.CreateJiraClientAsync());
        
        Assert.That(ex.Message, Does.Contain("Configuration is incomplete"));
    }

    [Test]
    public void CreateTempoClientAsync_WithIncompleteConfiguration_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var jiraMockHttp = new MockHttpMessageHandler();
        var jiraHttpClient = new HttpClient(jiraMockHttp);
        var jiraClient = new JiraClient(jiraHttpClient, "test-user", "test-token");

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await clientFactory.CreateTempoClientAsync(jiraClient));
        
        Assert.That(ex.Message, Does.Contain("Configuration is incomplete"));
        
        // Cleanup
        jiraMockHttp.Dispose();
        jiraHttpClient.Dispose();
    }

    [Test]
    public void ClientFactory_Integration_ShouldBeUsableWithDependencyInjection()
    {
        // This test verifies that ClientFactory can be used in a DI scenario
        // Act & Assert
        Assert.DoesNotThrow(() => new ClientFactory());
        
        // Verify the factory has the expected methods by checking their types
        var factoryType = typeof(ClientFactory);
        Assert.That(factoryType.GetMethod("CreateClockifyClientAsync"), Is.Not.Null);
        Assert.That(factoryType.GetMethod("CreateJiraClientAsync"), Is.Not.Null);
        Assert.That(factoryType.GetMethod("CreateTempoClientAsync"), Is.Not.Null);
    }
}
