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
            projectPart = $"[{NormalizeWhitespace(issue.Fields.Project.Name)}]";
        }

        // Add parent summary if available
        if (issue.Fields.Parent != null && 
            issue.Fields.Parent.Fields != null && 
            !string.IsNullOrWhiteSpace(issue.Fields.Parent.Fields.Summary))
        {
            hierarchyParts.Add(NormalizeWhitespace(issue.Fields.Parent.Fields.Summary));
        }

        // Add issue summary
        if (!string.IsNullOrWhiteSpace(issue.Fields.Summary))
        {
            hierarchyParts.Add(NormalizeWhitespace(issue.Fields.Summary));
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

    /// <summary>
    /// Normalizes whitespace in a string by trimming and collapsing multiple spaces into single spaces
    /// </summary>
    private static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Trim and collapse multiple spaces into single spaces
        return System.Text.RegularExpressions.Regex.Replace(input.Trim(), @"\s+", " ");
    }
}
