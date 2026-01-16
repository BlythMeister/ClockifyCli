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

    [Test]
    public void FormatTaskName_WithExtraWhitespace_NormalizesCorrectly()
    {
        // Arrange - Simulate Jira data with extra whitespace
        var mockJiraProject = new JiraProject(1, "PROJ", "  Project  Name  ");

        var mockJiraParent = new JiraIssue(
            1,
            "TEST-100",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "  Parent   Summary  "  // Multiple spaces
            )
        );

        var mockJiraIssue = new JiraIssue(
            12345,
            "TEST-123",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "  Task    Summary  ",  // Multiple spaces
                mockJiraProject,
                mockJiraParent
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert - All extra whitespace should be normalized to single spaces
        Assert.That(result, Is.EqualTo("TEST-123 [Project Name] - [Parent Summary / Task Summary]"));
    }

    [Test]
    public void FormatTaskName_WithTrailingSpaces_NormalizesCorrectly()
    {
        // Arrange - Simulate the exact issue from the bug report
        var mockJiraProject = new JiraProject(1, "PROJ", "Delivery Team Training");

        var mockJiraIssue = new JiraIssue(
            12345,
            "DTT-4",
            new JiraIssueFields(
                new JiraTimeTracking("0h"),
                new JiraStatus("In Progress", new JiraStatusCategory("indeterminate", "In Progress")),
                "Company / Compliance Training ",  // Trailing space
                mockJiraProject,
                null
            )
        );

        // Act
        var result = TaskNameFormatter.FormatTaskName(mockJiraIssue);

        // Assert - Should not have trailing spaces before the closing bracket
        Assert.That(result, Is.EqualTo("DTT-4 [Delivery Team Training] - [Company / Compliance Training]"));
        Assert.That(result, Does.Not.Contain("  ]"));  // No double space before bracket
        Assert.That(result, Does.Not.Contain(" ]"));   // This might fail, so we check the exact format
    }
}
