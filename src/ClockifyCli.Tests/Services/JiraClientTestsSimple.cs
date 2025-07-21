using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Net;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class JiraClientTests
{
    [Test]
    public async Task GetProject_WithInvalidProjectName_ShouldReturnNull()
    {
        // Arrange
        const string testUser = "test@example.com";
        const string testApiKey = "test-api-key";
        var jiraClient = new JiraClient(testUser, testApiKey);
        var projectInfo = new ProjectInfo("project123", "Invalid Project Name");

        // Act - No HTTP mocking needed for this test since it returns null for invalid names
        var result = await jiraClient.GetProject(projectInfo);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetIssue_WithInvalidTaskName_ShouldReturnNull()
    {
        // Arrange
        const string testUser = "test@example.com";
        const string testApiKey = "test-api-key";
        var jiraClient = new JiraClient(testUser, testApiKey);
        var taskInfo = new TaskInfo("task123", "Invalid Task Name", "ACTIVE");

        // Act - No HTTP mocking needed for this test since it returns null for invalid names
        var result = await jiraClient.GetIssue(taskInfo);

        // Assert
        Assert.That(result, Is.Null);
    }
}
