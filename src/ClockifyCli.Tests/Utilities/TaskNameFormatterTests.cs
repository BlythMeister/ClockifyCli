using ClockifyCli.Models;
using ClockifyCli.Utilities;
using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class TaskNameFormatterTests
{
    [Test]
    public void FormatTaskName_WithProjectAndParentAndSummary_FormatsCorrectly()
    {
        // Arrange
        var mockJiraProject = new JiraProject(1, "PROJ", "Project Name");
        var mockJiraParent = new JiraIssue(
            1,
            "TEST-100",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Parent Summary"
            )
        );

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Task Summary",
                mockJiraProject,
                mockJiraParent
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert
        Assert.That(result, Is.EqualTo("TEST-123 [Project Name] - [Parent Summary / Task Summary]"));
    }

    [Test]
    public void FormatTaskName_WithProjectAndSummaryOnly_FormatsCorrectly()
    {
        // Arrange
        var mockJiraProject = new JiraProject(1, "PROJ", "Project Name");

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Task Summary",
                mockJiraProject,
                null  // No parent
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert
        Assert.That(result, Is.EqualTo("TEST-123 [Project Name] - [Task Summary]"));
    }

    [Test]
    public void FormatTaskName_WithSummaryOnly_FormatsCorrectly()
    {
        // Arrange
        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Task Summary",
                null,  // No project
                null   // No parent
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert
        Assert.That(result, Is.EqualTo("TEST-123 [Task Summary]"));
    }

    [Test]
    public void FormatTaskName_WithParentButNoProject_FormatsCorrectly()
    {
        // Arrange
        var mockJiraParent = new JiraIssue(
            1,
            "TEST-100",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Parent Summary"
            )
        );

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Task Summary",
                null,  // No project
                mockJiraParent
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert
        Assert.That(result, Is.EqualTo("TEST-123 [Parent Summary / Task Summary]"));
    }

    [Test]
    public void FormatTaskName_WithNullIssue_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => TaskNameFormatter.FormatTaskName(null!));
    }

    [Test]
    public void FormatTaskName_WithNullFields_ThrowsArgumentNullException()
    {
        // Arrange
        var mockJiraIssue = new JiraIssue(12345, "TEST-123", null!);

        // Assert
        Assert.Throws<ArgumentNullException>(() => TaskNameFormatter.FormatTaskName(mockJiraIssue));
    }

    [Test]
    public void FormatTaskName_WithEmptySummary_IncludesEmptyBrackets()
    {
        // Arrange
        var mockJiraProject = new JiraProject(1, "PROJ", "Project Name");

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "",  // Empty summary
                mockJiraProject,
                null
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert
        Assert.That(result, Is.EqualTo("TEST-123 [Project Name]"));
    }
}
