using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class ConfigScheduleCommandTests
{
    private TestConsole testConsole = null!;

    [SetUp]
    public void Setup()
    {
        testConsole = new TestConsole();
    }

    [TearDown]
    public void TearDown()
    {
        testConsole.Dispose();
    }

    [Test]
    public void Constructor_WithDependencies_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new ConfigScheduleCommand(testConsole));
    }

    [Test, Explicit]
    public async Task ExecuteAsync_ShouldHandleBasicExecution()
    {
        // Arrange
        var command = new ConfigScheduleCommand(testConsole);
        var settings = new ConfigScheduleCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(null!, settings);

        // Assert
        Assert.That(result, Is.AnyOf(0, -1)); // May fail if not on Windows or missing permissions
        
        // Should display some output regardless of success/failure
        var output = testConsole.Output;
        Assert.That(output, Is.Not.Empty);
    }
}
