using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;
using RichardSzalay.MockHttp;
using ClockifyCli.Services;
using Spectre.Console.Cli;
using Moq;
using System.Net;
using ClockifyCli.Models;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class AddTaskFromJiraCommandTests
{
    [Test]
    public void Constructor_WithDependencies_ShouldCreateSuccessfully()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var jiraClient = new JiraClient(httpClient, "test-user", "test-token");
        var console = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new AddTaskFromJiraCommand(clockifyClient, jiraClient, console));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public void AddTaskFromJiraCommand_WithInjectedDependencies_ShouldInitializeCorrectly()
    {
        // Arrange - separate HttpClients to avoid base address conflicts
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        var jiraMockHttp = new MockHttpMessageHandler();
        var jiraHttpClient = new HttpClient(jiraMockHttp);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var jiraClient = new JiraClient(jiraHttpClient, "test-user", "test-token");
        var testConsole = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new AddTaskFromJiraCommand(clockifyClient, jiraClient, testConsole));

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
        jiraMockHttp.Dispose();
        jiraHttpClient.Dispose();
    }

    [Test]
    public async Task AddTask_WithNoWorkspace_ShouldDisplayErrorMessage()
    {
        // Arrange - separate HttpClients to avoid base address conflicts
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        var jiraMockHttp = new MockHttpMessageHandler();
        var jiraHttpClient = new HttpClient(jiraMockHttp);

        // Mock empty workspaces response
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                        .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var jiraClient = new JiraClient(jiraHttpClient, "test-user", "test-token");
        var testConsole = new TestConsole();

        var command = new AddTaskFromJiraCommand(clockifyClient, jiraClient, testConsole);
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        var settings = new AddTaskFromJiraCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        // Verify error message was displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"), "Should display no workspace error message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
        jiraMockHttp.Dispose();
        jiraHttpClient.Dispose();
    }

    [Test]
    public async Task SearchIssues_Integration_ShouldReturnValidResults()
    {
        // This test focuses on the JQL search integration rather than console interaction
        // Arrange
        var jiraMockHttp = new MockHttpMessageHandler();
        var jiraHttpClient = new HttpClient(jiraMockHttp);

        // Mock JQL search response with duplicate scenario
        jiraMockHttp.When(HttpMethod.Post, "https://15below.atlassian.net/rest/api/3/search")
                    .Respond("application/json", "{\"startAt\":0,\"maxResults\":100,\"total\":2,\"issues\":[{\"id\":\"10001\",\"key\":\"TEST-123\",\"fields\":{\"summary\":\"Existing Task\",\"status\":{\"name\":\"In Progress\"}}},{\"id\":\"10002\",\"key\":\"TEST-456\",\"fields\":{\"summary\":\"New Task\",\"status\":{\"name\":\"To Do\"}}}]}");

        var jiraClient = new JiraClient(jiraHttpClient, "test-user", "test-token");

        // Act
        var result = await jiraClient.SearchIssues("project = TEST", 100);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Total, Is.EqualTo(2));
        Assert.That(result.Issues, Has.Count.EqualTo(2));
        Assert.That(result.Issues[0].Key, Is.EqualTo("TEST-123"));
        Assert.That(result.Issues[0].Fields?.Summary, Is.EqualTo("Existing Task"));
        Assert.That(result.Issues[1].Key, Is.EqualTo("TEST-456"));
        Assert.That(result.Issues[1].Fields?.Summary, Is.EqualTo("New Task"));

        // Cleanup
        jiraMockHttp.Dispose();
        jiraHttpClient.Dispose();
    }

    [Test]
    public async Task GetTasks_Integration_ShouldReturnExistingTasks()
    {
        // This test verifies that we can properly check for existing tasks
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        // Mock existing tasks response
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", "[{\"id\":\"task1\",\"name\":\"TEST-123 [Existing Task]\",\"status\":\"ACTIVE\"},{\"id\":\"task2\",\"name\":\"TEST-456 [Another Task]\",\"status\":\"ACTIVE\"}]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");

        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var project = new ProjectInfo("project1", "Test Project");

        // Act
        var tasks = await clockifyClient.GetTasks(workspace, project);

        // Assert
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(2));
        Assert.That(tasks[0].Name, Is.EqualTo("TEST-123 [Existing Task]"));
        Assert.That(tasks[1].Name, Is.EqualTo("TEST-456 [Another Task]"));

        // Test duplicate detection logic (this is what happens in the command)
        var newTaskName = "TEST-123 [Existing Task]";
        var isDuplicate = tasks.Any(t => t.Name.Equals(newTaskName, StringComparison.OrdinalIgnoreCase));
        Assert.That(isDuplicate, Is.True, "Should detect duplicate task");

        var uniqueTaskName = "TEST-789 [New Task]";
        var isUnique = !tasks.Any(t => t.Name.Equals(uniqueTaskName, StringComparison.OrdinalIgnoreCase));
        Assert.That(isUnique, Is.True, "Should identify unique task");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task FindJiraKeyInOtherProjects_ShouldDetectDuplicatesInOtherProjects()
    {
        // This test verifies that the system can detect when a JIRA task exists in another project
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        var workspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var currentProject = new ProjectInfo("project1", "Current Project");
        var otherProject1 = new ProjectInfo("project2", "Other Project 1");
        var otherProject2 = new ProjectInfo("project3", "Other Project 2");

        // Mock projects response - return all projects
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json",
                            "[" +
                            "{\"id\":\"project1\",\"name\":\"Current Project\"}," +
                            "{\"id\":\"project2\",\"name\":\"Other Project 1\"}," +
                            "{\"id\":\"project3\",\"name\":\"Other Project 2\"}" +
                            "]");

        // Mock tasks for current project (no tasks)
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", "[]");

        // Mock tasks for other project 1 (has TEST-123)
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project2/tasks")
                        .Respond("application/json",
                            "[{\"id\":\"task1\",\"name\":\"TEST-123 [Duplicate Task]\",\"status\":\"ACTIVE\"}]");

        // Mock tasks for other project 2 (has TEST-456, not TEST-123)
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project3/tasks")
                        .Respond("application/json",
                            "[{\"id\":\"task2\",\"name\":\"TEST-456 [Different Task]\",\"status\":\"ACTIVE\"}]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");

        // Act - search for TEST-123 in other projects
        var allProjects = await clockifyClient.GetProjects(workspace);
        var results = new List<(ProjectInfo Project, TaskInfo Task)>();

        foreach (var project in allProjects)
        {
            if (project.Id == currentProject.Id)
            {
                continue;
            }

            var tasks = await clockifyClient.GetTasks(workspace, project);
            foreach (var task in tasks)
            {
                // Extract JIRA key from task name
                var jiraKey = ExtractJiraKeyFromTaskName(task.Name);
                if (!string.IsNullOrEmpty(jiraKey) && jiraKey.Equals("TEST-123", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((project, task));
                }
            }
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(1), "Should find TEST-123 in one other project");
        Assert.That(results[0].Project.Id, Is.EqualTo("project2"), "Should be found in Other Project 1");
        Assert.That(results[0].Task.Name, Is.EqualTo("TEST-123 [Duplicate Task]"));

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public void ExtractJiraKeyFromTaskName_ShouldExtractCorrectly()
    {
        // Test various task name formats
        Assert.That(ExtractJiraKeyFromTaskName("TEST-123 [My Task]"), Is.EqualTo("TEST-123"));
        Assert.That(ExtractJiraKeyFromTaskName("PROJ-456 Some Description"), Is.EqualTo("PROJ-456"));
        Assert.That(ExtractJiraKeyFromTaskName("ABC-789[NoSpace]"), Is.EqualTo("ABC-789"));
        Assert.That(ExtractJiraKeyFromTaskName("XYZ-001"), Is.EqualTo("XYZ-001"));
        Assert.That(ExtractJiraKeyFromTaskName("  TEST-999  [Trimmed]  "), Is.EqualTo("TEST-999"));
        Assert.That(ExtractJiraKeyFromTaskName(""), Is.Null);
        Assert.That(ExtractJiraKeyFromTaskName(null!), Is.Null);
    }

    private static string? ExtractJiraKeyFromTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return null;
        }

        // Trim the input first
        var trimmed = taskName.Trim();

        // Find the first space or "[" to extract the JIRA key
        var endIndex = trimmed.IndexOfAny(new[] { ' ', '[' });
        if (endIndex > 0)
        {
            return trimmed.Substring(0, endIndex).Trim();
        }

        // If no delimiter found, the entire name might be the JIRA key
        return trimmed;
    }
}
