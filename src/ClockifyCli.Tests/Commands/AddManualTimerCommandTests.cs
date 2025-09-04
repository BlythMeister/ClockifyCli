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
        Assert.That(output, Does.Contain("No projects found!"), "Should display no projects found message");

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
        testConsole.Input.PushTextWithEnter("10:00"); // Enter end time
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
        testConsole.Input.PushTextWithEnter("10:00"); // Enter end time
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

    #region Ambiguous Time Tests

    [Test]
    public void AddManualTimerCommand_HasCheckAndConfirmAmbiguousTimeMethod()
    {
        // Verify that AddManualTimerCommand has the CheckAndConfirmAmbiguousTime static method
        var methodInfo = typeof(AddManualTimerCommand).GetMethod("CheckAndConfirmAmbiguousTime", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        Assert.That(methodInfo, Is.Not.Null, "AddManualTimerCommand should have CheckAndConfirmAmbiguousTime static method");
        Assert.That(methodInfo.ReturnType, Is.EqualTo(typeof(string)), "Method should return string");
        
        var parameters = methodInfo.GetParameters();
        Assert.That(parameters.Length, Is.EqualTo(3), "Method should have 3 parameters");
        Assert.That(parameters[0].ParameterType.Name, Does.Contain("IAnsiConsole"), "First parameter should be IAnsiConsole");
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(string)), "Second parameter should be string");
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(string)), "Third parameter should be string");
    }

    [TestCase("4:37")]
    [TestCase("9:15")]
    [TestCase("1:30")]
    [TestCase("8:45")]
    [TestCase("2:00")]
    [TestCase("7:22")]
    public void AddManualTimerCommand_AmbiguousTimeDetection_ShouldIdentifyAmbiguousTimes(string timeInput)
    {
        // Test that various ambiguous time inputs are correctly identified
        var isAmbiguous = IntelligentTimeParser.IsAmbiguousTime(timeInput);
        Assert.That(isAmbiguous, Is.True, $"{timeInput} should be considered ambiguous");
    }

    [TestCase("10:30")]
    [TestCase("14:45")]
    [TestCase("23:59")]
    [TestCase("00:15")]
    [TestCase("12:00")]
    [TestCase("13:30")]
    public void AddManualTimerCommand_NonAmbiguousTimeDetection_ShouldNotIdentifyAsAmbiguous(string timeInput)
    {
        // Test that non-ambiguous time inputs are correctly identified
        var isAmbiguous = IntelligentTimeParser.IsAmbiguousTime(timeInput);
        Assert.That(isAmbiguous, Is.False, $"{timeInput} should not be considered ambiguous");
    }

    [Test]
    public void AddManualTimerCommand_AmbiguousStartTimeContext_ShouldInferCorrectly()
    {
        // Test that when a start time is ambiguous, it gets interpreted correctly in context
        var baseTime = new DateTime(2024, 1, 15, 14, 0, 0); // 2 PM context
        
        // Test "4:37" in afternoon context should be interpreted as PM
        var success = IntelligentTimeParser.TryParseStartTime("4:37", out var result, baseTime);
        Assert.That(success, Is.True, "Should successfully parse 4:37");
        Assert.That(result.Hours, Is.EqualTo(16), "Should interpret 4:37 as 4:37 PM (16:37) in afternoon context");
        Assert.That(result.Minutes, Is.EqualTo(37), "Minutes should be preserved");
        
        // Test "9:30" in afternoon context should be interpreted as AM (next day)
        success = IntelligentTimeParser.TryParseStartTime("9:30", out result, baseTime);
        Assert.That(success, Is.True, "Should successfully parse 9:30");
        Assert.That(result.Hours, Is.EqualTo(9), "Should interpret 9:30 as 9:30 AM in afternoon context");
        Assert.That(result.Minutes, Is.EqualTo(30), "Minutes should be preserved");
    }

    [Test]
    public void AddManualTimerCommand_AmbiguousEndTimeContext_ShouldInferCorrectly()
    {
        // Test that when an end time is ambiguous, it gets interpreted correctly based on start time
        var startTime = new DateTime(2024, 1, 15, 14, 58, 0); // Start at 2:58 PM
        
        // Test "4:37" as end time should be interpreted as 4:37 PM
        var success = IntelligentTimeParser.TryParseEndTime("4:37", out var result, startTime);
        Assert.That(success, Is.True, "Should successfully parse 4:37");
        Assert.That(result.Hours, Is.EqualTo(16), "Should interpret 4:37 as 4:37 PM (16:37) given the start time context");
        Assert.That(result.Minutes, Is.EqualTo(37), "Minutes should be preserved");
        
        // Test "2:15" as end time after 2:58 PM start should be interpreted as next day 2:15 AM
        success = IntelligentTimeParser.TryParseEndTime("2:15", out result, startTime);
        Assert.That(success, Is.True, "Should successfully parse 2:15");
        Assert.That(result.Hours, Is.EqualTo(2), "Should interpret 2:15 as 2:15 AM next day");
        Assert.That(result.Minutes, Is.EqualTo(15), "Minutes should be preserved");
    }

    [Test]
    public void AddManualTimerCommand_AmbiguousTimeOptions_ShouldProvideCorrectDisplayFormats()
    {
        // Test that ambiguous time options are formatted correctly for user display
        var timeInput = "5:20";
        var success = IntelligentTimeParser.TryParseStartTime(timeInput, out var parsedTime, DateTime.Today.AddHours(10));
        Assert.That(success, Is.True, "Should parse successfully");
        
        var (amVersion, pmVersion, display24Hour, displayAmPm) = IntelligentTimeParser.GetAmbiguousTimeOptions(timeInput, parsedTime);
        
        Assert.That(amVersion.Hours, Is.EqualTo(5), "AM version should be 5:20 AM");
        Assert.That(amVersion.Minutes, Is.EqualTo(20), "AM version minutes should be preserved");
        
        Assert.That(pmVersion.Hours, Is.EqualTo(17), "PM version should be 5:20 PM (17:20)");
        Assert.That(pmVersion.Minutes, Is.EqualTo(20), "PM version minutes should be preserved");
        
        // The display formats are based on the interpretedTime (parsedTime)
        // In morning context (10 AM), 5:20 should be interpreted as 17:20 (5:20 PM)
        Assert.That(display24Hour, Is.EqualTo("17:20"), "24-hour display should show the interpreted time");
        Assert.That(displayAmPm, Is.EqualTo("5:20 PM"), "12-hour display should show the interpreted time");
    }

    #endregion
}

