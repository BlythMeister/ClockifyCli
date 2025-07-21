using ClockifyCli.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace ClockifyCli.Services;

public class JiraClient
{
    private readonly HttpClient client;
    private readonly Regex taskInfoRegex;
    private readonly Regex projectInfoRegex;
    private readonly ConcurrentDictionary<string, Task<JiraIssue>> jiraIdMap;

    public Lazy<Task<string>> UserId { get; }

    public JiraClient(string user, string apiKey)
    {
        var base64BasicAuthText = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{apiKey}"));

        client = new HttpClient();
        client.BaseAddress = new Uri("https://15below.atlassian.net/rest/api/3/");
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64BasicAuthText}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        taskInfoRegex = new Regex(@"(?<jiraRef>\S*-\d*)(\s|-|).*");
        projectInfoRegex = new Regex(@"(?<projectRef>\S*)(\s|-|).*");
        jiraIdMap = new ConcurrentDictionary<string, Task<JiraIssue>>();

        UserId = new Lazy<Task<string>>(() => GetUser());
    }

    public async Task<JiraProject?> GetProject(ProjectInfo projectInfo)
    {
        var projectMatches = projectInfoRegex.Match(projectInfo.Name);
        if (!projectMatches.Success || !projectMatches.Groups["projectRef"].Success)
        {
            return null;
        }

        var projectRef = projectMatches.Groups["projectRef"].Value;

        try
        {
            var response = await client.GetAsync($"project/{projectRef}");
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
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
        var jiraRefMatches = taskInfoRegex.Match(taskInfo.Name);
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
            return await jiraIdMap.GetOrAdd(jiraRef, async x =>
                                                     {
                                                         try
                                                         {
                                                             var response = await client.GetAsync($"issue/{jiraRef}");
                                                             var responseContent = await response.Content.ReadAsStringAsync();
                                                             
                                                             if (!response.IsSuccessStatusCode)
                                                             {
                                                                 // For JIRA API, just return null for invalid issue references
                                                                 // This allows the calling code to handle it gracefully
                                                                 return null!; // Use null! to satisfy the non-nullable return type
                                                             }
                                                             
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
            var response = await client.GetAsync("myself");
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get JIRA user. Status: {response.StatusCode}, Response: {responseContent}");
            }
            
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
