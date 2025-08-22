using ClockifyCli.Models;

namespace ClockifyCli.Services;

public interface IJiraClient
{
    Task<JiraIssue?> GetIssue(TaskInfo taskInfo);
    Task<JiraIssue?> GetIssue(string jiraRef);
    Task<JiraSearchResult> SearchIssues(string jql, int maxResults = 50);
    Task<string> GetUser();
}
