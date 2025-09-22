using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;
using System.IO;
using System.Threading.Tasks;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class ShowChangelogCommandTests
{
    private TestConsole console;
    private ShowChangelogCommand command;
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        console = new TestConsole();
        command = new ShowChangelogCommand(console);
        tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        console?.Dispose();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateSuccessfully()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => new ShowChangelogCommand(console));
    }

    [Test]
    public void Constructor_WithNullConsole_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ShowChangelogCommand(null!));
    }

    [Test]
    public async Task ExecuteAsync_WithValidChangelog_ShouldDisplayChangelogContent()
    {
        // Arrange
        var changelogContent = """
            # Changelog

            ## [1.12] - 2025-09-22

            ### Bug Fixes

            - **Project Filtering**: Projects marked as archived in Clockify are now filtered out
            - Added `archived=false` parameter to GetProjects API call for server-side filtering

            ### Enhancements

            - **IntelligentTimeParser Rules**: Updated and clarified rule documentation

            ## [1.11] - 2025-09-08

            ### User Experience

            - **Improved Time Display**: Removed seconds from all user-facing time displays
            """;

        var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        // Temporarily change the working directory for the test
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            var result = await command.ExecuteAsync(null!);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            
            var output = console.Output;
            Assert.That(output, Does.Contain("ClockifyCli Changelog"));
            Assert.That(output, Does.Contain("Current version: 1.12"));
            Assert.That(output, Does.Contain("Release Date: 2025-09-22"));
            Assert.That(output, Does.Contain("Bug Fixes"));
            Assert.That(output, Does.Contain("Project Filtering"));
            Assert.That(output, Does.Contain("Enhancements"));
            Assert.That(output, Does.Contain("IntelligentTimeParser Rules"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithMissingChangelog_ShouldDisplayNotFoundMessage()
    {
        // Arrange - No changelog file exists in temp directory
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            var result = await command.ExecuteAsync(null!);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            
            var output = console.Output;
            Assert.That(output, Does.Contain("ClockifyCli Changelog"));
            Assert.That(output, Does.Contain("CHANGELOG.md not found!"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithVersionNotInChangelog_ShouldDisplayVersionNotFoundMessage()
    {
        // Arrange
        var changelogContent = """
            # Changelog

            ## [1.11] - 2025-09-08

            ### User Experience

            - **Improved Time Display**: Removed seconds from all user-facing time displays

            ## [1.10] - 2025-01-16

            ### Major Improvements

            - **Eliminated Ambiguous Time Prompts**: Completely removed unnecessary user interruptions
            """;

        var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            var result = await command.ExecuteAsync(null!);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            
            var output = console.Output;
            Assert.That(output, Does.Contain("ClockifyCli Changelog"));
            Assert.That(output, Does.Contain("No changelog section found for version 1.12"));
            Assert.That(output, Does.Contain("Available versions:"));
            Assert.That(output, Does.Contain("1.11"));
            Assert.That(output, Does.Contain("1.10"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyChangelogSection_ShouldDisplayNoContentMessage()
    {
        // Arrange
        var changelogContent = """
            # Changelog

            ## [1.12] - 2025-09-22

            ## [1.11] - 2025-09-08

            ### User Experience

            - **Improved Time Display**: Removed seconds from all user-facing time displays
            """;

        var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            var result = await command.ExecuteAsync(null!);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            
            var output = console.Output;
            Assert.That(output, Does.Contain("ClockifyCli Changelog"));
            Assert.That(output, Does.Contain("Current version: 1.12"));
            Assert.That(output, Does.Contain("Release Date: 2025-09-22"));
            Assert.That(output, Does.Contain("No changelog content found for this version"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithMalformedChangelog_ShouldHandleGracefully()
    {
        // Arrange
        var changelogContent = "This is not a valid changelog format";

        var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            var result = await command.ExecuteAsync(null!);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            
            var output = console.Output;
            Assert.That(output, Does.Contain("ClockifyCli Changelog"));
            Assert.That(output, Does.Contain("No changelog section found for version 1.12"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }
}