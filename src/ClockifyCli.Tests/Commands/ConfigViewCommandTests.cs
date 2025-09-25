using ClockifyCli.Commands;
using ClockifyCli.Services;
using ClockifyCli.Models;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class ConfigViewCommandTests
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
        Assert.DoesNotThrow(() => new ConfigViewCommand(configService, testConsole));
    }

    [Test]
    public async Task ExecuteAsync_ShouldDisplayConfiguration()
    {
        // Arrange
        // Save a config with custom recent values
        var config = new AppConfiguration("a", "b", "c", "d", 13, 17);
        await configService.SaveConfigurationAsync(config);

        var command = new ConfigViewCommand(configService, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        // Verify configuration display
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Current Configuration"));
        Assert.That(output, Does.Contain("Configuration file:"));
        Assert.That(output, Does.Contain("Recent Tasks Count"));
        Assert.That(output, Does.Contain("13"));
        Assert.That(output, Does.Contain("Recent Tasks Days"));
        Assert.That(output, Does.Contain("17"));
    }
}
