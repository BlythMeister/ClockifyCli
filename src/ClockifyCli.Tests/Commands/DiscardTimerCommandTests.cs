using ClockifyCli.Commands;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class DiscardTimerCommandTests
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
        Assert.DoesNotThrow(() => new DiscardTimerCommand(clockifyClient, console));

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

        // Mock user and workspace calls
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace1"}""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                .Respond("application/json", userJson);

        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspacesJson);

        // Mock no running timer
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces/workspace1/user/user123/time-entries?in-progress=true")
                .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var console = new TestConsole();
        var command = new DiscardTimerCommand(clockifyClient, console);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));

        // Verify no timer message
        var output = console.Output;
        Assert.That(output, Does.Contain("No time entry is currently running"));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }
}
