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
        WorkspaceInfo workspace)
    {
        // Load projects
        List<ProjectInfo> projects = new();
        await console.Status()
            .StartAsync("Loading projects...", async ctx =>
            {
                ctx.Status("Getting projects from Clockify...");
                projects = await clockifyClient.GetProjects(workspace);
            });

        if (!projects.Any())
        {
            console.MarkupLine("[yellow]No projects found![/]");
            console.MarkupLine("[dim]Create some projects in Clockify first.[/]");
            return null;
        }

        while (true)
        {
            var selectedProject = PromptForProject(console, projects);
            // Load tasks for the selected project
            List<TaskInfo> availableTasks = new();
            await console.Status()
                .StartAsync($"Loading tasks for {selectedProject.Name}...", async ctx =>
                {
                    ctx.Status($"Getting tasks from {selectedProject.Name}...");
                    var projectTasks = await clockifyClient.GetTasks(workspace, selectedProject);
                    availableTasks = projectTasks
                        .Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                        .OrderBy(t => t.Name)
                        .ToList();
                });

            if (!availableTasks.Any())
            {
                console.MarkupLine($"[yellow]No active tasks found for project '{Markup.Escape(selectedProject.Name)}'![/]");
                console.MarkupLine("[dim]Add some tasks to this project first using 'clockify-cli add-task-from-jira'.[/]");
                return null;
            }

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

        while (true)
        {
            var selectedProject = PromptForProject(console, projects, "Select new [green]project[/]:");
            var projectTasks = allTasks.Where(t => t.ProjectId == selectedProject.Id)
                                       .OrderBy(t => t.TaskName)
                                       .ToList();
            var allowBack = projects.Count > 1;
            var selectedTask = PromptForTaskWithProject(console, projectTasks, selectedProject, allowBack, true);
            if (selectedTask == null && allowBack)
                continue;
            // If user selected 'no task', return null for task
            return (selectedProject, selectedTask);
        }
    }
}
