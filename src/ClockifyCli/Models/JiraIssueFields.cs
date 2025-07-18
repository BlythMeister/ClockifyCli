namespace ClockifyCli.Models;

public record JiraIssueFields(JiraTimeTracking TimeTracking, JiraStatus Status, string Summary);