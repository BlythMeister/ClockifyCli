using ClockifyCli.Models;

namespace ClockifyCli.Services;

public interface IClockifyClient
{
    Task<UserInfo> GetLoggedInUser();
    Task<List<WorkspaceInfo>> GetLoggedInUserWorkspaces();
    Task<List<ProjectInfo>> GetProjects(WorkspaceInfo workspace);
    Task<List<TaskInfo>> GetTasks(WorkspaceInfo workspace, ProjectInfo project);
    Task<List<TimeEntry>> GetTimeEntries(WorkspaceInfo workspace, UserInfo user, DateTime start, DateTime end);
    Task<TimeEntry?> GetCurrentTimeEntry(WorkspaceInfo workspace, UserInfo user);
    Task UpdateTaskStatus(WorkspaceInfo workspace, ProjectInfo project, TaskInfo task, string status);
    Task<TimeEntry> StartTimeEntry(WorkspaceInfo workspace, string projectId, string? taskId, string? description);
    Task<TimeEntry> StopCurrentTimeEntry(WorkspaceInfo workspace, UserInfo user);
    Task<TimeEntry> UpdateTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry, DateTime newStartTime, DateTime newEndTime, string? description = null);
    Task<TimeEntry> UpdateRunningTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry, DateTime newStartTime, string? description = null);
    Task DeleteTimeEntry(WorkspaceInfo workspace, TimeEntry timeEntry);
    Task AddTask(WorkspaceInfo workspace, ProjectInfo project, string taskName);
}
