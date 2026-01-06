using ClockifyCli.Models;

namespace ClockifyCli.Utilities;

public static class TaskNameFormatter
{
    /// <summary>
    /// Formats a Jira issue into a task name with the format: "KEY [PROJECT] - [PARENT / SUMMARY]"
    /// </summary>
    /// <param name="issue">The Jira issue to format</param>
    /// <returns>Formatted task name</returns>
    public static string FormatTaskName(JiraIssue issue)
    {
        if (issue == null || issue.Fields == null)
        {
            throw new ArgumentNullException(nameof(issue));
        }

        var projectPart = "";
        var hierarchyParts = new List<string>();

        // Add project name if available
        if (issue.Fields.Project != null && !string.IsNullOrWhiteSpace(issue.Fields.Project.Name))
        {
            projectPart = $"[{issue.Fields.Project.Name.Trim()}]";
        }

        // Add parent summary if available
        if (issue.Fields.Parent != null && 
            issue.Fields.Parent.Fields != null && 
            !string.IsNullOrWhiteSpace(issue.Fields.Parent.Fields.Summary))
        {
            hierarchyParts.Add(issue.Fields.Parent.Fields.Summary.Trim());
        }

        // Add issue summary
        if (!string.IsNullOrWhiteSpace(issue.Fields.Summary))
        {
            hierarchyParts.Add(issue.Fields.Summary.Trim());
        }

        var hierarchyString = string.Join(" / ", hierarchyParts);
        
        // Build the final format: "KEY [PROJECT] - [HIERARCHY]"
        if (!string.IsNullOrWhiteSpace(projectPart) && !string.IsNullOrWhiteSpace(hierarchyString))
        {
            return $"{issue.Key} {projectPart} - [{hierarchyString}]";
        }
        else if (!string.IsNullOrWhiteSpace(hierarchyString))
        {
            // No project, just use hierarchy
            return $"{issue.Key} [{hierarchyString}]";
        }
        else if (!string.IsNullOrWhiteSpace(projectPart))
        {
            // No hierarchy, just project (shouldn't normally happen)
            return $"{issue.Key} {projectPart}";
        }
        else
        {
            // Fallback (shouldn't happen with valid data)
            return issue.Key;
        }
    }
}
