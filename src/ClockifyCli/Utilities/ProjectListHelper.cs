using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;

namespace ClockifyCli.Utilities;

public static class ProjectListHelper
{
    private const string BackOptionId = "__BACK__";
    private const string NewTaskOptionId = "__NEW__";

    private enum TaskSelectionResult
    {
        Selected,
        Back,
        NoTask,
        NewTask
    }

    private static ProjectInfo PromptForProject(IAnsiConsole console, IEnumerable<ProjectInfo> projects, string title = "Select a [green]project[/]:", bool allowNewTask = false, bool allowBack = false)
    {
        var projectChoices = projects.OrderBy(p => p.Name).ToList();

        if (allowNewTask)
        {
            // Inject a pseudo-project for adding a new task
            projectChoices.Add(new ProjectInfo(NewTaskOptionId, "+ Add new task"));
        }

        if (allowBack)
        {
            // Inject a pseudo-project for going back to recent timers
            projectChoices.Add(new ProjectInfo(BackOptionId, "← Back to recent tasks"));
        }

        return console.Prompt(
            new SelectionPrompt<ProjectInfo>()
                .Title(title)
                .PageSize(15)
                .AddChoices(projectChoices)
                .UseConverter(p => p.Id switch
                {
                    NewTaskOptionId => "[green]+ Add new task[/]",
                    BackOptionId => "[dim]← Back to recent tasks[/]",
                    _ => Markup.Escape(p.Name)
                }));
    }

    private static (TaskSelectionResult Result, TaskWithProject? Task) PromptForTaskWithProject(
        IAnsiConsole console,
        List<TaskWithProject> projectTasks,
        ProjectInfo selectedProject,
        bool allowBack,
        bool allowNoTask,
        bool allowNewTask)
    {
        var taskChoices = new List<TaskWithProject>(projectTasks);

        if (allowNoTask)
        {
            var noTaskOption = new TaskWithProject(string.Empty, "(No Task)", selectedProject.Id, selectedProject.Name);
            taskChoices.Add(noTaskOption);
        }

        if (allowNewTask)
        {
            var newTaskOption = new TaskWithProject(NewTaskOptionId, "+ Add new task", selectedProject.Id, selectedProject.Name);
            taskChoices.Add(newTaskOption);
        }

        if (allowBack)
        {
            var backOption = new TaskWithProject(BackOptionId, "← Back to project selection", selectedProject.Id, selectedProject.Name);
            taskChoices.Add(backOption);
        }

        var selectedTask = console.Prompt(
            new SelectionPrompt<TaskWithProject>()
                .Title($"Select a [green]task[/] from '{Markup.Escape(selectedProject.Name)}':")
                .PageSize(15)
                .AddChoices(taskChoices)
                .UseConverter(t => t.TaskId switch
                {
                    BackOptionId => $"[dim]{Markup.Escape(t.TaskName)}[/]",
                    NewTaskOptionId => "[green]+ Add new task[/]",
                    _ => Markup.Escape(t.TaskName)
                }));

        return selectedTask.TaskId switch
        {
            BackOptionId => (TaskSelectionResult.Back, null),
            NewTaskOptionId => (TaskSelectionResult.NewTask, null),
            "" => (TaskSelectionResult.NoTask, null),
            _ => (TaskSelectionResult.Selected, selectedTask)
        };
    }

    private static (TaskSelectionResult Result, TaskInfo? Task) PromptForTaskInfo(
        IAnsiConsole console,
        List<TaskInfo> availableTasks,
        ProjectInfo selectedProject,
        bool allowBack,
        bool allowNewTask)
    {
        var taskChoices = new List<TaskInfo>(availableTasks);

        if (allowNewTask)
        {
            var newTaskOption = new TaskInfo(NewTaskOptionId, "+ Add new task", "New");
            taskChoices.Add(newTaskOption);
        }

        if (allowBack)
        {
            var backOption = new TaskInfo(BackOptionId, "← Back to project selection", "Back");
            taskChoices.Add(backOption);
        }

        var selectedTask = console.Prompt(
            new SelectionPrompt<TaskInfo>()
                .Title($"Select a [green]task[/] from '{Markup.Escape(selectedProject.Name)}':")
                .PageSize(15)
                .AddChoices(taskChoices)
                .UseConverter(task => task.Id switch
                {
                    BackOptionId => $"[dim]{Markup.Escape(task.Name)}[/]",
                    NewTaskOptionId => "[green]+ Add new task[/]",
                    _ => Markup.Escape(task.Name)
                }));

        return selectedTask.Id switch
        {
            BackOptionId => (TaskSelectionResult.Back, null),
            NewTaskOptionId => (TaskSelectionResult.NewTask, null),
            _ => (TaskSelectionResult.Selected, selectedTask)
        };
    }

    private static string ExtractJiraKey(string jiraRefOrUrl)
    {
        if (string.IsNullOrWhiteSpace(jiraRefOrUrl))
        {
            return string.Empty;
        }

        var trimmed = jiraRefOrUrl.Trim();
        return trimmed.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
            ? trimmed[(trimmed.LastIndexOf('/') + 1)..]
            : trimmed;
    }

    private static async Task<(ProjectInfo Project, TaskInfo Task)?> CreateTaskFromJiraAsync(
        IClockifyClient clockifyClient,
        IJiraClient jiraClient,
        IAnsiConsole console,
        WorkspaceInfo workspace,
        Dictionary<string, List<TaskInfo>> projectTasksMap,
        List<ProjectInfo> selectableProjects,
        IEnumerable<ProjectInfo> allProjects,
        ProjectInfo? initialProject)
    {
        var project = initialProject ?? PromptForProject(console, allProjects, "Select [green]project[/] for new task:");

        if (!projectTasksMap.TryGetValue(project.Id, out var existingTasks))
        {
            existingTasks = new List<TaskInfo>();
            projectTasksMap[project.Id] = existingTasks;
        }

        var jiraRefOrUrl = console.Ask<string>("Enter [blue]Jira Ref[/] or [blue]URL[/]:");
        var jiraKey = ExtractJiraKey(jiraRefOrUrl);

        if (string.IsNullOrWhiteSpace(jiraKey))
        {
            console.MarkupLine("[yellow]No Jira reference provided. Operation cancelled.[/]");
            return null;
        }

        Models.JiraIssue? issue = null;
        await console.Status()
                     .StartAsync($"Finding jira: {Markup.Escape(jiraKey)}...", async _ => { issue = await jiraClient.GetIssue(jiraKey); });

        if (issue == null || string.IsNullOrWhiteSpace(issue.Key) || issue.Fields == null || string.IsNullOrWhiteSpace(issue.Fields.Summary))
        {
            console.MarkupLine($"[red]Unknown Issue '{Markup.Escape(jiraRefOrUrl)}' or issue data is incomplete[/]");
            return null;
        }

        var taskName = TaskNameFormatter.FormatTaskName(issue);

        var existingTask = existingTasks.FirstOrDefault(t => t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
        if (existingTask != null)
        {
            console.MarkupLine($"[yellow]Task '{Markup.Escape(taskName)}' already exists in project '{Markup.Escape(project.Name)}'.[/]");
            if (console.Confirm("Use existing task?"))
            {
                // Ensure the existing task has a valid ID by refreshing if needed
                if (string.IsNullOrEmpty(existingTask.Id))
                {
                    console.MarkupLine("[dim]Refreshing task details...[/]");
                    var latestTasks = await clockifyClient.GetTasks(workspace, project);
                    var refreshedTask = latestTasks.FirstOrDefault(t => t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
                    if (refreshedTask != null && !string.IsNullOrEmpty(refreshedTask.Id))
                    {
                        projectTasksMap[project.Id] = latestTasks
                            .Where(t => !t.Status.Equals("Done", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(t => t.Name)
                            .ToList();
                        return (project, refreshedTask);
                    }
                    console.MarkupLine("[red]Could not retrieve task details.[/]");
                    return null;
                }
                return (project, existingTask);
            }

            console.MarkupLine("[yellow]Operation cancelled.[/]");
            return null;
        }

        console.MarkupLine($"Will Add Task '[yellow]{Markup.Escape(taskName)}[/]' Into Project '[green]{Markup.Escape(project.Name)}[/]'");
        if (!console.Confirm("Confirm?"))
        {
            console.MarkupLine("[yellow]Operation cancelled.[/]");
            return null;
        }

        try
        {
            await console.Status()
                         .StartAsync("Adding task...", async _ => { await clockifyClient.AddTask(workspace, project, taskName); });
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Failed to add task: {Markup.Escape(ex.Message)}[/]");
            return null;
        }

        List<TaskInfo> refreshedTasks = new();
        await console.Status()
                     .StartAsync("Refreshing tasks...", async _ =>
                     {
                         var projectTasks = await clockifyClient.GetTasks(workspace, project);
                         refreshedTasks = projectTasks
                             .Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                             .OrderBy(t => t.Name)
                             .ToList();
                         projectTasksMap[project.Id] = refreshedTasks;
                     });

        var createdTask = refreshedTasks.FirstOrDefault(t => t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
        if (createdTask == null)
        {
            console.MarkupLine("[yellow]Task added but could not be located in refreshed list.[/]");
            return null;
        }

        if (!selectableProjects.Any(p => p.Id == project.Id))
        {
            selectableProjects.Add(project);
            selectableProjects.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        console.MarkupLine("[green]Task added successfully![/]");
        return (project, createdTask);
    }

    public static async Task<(ProjectInfo Project, TaskInfo Task)?> PromptForProjectAndTaskAsync(
        IClockifyClient clockifyClient,
        IJiraClient jiraClient,
        IAnsiConsole console,
        WorkspaceInfo workspace,
        AppConfiguration config,
        UserInfo user)
    {
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

                    projectTasksMap[project.Id] = activeTasks;
                }
            });

        List<ProjectInfo> projectsWithTasks = allProjects
            .Where(p => projectTasksMap.TryGetValue(p.Id, out var tasks) && tasks.Any())
            .OrderBy(p => p.Name)
            .ToList();

        if (!projectsWithTasks.Any())
        {
            console.MarkupLine("[yellow]No projects with active tasks found![/]");
            console.MarkupLine("[dim]Create projects and add tasks in Clockify first.[/]");
            return null;
        }

        async Task<(ProjectInfo Project, TaskInfo Task)?> CreateTaskAsync(ProjectInfo? initialProject)
        {
            return await CreateTaskFromJiraAsync(
                clockifyClient,
                jiraClient,
                console,
                workspace,
                projectTasksMap,
                projectsWithTasks,
                allProjects,
                initialProject);
        }

        // Wrap the selection flow so the user can return from the projects screen back to recent timers
        while (true)
        {
            // --- Recent Timers Selection ---
            if (config.RecentTasksCount > 0 && config.RecentTasksDays > 0)
            {
                while (true)
                {
                    var recentStart = DateTime.UtcNow.AddDays(-config.RecentTasksDays);
                    var recentEnd = DateTime.UtcNow;
                    var recentEntries = await clockifyClient.GetTimeEntries(workspace, user, recentStart, recentEnd);

                    var recentValid = recentEntries
                        .Where(e => !string.IsNullOrEmpty(e.ProjectId) && !string.IsNullOrEmpty(e.TaskId))
                        .Where(e => projectTasksMap.ContainsKey(e.ProjectId) && projectTasksMap[e.ProjectId].Any(t => t.Id == e.TaskId))
                        .GroupBy(e => (e.ProjectId, e.TaskId))
                        .Select(g => g.OrderByDescending(e => e.TimeInterval.Start).First())
                        .OrderByDescending(e => e.TimeInterval.Start)
                        .Take(config.RecentTasksCount)
                        .ToList();

                    if (!recentValid.Any())
                    {
                        break; // no recent tasks available - go to full selection
                    }

                    var recentChoices = recentValid
                        .Select(e => new
                        {
                            Entry = e,
                            Project = allProjects.First(p => p.Id == e.ProjectId),
                            Task = projectTasksMap[e.ProjectId].First(t => t.Id == e.TaskId)
                        })
                        .ToList();

                    var selectionPrompt = new SelectionPrompt<int>()
                        .Title("Select a [green]recent task[/] or choose [yellow]other task[/]:")
                        .PageSize(10);

                    for (var i = 0; i < recentChoices.Count; i++)
                    {
                        selectionPrompt.AddChoice(i);
                    }

                    var otherIndex = recentChoices.Count;

                    selectionPrompt.AddChoice(otherIndex);

                    selectionPrompt.UseConverter(idx =>
                    {
                        if (idx >= 0 && idx < recentChoices.Count)
                        {
                            var rc = recentChoices[idx];
                            return $"[bold]{Markup.Escape(rc.Project.Name)}[/] > [green]{Markup.Escape(rc.Task.Name)}[/]";
                        }

                        return "[yellow]Other task[/]";
                    });

                    var selectedIdx = console.Prompt(selectionPrompt);

                    if (selectedIdx < recentChoices.Count)
                    {
                        var rc = recentChoices[selectedIdx];
                        return (rc.Project, rc.Task);
                    }

                    break; // fall through to full project/task selection
                }
            }

        SelectProject:
            // --- Full Project/Task Selection ---
            // allow user to add a new task from the projects screen, or go back to recent tasks
            var selectedProject = PromptForProject(console, projectsWithTasks, "Select a [green]project[/]:", allowNewTask: true, allowBack: (config.RecentTasksCount > 0 && config.RecentTasksDays > 0) && projectsWithTasks.Count > 1);

            // Handle pseudo-selections
            if (selectedProject.Id == NewTaskOptionId)
            {
                var created = await CreateTaskAsync(null);
                if (created != null)
                {
                    return created;
                }

                continue;
            }

            if (selectedProject.Id == BackOptionId)
            {
                // Return to recent timers selection
                continue;
            }

            var allowBack = projectsWithTasks.Count > 1;

            if (!projectTasksMap.TryGetValue(selectedProject.Id, out var availableTasks))
            {
                availableTasks = new List<TaskInfo>();
                projectTasksMap[selectedProject.Id] = availableTasks;
            }

            while (true)
            {
                var (result, task) = PromptForTaskInfo(console, availableTasks, selectedProject, allowBack, allowNewTask: true);
                switch (result)
                {
                    case TaskSelectionResult.Selected:
                        return (selectedProject, task!);
                    case TaskSelectionResult.Back:
                        goto SelectProject;
                    case TaskSelectionResult.NewTask:
                        {
                            var created = await CreateTaskAsync(selectedProject);
                            if (created != null)
                            {
                                if (created.Value.Project.Id == selectedProject.Id)
                                {
                                    availableTasks = projectTasksMap[selectedProject.Id];
                                }

                                return created;
                            }

                            availableTasks = projectTasksMap[selectedProject.Id];
                            continue;
                        }
                    case TaskSelectionResult.NoTask:
                        return (selectedProject, new TaskInfo(string.Empty, "(No Task)", "None"));
                }
            }
        }
    }

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
            var (result, selectedTask) = PromptForTaskWithProject(console, projectTasks, selectedProject, allowBack, allowNoTask: true, allowNewTask: false);

            switch (result)
            {
                case TaskSelectionResult.Selected:
                    return (selectedProject, selectedTask);
                case TaskSelectionResult.NoTask:
                    return (selectedProject, null);
                case TaskSelectionResult.Back:
                    continue;
                default:
                    console.MarkupLine("[yellow]Creating new tasks is not supported in this context.[/]");
                    continue;
            }
        }
    }
}
