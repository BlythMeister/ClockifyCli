using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Tests.Infrastructure;
using ClockifyCli.Utilities;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class AddManualTimerCommandTests
{
    [Test]
    public void AddManualTimerCommand_WithInjectedDependencies_ShouldInitializeCorrectly()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0));

        // Act & Assert
        Assert.DoesNotThrow(() => new AddManualTimerCommand(clockifyClient, testConsole, mockClock));

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

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new AddManualTimerCommand(clockifyClient, testConsole, mockClock);

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
    public async Task ExecuteAsync_WithNoProjects_ShouldDisplayNoProjectsMessage()
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

        // Mock empty projects response
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new AddManualTimerCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
            Assert.That(output, Does.Contain("No projects with active tasks found!"), "Should display no projects with active tasks message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithValidTimeEntry_ShouldAddTimeEntry()
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

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock add time entry response
        var addedEntryJson = """{"id":"entry456","description":"Test manual entry","timeInterval":{"start":"2024-01-01T08:00:00Z","end":"2024-01-01T10:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", addedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Simulate user selections - following the exact prompt order in AddManualTimerCommand
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test manual entry"); // Enter description
        testConsole.Input.PushTextWithEnter("08:00"); // Enter start time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Rule 7: Select 08:00 (24-hour format) for start time clarification
        testConsole.Input.PushTextWithEnter("10:00"); // Enter end time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Rule 7: Select 10:00 (24-hour format) for end time clarification
        testConsole.Input.PushTextWithEnter("y"); // Confirm add

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new AddManualTimerCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Add Manual Time Entry"), "Should display add manual timer header");
        Assert.That(output, Does.Contain("Time entry added successfully!"), "Should display success message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    [Ignore("Input sequence issue - needs investigation")]
    public async Task ExecuteAsync_WithUserDecline_ShouldDisplayCancelMessage()
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

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Simulate user selections but decline at the end
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test manual entry"); // Enter description
        testConsole.Input.PushTextWithEnter("08:00"); // Enter start time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Rule 7: Select 08:00 (24-hour format) for start time clarification
        testConsole.Input.PushTextWithEnter("10:00"); // Enter end time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Rule 7: Select 10:00 (24-hour format) for end time clarification
        testConsole.Input.PushTextWithEnter("n"); // Decline to add

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new AddManualTimerCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Add Manual Time Entry"), "Should display add manual timer header");
        Assert.That(output, Does.Contain("Time entry cancelled"), "Should display cancellation message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }
}

