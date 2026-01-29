using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Moq;
using NUnit.Framework;
using Spectre.Console.Testing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class ProjectListHelperTests
{
    private static AppConfiguration CreateConfigWithRecents(int count = 5, int days = 7)
    {
        return AppConfiguration.Empty with { RecentTasksCount = count, RecentTasksDays = days };
    }

    private static TestConsole CreateInteractiveConsole()
    {
        return new TestConsole().Interactive();
    }

    [Test]
    public async Task PromptForProjectAndTaskAsync_WhenSelectingRecentTask_ReturnsRecentSelectionWithoutTimestamp()
    {
        // Arrange
        var clockifyClient = new Mock<IClockifyClient>();
        var jiraClient = new Mock<IJiraClient>();
        var console = CreateInteractiveConsole();

        var workspace = new WorkspaceInfo("ws-1", "Workspace One");
        var user = new UserInfo("user-1", "Test User", "user@example.com", workspace.Id);
        var config = CreateConfigWithRecents();

        var project = new ProjectInfo("proj-1", "Project Alpha");
        var task = new TaskInfo("task-1", "Task A", "Active");

        clockifyClient.Setup(c => c.GetProjects(workspace))
            .ReturnsAsync(new List<ProjectInfo> { project });

        clockifyClient.Setup(c => c.GetTasks(workspace, project))
            .ReturnsAsync(new List<TaskInfo> { task });

        var recentEntry = new TimeEntry(
            "entry-1",
            "Recent work",
            task.Id,
            project.Id,
            "REGULAR",
            new TimeInterval("2024-01-02T09:00:00Z", "2024-01-02T10:00:00Z"));

        clockifyClient.Setup(c => c.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TimeEntry> { recentEntry });

        console.Input.PushKey(ConsoleKey.Enter);

        // Act
        var result = await ProjectListHelper.PromptForProjectAndTaskAsync(
            clockifyClient.Object,
            jiraClient.Object,
            console,
            workspace,
            config,
            user);

        // Assert
        Assert.That(result, Is.Not.Null, "Result should be returned when selecting a recent task.");
        Assert.That(result!.Value.Project, Is.EqualTo(project));
        Assert.That(result.Value.Task, Is.EqualTo(task));
        Assert.That(console.Output, Does.Contain("Project Alpha > Task A"),
            "Recent task display should show project and task without timestamp.");
        Assert.That(console.Output, Does.Not.Contain("Last used").IgnoreCase,
            "Recent task display should not include last used timestamps.");
    }

    [Test]
    public async Task PromptForProjectAndTaskAsync_WhenCreatingNewTaskFromRecent_AddsTaskAndReturnsCreatedTask()
    {
        // Arrange
        var clockifyClient = new Mock<IClockifyClient>();
        var jiraClient = new Mock<IJiraClient>();
        var console = CreateInteractiveConsole();

        var workspace = new WorkspaceInfo("ws-1", "Workspace One");
        var user = new UserInfo("user-1", "Test User", "user@example.com", workspace.Id);
        var config = CreateConfigWithRecents();

        var project = new ProjectInfo("proj-1", "Project Alpha");
        var existingTask = new TaskInfo("task-1", "Task A", "Active");
        var createdTaskName = "PROJ-1 [Implement feature]";
        var createdTask = new TaskInfo("task-2", createdTaskName, "Active");

        clockifyClient.Setup(c => c.GetProjects(workspace))
            .ReturnsAsync(new List<ProjectInfo> { project });

        clockifyClient.SetupSequence(c => c.GetTasks(workspace, project))
            .ReturnsAsync(new List<TaskInfo> { existingTask })
            .ReturnsAsync(new List<TaskInfo> { existingTask, createdTask });

        var recentEntry = new TimeEntry(
            "entry-1",
            "Recent work",
            existingTask.Id,
            project.Id,
            "REGULAR",
            new TimeInterval("2024-01-02T09:00:00Z", "2024-01-02T10:00:00Z"));

        clockifyClient.Setup(c => c.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TimeEntry> { recentEntry });

        clockifyClient.Setup(c => c.AddTask(workspace, project, createdTaskName))
            .Returns(Task.CompletedTask)
            .Verifiable();

        jiraClient.Setup(j => j.GetIssue("PROJ-1"))
            .ReturnsAsync(new JiraIssue(
                123,
                "PROJ-1",
                new JiraIssueFields(
                    new JiraTimeTracking(""),
                    new JiraStatus("In Progress", new JiraStatusCategory("in-progress", "In Progress")),
                    "Implement feature")));

        console.Input.PushKey(ConsoleKey.DownArrow); // Move to "Other task"
        console.Input.PushKey(ConsoleKey.Enter);     // Choose other task menu
        console.Input.PushKey(ConsoleKey.Enter);     // Select existing project
        console.Input.PushKey(ConsoleKey.DownArrow); // Move to "+ Add new task"
        console.Input.PushKey(ConsoleKey.Enter);     // Select new task option
        console.Input.PushTextWithEnter("PROJ-1");  // Provide Jira reference
        console.Input.PushTextWithEnter("y");       // Confirm creation

        // Act
        var result = await ProjectListHelper.PromptForProjectAndTaskAsync(
            clockifyClient.Object,
            jiraClient.Object,
            console,
            workspace,
            config,
            user);

        // Assert
        Assert.That(result, Is.Not.Null, "Result should be returned when creating a new task.");
        Assert.That(result!.Value.Project, Is.EqualTo(project));
        Assert.That(result.Value.Task.Id, Is.EqualTo(createdTask.Id));
        Assert.That(result.Value.Task.Name, Is.EqualTo(createdTask.Name));

        clockifyClient.Verify();
        clockifyClient.Verify(c => c.GetTasks(workspace, project), Times.Exactly(2));
        jiraClient.Verify(j => j.GetIssue("PROJ-1"), Times.Once);
    }

    [Test]
    public async Task PromptForProjectAndTaskAsync_WhenReturningFromProjectsToRecents_ReplaysRecentSelection()
    {
        // Arrange
        var clockifyClient = new Mock<IClockifyClient>();
        var jiraClient = new Mock<IJiraClient>();
        var console = CreateInteractiveConsole();

        var workspace = new WorkspaceInfo("ws-1", "Workspace One");
        var user = new UserInfo("user-1", "Test User", "user@example.com", workspace.Id);
        var config = CreateConfigWithRecents();

        var projectAlpha = new ProjectInfo("proj-a", "Alpha");
        var projectBeta = new ProjectInfo("proj-b", "Beta");
        var taskAlpha = new TaskInfo("task-a", "Task Alpha", "Active");
        var taskBeta = new TaskInfo("task-b", "Task Beta", "Active");

        clockifyClient.Setup(c => c.GetProjects(workspace))
            .ReturnsAsync(new List<ProjectInfo> { projectAlpha, projectBeta });

        clockifyClient.Setup(c => c.GetTasks(workspace, projectAlpha))
            .ReturnsAsync(new List<TaskInfo> { taskAlpha });
        clockifyClient.Setup(c => c.GetTasks(workspace, projectBeta))
            .ReturnsAsync(new List<TaskInfo> { taskBeta });

        var recentEntry = new TimeEntry(
            "entry-1",
            "Recent work",
            taskAlpha.Id,
            projectAlpha.Id,
            "REGULAR",
            new TimeInterval("2024-01-02T09:00:00Z", "2024-01-02T10:00:00Z"));

        clockifyClient.Setup(c => c.GetTimeEntries(workspace, user, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TimeEntry> { recentEntry });

        console.Input.PushKey(ConsoleKey.DownArrow); // Choose "Other task" from recents
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.DownArrow); // Move to project Beta
        console.Input.PushKey(ConsoleKey.DownArrow); // Move to "+ Add new task"
        console.Input.PushKey(ConsoleKey.DownArrow); // Move to "← Back to recent tasks"
        console.Input.PushKey(ConsoleKey.Enter);     // Go back to recent tasks
        console.Input.PushKey(ConsoleKey.Enter);     // Select the recent entry

        // Act
        var result = await ProjectListHelper.PromptForProjectAndTaskAsync(
            clockifyClient.Object,
            jiraClient.Object,
            console,
            workspace,
            config,
            user);

        // Assert
        Assert.That(result, Is.Not.Null, "Returning to recents should still allow selecting an entry.");
        Assert.That(result!.Value.Project, Is.EqualTo(projectAlpha));
        Assert.That(result.Value.Task, Is.EqualTo(taskAlpha));

    }

    [Test]
    public async Task PromptForProjectAndTaskAsync_WhenTaskExistsAndUserChoosesToUseIt_ReturnsExistingTaskWithValidId()
    {
        // Arrange
        var clockifyClient = new Mock<IClockifyClient>();
        var jiraClient = new Mock<IJiraClient>();
        var console = CreateInteractiveConsole();

        var workspace = new WorkspaceInfo("ws-1", "Workspace One");
        var user = new UserInfo("user-1", "Test User", "user@example.com", workspace.Id);
        var config = CreateConfigWithRecents(0, 0); // Disable recent tasks for this test

        var project = new ProjectInfo("proj-1", "Project Alpha");
        var existingTaskName = "PROJ-123 [Existing feature]";
        var existingTask = new TaskInfo("task-existing-123", existingTaskName, "Active");

        clockifyClient.Setup(c => c.GetProjects(workspace))
            .ReturnsAsync(new List<ProjectInfo> { project });

        clockifyClient.Setup(c => c.GetTasks(workspace, project))
            .ReturnsAsync(new List<TaskInfo> { existingTask });

        jiraClient.Setup(j => j.GetIssue("PROJ-123"))
            .ReturnsAsync(new JiraIssue(
                123,
                "PROJ-123",
                new JiraIssueFields(
                    new JiraTimeTracking(""),
                    new JiraStatus("In Progress", new JiraStatusCategory("in-progress", "In Progress")),
                    "Existing feature")));

        console.Input.PushKey(ConsoleKey.Enter);     // Select project
        console.Input.PushKey(ConsoleKey.DownArrow); // Move to "+ Add new task"
        console.Input.PushKey(ConsoleKey.Enter);     // Select new task option
        console.Input.PushTextWithEnter("PROJ-123"); // Provide Jira reference
        console.Input.PushTextWithEnter("y");        // Confirm to use existing task

        // Act
        var result = await ProjectListHelper.PromptForProjectAndTaskAsync(
            clockifyClient.Object,
            jiraClient.Object,
            console,
            workspace,
            config,
            user);

        // Assert
        Assert.That(result, Is.Not.Null, "Result should be returned when choosing to use existing task.");
        Assert.That(result!.Value.Project, Is.EqualTo(project), "Returned project should match selected project.");
        Assert.That(result.Value.Task.Id, Is.EqualTo(existingTask.Id), "Returned task ID should match existing task ID.");
        Assert.That(result.Value.Task.Name, Is.EqualTo(existingTask.Name), "Returned task name should match existing task name.");
        Assert.That(result.Value.Task.Id, Is.Not.Empty, "Task ID should not be empty.");
        Assert.That(result.Value.Task.Id, Is.Not.Null, "Task ID should not be null.");

        // Verify that AddTask was NOT called since task already exists
        clockifyClient.Verify(c => c.AddTask(It.IsAny<WorkspaceInfo>(), It.IsAny<ProjectInfo>(), It.IsAny<string>()), Times.Never);
        jiraClient.Verify(j => j.GetIssue("PROJ-123"), Times.Once);
    }
}
