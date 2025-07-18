using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using ClockifyCli.Models;
using Newtonsoft.Json;

namespace ClockifyCli.Services;

public class JiraClient
{
    private readonly HttpClient _client;
    private readonly Regex _taskInfoRegex;
    private readonly Regex _projectInfoRegex;
    private readonly ConcurrentDictionary<string, Task<JiraIssue>> _jiraIdMap;

    public Lazy<Task<string>> UserId { get; }

    public JiraClient(string user, string apiKey)
    {
        var base64BasicAuthText = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{apiKey}"));

        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://15below.atlassian.net/rest/api/3/");
        _client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64BasicAuthText}");
        _client.DefaultRequestHeaders.Add("Accept", "application/json");

        _taskInfoRegex = new Regex(@"(?<jiraRef>\S*-\d*)(\s|-|).*");
        _projectInfoRegex = new Regex(@"(?<projectRef>\S*)(\s|-|).*");
        _jiraIdMap = new ConcurrentDictionary<string, Task<JiraIssue>>();

        UserId = new Lazy<Task<string>>(() => GetUser());
    }

    public async Task<JiraProject?> GetProject(ProjectInfo projectInfo)
    {
        var projectMatches = _projectInfoRegex.Match(projectInfo.Name);
        if (!projectMatches.Success || !projectMatches.Groups["projectRef"].Success)
        {
            return null;
        }
        var projectRef = projectMatches.Groups["projectRef"].Value;

        try
        {
            var response = await _client.GetAsync($"project/{projectRef}");
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<JiraProject>(responseContent);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<JiraIssue?> GetIssue(TaskInfo taskInfo)
    {
        var jiraRefMatches = _taskInfoRegex.Match(taskInfo.Name);
        if (!jiraRefMatches.Success || !jiraRefMatches.Groups["jiraRef"].Success)
        {
            return null;
        }
        var jiraRef = jiraRefMatches.Groups["jiraRef"].Value;

        return await GetIssue(jiraRef);
    }

    public async Task<JiraIssue?> GetIssue(string jiraRef)
    {
        try
        {
            return await _jiraIdMap.GetOrAdd(jiraRef, async x =>
            {
                try
                {
                    var response = await _client.GetAsync($"issue/{jiraRef}");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<JiraIssue>(responseContent)!;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    throw;
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<string> GetUser()
    {
        try
        {
            var response = await _client.GetAsync("myself");
            var responseContent = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<JiraUser>(responseContent)!;
            return user.AccountId;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }
}