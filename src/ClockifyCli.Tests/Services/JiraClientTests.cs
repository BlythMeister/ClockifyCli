using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Net;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class JiraClientTests
{
    private MockHttpMessageHandler mockHttp = null!;
    private HttpClient httpClient = null!;
    private const string TestUser = "test@example.com";
    private const string TestApiKey = "test-api-key";

    [SetUp]
    public void Setup()
    {
        mockHttp = new MockHttpMessageHandler();
        httpClient = new HttpClient(mockHttp);
        // Don't set BaseAddress here since JiraClient will do it
    }

    [TearDown]
    public void TearDown()
    {
        mockHttp?.Dispose();
        httpClient?.Dispose();
    }

    [Test]
    public async Task GetIssue_WithValidTaskName_ShouldReturnIssue()
    {
        // Arrange
        var taskInfo = new TaskInfo("task123", "TEST-456 Some task description", "ACTIVE");
        var jsonResponse = """
        {
            "id": "10002",
            "key": "TEST-456",
            "fields": {
                "summary": "Test Issue",
                "status": {
                    "name": "In Progress",
                    "statusCategory": {
                        "key": "indeterminate",
                        "name": "In Progress"
                    }
                },
                "timetracking": {
                    "originalEstimate": "1h",
                    "remainingEstimate": "30m",
                    "timeSpent": "30m"
                }
            }
        }
        """;

        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-456")
                 .Respond("application/json", jsonResponse);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act
        var result = await jiraClient.GetIssue(taskInfo);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10002));
        Assert.That(result.Key, Is.EqualTo("TEST-456"));
        Assert.That(result.Fields.Summary, Is.EqualTo("Test Issue"));
    }

    [Test]
    public async Task GetIssue_WithJiraRef_ShouldReturnIssue()
    {
        // Arrange
        var jiraRef = "TEST-789";
        var jsonResponse = """
        {
            "id": "10003",
            "key": "TEST-789",
            "fields": {
                "summary": "Another Test Issue",
                "status": {
                    "name": "Done",
                    "statusCategory": {
                        "key": "done",
                        "name": "Done"
                    }
                },
                "timetracking": {
                    "originalEstimate": "2h",
                    "remainingEstimate": "0m",
                    "timeSpent": "2h"
                }
            }
        }
        """;

        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-789")
                 .Respond("application/json", jsonResponse);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act
        var result = await jiraClient.GetIssue(jiraRef);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10003));
        Assert.That(result.Key, Is.EqualTo("TEST-789"));
        Assert.That(result.Fields.Summary, Is.EqualTo("Another Test Issue"));
    }

    [Test]
    public async Task GetIssue_WithInvalidTaskName_ShouldReturnNull()
    {
        // Arrange
        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);
        var taskInfo = new TaskInfo("task123", "Invalid Task Name", "ACTIVE");

        // Act - No HTTP mocking needed for this test since it returns null for invalid names
        var result = await jiraClient.GetIssue(taskInfo);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetIssue_WithHttpError_ShouldReturnNull()
    {
        // Arrange
        var jiraRef = "TEST-404";

        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-404")
                 .Respond(HttpStatusCode.NotFound);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act
        var result = await jiraClient.GetIssue(jiraRef);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetUser_WithValidCredentials_ShouldReturnAccountId()
    {
        // Arrange
        var jsonResponse = """{"accountId":"abc123def456","displayName":"Test User","emailAddress":"test@example.com"}""";

        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/myself")
                 .Respond("application/json", jsonResponse);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act
        var result = await jiraClient.GetUser();

        // Assert
        Assert.That(result, Is.EqualTo("abc123def456"));
    }

    [Test]
    public void GetUser_WithInvalidCredentials_ShouldThrowHttpRequestException()
    {
        // Arrange
        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/myself")
                 .Respond(HttpStatusCode.Unauthorized, "application/json", """{"errorMessages":["Unauthorized"]}""");

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () => await jiraClient.GetUser());
    }

    [Test]
    public async Task UserId_PropertyLazyLoads_ShouldReturnAccountId()
    {
        // Arrange
        var jsonResponse = """{"accountId":"lazy123def456","displayName":"Lazy User","emailAddress":"lazy@example.com"}""";

        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/myself")
                 .Respond("application/json", jsonResponse);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act
        var result = await jiraClient.UserId.Value;

        // Assert
        Assert.That(result, Is.EqualTo("lazy123def456"));
    }

    [Test]
    public async Task GetIssue_WithSameJiraRef_ShouldUseCaching()
    {
        // Arrange
        var jiraRef = "TEST-CACHE";
        var jsonResponse = """
        {
            "id": "10004",
            "key": "TEST-CACHE",
            "fields": {
                "summary": "Cached Issue",
                "status": {
                    "name": "In Progress",
                    "statusCategory": {
                        "key": "indeterminate",
                        "name": "In Progress"
                    }
                },
                "timetracking": {
                    "originalEstimate": "1h",
                    "remainingEstimate": "1h",
                    "timeSpent": "0m"
                }
            }
        }
        """;

        // Setup mock to only respond once - if caching doesn't work, second call will fail
        mockHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-CACHE")
                 .Respond("application/json", jsonResponse);

        var jiraClient = new JiraClient(httpClient, TestUser, TestApiKey);

        // Act - Call twice to test caching
        var result1 = await jiraClient.GetIssue(jiraRef);
        var result2 = await jiraClient.GetIssue(jiraRef);

        // Assert
        Assert.That(result1, Is.Not.Null);
        Assert.That(result2, Is.Not.Null);
        // Verify it's the same instance (caching working)
        Assert.That(result1, Is.SameAs(result2));
        Assert.That(result1.Key, Is.EqualTo("TEST-CACHE"));
        Assert.That(result2.Key, Is.EqualTo("TEST-CACHE"));
    }
}
