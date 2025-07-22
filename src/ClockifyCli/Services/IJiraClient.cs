using ClockifyCli.Models;

namespace ClockifyCli.Services;

public interface IJiraClient
{
    Task<JiraIssue?> GetIssue(TaskInfo taskInfo);
    Task<JiraIssue?> GetIssue(string jiraRef);
    Task<string> GetUser();
}
