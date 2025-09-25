using ClockifyCli.Commands;
using NUnit.Framework;
using Spectre.Console.Testing;
using RichardSzalay.MockHttp;
using ClockifyCli.Services;
using ClockifyCli.Models;
using System.Net;
using Spectre.Console.Cli;
using Moq;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class AddProjectCommandTests
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
        Assert.DoesNotThrow(() => new AddProjectCommand(clockifyClient, console));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public void Constructor_WithNullClockifyClient_ShouldThrowArgumentNullException()
    {
        // Arrange
        var console = new TestConsole();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AddProjectCommand(null!, console));
    }

    [Test]
    public void Constructor_WithNullConsole_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        var clockifyClient = new ClockifyClient(httpClient, "test-key");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AddProjectCommand(clockifyClient, null!));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public void AddProjectCommand_WithInjectedDependencies_ShouldInitializeCorrectly()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);
        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var testConsole = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new AddProjectCommand(clockifyClient, testConsole));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task AddProject_WithNoWorkspace_ShouldDisplayErrorMessage()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);

        // Mock the workspace request to return empty list
        mockHttp.When("https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", "[]");

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var testConsole = new TestConsole();
        var command = new AddProjectCommand(clockifyClient, testConsole);

        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        var settings = new AddProjectCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task AddProject_Integration_ShouldCreateProject()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);

        // Mock workspace response
        var workspaceResponse = """
        [
            {
                "id": "workspace-123",
                "name": "Test Workspace"
            }
        ]
        """;

        // Mock existing projects response (empty)
        var projectsResponse = "[]";

        // Mock project creation response
        var createResponse = """
        {
            "id": "project-456",
            "name": "Test Project"
        }
        """;

        mockHttp.When("https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspaceResponse);

        mockHttp.When("https://api.clockify.me/api/v1/workspaces/workspace-123/projects*")
                .Respond("application/json", projectsResponse);

        mockHttp.When(HttpMethod.Post, "https://api.clockify.me/api/v1/workspaces/workspace-123/projects")
                .Respond(HttpStatusCode.Created, "application/json", createResponse);

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var testConsole = new TestConsole();

        // Simulate user input for project name and confirmation
        testConsole.Input.PushTextWithEnter("Test Project");
        testConsole.Input.PushTextWithEnter("y"); // Confirm

        var command = new AddProjectCommand(clockifyClient, testConsole);

        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        var settings = new AddProjectCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Project created successfully!"));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }

    [Test]
    public async Task AddProject_DuplicateName_ShouldDisplayErrorMessage()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHttp);

        // Mock workspace response
        var workspaceResponse = """
        [
            {
                "id": "workspace-123",
                "name": "Test Workspace"
            }
        ]
        """;

        // Mock existing projects response with existing project
        var projectsResponse = """
        [
            {
                "id": "project-existing",
                "name": "Existing Project"
            }
        ]
        """;

        mockHttp.When("https://api.clockify.me/api/v1/workspaces")
                .Respond("application/json", workspaceResponse);

        mockHttp.When("https://api.clockify.me/api/v1/workspaces/workspace-123/projects*")
                .Respond("application/json", projectsResponse);

        var clockifyClient = new ClockifyClient(httpClient, "test-key");
        var testConsole = new TestConsole();

        // Simulate user input for existing project name
        testConsole.Input.PushTextWithEnter("Existing Project");

        var command = new AddProjectCommand(clockifyClient, testConsole);

        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);
        var settings = new AddProjectCommand.Settings();

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("already exists!"));

        // Cleanup
        mockHttp.Dispose();
        httpClient.Dispose();
    }
}
