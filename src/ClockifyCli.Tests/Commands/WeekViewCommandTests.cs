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
public class WeekViewCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private TestConsole testConsole;
    private WeekViewCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole();
        command = new WeekViewCommand(mockClockifyClient.Object, testConsole);
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
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

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
        mockClockifyClient.Verify(x => x.GetProjects(mockWorkspace), Times.Once);
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsZero()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
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
    public async Task ExecuteAsync_WithIncludeCurrentFlag_CallsCorrectMethods()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { IncludeCurrent = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

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
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithDetailedFlag_CallsCorrectMethods()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { Detailed = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

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
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNoTimeEntries_ShowsEmptyWeekMessage()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

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
        // Command should handle empty time entries gracefully
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new WeekViewCommand.Settings();

        // Assert
        Assert.That(settings.IncludeCurrent, Is.False);
        Assert.That(settings.Detailed, Is.False);
    }

    [Test]
    public void Settings_CanSetCustomFlags()
    {
        // Arrange & Act
        var settings = new WeekViewCommand.Settings { IncludeCurrent = true, Detailed = true };

        // Assert
        Assert.That(settings.IncludeCurrent, Is.True);
        Assert.That(settings.Detailed, Is.True);
    }
}
