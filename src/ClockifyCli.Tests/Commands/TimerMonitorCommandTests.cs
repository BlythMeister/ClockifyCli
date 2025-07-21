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
public class TimerMonitorCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private TestConsole testConsole;
    private TimerMonitorCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole();
        command = new TimerMonitorCommand(mockClockifyClient.Object, testConsole);
    }

    [TearDown]
    public void TearDown()
    {
        testConsole?.Dispose();
    }

    [Test]
    [Explicit("Shows system notifications on Windows")]
    public async Task ExecuteAsync_WithValidSettings_ReturnsZero()
    {
        // Arrange
        var settings = new TimerMonitorCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockTimeInterval = new TimeInterval(DateTime.UtcNow.AddHours(-1).ToString("O"), "");
        var mockCurrentEntry = new TimeEntry("entry1", "Test Description", "task1", "project1", "REGULAR", mockTimeInterval);

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync(mockCurrentEntry);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(new List<ProjectInfo>());
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsOne()
    {
        // Arrange
        var settings = new TimerMonitorCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(1));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"));
    }

    [Test]
    [Explicit("Shows system notifications on Windows")]
    public async Task ExecuteAsync_NoTimerRunning_ReturnsTwo()
    {
        // Arrange
        var settings = new TimerMonitorCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(new List<ProjectInfo>());
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(2));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
    }

    [Test]
    [Explicit("Shows system notifications on Windows")]
    public async Task ExecuteAsync_WithSilentMode_SuppressesOutput()
    {
        // Arrange
        var settings = new TimerMonitorCommand.Settings { Silent = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockTimeInterval = new TimeInterval(DateTime.UtcNow.AddHours(-1).ToString("O"), "");
        var mockCurrentEntry = new TimeEntry("entry1", "Test Description", "task1", "project1", "REGULAR", mockTimeInterval);

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync(mockCurrentEntry);
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(new List<ProjectInfo>());
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        // In silent mode, output should be minimal
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new TimerMonitorCommand.Settings();

        // Assert
        Assert.That(settings.Silent, Is.False);
        Assert.That(settings.AlwaysNotify, Is.False);
    }

    [Test]
    public void Settings_CanSetCustomFlags()
    {
        // Arrange & Act
        var settings = new TimerMonitorCommand.Settings { Silent = true, AlwaysNotify = true };

        // Assert
        Assert.That(settings.Silent, Is.True);
        Assert.That(settings.AlwaysNotify, Is.True);
    }
}
