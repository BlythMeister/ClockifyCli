using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class StopCommandTests
{
    [Test]
    public void StopCommand_WithInjectedDependencies_ShouldInitializeCorrectly()
    {
        // Arrange
        var clockifyMockHttp = new MockHttpMessageHandler();
        var clockifyHttpClient = new HttpClient(clockifyMockHttp);

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new StopCommand(clockifyClient, testConsole));

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

        var command = new StopCommand(clockifyClient, testConsole);

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
    public async Task ExecuteAsync_WithNoRunningTimer_ShouldDisplayNoTimerMessage()
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

        // Mock no current time entry (no running timer)
        clockifyMockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                        .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(clockifyHttpClient, "test-key");
        var testConsole = new TestConsole();

        var command = new StopCommand(clockifyClient, testConsole);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        // Verify no timer message was displayed
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No time entry is currently running"), "Should display no timer running message");

        // Cleanup
        clockifyMockHttp.Dispose();
        clockifyHttpClient.Dispose();
    }
}
