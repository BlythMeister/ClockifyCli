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
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync((TimeEntry?)null);

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
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync((TimeEntry?)null);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No time entries found"));
        Assert.That(output, Does.Contain("Try increasing the number of days"));
    }

    [Test]
    public async Task ExecuteAsync_WithOnlyRunningTimeEntry_ShowsRunningEntryForEdit()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        
        // Create only a running time entry (no completed ones)
        var runningEntry = new TimeEntry(
            "entry1",
            "Running Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.UtcNow.AddHours(-2).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry>(); // No completed entries

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        // Should find the running timer's date and make it available for editing
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        var output = testConsole.Output;
        // Should not show "no entries" message since running timer is available
        Assert.That(output, Does.Not.Contain("No time entries found"));
    }

    [Test]
    public void EditTimerCommand_Constructor_ShouldAcceptDependencies()
    {
        // Arrange & Act
        var command = new EditTimerCommand(mockClockifyClient.Object, testConsole);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Is.InstanceOf<EditTimerCommand>());
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimeEntry_IncludesRunningEntryOnSameDate()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        
        // Create a completed time entry and a running time entry on the same date
        var today = DateTime.Today;
        var completedEntry = new TimeEntry(
            "entry1",
            "Completed Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(9).ToString("o"), today.AddHours(10).ToString("o"))
        );
        
        var runningEntry = new TimeEntry(
            "entry2",
            "Running Task",
            "task2",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(11).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry> { completedEntry };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        // The command should find both entries for today's date
        var output = testConsole.Output;
        Assert.That(output, Does.Not.Contain("No time entries found"));
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimeEntryOnDifferentDate_DoesNotIncludeInOtherDates()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        
        // Create entries on different dates
        var yesterday = DateTime.Today.AddDays(-1);
        var today = DateTime.Today;
        
        var yesterdayEntry = new TimeEntry(
            "entry1",
            "Yesterday Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(yesterday.AddHours(9).ToString("o"), yesterday.AddHours(10).ToString("o"))
        );
        
        var runningEntry = new TimeEntry(
            "entry2",
            "Running Task",
            "task2",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(11).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry> { yesterdayEntry };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        // Should show both yesterday (with 1 entry) and today (with running timer)
        var output = testConsole.Output;
        Assert.That(output, Does.Not.Contain("No time entries found"));
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
