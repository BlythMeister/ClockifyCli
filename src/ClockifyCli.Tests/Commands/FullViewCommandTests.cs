using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class FullViewCommandTests
{
    [Test]
    public void Constructor_WithDependencies_ShouldCreateSuccessfully()
    {
        // Arrange
        var console = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new FullViewCommand(console));
    }

    [Test, Explicit]
    public async Task ExecuteAsync_ShouldAttemptToOpenBrowser()
    {
        // Arrange
        var console = new TestConsole();
        var command = new FullViewCommand(console);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Should display success message
        var output = console.Output;
        Assert.That(output, Does.Contain("Opening Clockify"));
    }
}
