using ClockifyCli.Commands;
using ClockifyCli.Services;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class ConfigSetCommandTests
{
    private string testConfigDirectory = null!;
    private ConfigurationService configService = null!;
    private TestConsole testConsole = null!;

    [SetUp]
    public void Setup()
    {
        // Create a temporary directory for test config files
        testConfigDirectory = Path.Combine(Path.GetTempPath(), $"ClockifyCliTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testConfigDirectory);

        configService = new ConfigurationService(testConfigDirectory);
        testConsole = new TestConsole();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test console
        testConsole.Dispose();

        // Clean up test directory
        if (Directory.Exists(testConfigDirectory))
        {
            Directory.Delete(testConfigDirectory, true);
        }
    }

    [Test]
    public void Constructor_WithDependencies_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new ConfigSetCommand(configService, testConsole));
    }

    [Test]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        // Simulate user input for the prompts
        testConsole.Input.PushTextWithEnter("test-clockify-key");
        testConsole.Input.PushTextWithEnter("test@example.com");
        testConsole.Input.PushTextWithEnter("test-jira-token");
        testConsole.Input.PushTextWithEnter("test-tempo-key");

        var command = new ConfigSetCommand(configService, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        // Verify success message was displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Configuration saved successfully!"));
    }
}
