using ClockifyCli.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace ClockifyCli.Services;

public class TempoClient : ITempoClient
{
    private readonly HttpClient client;
    private readonly JiraClient jiraClient;
    private readonly Regex descriptionRegex;
    private readonly Regex timeParseRegex;

    // Constructor for dependency injection with HttpClient
    public TempoClient(HttpClient httpClient, string apiKey, JiraClient jiraClient)
    {
        this.jiraClient = jiraClient;

        client = httpClient;
        client.BaseAddress = new Uri("https://api.tempo.io/4/");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        descriptionRegex = new Regex(@".*(?<remBlock>\[rem:(?<remVal>.*)\]).*");
        timeParseRegex = new Regex(@"((?<week>(?:\d|\.)*)w)?((?<day>(?:\d|\.)*)d)?((?<hour>(?:\d|\.)*)h)?((?<minute>(?:\d|\.)*)m)?");
    }

    // Original constructor for backward compatibility
    public TempoClient(string apiKey, JiraClient jiraClient) : this(new HttpClient(), apiKey, jiraClient)
    {
    }

    public async Task Delete(TempoTime tempoTime)
    {
        try
        {
            var response = await client.DeleteAsync($"worklogs/{tempoTime.TempoWorklogId}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task ExportTimeEntry(TimeEntry timeEntry, TaskInfo taskInfo)
    {
        var jiraIssue = await jiraClient.GetIssue(taskInfo);
        if (jiraIssue == null)
        {
            throw new InvalidOperationException($"Could not find Jira issue for task {taskInfo.Name}");
        }

        var accountId = await jiraClient.UserId.Value;
        var durationSeconds = (long)timeEntry.TimeInterval.DurationSpan.TotalSeconds;
        var description = $"Working on {jiraIssue.Key} [cid:{timeEntry.Id}]";
        long? remaining = null;

        if (!string.IsNullOrWhiteSpace(timeEntry.Description))
        {
            var descriptionRegexMatch = descriptionRegex.Match(timeEntry.Description);

            if (descriptionRegexMatch.Groups["remBlock"].Success)
            {
                description = $"{timeEntry.Description.Replace(descriptionRegexMatch.Groups["remBlock"].Value, string.Empty)} [cid:{timeEntry.Id}]".Trim();
                var remValue = descriptionRegexMatch.Groups["remVal"].Value;
                if (remValue.Equals("auto", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Get the current remaining estimate from Jira first
                    var currentRemaining = ParseTimeStringToSeconds(jiraIssue.Fields.TimeTracking.RemainingEstimate);
                    remaining = currentRemaining - durationSeconds;
                    if (remaining < 0)
                    {
                        remaining = 0;
                    }
                }
                else
                {
                    remaining = ParseTimeStringToSeconds(descriptionRegexMatch.Groups["remVal"].Value);
                }
            }
            else
            {
                description = $"{timeEntry.Description} [cid:{timeEntry.Id}]";
            }
        }

        if (remaining == null)
        {
            remaining = ParseTimeStringToSeconds(jiraIssue.Fields.TimeTracking.RemainingEstimate);
        }

        var worklog = new TempoLog(accountId, description, jiraIssue.Id, timeEntry.TimeInterval.StartDate.ToString("yyyy-MM-dd"), timeEntry.TimeInterval.StartDate.ToString("HH:mm:ss"), durationSeconds, remaining);

        try
        {
            var worklogJson = JsonConvert.SerializeObject(worklog);
            var content = new StringContent(worklogJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
            var response = await client.PostAsync($"worklogs", content);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"HTTP Status: {response.StatusCode}\nContent: {responseContent}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<List<TempoTime>> GetCurrentTime(DateTime startDate, DateTime endDate)
    {
        var accountId = await jiraClient.UserId.Value;
        return await GetPaged<TempoTime>($"worklogs/user/{accountId}?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}");
    }

    public async Task<List<TempoApproval>> GetUnsubmittedPeriods()
    {
        var accountId = await jiraClient.UserId.Value;
        var periods = await GetPaged<TempoApproval>($"timesheet-approvals/waiting");
        return periods.Where(x => x.User.AccountId == accountId).ToList();
    }

    private long ParseTimeStringToSeconds(string timeString)
    {
        if (timeString == null)
        {
            return 0;
        }

        double value = 0;
        var match = timeParseRegex.Match(timeString);

        if (match.Success)
        {
            if (match.Groups["week"].Success)
            {
                value += double.Parse(match.Groups["week"].Value) * 5 * 5 * 60 * 60;
            }

            if (match.Groups["day"].Success)
            {
                value += double.Parse(match.Groups["day"].Value) * 5 * 60 * 60;
            }

            if (match.Groups["hour"].Success)
            {
                value += double.Parse(match.Groups["hour"].Value) * 60 * 60;
            }

            if (match.Groups["minute"].Success)
            {
                value += double.Parse(match.Groups["minute"].Value) * 60;
            }
        }

        return (long)value;
    }

    private async Task<List<T>> GetPaged<T>(string baseUrl)
    {
        try
        {
            var returnItems = new List<T>();
            var url = baseUrl;
            var moreToGet = true;
            var visitedUrls = new HashSet<string>();

            while (moreToGet)
            {
                // Check if we've already called this URL to prevent infinite loops
                if (visitedUrls.Contains(url, StringComparer.InvariantCultureIgnoreCase))
                {
                    break;
                }

                visitedUrls.Add(url);

                var response = await client.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Tempo API Error: {responseContent} (Status: {response.StatusCode})");
                }

                var page = JsonConvert.DeserializeObject<TempoPage<T>>(responseContent)!;

                returnItems.AddRange(page.Results);
                if (page.MetaData.Next != null)
                {
                    // If the next URL is a full URL, extract just the relative part after the base address
                    if (page.MetaData.Next.StartsWith(client.BaseAddress?.ToString() ?? ""))
                    {
                        url = page.MetaData.Next.Substring(client.BaseAddress!.ToString().Length);
                    }
                    else
                    {
                        url = page.MetaData.Next;
                    }
                }
                else
                {
                    moreToGet = false;
                }
            }

            return returnItems;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }
}
