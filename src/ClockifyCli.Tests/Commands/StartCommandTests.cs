using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Tests.Infrastructure;
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
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0));

        // Act & Assert
        Assert.DoesNotThrow(() => new StartCommand(clockifyClient, testConsole, mockClock));

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

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

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

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

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

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

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

    [Test]
    public async Task ExecuteAsync_WithStartTimeNow_ShouldStartTimerImmediately()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2024-01-01T09:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Simulate user selections
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Now" for start time
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Start New Timer"), "Should display start timer header");
        Assert.That(output, Does.Contain("When do you want to start the timer?"), "Should ask for start time option");
        Assert.That(output, Does.Contain("Start time: Now"), "Should show 'Now' as start time in confirmation");
        Assert.That(output, Does.Contain("Timer started successfully!"), "Should display success message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithEarlierStartTime_ShouldStartTimerWithSpecifiedTime()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2024-01-01T08:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Simulate user selections
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.DownArrow); // Navigate to "Earlier time"
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Earlier time"
        testConsole.Input.PushTextWithEnter("08:00"); // Enter start time
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Start New Timer"), "Should display start timer header");
        Assert.That(output, Does.Contain("When do you want to start the timer?"), "Should ask for start time option");
        Assert.That(output, Does.Contain("Start time (HH:mm or HH:mm:ss):"), "Should prompt for specific time");
        Assert.That(output, Does.Contain("Start time: 08:00:00"), "Should show specified start time in confirmation");
        Assert.That(output, Does.Contain("Timer started successfully!"), "Should display success message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidTimeFormat_ShouldShowValidationError()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2024-01-01T08:30:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Simulate user selections
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.DownArrow); // Navigate to "Earlier time"
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Earlier time"
        testConsole.Input.PushTextWithEnter("invalid-time"); // Enter invalid time format
        testConsole.Input.PushTextWithEnter("08:30"); // Enter valid time after error
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0)); var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Please enter a valid time format"), "Should show validation error for invalid time format");
        Assert.That(output, Does.Contain("Timer started successfully!"), "Should eventually start timer with valid time");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithFutureTimeAsEarlierTime_ShouldInterpretAsYesterday()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2023-12-31T23:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Set up a mock clock with current time of 08:00 (8 AM)
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 8, 0, 0));

        // Use a time that would be "in the future" if interpreted as today, but within 10-hour window when interpreted as yesterday
        // With current time at 08:00, entering "23:00" should be interpreted as yesterday 23:00 (9 hours ago - within 10-hour limit)
        var futureTime = "23:00";
        
        // Simulate user selections
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.DownArrow); // Move to "Earlier time"
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Earlier time"
        testConsole.Input.PushTextWithEnter(futureTime); // Enter future time (should be interpreted as yesterday)
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Start New Timer"), "Should display start timer header");
        Assert.That(output, Does.Contain("(yesterday)"), "Should show that time is interpreted as yesterday");
        Assert.That(output, Does.Contain("Timer started successfully!"), "Should display success message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithTimeMoreThan10HoursAgo_ShouldShowValidationError()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response (for the valid time)
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2024-01-01T09:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Set up a mock clock with current time of 14:00 (2 PM)
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0));

        // Use a time that would be more than 10 hours ago when interpreted as yesterday
        // With current time at 14:00, entering "02:00" should be interpreted as yesterday 02:00 (12 hours ago - exceeds 10-hour limit)
        var tooEarlyTime = "02:00";
        var validTime = "09:00"; // Within 10 hours (5 hours ago when interpreted as yesterday)
        
        // Simulate user selections - first try invalid time, then valid time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.DownArrow); // Move to "Earlier time"
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Earlier time"
        testConsole.Input.PushTextWithEnter(tooEarlyTime); // Enter time more than 10 hours ago (should show error)
        testConsole.Input.PushTextWithEnter(validTime); // Enter valid time (within 10 hours)
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert - the command should complete successfully after the user corrects the time
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Start time cannot be more than 10 hours ago"), "Should show validation error for time more than 10 hours ago");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithPastTimeMoreThan10HoursAgo_ShouldShowValidationError()
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

        // Mock no current time entry
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        // Mock projects response
        var projectsJson = """[{"id":"project1","name":"Test Project"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects")
                        .Respond("application/json", projectsJson);

        // Mock tasks response
        var tasksJson = """[{"id":"task1","name":"Test Task","status":"Active"}]""";
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/projects/project1/tasks")
                        .Respond("application/json", tasksJson);

        // Mock start time entry response (for the valid time)
        var startedEntryJson = """{"id":"entry456","description":"Test timer","timeInterval":{"start":"2024-01-01T12:00:00Z"}}""";
        clockifyMockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace1/time-entries")
                        .Respond("application/json", startedEntryJson);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole().Interactive();

        // Set up a mock clock with current time of 14:00 (2 PM)
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0));

        // Use a time that would be more than 10 hours ago as a past time (not future interpreted as yesterday)
        // With current time at 14:00, entering "03:00" should be interpreted as today 03:00 (11 hours ago - exceeds 10-hour limit)
        var tooEarlyPastTime = "03:00";
        var validTime = "12:00"; // Within 10 hours (2 hours ago)
        
        // Simulate user selections - first try invalid past time, then valid time
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select first task
        testConsole.Input.PushTextWithEnter("Test description"); // Enter description
        testConsole.Input.PushKey(ConsoleKey.DownArrow); // Move to "Earlier time"
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select "Earlier time"
        testConsole.Input.PushTextWithEnter(tooEarlyPastTime); // Enter past time more than 10 hours ago (should show error)
        testConsole.Input.PushTextWithEnter(validTime); // Enter valid time (within 10 hours)
        testConsole.Input.PushTextWithEnter("y"); // Confirm start

        var command = new StartCommand(clockifyClient, testConsole, mockClock);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert - the command should complete successfully after the user corrects the time
        Assert.That(result, Is.EqualTo(0));

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Start time cannot be more than 10 hours ago"), "Should show validation error for past time more than 10 hours ago");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }
}

