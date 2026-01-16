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
public class UpdateTaskNamesForJirasCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private Mock<IJiraClient> mockJiraClient;
    private TestConsole testConsole;
    private UpdateTaskNamesForJirasCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        mockJiraClient = new Mock<IJiraClient>();
        testConsole = new TestConsole();
        command = new UpdateTaskNamesForJirasCommand(mockClockifyClient.Object, mockJiraClient.Object, testConsole);
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
    public async Task ExecuteAsync_WithTasksNoChangeNeeded_ShowsUpToDateMessage()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };

        // Task name already matches the expected format
        var mockTask = new TaskInfo("task1", "TEST-123 [Project] - [Parent / Summary]", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraProject = new JiraProject(1, "PROJ", "Project");
        var mockJiraParent = new JiraIssue(
            1,
            "TEST-100",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Parent"
            )
        );

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Summary",
                mockJiraProject,
                mockJiraParent
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No tasks need to be updated"));
    }

    [Test]
    public async Task ExecuteAsync_WithTasksNeedingUpdate_ProcessesUpdates()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };

        // Old format task name
        var mockTask = new TaskInfo("task1", "TEST-123 [Old Summary]", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraProject = new JiraProject(1, "PROJ", "Project Name");
        var mockJiraParent = new JiraIssue(
            1,
            "TEST-100",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Parent Summary"
            )
        );

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "New Summary",
                mockJiraProject,
                mockJiraParent
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);
        mockClockifyClient.Setup(x => x.UpdateTaskName(mockWorkspace, mockProject, mockTask, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Provide input for the confirmation prompt
        testConsole.Input.PushTextWithEnter("y");

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockJiraClient.Verify(x => x.GetIssue(mockTask), Times.Once);
        mockClockifyClient.Verify(x => x.UpdateTaskName(mockWorkspace, mockProject, mockTask, "TEST-123 [Project Name] - [Parent Summary / New Summary]"), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithTasksNoProjectOrParent_UpdatesWithSummaryOnly()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };

        // Old format task name
        var mockTask = new TaskInfo("task1", "TEST-123 [Old Summary]", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "New Summary",
                null,  // No project
                null   // No parent
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);
        mockClockifyClient.Setup(x => x.UpdateTaskName(mockWorkspace, mockProject, mockTask, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Provide input for the confirmation prompt
        testConsole.Input.PushTextWithEnter("y");

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.UpdateTaskName(mockWorkspace, mockProject, mockTask, "TEST-123 [New Summary]"), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_UserCancelsUpdate_DoesNotUpdateTasks()
    {
        // Arrange
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { mockProject };

        var mockTask = new TaskInfo("task1", "TEST-123 [Old Summary]", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "New Summary"
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);

        // Provide input for the confirmation prompt (No)
        testConsole.Input.PushTextWithEnter("n");

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.UpdateTaskName(It.IsAny<WorkspaceInfo>(), It.IsAny<ProjectInfo>(), It.IsAny<TaskInfo>(), It.IsAny<string>()), Times.Never);
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Update operation cancelled"));
    }

    [Test]
    public void Constructor_WithNullClockifyClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateTaskNamesForJirasCommand(null!, mockJiraClient.Object, testConsole));
    }

    [Test]
    public void Constructor_WithNullJiraClient_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateTaskNamesForJirasCommand(mockClockifyClient.Object, null!, testConsole));
    }

    [Test]
    public void Constructor_WithNullConsole_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateTaskNamesForJirasCommand(mockClockifyClient.Object, mockJiraClient.Object, null!));
    }

    [Test]
    public async Task ExecuteAsync_WithWhitespaceOnlyDifferences_DoesNotUpdate()
    {
        // Arrange - This test verifies the fix for the whitespace bug
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProject = new ProjectInfo("project1", "Internal");
        var mockProjects = new List<ProjectInfo> { mockProject };

        // Task name with extra space before closing bracket (simulating the bug)
        var mockTask = new TaskInfo("task1", "DTT-4 [Delivery Team Training] - [Company / Compliance Training ]", "Active");
        var mockTasks = new List<TaskInfo> { mockTask };

        var mockJiraProject = new JiraProject(1, "PROJ", "Delivery Team Training");

        // Jira returns data with different whitespace (extra trailing space)
        var mockJiraIssue = new JiraIssue(
            12345,
            "DTT-4",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Company / Compliance Training  ",  // Two trailing spaces
                mockJiraProject,
                null
            )
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, mockProject)).ReturnsAsync(mockTasks);
        mockJiraClient.Setup(x => x.GetIssue(mockTask)).ReturnsAsync(mockJiraIssue);

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        // Should show "no tasks need to be updated" because whitespace differences should be ignored
        Assert.That(output, Does.Contain("No tasks need to be updated"));
        
        // Should NOT ask to update tasks
        Assert.That(output, Does.Not.Contain("Update"));
        
        // Verify UpdateTaskName was never called
        mockClockifyClient.Verify(x => x.UpdateTaskName(It.IsAny<WorkspaceInfo>(), It.IsAny<ProjectInfo>(), It.IsAny<TaskInfo>(), It.IsAny<string>()), Times.Never);
    }
}
