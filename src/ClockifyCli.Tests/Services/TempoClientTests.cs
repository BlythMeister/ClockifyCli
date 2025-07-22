using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http.Headers;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class TempoClientTests
{
    private MockHttpMessageHandler mockTempoHttp = null!;
    private MockHttpMessageHandler mockJiraHttp = null!;
    private HttpClient tempoHttpClient = null!;
    private HttpClient jiraHttpClient = null!;
    private JiraClient jiraClient = null!;
    private const string TestApiKey = "test-tempo-api-key";
    private const string TestJiraUser = "test@example.com";
    private const string TestJiraApiKey = "test-jira-api-key";
    private const string TestAccountId = "account123";

    [SetUp]
    public void Setup()
    {
        mockTempoHttp = new MockHttpMessageHandler();
        mockJiraHttp = new MockHttpMessageHandler();
        tempoHttpClient = new HttpClient(mockTempoHttp);
        jiraHttpClient = new HttpClient(mockJiraHttp);

        // Setup Jira client with mock
        jiraClient = new JiraClient(jiraHttpClient, TestJiraUser, TestJiraApiKey);

        // Mock the JIRA user endpoint for the lazy-loaded UserId property
        var jiraUserResponse = $$"""{"accountId":"{{TestAccountId}}","displayName":"Test User","emailAddress":"{{TestJiraUser}}"}""";
        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/myself")
                    .Respond("application/json", jiraUserResponse);
    }

    [TearDown]
    public void TearDown()
    {
        mockTempoHttp?.Dispose();
        mockJiraHttp?.Dispose();
        tempoHttpClient?.Dispose();
        jiraHttpClient?.Dispose();
    }

    [Test]
    public async Task Delete_WithValidTempoTime_ShouldDeleteSuccessfully()
    {
        // Arrange
        var tempoTime = new TempoTime(12345, "Test worklog", DateTime.UtcNow);

        mockTempoHttp.When(HttpMethod.Delete, "https://api.tempo.io/4/worklogs/12345")
                     .Respond(HttpStatusCode.NoContent);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert - Should not throw
        await tempoClient.Delete(tempoTime);
    }

    [Test]
    public void Delete_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var tempoTime = new TempoTime(12345, "Test worklog", DateTime.UtcNow);

        mockTempoHttp.When(HttpMethod.Delete, "https://api.tempo.io/4/worklogs/12345")
                     .Respond(HttpStatusCode.BadRequest, "application/json", """{"message":"Bad Request"}""");

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () => await tempoClient.Delete(tempoTime));
    }

    [Test]
    public async Task ExportTimeEntry_WithValidData_ShouldCreateWorklog()
    {
        // Arrange
        var timeEntry = new TimeEntry(
            "entry123",
            "Working on feature",
            "task123",
            "project123",
            "regular",
            new TimeInterval("2024-01-15T09:00:00Z", "2024-01-15T11:30:00Z")
        );
        var taskInfo = new TaskInfo("task123", "TEST-456 Some task description", "ACTIVE");

        // Mock JIRA issue response
        var jiraIssueResponse = """
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
                    "originalEstimate": "4h",
                    "remainingEstimate": "2h",
                    "timeSpent": "2h"
                }
            }
        }
        """;

        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-456")
                    .Respond("application/json", jiraIssueResponse);

        // Mock Tempo worklog creation
        var tempoWorklogResponse = """{"tempoWorklogId":98765,"worker":{"accountId":"account123"}}""";

        mockTempoHttp.When(HttpMethod.Post, "https://api.tempo.io/4/worklogs")
                     .WithContent("{\"authorAccountId\":\"account123\",\"description\":\"Working on feature [cid:entry123]\",\"issueId\":10002,\"startDate\":\"2024-01-15\",\"startTime\":\"09:00:00\",\"timeSpentSeconds\":9000,\"remainingEstimateSeconds\":7200}")
                     .Respond("application/json", tempoWorklogResponse);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert - Should not throw
        await tempoClient.ExportTimeEntry(timeEntry, taskInfo);
    }

    [Test]
    public async Task ExportTimeEntry_WithRemainingTimeBlock_ShouldParseRemainingTime()
    {
        // Arrange
        var timeEntry = new TimeEntry(
            "entry124",
            "Working on bug fix [rem:1h30m]",
            "task124",
            "project123",
            "regular",
            new TimeInterval("2024-01-15T09:00:00Z", "2024-01-15T10:00:00Z")
        );
        var taskInfo = new TaskInfo("task124", "TEST-457 Bug fix task", "ACTIVE");

        // Mock JIRA issue response
        var jiraIssueResponse = """
        {
            "id": "10003",
            "key": "TEST-457",
            "fields": {
                "summary": "Bug Fix Issue",
                "status": {
                    "name": "In Progress",
                    "statusCategory": {
                        "key": "indeterminate",
                        "name": "In Progress"
                    }
                },
                "timetracking": {
                    "originalEstimate": "3h",
                    "remainingEstimate": "2h",
                    "timeSpent": "1h"
                }
            }
        }
        """;

        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-457")
                    .Respond("application/json", jiraIssueResponse);

        // Mock Tempo worklog creation - should use parsed remaining time (1h30m = 5400 seconds)
        var tempoWorklogResponse = """{"tempoWorklogId":98766,"worker":{"accountId":"account123"}}""";

        mockTempoHttp.When(HttpMethod.Post, "https://api.tempo.io/4/worklogs")
                     .Respond("application/json", tempoWorklogResponse);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert - Should not throw
        await tempoClient.ExportTimeEntry(timeEntry, taskInfo);
    }

    [Test]
    public async Task ExportTimeEntry_WithAutoRemainingTime_ShouldCalculateRemaining()
    {
        // Arrange
        var timeEntry = new TimeEntry(
            "entry125",
            "Working on enhancement [rem:auto]",
            "task125",
            "project123",
            "regular",
            new TimeInterval("2024-01-15T09:00:00Z", "2024-01-15T10:30:00Z") // 1.5 hours
        );
        var taskInfo = new TaskInfo("task125", "TEST-458 Enhancement task", "ACTIVE");

        // Mock JIRA issue response with 2h remaining
        var jiraIssueResponse = """
        {
            "id": "10004",
            "key": "TEST-458",
            "fields": {
                "summary": "Enhancement Issue",
                "status": {
                    "name": "In Progress",
                    "statusCategory": {
                        "key": "indeterminate",
                        "name": "In Progress"
                    }
                },
                "timetracking": {
                    "originalEstimate": "3h",
                    "remainingEstimate": "2h",
                    "timeSpent": "1h"
                }
            }
        }
        """;

        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-458")
                    .Respond("application/json", jiraIssueResponse);

        // Mock Tempo worklog creation - should use auto-calculated remaining (2h - 1.5h = 0.5h = 1800 seconds)
        var tempoWorklogResponse = """{"tempoWorklogId":98767,"worker":{"accountId":"account123"}}""";

        mockTempoHttp.When(HttpMethod.Post, "https://api.tempo.io/4/worklogs")
                     .Respond("application/json", tempoWorklogResponse);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert - Should not throw
        await tempoClient.ExportTimeEntry(timeEntry, taskInfo);
    }

    [Test]
    public void ExportTimeEntry_WithInvalidJiraIssue_ShouldThrowException()
    {
        // Arrange
        var timeEntry = new TimeEntry(
            "entry126",
            "Working on invalid task",
            "task126",
            "project123",
            "regular",
            new TimeInterval("2024-01-15T09:00:00Z", "2024-01-15T10:00:00Z")
        );
        var taskInfo = new TaskInfo("task126", "INVALID-999 Non-existent task", "ACTIVE");

        // Mock JIRA issue response with 404
        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/INVALID-999")
                    .Respond(HttpStatusCode.NotFound);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await tempoClient.ExportTimeEntry(timeEntry, taskInfo));
    }

    [Test]
    public void ExportTimeEntry_WithTempoApiError_ShouldThrowException()
    {
        // Arrange
        var timeEntry = new TimeEntry(
            "entry127",
            "Working on feature",
            "task127",
            "project123",
            "regular",
            new TimeInterval("2024-01-15T09:00:00Z", "2024-01-15T10:00:00Z")
        );
        var taskInfo = new TaskInfo("task127", "TEST-459 Valid task", "ACTIVE");

        // Mock JIRA issue response
        var jiraIssueResponse = """
        {
            "id": "10005",
            "key": "TEST-459",
            "fields": {
                "summary": "Valid Issue",
                "status": {
                    "name": "In Progress",
                    "statusCategory": {
                        "key": "indeterminate",
                        "name": "In Progress"
                    }
                },
                "timetracking": {
                    "originalEstimate": "2h",
                    "remainingEstimate": "1h",
                    "timeSpent": "1h"
                }
            }
        }
        """;

        mockJiraHttp.When(HttpMethod.Get, "https://15below.atlassian.net/rest/api/3/issue/TEST-459")
                    .Respond("application/json", jiraIssueResponse);

        // Mock Tempo API error
        mockTempoHttp.When(HttpMethod.Post, "https://api.tempo.io/4/worklogs")
                     .Respond(HttpStatusCode.BadRequest, "application/json", """{"message":"Invalid worklog data"}""");

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(async () =>
            await tempoClient.ExportTimeEntry(timeEntry, taskInfo));
    }

    [Test]
    public async Task GetCurrentTime_WithValidDateRange_ShouldReturnTempoTimes()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 15);
        var endDate = new DateTime(2024, 1, 21);

        var tempoResponse = """
        {
            "results": [
                {
                    "tempoWorklogId": 12345,
                    "issue": {"key": "TEST-456"},
                    "timeSpentSeconds": 3600,
                    "startDate": "2024-01-15",
                    "description": "Work on feature",
                    "author": {"accountId": "account123"}
                },
                {
                    "tempoWorklogId": 12346,
                    "issue": {"key": "TEST-457"},
                    "timeSpentSeconds": 7200,
                    "startDate": "2024-01-16",
                    "description": "Bug fixing",
                    "author": {"accountId": "account123"}
                }
            ],
            "metadata": {
                "count": 2,
                "offset": 0,
                "limit": 50,
                "next": null
            }
        }
        """;

        mockTempoHttp.When(HttpMethod.Get, $"https://api.tempo.io/4/worklogs/user/{TestAccountId}")
                     .WithExactQueryString("from=2024-01-15&to=2024-01-21")
                     .Respond("application/json", tempoResponse);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act
        var result = await tempoClient.GetCurrentTime(startDate, endDate);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].TempoWorklogId, Is.EqualTo(12345));
        Assert.That(result[1].TempoWorklogId, Is.EqualTo(12346));
    }

    [Test]
    public async Task GetCurrentTime_WithPaginatedResponse_ShouldReturnAllPages()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 15);
        var endDate = new DateTime(2024, 1, 21);

        // Page 1: Contains a 'next' URL to trigger pagination
        var tempoResponsePage1 = """
        {
            "results": [
                {
                    "tempoWorklogId": 12345,
                    "issue": {"key": "TEST-456"},
                    "timeSpentSeconds": 3600,
                    "startDate": "2024-01-15",
                    "description": "Work on feature page 1",
                    "author": {"accountId": "account123"}
                }
            ],
            "metadata": {
                "count": 1,
                "offset": 0,
                "limit": 1,
                "next": "https://api.tempo.io/4/worklogs/user/account123?from=2024-01-15&to=2024-01-21&offset=1"
            }
        }
        """;

        // Page 2: No 'next' URL to stop pagination
        var tempoResponsePage2 = """
        {
            "results": [
                {
                    "tempoWorklogId": 12346,
                    "issue": {"key": "TEST-457"},
                    "timeSpentSeconds": 7200,
                    "startDate": "2024-01-16",
                    "description": "Work on feature page 2",
                    "author": {"accountId": "account123"}
                }
            ],
            "metadata": {
                "count": 1,
                "offset": 1,
                "limit": 1,
                "next": null
            }
        }
        """;

        // Mock the first page - initial API call 
        var firstPageMock = mockTempoHttp.When(HttpMethod.Get, "https://api.tempo.io/4/worklogs/user/account123")
                     .WithExactQueryString("from=2024-01-15&to=2024-01-21")
                     .Respond("application/json", tempoResponsePage1);

        // Mock the second page - with offset parameter
        var secondPageMock = mockTempoHttp.When(HttpMethod.Get, "https://api.tempo.io/4/worklogs/user/account123")
                     .WithExactQueryString("from=2024-01-15&to=2024-01-21&offset=1")
                     .Respond("application/json", tempoResponsePage2);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act
        var result = await tempoClient.GetCurrentTime(startDate, endDate);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].TempoWorklogId, Is.EqualTo(12345));
        Assert.That(result[1].TempoWorklogId, Is.EqualTo(12346));
    }

    [Test]
    public void GetCurrentTime_WithHttpError_ShouldThrowHttpRequestException()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 15);
        var endDate = new DateTime(2024, 1, 21);

        mockTempoHttp.When(HttpMethod.Get, $"https://api.tempo.io/4/worklogs/user/{TestAccountId}")
                     .WithExactQueryString("from=2024-01-15&to=2024-01-21")
                     .Respond(HttpStatusCode.Unauthorized, "application/json", """{"message":"Unauthorized"}""");

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await tempoClient.GetCurrentTime(startDate, endDate));
    }

    [Test]
    public async Task GetUnsubmittedPeriods_WithValidResponse_ShouldReturnUserPeriods()
    {
        // Arrange
        var tempoResponse = """
        {
            "results": [
                {
                    "id": "period1",
                    "user": {"accountId": "account123"},
                    "period": {"from": "2024-01-15", "to": "2024-01-21"},
                    "status": {"key": "WAITING_FOR_APPROVAL"}
                },
                {
                    "id": "period2",
                    "user": {"accountId": "other-account"},
                    "period": {"from": "2024-01-15", "to": "2024-01-21"},
                    "status": {"key": "WAITING_FOR_APPROVAL"}
                },
                {
                    "id": "period3",
                    "user": {"accountId": "account123"},
                    "period": {"from": "2024-01-22", "to": "2024-01-28"},
                    "status": {"key": "WAITING_FOR_APPROVAL"}
                }
            ],
            "metadata": {
                "count": 3,
                "offset": 0,
                "limit": 50,
                "next": null
            }
        }
        """;

        mockTempoHttp.When(HttpMethod.Get, "https://api.tempo.io/4/timesheet-approvals/waiting")
                     .Respond("application/json", tempoResponse);

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act
        var result = await tempoClient.GetUnsubmittedPeriods();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2)); // Should only return periods for current user
        Assert.That(result[0].Period.From, Is.EqualTo("2024-01-15"));
        Assert.That(result[1].Period.From, Is.EqualTo("2024-01-22"));
        Assert.That(result.All(p => p.User.AccountId == TestAccountId), Is.True);
    }

    [Test]
    public void GetUnsubmittedPeriods_WithHttpError_ShouldThrowHttpRequestException()
    {
        // Arrange
        mockTempoHttp.When(HttpMethod.Get, "https://api.tempo.io/4/timesheet-approvals/waiting")
                     .Respond(HttpStatusCode.Forbidden, "application/json", """{"message":"Forbidden"}""");

        var tempoClient = new TempoClient(tempoHttpClient, TestApiKey, jiraClient);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await tempoClient.GetUnsubmittedPeriods());
    }
}
