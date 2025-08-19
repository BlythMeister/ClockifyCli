using ClockifyCli.Models;
using ClockifyCli.Utilities;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace ClockifyCli.Services;

public class ClockifyClient : IClockifyClient
{
    private readonly HttpClient client;
    private readonly IClock clock;
    private readonly RateLimiter rateLimiter;

    // Constructor for dependency injection with HttpClient and IClock
    public ClockifyClient(HttpClient httpClient, string apiKey, IClock clock)
    {
        client = httpClient;
        this.clock = clock;
        this.rateLimiter = RateLimiter.ForClockifyApi();
        client.BaseAddress = new Uri("https://api.clockify.me/api/v1/");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    // Constructor for dependency injection with HttpClient only (uses SystemClock)
    public ClockifyClient(HttpClient httpClient, string apiKey) : this(httpClient, apiKey, new SystemClock())
    {
    }

    // Original constructor for backward compatibility
    public ClockifyClient(string apiKey) : this(new HttpClient(), apiKey)
    {
    }

    private async Task<HttpResponseMessage> GetWithRateLimitAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        return await client.GetAsync(requestUri, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostWithRateLimitAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        return await client.PostAsync(requestUri, content, cancellationToken);
    }

    private async Task<HttpResponseMessage> PutWithRateLimitAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        return await client.PutAsync(requestUri, content, cancellationToken);
    }

    private async Task<HttpResponseMessage> PatchWithRateLimitAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        return await client.PatchAsync(requestUri, content, cancellationToken);
    }

    private async Task<HttpResponseMessage> DeleteWithRateLimitAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        return await client.DeleteAsync(requestUri, cancellationToken);
    }

    public async Task<UserInfo> GetLoggedInUser()
    {
        try
        {
            var response = await GetWithRateLimitAsync("user");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Try to parse error response
                try
                {
                    var error = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    throw new HttpRequestException($"API Error: {error?.Message ?? "Unknown error"} (Status: {response.StatusCode})");
                }
                catch (JsonException)
                {
                    // If we can't parse the error response, use the raw content
                    throw new HttpRequestException($"API Error: {responseContent} (Status: {response.StatusCode})");
                }
            }

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
            var response = await GetWithRateLimitAsync("workspaces");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Try to parse error response
                try
                {
                    var error = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    throw new HttpRequestException($"API Error: {error?.Message ?? "Unknown error"} (Status: {response.StatusCode})");
                }
                catch (JsonException)
                {
                    // If we can't parse the error response, use the raw content
                    throw new HttpRequestException($"API Error: {responseContent} (Status: {response.StatusCode})");
                }
            }

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

            var response = await PostWithRateLimitAsync($"workspaces/{workspace.Id}/projects/{project.Id}/tasks", content);
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
            var response = await GetWithRateLimitAsync($"workspaces/{workspace.Id}/user/{user.Id}/time-entries?in-progress=true");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Try to parse error response
                try
                {
                    var error = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    throw new HttpRequestException($"API Error: {error?.Message ?? "Unknown error"} (Status: {response.StatusCode})");
                }
                catch (JsonException)
                {
                    // If we can't parse the error response, use the raw content
                    throw new HttpRequestException($"API Error: {responseContent} (Status: {response.StatusCode})");
                }
            }

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
            var stopTimeData = new { end = clock.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
            var stopTimeJson = JsonConvert.SerializeObject(stopTimeData);
            var content = new StringContent(stopTimeJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await PatchWithRateLimitAsync($"workspaces/{workspace.Id}/user/{user.Id}/time-entries", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Try to parse error response
                try
                {
                    var error = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    throw new HttpRequestException($"API Error: {error?.Message ?? "Unknown error"} (Status: {response.StatusCode})");
                }
                catch (JsonException)
                {
                    // If we can't parse the error response, use the raw content
                    throw new HttpRequestException($"API Error: {responseContent} (Status: {response.StatusCode})");
                }
            }

            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<TimeEntry> StartTimeEntry(WorkspaceInfo workspace, string projectId, string? taskId, string? description, DateTime? startTime = null)
    {
        try
        {
            var effectiveStartTime = startTime ?? clock.UtcNow;
            
            // Convert local time to UTC if startTime was provided (user input is local)
            // If no startTime provided, clock.UtcNow is already UTC
            if (startTime.HasValue && startTime.Value.Kind != DateTimeKind.Utc)
            {
                effectiveStartTime = startTime.Value.ToUniversalTime();
            }
            
            var startTimeEntry = new StartTimeEntry(
                                                    effectiveStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
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

            var response = await PostWithRateLimitAsync($"workspaces/{workspace.Id}/time-entries", content);

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

    public async Task<TimeEntry> AddTimeEntry(WorkspaceInfo workspace, string projectId, string? taskId, string? description, DateTime startTime, DateTime endTime)
    {
        try
        {
            // Convert local times to UTC if they aren't already UTC
            var utcStartTime = startTime.Kind == DateTimeKind.Utc ? startTime : startTime.ToUniversalTime();
            var utcEndTime = endTime.Kind == DateTimeKind.Utc ? endTime : endTime.ToUniversalTime();

            var timeInterval = new
            {
                start = utcStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                end = utcEndTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var addTimeEntry = new
            {
                start = utcStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                end = utcEndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                projectId = projectId,
                taskId = string.IsNullOrEmpty(taskId) ? null : taskId,
                description = string.IsNullOrWhiteSpace(description) ? null : description
            };

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var addTimeJson = JsonConvert.SerializeObject(addTimeEntry, serializerSettings);
            var content = new StringContent(addTimeJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await PostWithRateLimitAsync($"workspaces/{workspace.Id}/time-entries", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to add time entry. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error adding time entry: {e}");
            throw;
        }
    }

    public async Task UpdateTaskStatus(WorkspaceInfo workspace, ProjectInfo project, TaskInfo task, string status)
    {
        try
        {
            // Use the full task update endpoint instead of just updating status
            var updateData = new 
            { 
                name = task.Name,
                status = status
            };
            var updateJson = JsonConvert.SerializeObject(updateData);
            var content = new StringContent(updateJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await PutWithRateLimitAsync($"workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Response status code does not indicate success: {response.StatusCode} ({response.ReasonPhrase}). Response: {responseContent}");
            }
            
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    public async Task<TimeEntry> UpdateTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry, DateTime newStartTime, DateTime newEndTime, string? description = null)
    {
        try
        {
            var updateData = new
            {
                start = newStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                end = newEndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                projectId = timeEntry.ProjectId,
                taskId = string.IsNullOrEmpty(timeEntry.TaskId) ? (string?)null : timeEntry.TaskId,
                description = description ?? timeEntry.Description
            };

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var updateJson = JsonConvert.SerializeObject(updateData, serializerSettings);
            var content = new StringContent(updateJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await PutWithRateLimitAsync($"workspaces/{workspace.Id}/time-entries/{timeEntry.Id}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update time entry. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error updating time entry: {e}");
            throw;
        }
    }

    public async Task<TimeEntry> UpdateRunningTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry, DateTime newStartTime, string? description = null)
    {
        try
        {
            var updateData = new
            {
                start = newStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                projectId = timeEntry.ProjectId,
                taskId = string.IsNullOrEmpty(timeEntry.TaskId) ? (string?)null : timeEntry.TaskId,
                description = description ?? timeEntry.Description
            };

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var updateJson = JsonConvert.SerializeObject(updateData, serializerSettings);
            var content = new StringContent(updateJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await PutWithRateLimitAsync($"workspaces/{workspace.Id}/time-entries/{timeEntry.Id}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update running time entry. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TimeEntry>(responseContent)!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error updating running time entry: {e}");
            throw;
        }
    }

    public async Task DeleteTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry)
    {
        try
        {
            var response = await DeleteWithRateLimitAsync($"workspaces/{workspace.Id}/time-entries/{timeEntry.Id}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to delete time entry. Status: {response.StatusCode}, Response: {errorContent}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deleting time entry: {e}");
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
                var response = await GetWithRateLimitAsync($"{baseUrl}{pageInfo}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Try to parse error response
                    try
                    {
                        var error = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                        throw new HttpRequestException($"API Error: {error?.Message ?? "Unknown error"} (Status: {response.StatusCode})");
                    }
                    catch (JsonException)
                    {
                        // If we can't parse the error response, use the raw content
                        throw new HttpRequestException($"API Error: {responseContent} (Status: {response.StatusCode})");
                    }
                }

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
