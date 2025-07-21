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
public class ArchiveCompletedJirasCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private Mock<IJiraClient> mockJiraClient;
    private TestConsole testConsole;
    private ArchiveCompletedJirasCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        mockJiraClient = new Mock<IJiraClient>();
        testConsole = new TestConsole();
        command = new ArchiveCompletedJirasCommand(mockClockifyClient.Object, mockJiraClient.Object, testConsole);
    }

    [TearDown]
    public void TearDown()
    {
        testConsole?.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithValidWorkspace_ReturnsZero()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
        mockClockifyClient.Verify(x => x.GetProjects(mockWorkspace), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsZero()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo>());

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"));
    }

    [Test]
    public async Task ExecuteAsync_WithProjects_ChecksTasks()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };
        var mockTasks = new List<TaskInfo>();

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetTasks(mockWorkspace, mockProject), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithActiveTasksButNoJiraConnection_SkipsTasks()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };
        
        var mockTask = new TaskInfo("task1", "TEST-123 Test Task", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        
        // Mock Jira client to return null (no connection or issue not found)
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync((JiraIssue?)null);

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockJiraClient.Verify(x => x.GetIssue(mockTask), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithCompletedJiraIssue_ProcessesArchiving()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);
        
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };
        
        var mockTask = new TaskInfo("task1", "TEST-123 Test Task", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("Done", new JiraStatusCategory("done", "Done")),
                "Test Summary"
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);
        mockClockifyClient.Setup(x => x.UpdateTaskStatus(mockWorkspace, mockProject, mockTask, "DONE")).Returns(Task.CompletedTask);

        // Provide input for the confirmation prompt
        testConsole.Input.PushTextWithEnter("y");

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockJiraClient.Verify(x => x.GetIssue(mockTask), Times.Once);
        mockClockifyClient.Verify(x => x.UpdateTaskStatus(mockWorkspace, mockProject, mockTask, "DONE"), Times.Once);
    }

    [Test]
    public void Constructor_WithNullClockifyClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ArchiveCompletedJirasCommand(null!, mockJiraClient.Object, testConsole));
    }

    [Test]
    public void Constructor_WithNullJiraClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ArchiveCompletedJirasCommand(mockClockifyClient.Object, null!, testConsole));
    }

    [Test]
    public void Constructor_WithNullConsole_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ArchiveCompletedJirasCommand(mockClockifyClient.Object, mockJiraClient.Object, null!));
    }
}
