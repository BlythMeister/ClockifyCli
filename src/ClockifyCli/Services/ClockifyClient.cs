using ClockifyCli.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace ClockifyCli.Services;

public class ClockifyClient
{
    private readonly HttpClient client;

    public ClockifyClient(string apiKey)
    {
        client = new HttpClient();
        client.BaseAddress = new Uri("https://api.clockify.me/api/v1/");
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<UserInfo> GetLoggedInUser()
    {
        try
        {
            var response = await client.GetAsync("user");
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
            var response = await client.GetAsync("workspaces");
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

            var response = await client.PostAsync($"workspaces/{workspace.Id}/projects/{project.Id}/tasks", content);
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

    public async Task<TimeEntry?> GetCurrentTimeEntry(WorkspaceInfo workspace, UserInfo user)
    {
        try
        {
            var response = await client.GetAsync($"workspaces/{workspace.Id}/user/{user.Id}/time-entries?in-progress=true");
            var responseContent = await response.Content.ReadAsStringAsync();
            var entries = JsonConvert.DeserializeObject<List<TimeEntry>>(responseContent)!;
            return entries.FirstOrDefault();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<TimeEntry> StopCurrentTimeEntry(WorkspaceInfo workspace, UserInfo user)
    {
        try
        {
            var stopTimeData = new { end = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
            var stopTimeJson = JsonConvert.SerializeObject(stopTimeData);
            var content = new StringContent(stopTimeJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await client.PatchAsync($"workspaces/{workspace.Id}/user/{user.Id}/time-entries", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<TimeEntry> StartTimeEntry(WorkspaceInfo workspace, string projectId, string? taskId, string? description)
    {
        try
        {
            var startTimeEntry = new StartTimeEntry(
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                projectId,
                string.IsNullOrEmpty(taskId) ? null : taskId,
                string.IsNullOrWhiteSpace(description) ? null : description
            );

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var startTimeJson = JsonConvert.SerializeObject(startTimeEntry, serializerSettings);
            var content = new StringContent(startTimeJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await client.PostAsync($"workspaces/{workspace.Id}/time-entries", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to start time entry. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error starting time entry: {e}");
            throw;
        }
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
                var response = await client.GetAsync($"{baseUrl}{pageInfo}");
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
