using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class StartCommandTests
{
    [Test]
    public void StartCommand_WithInjectedDependencies_ShouldInitializeCorrectly()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);
        
        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();
        
        // Act & Assert
        Assert.DoesNotThrow(() => new StartCommand(clockifyClient, testConsole));
        
        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]  
    public async Task ExecuteAsync_WithNoWorkspace_ShouldDisplayErrorMessage()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);
        
        // Mock user info (required first call)
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace123"}""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                        .Respond("application/json", userJson);
        
        // Mock empty workspaces response
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                        .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();
        
        var command = new StartCommand(clockifyClient, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify error message was displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"), "Should display no workspace error message");
        
        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]  
    public async Task ExecuteAsync_WithRunningTimer_WhenUserDeclines_ShouldDisplayCancelMessage()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);
        
        // Mock user info
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace123"}""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                        .Respond("application/json", userJson);
        
        // Mock workspaces response
        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                        .Respond("application/json", workspacesJson);
        
        // Mock current time entry (running timer)
        var currentEntryJson = """{"id":"entry123","description":"Running timer","timeInterval":{"start":"2024-01-01T09:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", $"[{currentEntryJson}]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();
        
        // Simulate user declining to stop current timer
        testConsole.Input.PushTextWithEnter("n"); // Answer "No" to the confirmation prompt
        
        var command = new StartCommand(clockifyClient, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify the appropriate messages were displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("A timer is already running!"), "Should display already running warning message");
        Assert.That(output, Does.Contain("Do you want to stop the current timer and start a new one?"), "Should ask for confirmation");
        Assert.That(output, Does.Contain("Timer start cancelled"), "Should display cancellation message");
        
        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]  
    public async Task ExecuteAsync_WithRunningTimer_WhenUserAccepts_ShouldStopCurrentTimer()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);
        
        // Mock user info
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace123"}""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                        .Respond("application/json", userJson);
        
        // Mock workspaces response
        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                        .Respond("application/json", workspacesJson);
        
        // Mock current time entry (running timer)
        var currentEntryJson = """{"id":"entry123","description":"Running timer","timeInterval":{"start":"2024-01-01T09:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", $"[{currentEntryJson}]");

        // Mock stop timer endpoint
        var stoppedEntryJson = """{"id":"entry123","description":"Running timer","timeInterval":{"start":"2024-01-01T09:00:00Z","end":"2024-01-01T10:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Patch, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries")
                        .Respond("application/json", stoppedEntryJson);

        // Mock projects endpoint (will get called but we'll stop before selecting)
        var projectsJson = """[]"""; // Empty projects to stop the selection process
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();
        
        // Simulate user accepting to stop current timer
        testConsole.Input.PushTextWithEnter("y"); // Answer "Yes" to the confirmation prompt
        
        var command = new StartCommand(clockifyClient, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Verify the appropriate messages were displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("A timer is already running!"), "Should display already running warning message");
        Assert.That(output, Does.Contain("Do you want to stop the current timer and start a new one?"), "Should ask for confirmation");
        Assert.That(output, Does.Contain("Current timer stopped"), "Should display timer stopped message");
        
        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }
}
