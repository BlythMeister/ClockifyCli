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
public class BreaksReportCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient = null!;
    private TestConsole testConsole = null!;
    private Mock<IClock> mockClock = null!;
    private BreaksReportCommand command = null!;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole();
        mockClock = new Mock<IClock>();
        command = new BreaksReportCommand(mockClockifyClient.Object, testConsole, mockClock.Object);

        // Setup default mock responses
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(user);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { workspace });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(workspace, user)).ReturnsAsync((TimeEntry?)null);
        
        // Setup default empty projects and tasks
        mockClockifyClient.Setup(x => x.GetProjects(workspace)).ReturnsAsync(new List<ProjectInfo>());
        mockClockifyClient.Setup(x => x.GetTasks(workspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<TimeEntry>());

        // Setup clock to return a consistent date
        mockClock.Setup(x => x.Today).Returns(new DateTime(2025, 9, 4));
        mockClock.Setup(x => x.Now).Returns(new DateTime(2025, 9, 4, 14, 30, 0));
    }

    [TearDown]
    public void TearDown()
    {
        testConsole?.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithBreakEntries_ShowsBreakReport()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("work-project", "Work Project"),
            new ProjectInfo("breaks-project", "Breaks")
        };

        var breakEntries = new List<TimeEntry>
        {
            new TimeEntry("entry1", "Coffee break", "task1", "project1", "BREAK", 
                new TimeInterval("2025-09-04T10:00:00Z", "2025-09-04T10:15:00Z")),
            new TimeEntry("entry2", "Lunch break", "task2", "breaks-project", "REGULAR", 
                new TimeInterval("2025-09-04T12:00:00Z", "2025-09-04T13:00:00Z")),
            new TimeEntry("entry3", "Regular work", "task3", "work-project", "REGULAR", 
                new TimeInterval("2025-09-04T09:00:00Z", "2025-09-04T10:00:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetProjects(workspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(breakEntries);

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Breaks Report"));
        Assert.That(output, Does.Contain("Coffee break")); // BREAK type entry
        Assert.That(output, Does.Contain("Lunch break")); // Breaks project entry
        Assert.That(output, Does.Not.Contain("Regular work")); // Should be filtered out
        Assert.That(output, Does.Contain("Total break time"));
    }

    [Test]
    public async Task ExecuteAsync_WithDetailedFlag_ShowsDetailedBreakReport()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var breakEntries = new List<TimeEntry>
        {
            new TimeEntry("entry1", "Coffee break", "task1", "project1", "BREAK", 
                new TimeInterval("2025-09-04T10:00:00Z", "2025-09-04T10:15:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(breakEntries);

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings { Detailed = true };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("☕ Breaks Report"));
        Assert.That(output, Does.Contain("Coffee break"));
        // In detailed mode, should show more information like project names, task details etc.
    }

    [Test]
    public async Task ExecuteAsync_WithCustomDays_UsesCorrectDateRange()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings { Days = 14 };

        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TimeEntry>());

        // Act
        await command.ExecuteAsync(context, settings);

        // Assert
        mockClockifyClient.Verify(x => x.GetTimeEntries(
            workspace, 
            user, 
            new DateTime(2025, 8, 21), // 14 days back from 2025-09-04
            new DateTime(2025, 9, 5)   // clock.Today.AddDays(1)
        ), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNoBreakEntries_ShowsNoBreaksMessage()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var regularEntries = new List<TimeEntry>
        {
            new TimeEntry("entry1", "Regular work", "task1", "work-project", "REGULAR", 
                new TimeInterval("2025-09-04T09:00:00Z", "2025-09-04T10:00:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(regularEntries);

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No break entries found"));
    }

    [Test]
    public async Task ExecuteAsync_WithRunningBreakEntry_ShowsRunningBreakNotification()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var runningBreakEntry = new TimeEntry("running1", "Coffee break", "task1", "project1", "BREAK", 
            new TimeInterval("2025-09-04T14:00:00Z", null!));

        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(workspace, user))
            .ReturnsAsync(runningBreakEntry);

        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TimeEntry>());

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Currently running break"));
        Assert.That(output, Does.Contain("Coffee break"));
    }

    [Test]
    public async Task ExecuteAsync_FiltersBreaksByProjectName_CaseInsensitive()
    {
        // Arrange
        var user = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("breaks-project", "BREAKS"), // Uppercase
            new ProjectInfo("work-project", "Work Project")
        };

        var entries = new List<TimeEntry>
        {
            new TimeEntry("entry1", "Coffee break", "task1", "breaks-project", "REGULAR", 
                new TimeInterval("2025-09-04T10:00:00Z", "2025-09-04T10:15:00Z")),
            new TimeEntry("entry2", "Regular work", "task2", "work-project", "REGULAR", 
                new TimeInterval("2025-09-04T09:00:00Z", "2025-09-04T10:00:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetProjects(workspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(entries);

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Coffee break")); // Should be included (from BREAKS project)
        Assert.That(output, Does.Not.Contain("Regular work")); // Should be filtered out
    }

    [Test]
    public async Task ExecuteAsync_WithNoWorkspace_ReturnsError()
    {
        // Arrange
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo>());

        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        var settings = new BreaksReportCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0)); // Command handles gracefully
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found"));
    }
}
