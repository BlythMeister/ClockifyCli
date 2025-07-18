using System.Net.Http.Headers;
using System.Text;
using ClockifyCli.Models;
using Newtonsoft.Json;

namespace ClockifyCli.Services;

public class ClockifyClient
{
    private readonly HttpClient _client;

    public ClockifyClient(string apiKey)
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://api.clockify.me/api/v1/");
        _client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<UserInfo> GetLoggedInUser()
    {
        try
        {
            var response = await _client.GetAsync("user");
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserInfo>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<List<WorkspaceInfo>> GetLoggedInUserWorkspaces()
    {
        try
        {
            var response = await _client.GetAsync("workspaces");
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<WorkspaceInfo>>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task AddTask(WorkspaceInfo workspace, ProjectInfo project, string taskName)
    {
        try
        {
            var newTask = new NewTask(taskName);
            var newTaskJson = JsonConvert.SerializeObject(newTask);
            var content = new StringContent(newTaskJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await _client.PostAsync($"workspaces/{workspace.Id}/projects/{project.Id}/tasks", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<List<TimeEntry>> GetTimeEntries(WorkspaceInfo workspace, UserInfo user, DateTime start, DateTime end)
    {
        return await GetPagedAsync<TimeEntry>($"workspaces/{workspace.Id}/user/{user.Id}/time-entries?start={start:yyyy-MM-dd}T00:00:00Z&end={end:yyyy-MM-dd}T23:59:59Z&in-progress=false");
    }

    public async Task<List<ProjectInfo>> GetProjects(WorkspaceInfo workspace)
    {
        return await GetPagedAsync<ProjectInfo>($"workspaces/{workspace.Id}/projects");
    }

    public async Task<List<TaskInfo>> GetTasks(WorkspaceInfo workspace, ProjectInfo project)
    {
        return await GetPagedAsync<TaskInfo>($"workspaces/{workspace.Id}/projects/{project.Id}/tasks");
    }

    private async Task<List<T>> GetPagedAsync<T>(string baseUrl)
    {
        try
        {
            var returnItems = new List<T>();
            var page = 1;
            var pageSize = 100;
            var moreToGet = true;
            while (moreToGet)
            {
                var pageInfo = baseUrl.Contains("?") ? $"&page={page}&page-size={pageSize}" : $"?page={page}&page-size={pageSize}";
                var response = await _client.GetAsync($"{baseUrl}{pageInfo}");
                var responseContent = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<List<T>>(responseContent)!;

                returnItems.AddRange(items);

                if (items.Count < pageSize)
                {
                    moreToGet = false;
                }
                else
                {
                    page++;
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