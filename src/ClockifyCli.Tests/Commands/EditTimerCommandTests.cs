using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using Moq;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class EditTimerCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private TestConsole testConsole;
    private EditTimerCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole();
        command = new EditTimerCommand(mockClockifyClient.Object, testConsole);
    }

    [TearDown]
    public void TearDown()
    {
        testConsole?.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithValidSettings_ReturnsZero()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsZero()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"));
    }

    [Test]
    public async Task ExecuteAsync_NoTimeEntriesFound_ShowsWarningMessage()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 14 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No completed time entries found"));
        Assert.That(output, Does.Contain("Try increasing the number of days"));
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimeEntry_FiltersOutRunningEntry()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        
        // Create a running time entry (no end time) - should be filtered out
        var runningEntry = new TimeEntry(
            "entry1",
            "Running Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.UtcNow.AddHours(-2).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry> { runningEntry }; // Only running entry, no completed ones

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        // Should process the entries but filter out running ones, leaving none to edit
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No completed time entries found"));
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new EditTimerCommand.Settings();

        // Assert
        Assert.That(settings.Days, Is.EqualTo(7));
    }

    [Test]
    public void Settings_CanSetCustomDays()
    {
        // Arrange & Act
        var settings = new EditTimerCommand.Settings { Days = 30 };

        // Assert
        Assert.That(settings.Days, Is.EqualTo(30));
    }
}
