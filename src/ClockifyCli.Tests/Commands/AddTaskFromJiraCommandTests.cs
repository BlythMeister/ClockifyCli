using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;
using RichardSzalay.MockHttp;
using ClockifyCli.Services;
using Spectre.Console.Cli;
using Moq;

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
}
