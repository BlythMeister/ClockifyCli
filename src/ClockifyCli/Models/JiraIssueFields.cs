namespace ClockifyCli.Models;

public record JiraIssueFields(
    JiraTimeTracking TimeTracking, 
    JiraStatus Status, 
    string Summary, 
    JiraProject? Project = null, 
    JiraIssue? Parent = null);
