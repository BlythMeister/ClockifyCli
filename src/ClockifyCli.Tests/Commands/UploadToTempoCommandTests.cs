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
}
