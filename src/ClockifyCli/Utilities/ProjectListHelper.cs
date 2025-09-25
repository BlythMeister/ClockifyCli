using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;

namespace ClockifyCli.Utilities;

public static class ProjectListHelper
{
    // Shared private method for project selection
    private static ProjectInfo PromptForProject(IAnsiConsole console, List<ProjectInfo> projects, string title = "Select a [green]project[/]:")
    {
        return console.Prompt(
            new SelectionPrompt<ProjectInfo>()
                .Title(title)
                .PageSize(15)
                .AddChoices(projects.OrderBy(p => p.Name))
                .UseConverter(p => Markup.Escape(p.Name)));
    }

    // Shared private method for TaskWithProject selection (with back and no-task options)
    private static TaskWithProject? PromptForTaskWithProject(IAnsiConsole console, List<TaskWithProject> projectTasks, ProjectInfo selectedProject, bool allowBack, bool allowNoTask)
    {
        var taskChoices = new List<TaskWithProject>(projectTasks);
        if (allowNoTask)
        {
            var noTaskOption = new TaskWithProject(string.Empty, "(No Task)", selectedProject.Id, selectedProject.Name);
            taskChoices.Add(noTaskOption);
        }
        if (allowBack)
        {
            var backOption = new TaskWithProject("__BACK__", "← Back to project selection", selectedProject.Id, selectedProject.Name);
            taskChoices.Add(backOption);
        }
        var selectedTaskOrBack = console.Prompt(
            new SelectionPrompt<TaskWithProject>()
                .Title($"Select new [green]task[/] from '{Markup.Escape(selectedProject.Name)}':")
                .PageSize(15)
                .AddChoices(taskChoices)
                .UseConverter(t => t.TaskId == "__BACK__" ? $"[dim]{Markup.Escape(t.TaskName)}[/]" : Markup.Escape(t.TaskName)));
        if (selectedTaskOrBack.TaskId == "__BACK__")
            return null; // Signal to go back
        if (string.IsNullOrEmpty(selectedTaskOrBack.TaskId))
            return null; // No task selected
        return selectedTaskOrBack;
    }

    // Shared private method for TaskInfo selection (with back option)
    private static TaskInfo PromptForTaskInfo(IAnsiConsole console, List<TaskInfo> availableTasks, ProjectInfo selectedProject, bool allowBack)
    {
        var taskChoices = new List<TaskInfo>(availableTasks);
        if (allowBack)
        {
            var backOption = new TaskInfo("__BACK__", "← Back to project selection", "Back");
            taskChoices.Add(backOption);
        }
        var selectedTaskOrBack = console.Prompt(
            new SelectionPrompt<TaskInfo>()
                .Title($"Select a [green]task[/] from '{Markup.Escape(selectedProject.Name)}':")
                .PageSize(15)
                .AddChoices(taskChoices)
                .UseConverter(task => task.Id == "__BACK__" ? $"[dim]{Markup.Escape(task.Name)}[/]" : Markup.Escape(task.Name)));
        return selectedTaskOrBack;
    }

    /// <summary>
    /// Prompts the user to select a project and a task, supporting back navigation.
    /// </summary>
    /// <param name="clockifyClient">Clockify API client</param>
    /// <param name="console">Console for user interaction</param>
    /// <param name="workspace">WorkspaceInfo for the current workspace</param>
    /// <returns>Tuple of selected ProjectInfo and TaskInfo, or null if cancelled</returns>
    public static async Task<(ProjectInfo Project, TaskInfo Task)?> PromptForProjectAndTaskAsync(
        IClockifyClient clockifyClient,
        IAnsiConsole console,
        WorkspaceInfo workspace,
        AppConfiguration config,
        UserInfo user)
    {
        // Load projects and tasks
        List<ProjectInfo> allProjects = new();
        Dictionary<string, List<TaskInfo>> projectTasksMap = new();
        await console.Status()
            .StartAsync("Loading projects and tasks...", async ctx =>
            {
                ctx.Status("Getting projects from Clockify...");
                allProjects = await clockifyClient.GetProjects(workspace);
                foreach (var project in allProjects)
                {
                    ctx.Status($"Getting tasks for {project.Name}...");
                    var projectTasks = await clockifyClient.GetTasks(workspace, project);
                    var activeTasks = projectTasks
                        .Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                        .OrderBy(t => t.Name)
                        .ToList();
                    if (activeTasks.Any())
                    {
                        projectTasksMap[project.Id] = activeTasks;
                    }
                }
            });

        var projects = allProjects.Where(p => projectTasksMap.ContainsKey(p.Id)).ToList();
        if (!projects.Any())
        {
            console.MarkupLine("[yellow]No projects with active tasks found![/]");
            console.MarkupLine("[dim]Create projects and add tasks in Clockify first.[/]");
            return null;
        }

        // --- Recent Timers Selection ---
        if (config.RecentTasksCount > 0 && config.RecentTasksDays > 0)
        {
            var recentStart = DateTime.UtcNow.AddDays(-config.RecentTasksDays);
            var recentEnd = DateTime.UtcNow;
            var recentEntries = await clockifyClient.GetTimeEntries(workspace, user, recentStart, recentEnd);
            // Only consider entries with valid project and task, and that are still active
            var recentValid = recentEntries
                .Where(e => !string.IsNullOrEmpty(e.ProjectId) && !string.IsNullOrEmpty(e.TaskId))
                .Where(e => projectTasksMap.ContainsKey(e.ProjectId) && projectTasksMap[e.ProjectId].Any(t => t.Id == e.TaskId))
                .GroupBy(e => (e.ProjectId, e.TaskId))
                .Select(g => g.OrderByDescending(e => e.TimeInterval.Start).First())
                .OrderByDescending(e => e.TimeInterval.Start)
                .Take(config.RecentTasksCount)
                .ToList();

            if (recentValid.Any())
            {
                // Build display list
                var recentChoices = recentValid
                    .Select(e => new
                    {
                        Entry = e,
                        Project = allProjects.First(p => p.Id == e.ProjectId),
                        Task = projectTasksMap[e.ProjectId].First(t => t.Id == e.TaskId)
                    })
                    .ToList();

                var selectionPrompt = new SelectionPrompt<int>()
                    .Title("Select a [green]recent task[/] or [yellow]other task[/]:")
                    .PageSize(10);
                int otherIdx = recentChoices.Count;
                for (int i = 0; i < recentChoices.Count; i++)
                {
                    selectionPrompt.AddChoice(i);
                }
                selectionPrompt.AddChoice(otherIdx);
                selectionPrompt.UseConverter(idx =>
                {
                    if (idx >= 0 && idx < recentChoices.Count)
                    {
                        var rc = recentChoices[idx];
                        return $"[bold]{Markup.Escape(rc.Project.Name)}[/] > [green]{Markup.Escape(rc.Task.Name)}[/] [dim]({rc.Entry.TimeInterval.Start:yyyy-MM-dd HH:mm})[/]";
                    }
                    else if (idx == otherIdx)
                    {
                        return "[yellow]Other task[/]";
                    }
                    return "[dim]Unknown[/]";
                });

                var selectedIdx = console.Prompt(selectionPrompt);
                if (selectedIdx < recentChoices.Count)
                {
                    var rc = recentChoices[selectedIdx];
                    return (rc.Project, rc.Task);
                }
                // else fall through to full project/task selection
            }
        }

        // --- Full Project/Task Selection ---
        while (true)
        {
            var selectedProject = PromptForProject(console, projects);
            var availableTasks = projectTasksMap[selectedProject.Id];
            var allowBack = projects.Count > 1;
            var selectedTask = PromptForTaskInfo(console, availableTasks, selectedProject, allowBack);
            if (selectedTask.Id == "__BACK__")
                continue;
            return (selectedProject, selectedTask);
        }
    }

    /// <summary>
    /// Prompts the user to select a project and a task from pre-fetched lists, supporting back navigation and 'no task' option.
    /// </summary>
    /// <param name="console">Console for user interaction</param>
    /// <param name="projects">List of available projects</param>
    /// <param name="allTasks">List of all tasks with project info</param>
    /// <returns>Tuple of selected ProjectInfo and TaskWithProject (or null for no task), or null if cancelled</returns>
    public static (ProjectInfo Project, TaskWithProject? Task)? PromptForProjectAndTaskFromLists(
        IAnsiConsole console,
        List<ProjectInfo> projects,
        List<TaskWithProject> allTasks)
    {
        if (projects == null || projects.Count == 0)
        {
            console.MarkupLine("[yellow]No projects found![/]");
            return null;
        }

        // Only show projects that have at least one task in allTasks
        var projectsWithTasks = projects.Where(p => allTasks.Any(t => t.ProjectId == p.Id)).ToList();
        if (!projectsWithTasks.Any())
        {
            console.MarkupLine("[yellow]No projects with tasks found![/]");
            return null;
        }

        while (true)
        {
            var selectedProject = PromptForProject(console, projectsWithTasks, "Select new [green]project[/]:");
            var projectTasks = allTasks.Where(t => t.ProjectId == selectedProject.Id)
                                       .OrderBy(t => t.TaskName)
                                       .ToList();
            var allowBack = projectsWithTasks.Count > 1;
            var selectedTask = PromptForTaskWithProject(console, projectTasks, selectedProject, allowBack, true);
            if (selectedTask == null && allowBack)
                continue;
            // If user selected 'no task', return null for task
            return (selectedProject, selectedTask);
        }
    }
}
