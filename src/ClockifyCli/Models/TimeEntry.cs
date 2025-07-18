namespace ClockifyCli.Models;

public record TimeEntry(string Id, string Description, string TaskId, string ProjectId, string Type, TimeInterval TimeInterval);