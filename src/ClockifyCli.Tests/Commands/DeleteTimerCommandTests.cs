using ClockifyCli.Commands;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class DeleteTimerCommandTests
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
        Assert.DoesNotThrow(() => new DeleteTimerCommand(clockifyClient, console));
        
        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithNoCompletedTimers_ShouldDisplayNoTimersMessage()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        
        // Mock user and workspace calls
        var userJson = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace123"}""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                .Respond("application/json", userJson);
        
        var workspacesJson = """[{"id":"workspace1","name":"Test Workspace"}]""";
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspacesJson);
        
        // Mock no completed timers (empty array)
        mockHttp.When(HttpMethod.Get, "*")
                .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var console = new TestConsole();
        var command = new DeleteTimerCommand(clockifyClient, console);

        // Act
        var result = await command.ExecuteAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        // Should complete without errors
        var output = console.Output;
        Assert.That(output, Is.Not.Empty);
        
        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }
}
