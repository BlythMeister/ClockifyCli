using ClockifyCli.Commands;
using ClockifyCli.Tests.Mocks;
using NUnit.Framework;
using Spectre.Console.Testing;
using System.Threading.Tasks;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class ShowChangelogCommandTests
{
    private TestConsole console;

    [SetUp]
    public void SetUp()
    {
        console = new TestConsole();
    }

    [TearDown]
    public void TearDown()
    {
        console?.Dispose();
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateSuccessfully()
    {
        // Arrange & Act & Assert
        var mockReader = new MockChangelogReader("test content");
        Assert.DoesNotThrow(() => new ShowChangelogCommand(console, mockReader));
    }

    [Test]
    public async Task ExecuteAsync_WithSpecificVersion_ShouldDisplayThatVersion()
    {
        // Arrange
        var changelogContent = """
            # Changelog

            ## [1.14] - 2025-09-23

            ### Enhancements

            - **Embedded Changelog Resource**: Improved changelog access reliability
            """;

        var mockReader = new MockChangelogReader(changelogContent);
        var command = new ShowChangelogCommand(console, mockReader);

        // Act
        var settings = new ShowChangelogCommand.Settings { Version = "1.14" };
        var result = await command.ExecuteAsync(null!, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = console.Output;
        Assert.That(output, Does.Contain("ClockifyCli Changelog"));
        Assert.That(output, Does.Contain("Version 1.14"));
    }
}
