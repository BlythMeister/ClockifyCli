using ClockifyCli.Commands;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class StatusCommandTests
{
    [Test]
    public void Constructor_WithDependencies_ShouldCreateSuccessfully()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var console = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new StatusCommand(clockifyClient, console));
        
        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithNoRunningTimer_ShouldDisplayNoTimerMessage()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        
        // Mock user info
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace1"}""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                .Respond("application/json", userJson);
        
        // Mock workspaces
        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspacesJson);
        
        // Mock no running timer
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var console = new TestConsole();
        var command = new StatusCommand(clockifyClient, console);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify no timer message
        var output = console.Output;
        Assert.That(output, Does.Contain("No time entry currently running"));
        
        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimer_ShouldDisplayTimerInfo()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        
        // Mock user info
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace1"}""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                .Respond("application/json", userJson);
        
        // Mock workspaces
        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspacesJson);
        
        // Mock running timer
        var runningTimerJson = """[{"id":"timer123","description":"Working on task","timeInterval":{"start":"2024-01-01T09:00:00Z"},"projectId":"project1","taskId":"task1"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                .Respond("application/json", runningTimerJson);
        
        // Mock projects
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects?page=1&page-size=100")
                .Respond("application/json", projectsJson);
        
        // Mock tasks
        var tasksJson = """[{"id":"task1","name":"Test Task"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks?page=1&page-size=100")
                .Respond("application/json", tasksJson);

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var console = new TestConsole();
        var command = new StatusCommand(clockifyClient, console);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify timer info is displayed
        var output = console.Output;
        Assert.That(output, Does.Contain("Current Clockify Status"));
        Assert.That(output, Does.Contain("Working on task"));
        
        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }
}
