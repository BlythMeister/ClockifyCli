using ClockifyCli.Models;

namespace ClockifyCli.Services;

public interface IJiraClient
{
    Task<JiraProject?> GetProject(ProjectInfo projectInfo);
    Task<JiraIssue?> GetIssue(TaskInfo taskInfo);
    Task<JiraIssue?> GetIssue(string jiraRef);
    Task<string> GetUser();
}
