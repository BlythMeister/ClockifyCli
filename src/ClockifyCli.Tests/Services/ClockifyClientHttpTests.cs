using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Net;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class ClockifyClientTests
{
    private MockHttpMessageHandler mockHttp = null!;
    private HttpClient httpClient = null!;
    private const string TestApiKey = "test-api-key";

    [SetUp]
    public void Setup()
    {
        mockHttp = new MockHttpMessageHandler();
        httpClient = new HttpClient(mockHttp);
        // Don't set BaseAddress here since ClockifyClient will do it
    }

    [TearDown]
    public void TearDown()
    {
        mockHttp?.Dispose();
        httpClient?.Dispose();
    }

    [Test]
    public async Task GetLoggedInUser_WithValidApiKey_ShouldReturnUserInfo()
    {
        // Arrange
        var jsonResponse = """{"id":"user123","name":"Test User","email":"test@example.com","defaultWorkspace":"workspace123"}""";
        
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                 .Respond("application/json", jsonResponse);

        var clockifyClient = new ClockifyClient(httpClient, TestApiKey);

        // Act
        var result = await clockifyClient.GetLoggedInUser();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo("user123"));
        Assert.That(result.Name, Is.EqualTo("Test User"));
        Assert.That(result.Email, Is.EqualTo("test@example.com"));
    }

    [Test]
    public void GetLoggedInUser_WithInvalidApiKey_ShouldThrowException()
    {
        // Arrange
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/user")
                 .Respond(HttpStatusCode.Unauthorized, "application/json", """{"message":"Unauthorized"}""");

        var clockifyClient = new ClockifyClient(httpClient, TestApiKey);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () => await clockifyClient.GetLoggedInUser());
    }

    [Test]
    public async Task GetLoggedInUserWorkspaces_WithValidApiKey_ShouldReturnWorkspaces()
    {
        // Arrange
        var jsonResponse = """[{"id":"workspace1","name":"Test Workspace"}]""";
        
        mockHttp.When(HttpMethod.Get, "https://api.clockify.me/api/v1/workspaces")
                 .Respond("application/json", jsonResponse);

        var clockifyClient = new ClockifyClient(httpClient, TestApiKey);

        // Act
        var result = await clockifyClient.GetLoggedInUserWorkspaces();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("workspace1"));
        Assert.That(result[0].Name, Is.EqualTo("Test Workspace"));
    }

    [Test]
    public async Task GetProjects_WithValidWorkspace_ShouldReturnProjects()
    {
        // Arrange
        var workspace = new WorkspaceInfo("workspace123", "Test Workspace");
        var jsonResponse = """[{"id":"project1","name":"Test Project","clientName":"Test Client"}]""";
        
        mockHttp.When(HttpMethod.Get, $"https://api.clockify.me/api/v1/workspaces/{workspace.Id}/projects?page=1&page-size=100")
                 .Respond("application/json", jsonResponse);

        var clockifyClient = new ClockifyClient(httpClient, TestApiKey);

        // Act
        var result = await clockifyClient.GetProjects(workspace);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("project1"));
        Assert.That(result[0].Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task GetTimeEntries_WithValidParameters_ShouldReturnTimeEntries()
    {
        // Arrange
        var workspace = new WorkspaceInfo("workspace123", "Test Workspace");
        var user = new UserInfo("user123", "Test User", "test@example.com", "workspace123");
        var startDate = DateTime.Today.AddDays(-7);
        var endDate = DateTime.Today;
        var jsonResponse = """[{"id":"entry1","description":"Test entry","timeInterval":{"start":"2024-01-01T09:00:00Z","end":"2024-01-01T10:00:00Z"}}]""";
        
        var expectedUrl = $"https://api.clockify.me/api/v1/workspaces/{workspace.Id}/user/{user.Id}/time-entries?start={startDate:yyyy-MM-dd}T00:00:00Z&end={endDate:yyyy-MM-dd}T23:59:59Z&in-progress=false&page=1&page-size=100";
        mockHttp.When(HttpMethod.Get, expectedUrl)
                 .Respond("application/json", jsonResponse);

        var clockifyClient = new ClockifyClient(httpClient, TestApiKey);

        // Act
        var result = await clockifyClient.GetTimeEntries(workspace, user, startDate, endDate);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("entry1"));
        Assert.That(result[0].Description, Is.EqualTo("Test entry"));
    }
}
