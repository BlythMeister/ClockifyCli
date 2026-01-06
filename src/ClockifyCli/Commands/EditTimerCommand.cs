using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace ClockifyCli.Commands;

public class EditTimerCommand : BaseCommand<EditTimerCommand.Settings>
{
    private readonly IClockifyClient clockifyClient;
    private readonly IJiraClient jiraClient;
    private readonly IAnsiConsole console;
    private readonly ConfigurationService configService;

    internal Func<SplitPromptContext, Task<SplitPromptResult?>>? SplitPromptOverride { get; set; }

    // Constructor for dependency injection (now required)
    public EditTimerCommand(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console, ConfigurationService configService)
    {
        this.clockifyClient = clockifyClient;
        this.jiraClient = jiraClient;
        this.console = console;
        this.configService = configService;
    }

    public class Settings : CommandSettings
    {
        [Description("Number of days back to look for time entries")]
        [CommandOption("-d|--days")]
        [DefaultValue(7)]
        public int Days { get; init; } = 7;
    }

    internal record SplitPromptContext(
        TimeEntry SelectedEntry,
        DateTime CurrentStartTime,
        DateTime OriginalEndLocal,
        IReadOnlyList<ProjectInfo> Projects,
        IReadOnlyList<TaskWithProject> AllTasks,
        AppConfiguration Configuration,
        UserInfo UserInfo,
        WorkspaceInfo Workspace);

    internal record SplitPromptResult(
        DateTime SplitTimeLocal,
        ProjectInfo Project,
        TaskInfo Task,
        string? Description,
        bool Proceed = true);

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await EditTimeEntry(clockifyClient, jiraClient, console, settings.Days);
        return 0;
    }

    private async Task EditTimeEntry(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console, int daysBack)
    {
        console.MarkupLine("[bold]Edit Time Entry[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Get date range for entries
        var endDate = DateTime.Today.AddDays(1);
        var startDate = DateTime.Today.AddDays(-daysBack);

        console.MarkupLine($"[dim]Looking for entries from {Markup.Escape(startDate.ToString("MMM dd"))} to {Markup.Escape(DateTime.Today.ToString("MMM dd, yyyy"))}[/]");
        console.WriteLine();

        List<TimeEntry> timeEntries = new();
        List<ProjectInfo> projects = new();
        // Load config for recent tasks feature
        var config = await configService.LoadConfigurationAsync();
        List<TaskWithProject> allTasks = new();
        TimeEntry? currentRunningEntry = null;

        await console.Status()
            .StartAsync("Loading time entries...", async ctx =>
            {
                ctx.Status("Getting time entries from Clockify...");
                timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startDate, endDate);

                ctx.Status("Checking for running timer...");
                currentRunningEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                ctx.Status("Getting projects and tasks from Clockify...");
                projects = await clockifyClient.GetProjects(workspace);

                foreach (var project in projects)
                {
                    var projectTasks = await clockifyClient.GetTasks(workspace, project);
                    var tasksWithProject = projectTasks.Select(task => new TaskWithProject(task.Id, task.Name, project.Id, project.Name)).ToList();
                    allTasks.AddRange(tasksWithProject);
                }
            });

        if (!timeEntries.Any() && currentRunningEntry == null)
        {
            console.MarkupLine("[yellow]No time entries found in the specified date range.[/]");
            console.MarkupLine("[dim]Try increasing the number of days with --days option.[/]");
            return;
        }

        // Step 1: Select date
        // Get all dates that have entries or the running timer
        var allDates = timeEntries.Select(e => e.TimeInterval.StartDate.Date).ToList();

        if (currentRunningEntry != null)
        {
            allDates.Add(currentRunningEntry.TimeInterval.StartDate.Date);
        }

        var entriesByDate = allDates.Distinct()
                                  .OrderByDescending(date => date)
                                  .Select(date => new { Date = date, Entries = timeEntries.Where(e => e.TimeInterval.StartDate.Date == date).ToList() })
                                  .ToList();

        var selectedDate = console.Prompt(
                                              new SelectionPrompt<DateTime>()
                                                  .Title("Select a [green]date[/] to edit entries from:")
                                                  .PageSize(10)
                                                  .AddChoices(entriesByDate.Select(g => g.Date))
                                                  .UseConverter(date =>
                                                  {
                                                      var entryCount = entriesByDate.First(g => g.Date == date).Entries.Count();
                                                      var hasRunningTimer = currentRunningEntry != null && currentRunningEntry.TimeInterval.StartDate.Date == date.Date;

                                                      // Include running timer in the total count
                                                      var totalCount = hasRunningTimer ? entryCount + 1 : entryCount;

                                                      // Use proper singular/plural form
                                                      var entryText = totalCount == 1 ? "entry" : "entries";
                                                      return Markup.Escape($"{date:ddd, MMM dd, yyyy} ({totalCount} {entryText})");
                                                  }));

        // Step 2: Select specific time entry from that date
        var entriesForDate = entriesByDate.First(g => g.Date == selectedDate).Entries.OrderBy(e => e.TimeInterval.StartDate).ToList();

        // Add running timer to the list only if it's on the selected date
        if (currentRunningEntry != null && currentRunningEntry.TimeInterval.StartDate.Date == selectedDate.Date)
        {
            // Check if it's not already in the list (it might be if it was started today)
            if (!entriesForDate.Any(e => e.Id == currentRunningEntry.Id))
            {
                entriesForDate.Add(currentRunningEntry);
                // Re-sort to maintain chronological order
                entriesForDate = entriesForDate.OrderBy(e => e.TimeInterval.StartDate).ToList();
            }
        }

        var selectedEntry = console.Prompt(
                                               new SelectionPrompt<TimeEntry>()
                                                   .Title($"Select a [green]time entry[/] from {Markup.Escape(selectedDate.ToString("MMM dd"))} to edit:")
                                                   .PageSize(15)
                                                   .AddChoices(entriesForDate)
                                                   .UseConverter(entry =>
                                                                 {
                                                                     var project = projects.FirstOrDefault(p => p.Id == entry.ProjectId);
                                                                     var task = allTasks.FirstOrDefault(t => t.TaskId == entry.TaskId);
                                                                     var projectName = project?.Name ?? "Unknown Project";
                                                                     var taskName = task?.TaskName ?? "No Task";
                                                                     var description = string.IsNullOrWhiteSpace(entry.Description) ? "No description" : entry.Description;

                                                                     // Check if this is the running timer
                                                                     var isRunning = currentRunningEntry != null && entry.Id == currentRunningEntry.Id;

                                                                     if (isRunning)
                                                                     {
                                                                         var startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");
                                                                         var elapsed = TimeFormatter.FormatDurationCompact(DateTime.UtcNow - entry.TimeInterval.StartDate);
                                                                         return Markup.Escape($":stopwatch: {startTime}-NOW ({elapsed}) | {projectName} > {taskName} | {description} [RUNNING]");
                                                                     }
                                                                     else
                                                                     {
                                                                         var startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");
                                                                         var endTime = entry.TimeInterval.EndDate.ToLocalTime().ToString("HH:mm");
                                                                         var duration = TimeFormatter.FormatDurationCompact(entry.TimeInterval.DurationSpan);
                                                                         return Markup.Escape($"{startTime}-{endTime} ({duration}) | {projectName} > {taskName} | {description}");
                                                                     }
                                                                 }));

        // Step 3: Show current details and edit
        await EditSelectedEntry(clockifyClient, workspace, selectedEntry, projects, allTasks, currentRunningEntry, config, user);
    }

    private async Task EditSelectedEntry(IClockifyClient clockifyClient, WorkspaceInfo workspace, TimeEntry selectedEntry, List<ProjectInfo> projects, List<TaskWithProject> allTasks, TimeEntry? currentRunningEntry, AppConfiguration config, UserInfo userInfo)
    {
        var project = projects.FirstOrDefault(p => p.Id == selectedEntry.ProjectId);
        var task = allTasks.FirstOrDefault(t => t.TaskId == selectedEntry.TaskId);
        var isRunning = currentRunningEntry != null && selectedEntry.Id == currentRunningEntry.Id;

        console.WriteLine();
        if (isRunning)
        {
            console.MarkupLine("[bold green]Current Running Timer Details[/]");
        }
        else
        {
            console.MarkupLine("[bold]Current Time Entry Details[/]");
        }
        console.WriteLine();

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Current Value");

        var projectName = project?.Name != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task?.TaskName != null ? Markup.Escape(task.TaskName) : "No Task";
        var description = string.IsNullOrWhiteSpace(selectedEntry.Description) ? "No description" : Markup.Escape(selectedEntry.Description);
        var currentStartTime = selectedEntry.TimeInterval.StartDate.ToLocalTime();

        table.AddRow("Project", projectName);
        table.AddRow("Task", taskName);
        table.AddRow("Description", description);
        table.AddRow("Start Time", currentStartTime.ToString("MMM dd, yyyy HH:mm"));

        if (isRunning)
        {
            var elapsed = TimeFormatter.FormatDurationCompact(DateTime.UtcNow - selectedEntry.TimeInterval.StartDate);
            table.AddRow("Status", "[green]RUNNING[/]");
            table.AddRow("Elapsed Time", elapsed);
        }
        else
        {
            var currentEndTime = selectedEntry.TimeInterval.EndDate.ToLocalTime();
            var currentDuration = TimeFormatter.FormatDurationCompact(selectedEntry.TimeInterval.DurationSpan);
            table.AddRow("End Time", currentEndTime.ToString("MMM dd, yyyy HH:mm"));
            table.AddRow("Duration", currentDuration);
        }

        console.Write(table);
        console.WriteLine();

        // Initialize variables with current values
        // Local method to edit project and task
        var newStartTime = currentStartTime;
        DateTime? newEndTime = isRunning ? null : selectedEntry.TimeInterval.EndDate.ToLocalTime();
        var newDescription = selectedEntry.Description;
        var newProjectId = selectedEntry.ProjectId;
        var newTaskId = selectedEntry.TaskId;
        var hasChanges = false;
        ProjectInfo? newProject = null;
        TaskInfo? newTask = null;

        // Local method to edit project and task
        async Task EditProjectAndTaskAsync()
        {
            console.WriteLine();
            console.MarkupLine("[bold]Selecting New Project and Task[/]");
            console.WriteLine();

            // Use recent timers selection logic (with fallback)
            var result = await ProjectListHelper.PromptForProjectAndTaskAsync(clockifyClient, jiraClient, console, workspace, config, userInfo);
            if (result == null)
            {
                console.MarkupLine("[yellow]Project/task selection cancelled or no projects available.[/]");
                return;
            }
            (ProjectInfo selectedProject, TaskInfo selectedTask) = result.Value;
            var tempNewProjectId = selectedProject.Id;
            var tempNewTaskId = selectedTask.Id;

            // Check if this is actually a change
            if (newProjectId != tempNewProjectId || newTaskId != tempNewTaskId)
            {
                newProjectId = tempNewProjectId;
                newTaskId = tempNewTaskId;
                newProject = selectedProject;
                newTask = selectedTask;
                hasChanges = true;
                var taskName = selectedTask.Name ?? "(No Task)";
                console.MarkupLine($"[green]✓[/] Project/Task will be changed to: [cyan]{Markup.Escape(selectedProject.Name)}[/] - [yellow]{Markup.Escape(taskName)}[/]");
            }
            else
            {
                console.MarkupLine("[dim]Project/Task unchanged (same as current).[/]");
            }
        }

        async Task<bool> SplitTimeEntryAsync()
        {
            if (isRunning)
            {
                console.MarkupLine("[yellow]Cannot split a running timer.[/]");
                return false;
            }

            if (selectedEntry.TimeInterval.IsRunning)
            {
                console.MarkupLine("[yellow]Cannot split a timer without an end time.[/]");
                return false;
            }

            if (hasChanges)
            {
                console.WriteLine();
                if (!console.Confirm("Splitting will discard pending edits. Continue?"))
                {
                    console.MarkupLine("[yellow]Split cancelled.[/]");
                    return false;
                }
            }

            var originalEndLocal = selectedEntry.TimeInterval.EndDate.ToLocalTime();
            if (originalEndLocal <= currentStartTime)
            {
                console.MarkupLine("[red]Invalid timer duration. Unable to split.[/]");
                return false;
            }

            console.WriteLine();
            console.MarkupLine("[bold]Splitting Time Entry[/]");
            console.WriteLine();

            DateTime splitTimeLocal;
            ProjectInfo splitProject;
            TaskInfo splitTask;
            string? splitDescription;
            bool keepNewTimerAtEnd = true; // Default: new timer at end (current behavior)
            SplitPromptResult? overrideResult = null;

            if (SplitPromptOverride != null)
            {
                overrideResult = await SplitPromptOverride(new SplitPromptContext(
                    selectedEntry,
                    currentStartTime,
                    originalEndLocal,
                    projects,
                    allTasks,
                    config,
                    userInfo,
                    workspace));

                if (overrideResult == null || !overrideResult.Proceed)
                {
                    console.MarkupLine("[yellow]Split cancelled.[/]");
                    return false;
                }

                splitTimeLocal = DateTime.SpecifyKind(overrideResult.SplitTimeLocal, DateTimeKind.Local);
                if (splitTimeLocal <= currentStartTime || splitTimeLocal >= originalEndLocal)
                {
                    console.MarkupLine("[red]Override split time must fall within the entry duration.[/]");
                    return false;
                }

                splitProject = overrideResult.Project;
                splitTask = overrideResult.Task;
                splitDescription = string.IsNullOrWhiteSpace(overrideResult.Description)
                    ? null
                    : overrideResult.Description!.Trim();
            }
            else
            {
                // Ask user which portion should get the new project/task
                var splitDirection = console.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which portion should be assigned to the [green]new project/task[/]?")
                        .AddChoices(new[]
                        {
                            "End portion (split time → end time)",
                            "Start portion (start time → split time)"
                        }));

                keepNewTimerAtEnd = splitDirection.StartsWith("End");

                while (true)
                {
                    var splitInput = console.Prompt(
                        new TextPrompt<string>($"Enter [green]split time[/] between {Markup.Escape(currentStartTime.ToString("HH:mm"))} and {Markup.Escape(originalEndLocal.ToString("HH:mm"))}:")
                            .Validate(input =>
                            {
                                if (!IntelligentTimeParser.TryParseEndTime(input, out var parsedSplit, currentStartTime))
                                {
                                    return ValidationResult.Error("Please enter a valid time format (e.g., 10:15, 3:30 PM, 15:30).");
                                }

                                var candidate = currentStartTime.Date.Add(parsedSplit);
                                if (candidate <= currentStartTime)
                                {
                                    candidate = candidate.AddDays(1);
                                }

                                if (candidate <= currentStartTime)
                                {
                                    return ValidationResult.Error("Split time must be after the entry start time.");
                                }

                                if (candidate >= originalEndLocal)
                                {
                                    return ValidationResult.Error("Split time must be before the entry end time.");
                                }

                                return ValidationResult.Success();
                            }));

                    if (IntelligentTimeParser.TryParseEndTime(splitInput, out var splitTimeSpan, currentStartTime))
                    {
                        var candidate = currentStartTime.Date.Add(splitTimeSpan);
                        if (candidate <= currentStartTime)
                        {
                            candidate = candidate.AddDays(1);
                        }

                        if (candidate > currentStartTime && candidate < originalEndLocal)
                        {
                            splitTimeLocal = DateTime.SpecifyKind(candidate, DateTimeKind.Local);
                            break;
                        }
                    }

                    console.MarkupLine("[red]Invalid split time. Please try again.[/]");
                }

                var projectSelection = await ProjectListHelper.PromptForProjectAndTaskAsync(clockifyClient, jiraClient, console, workspace, config, userInfo);
                if (projectSelection == null)
                {
                    console.MarkupLine("[yellow]Split cancelled. No project selected.[/]");
                    return false;
                }

                (splitProject, splitTask) = projectSelection.Value;

                var splitDescriptionRaw = console.Prompt(
                    new TextPrompt<string>("Enter [green]description[/] for new entry (optional):").AllowEmpty());
                splitDescription = string.IsNullOrWhiteSpace(splitDescriptionRaw) ? null : splitDescriptionRaw.Trim();
            }

            var splitTaskName = string.IsNullOrEmpty(splitTask.Name) ? "No Task" : splitTask.Name;
            var originalTask = allTasks.FirstOrDefault(t => t.TaskId == selectedEntry.TaskId);
            var originalTaskName = originalTask?.TaskName ?? "No Task";
            var originalDescriptionDisplay = string.IsNullOrWhiteSpace(selectedEntry.Description) ? "[dim]No description[/]" : Markup.Escape(selectedEntry.Description);
            var newDescriptionDisplay = string.IsNullOrWhiteSpace(splitDescription) ? "[dim]No description[/]" : Markup.Escape(splitDescription);

            var summary = new Table();
            summary.AddColumn("Entry");
            summary.AddColumn("Project");
            summary.AddColumn("Task");
            summary.AddColumn("Start");
            summary.AddColumn("End");
            summary.AddColumn("Description");

            if (keepNewTimerAtEnd)
            {
                // New timer at end: original keeps start→split, new gets split→end
                summary.AddRow(
                    "Original",
                    projectName,
                    Markup.Escape(originalTaskName),
                    currentStartTime.ToString("HH:mm"),
                    splitTimeLocal.ToString("HH:mm"),
                    originalDescriptionDisplay);

                summary.AddRow(
                    "New",
                    Markup.Escape(splitProject.Name),
                    Markup.Escape(splitTaskName),
                    splitTimeLocal.ToString("HH:mm"),
                    originalEndLocal.ToString("HH:mm"),
                    newDescriptionDisplay);
            }
            else
            {
                // New timer at start: new gets start→split, original keeps split→end
                summary.AddRow(
                    "New",
                    Markup.Escape(splitProject.Name),
                    Markup.Escape(splitTaskName),
                    currentStartTime.ToString("HH:mm"),
                    splitTimeLocal.ToString("HH:mm"),
                    newDescriptionDisplay);

                summary.AddRow(
                    "Original",
                    projectName,
                    Markup.Escape(originalTaskName),
                    splitTimeLocal.ToString("HH:mm"),
                    originalEndLocal.ToString("HH:mm"),
                    originalDescriptionDisplay);
            }

            console.WriteLine();
            console.MarkupLine("[bold]Split Summary[/]");
            console.Write(summary);
            console.WriteLine();

            if (overrideResult == null)
            {
                if (!console.Confirm("Split this time entry?"))
                {
                    console.MarkupLine("[yellow]Split cancelled.[/]");
                    return false;
                }
            }

            var splitTimeUtc = splitTimeLocal.ToUniversalTime();
            var originalStartUtc = selectedEntry.TimeInterval.StartDate.Kind == DateTimeKind.Local
                ? selectedEntry.TimeInterval.StartDate.ToUniversalTime()
                : selectedEntry.TimeInterval.StartDate;
            var originalEndUtc = selectedEntry.TimeInterval.EndDate.Kind == DateTimeKind.Local
                ? selectedEntry.TimeInterval.EndDate.ToUniversalTime()
                : selectedEntry.TimeInterval.EndDate;

            await console.Status()
                .StartAsync("Splitting time entry...", async _ =>
                {
                    if (keepNewTimerAtEnd)
                    {
                        // New timer at end: original keeps start→split, new gets split→end
                        await clockifyClient.UpdateTimeEntry(
                            workspace,
                            selectedEntry,
                            originalStartUtc,
                            splitTimeUtc,
                            selectedEntry.Description,
                            selectedEntry.ProjectId,
                            selectedEntry.TaskId);

                        await clockifyClient.AddTimeEntry(
                            workspace,
                            splitProject.Id,
                            string.IsNullOrWhiteSpace(splitTask.Id) ? null : splitTask.Id,
                            splitDescription,
                            splitTimeLocal,
                            originalEndLocal);
                    }
                    else
                    {
                        // New timer at start: new gets start→split, original keeps split→end
                        await clockifyClient.UpdateTimeEntry(
                            workspace,
                            selectedEntry,
                            splitTimeUtc,
                            originalEndUtc,
                            selectedEntry.Description,
                            selectedEntry.ProjectId,
                            selectedEntry.TaskId);

                        await clockifyClient.AddTimeEntry(
                            workspace,
                            splitProject.Id,
                            string.IsNullOrWhiteSpace(splitTask.Id) ? null : splitTask.Id,
                            splitDescription,
                            currentStartTime,
                            splitTimeLocal);
                    }
                });

            console.MarkupLine("[green]:check_mark: Time entry split successfully![/]");
            return true;
        }

        // Menu-based editing loop
        var menuOptions = new List<string>
        {
            "Change project/task",
            "Change times",
            "Change description"
        };

        if (!isRunning)
        {
            menuOptions.Add("Split timer");
        }

        menuOptions.Add("Done (apply changes and exit)");

        while (true)
        {
            console.WriteLine();
            var editOption = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to edit?")
                    .PageSize(10)
                    .AddChoices(menuOptions)
                    .UseConverter(choice => choice switch
                    {
                        "Done (apply changes and exit)" => hasChanges ? "[green]Done (apply changes and exit)[/]" : "[dim]Done (no changes to apply)[/]",
                        _ => choice
                    }));

            switch (editOption)
            {
                case "Change project/task":
                    await EditProjectAndTaskAsync();
                    break;

                case "Change times":
                    EditTimes();
                    break;

                case "Change description":
                    EditDescription();
                    break;

                case "Split timer":
                    if (await SplitTimeEntryAsync())
                    {
                        return;
                    }
                    break;

                case "Done (apply changes and exit)":
                    goto ExitEditLoop;

                default:
                    console.MarkupLine("[red]Invalid option selected.[/]");
                    break;
            }
        }

    ExitEditLoop:

        if (!hasChanges)
        {
            console.MarkupLine("[yellow]No changes made. Operation cancelled.[/]");
            return;
        }

        // Local method to edit times
        void EditTimes()
        {
            console.WriteLine();
            console.MarkupLine("[bold]Editing Times[/]");
            console.WriteLine();

            var originalStartTime = newStartTime;
            var originalEndTime = newEndTime;

            var newStartTimeStr = console.Prompt(
                new TextPrompt<string>($"Enter new [green]start time[/] (e.g., 9:30, 2:30 PM, 2:30p, 14:30, or leave blank to keep {Markup.Escape(newStartTime.ToString("HH:mm"))}):")
                    .AllowEmpty());

            if (!string.IsNullOrWhiteSpace(newStartTimeStr))
            {
                // For running timers, use current time as context. For completed timers, use the end time as context for better disambiguation.
                // If end time is null, fallback to start time as context.
                var contextTime = isRunning ? DateTime.Now : (newEndTime ?? newStartTime);

                if (IntelligentTimeParser.TryParseStartTime(newStartTimeStr, out var startTimeSpan, contextTime))
                {
                    var proposedStartTime = newStartTime.Date.Add(startTimeSpan);

                    // Validate the time makes sense
                    if (IntelligentTimeParser.ValidateTimeInContext(startTimeSpan, contextTime, isStartTime: true, out var startErrorMessage))
                    {
                        newStartTime = proposedStartTime;
                    }
                    else
                    {
                        console.MarkupLine($"[red]Error: {startErrorMessage}[/]");
                        return;
                    }
                }
                else
                {
                    console.MarkupLine("[red]Invalid time format. Please use formats like 9:30, 2:30 PM, or 14:30.[/]");
                    return;
                }
            }

            if (!isRunning)
            {
                var newEndTimeStr = console.Prompt(
                    new TextPrompt<string>($"Enter new [green]end time[/] (e.g., 5:30, 2:30 PM, 2:30p, 17:30, or leave blank to keep {Markup.Escape(newEndTime!.Value.ToString("HH:mm"))}):")
                        .AllowEmpty());

                if (!string.IsNullOrWhiteSpace(newEndTimeStr))
                {
                    if (IntelligentTimeParser.TryParseEndTime(newEndTimeStr, out var endTimeSpan, newStartTime))
                    {
                        var proposedEndTime = newStartTime.Date.Add(endTimeSpan);

                        // Handle case where end time is next day
                        if (proposedEndTime <= newStartTime)
                        {
                            proposedEndTime = proposedEndTime.AddDays(1);
                        }

                        // Validate the time makes sense
                        if (IntelligentTimeParser.ValidateTimeInContext(endTimeSpan, newStartTime, isStartTime: false, out var endErrorMessage))
                        {
                            newEndTime = proposedEndTime;
                        }
                        else
                        {
                            console.MarkupLine($"[red]Error: {endErrorMessage}[/]");
                            return;
                        }
                    }
                    else
                    {
                        console.MarkupLine("[red]Invalid time format. Please use formats like 5:30, 5:30 PM, or 17:30.[/]");
                        return;
                    }
                }

                // Validate times for completed entries
                if (newEndTime <= newStartTime)
                {
                    console.MarkupLine("[red]End time must be after start time. No changes applied.[/]");
                    return;
                }
            }

            // Check if times actually changed
            if (originalStartTime != newStartTime || originalEndTime != newEndTime)
            {
                hasChanges = true;
                if (isRunning)
                {
                    console.MarkupLine($"[green]✓[/] Start time will be changed to: [cyan]{newStartTime:HH:mm}[/]");
                }
                else
                {
                    console.MarkupLine($"[green]✓[/] Times will be changed to: [cyan]{newStartTime:HH:mm}[/] - [cyan]{newEndTime!.Value:HH:mm}[/]");
                }
            }
            else
            {
                console.MarkupLine("[dim]Times unchanged (same as current).[/]");
            }
        }

        // Local method to edit description
        void EditDescription()
        {
            console.WriteLine();
            console.MarkupLine("[bold]Editing Description[/]");
            console.WriteLine();

            var originalDescription = newDescription;

            newDescription = console.Prompt(
                new TextPrompt<string>("Enter new [green]description[/] (leave blank to keep current, or enter [red]-[/] to clear):")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(newDescription))
            {
                newDescription = originalDescription;
            }
            else if (newDescription.Trim() == "-")
            {
                newDescription = "";
            }

            // Check if description actually changed
            if (originalDescription != newDescription)
            {
                hasChanges = true;
                if (string.IsNullOrWhiteSpace(newDescription))
                {
                    console.MarkupLine("[green]✓[/] Description will be cleared");
                }
                else
                {
                    console.MarkupLine($"[green]✓[/] Description will be changed to: [cyan]{Markup.Escape(newDescription)}[/]");
                }
            }
            else
            {
                console.MarkupLine("[dim]Description unchanged (same as current).[/]");
            }
        }

        // Step 3: Show summary of changes
        console.WriteLine();
        console.MarkupLine("[bold]Summary of Changes[/]");

        var summaryTable = new Table();
        summaryTable.AddColumn("Field");
        summaryTable.AddColumn("Current");
        summaryTable.AddColumn("New");

        // Get new project and task names for display
        // Use stored objects if available (from EditProjectAndTaskAsync), otherwise fall back to lookups
        if (newProject == null)
        {
            newProject = projects.FirstOrDefault(p => p.Id == newProjectId);
        }
        if (newTask == null)
        {
            var taskWithProject = allTasks.FirstOrDefault(t => t.TaskId == newTaskId);
            if (taskWithProject != null)
            {
                newTask = new TaskInfo(taskWithProject.TaskId, taskWithProject.TaskName, "Active");
            }
        }
        var newProjectName = newProject?.Name ?? "Unknown Project";
        var newTaskName = newTask?.Name ?? "No Task";

        summaryTable.AddRow(
            "Project",
            projectName,
            Markup.Escape(newProjectName));

        summaryTable.AddRow(
            "Task",
            taskName,
            Markup.Escape(newTaskName));

        summaryTable.AddRow(
                            "Start Time",
                            currentStartTime.ToString("HH:mm"),
                            newStartTime.ToString("HH:mm"));

        if (!isRunning)
        {
            var currentEndTime = selectedEntry.TimeInterval.EndDate.ToLocalTime();
            var currentDuration = TimeFormatter.FormatDurationCompact(selectedEntry.TimeInterval.DurationSpan);
            var newDuration = TimeFormatter.FormatDurationCompact(newEndTime!.Value - newStartTime);

            summaryTable.AddRow(
                                "End Time",
                                currentEndTime.ToString("HH:mm"),
                                newEndTime.Value.ToString("HH:mm"));
            summaryTable.AddRow(
                                "Duration",
                                currentDuration,
                                newDuration);
        }
        else
        {
            summaryTable.AddRow(
                                "Status",
                                "[green]RUNNING[/]",
                                "[green]RUNNING[/]");
        }
        summaryTable.AddRow(
                            "Description",
                            string.IsNullOrWhiteSpace(selectedEntry.Description) ? "[dim]No description[/]" : Markup.Escape(selectedEntry.Description),
                            string.IsNullOrWhiteSpace(newDescription) ? "[dim]No description[/]" : Markup.Escape(newDescription));

        console.Write(summaryTable);
        console.WriteLine();

        // Step 4: Confirm changes
        if (console.Confirm("Apply these changes?"))
        {
            await console.Status()
                             .StartAsync("Updating time entry...", async ctx =>
                             {
                                 if (isRunning)
                                 {
                                     await clockifyClient.UpdateRunningTimeEntry(workspace, selectedEntry, newStartTime.ToUniversalTime(), newDescription, newProjectId, newTaskId);
                                 }
                                 else
                                 {
                                     await clockifyClient.UpdateTimeEntry(workspace, selectedEntry, newStartTime.ToUniversalTime(), newEndTime!.Value.ToUniversalTime(), newDescription, newProjectId, newTaskId);
                                 }
                             });

            console.MarkupLine("[green]:check_mark: Time entry updated successfully![/]");
        }
        else
        {
            console.MarkupLine("[yellow]Changes cancelled.[/]");
        }
    }
}
