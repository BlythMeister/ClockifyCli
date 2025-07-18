using Spectre.Console;

namespace ClockifyCli.Models;

public record TaskWithProject(
    string TaskId,
    string TaskName,
    string ProjectId,
    string ProjectName
)
{
    public string DisplayName => $"{ProjectName} > {TaskName}";
    public string SafeDisplayName => $"{Markup.Escape(ProjectName)} > {Markup.Escape(TaskName)}";
}