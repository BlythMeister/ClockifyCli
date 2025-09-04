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
public class UploadToTempoCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private Mock<ITempoClient> mockTempoClient;
    private TestConsole testConsole;
    private UploadToTempoCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        mockTempoClient = new Mock<ITempoClient>();
        testConsole = new TestConsole();
        command = new UploadToTempoCommand(mockClockifyClient.Object, mockTempoClient.Object, testConsole);
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
        var settings = new UploadToTempoCommand.Settings { Days = 7 };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockTimeEntries = new List<TimeEntry>();
        var mockProjects = new List<ProjectInfo>();
        var mockTasks = new List<TaskInfo>();
        var mockTempoTimes = new List<TempoTime>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(mockTasks);
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(mockTempoTimes);

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
        var settings = new UploadToTempoCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

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
    public async Task ExecuteAsync_WithCleanupOrphanedFlag_ProcessesCleanup()
    {
        // Arrange
        var settings = new UploadToTempoCommand.Settings { CleanupOrphaned = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockTimeEntries = new List<TimeEntry>();
        var mockProjects = new List<ProjectInfo>();
        var mockTasks = new List<TaskInfo>();
        var mockTempoTimes = new List<TempoTime>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(mockTasks);
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(mockTempoTimes);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Orphaned entry cleanup is enabled"));
    }

    [Test]
    public async Task ExecuteAsync_WithCustomDays_UsesCorrectDateRange()
    {
        // Arrange
        var settings = new UploadToTempoCommand.Settings { Days = 30 };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockTimeEntries = new List<TimeEntry>();
        var mockProjects = new List<ProjectInfo>();
        var mockTasks = new List<TaskInfo>();
        var mockTempoTimes = new List<TempoTime>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(mockTasks);
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(mockTempoTimes);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Processing last 30 days"));
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithTimeEntries_ProcessesUpload()
    {
        // Arrange
        var settings = new UploadToTempoCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");

        var mockTimeEntry = new TimeEntry(
            "entry1",
            "TEST-123 Test Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(
                DateTime.UtcNow.AddHours(-2).ToString("o"),
                DateTime.UtcNow.AddHours(-1).ToString("o")
            )
        );
        var mockTimeEntries = new List<TimeEntry> { mockTimeEntry };
        var mockProjects = new List<ProjectInfo>();
        var mockTasks = new List<TaskInfo>();
        var mockTempoTimes = new List<TempoTime>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(mockTasks);
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(mockTempoTimes);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new UploadToTempoCommand.Settings();

        // Assert
        Assert.That(settings.Days, Is.EqualTo(14));
        Assert.That(settings.CleanupOrphaned, Is.False);
    }

    [Test]
    public void Settings_CanSetCustomValues()
    {
        // Arrange & Act
        var settings = new UploadToTempoCommand.Settings { Days = 30, CleanupOrphaned = true };

        // Assert
        Assert.That(settings.Days, Is.EqualTo(30));
        Assert.That(settings.CleanupOrphaned, Is.True);
    }

    [Test]
    public void Constructor_WithNullClockifyClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadToTempoCommand(null!, mockTempoClient.Object, testConsole));
    }

    [Test]
    public void Constructor_WithNullTempoClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadToTempoCommand(mockClockifyClient.Object, null!, testConsole));
    }

    [Test]
    public void Constructor_WithNullConsole_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadToTempoCommand(mockClockifyClient.Object, mockTempoClient.Object, null!));
    }

    [Test]
    public async Task ExecuteAsync_FiltersOutBreakEntries()
    {
        // Arrange
        var settings = new UploadToTempoCommand.Settings { Days = 7 };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("work-project", "Work Project"),
            new ProjectInfo("breaks-project", "Breaks") // Breaks project
        };

        var timeEntries = new List<TimeEntry>
        {
            // Regular work entry - should be uploaded
            new TimeEntry("entry1", "Regular work", "task1", "work-project", "REGULAR", 
                new TimeInterval("2024-01-01T09:00:00Z", "2024-01-01T10:00:00Z")),
            
            // Break type entry - should be excluded
            new TimeEntry("entry2", "Coffee break", "task2", "work-project", "BREAK", 
                new TimeInterval("2024-01-01T10:15:00Z", "2024-01-01T10:30:00Z")),
            
            // Breaks project entry - should be excluded
            new TimeEntry("entry3", "Lunch break", "task3", "breaks-project", "REGULAR", 
                new TimeInterval("2024-01-01T12:00:00Z", "2024-01-01T13:00:00Z"))
        };

        var tasks = new List<TaskInfo>
        {
            new TaskInfo("task1", "PROJ-123 Work task", "ACTIVE")
        };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(timeEntries);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(tasks);
        
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<TempoTime>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify that only the regular work entry was attempted to be uploaded
        mockTempoClient.Verify(x => x.ExportTimeEntry(
            It.Is<TimeEntry>(e => e.Id == "entry1" && e.Description == "Regular work"), 
            It.IsAny<TaskInfo>()
        ), Times.Once);

        // Verify that break entries were NOT uploaded
        mockTempoClient.Verify(x => x.ExportTimeEntry(
            It.Is<TimeEntry>(e => e.Id == "entry2" || e.Id == "entry3"), 
            It.IsAny<TaskInfo>()
        ), Times.Never);
        
        var output = testConsole.Output;
        // The important thing is that break entries were filtered out from upload
        Assert.That(output, Does.Contain("Upload completed"));
    }

    [Test]
    public async Task ExecuteAsync_WithOnlyBreakEntries_ShowsNoEntriesToUpload()
    {
        // Arrange
        var settings = new UploadToTempoCommand.Settings { Days = 7 };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("breaks-project", "Breaks")
        };

        var timeEntries = new List<TimeEntry>
        {
            // Only break entries
            new TimeEntry("entry1", "Coffee break", "task1", "breaks-project", "REGULAR", 
                new TimeInterval("2024-01-01T10:15:00Z", "2024-01-01T10:30:00Z")),
            
            new TimeEntry("entry2", "Lunch break", "task2", "breaks-project", "BREAK", 
                new TimeInterval("2024-01-01T12:00:00Z", "2024-01-01T13:00:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(timeEntries);
        
        mockTempoClient.Setup(x => x.GetCurrentTime(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<TempoTime>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify that no entries were uploaded
        mockTempoClient.Verify(x => x.ExportTimeEntry(It.IsAny<TimeEntry>(), It.IsAny<TaskInfo>()), Times.Never);
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("All time entries are already up to date"));
    }
}
