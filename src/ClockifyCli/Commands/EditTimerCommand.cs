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
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public EditTimerCommand(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

    public class Settings : CommandSettings
    {
        [Description("Number of days back to look for time entries")]
        [CommandOption("-d|--days")]
        [DefaultValue(7)]
        public int Days { get; init; } = 7;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await EditTimeEntry(clockifyClient, console, settings.Days);
        return 0;
    }

    private async Task EditTimeEntry(IClockifyClient clockifyClient, IAnsiConsole console, int daysBack)
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
        await EditSelectedEntry(clockifyClient, workspace, selectedEntry, projects, allTasks, currentRunningEntry);
    }

    private async Task EditSelectedEntry(IClockifyClient clockifyClient, WorkspaceInfo workspace, TimeEntry selectedEntry, List<ProjectInfo> projects, List<TaskWithProject> allTasks, TimeEntry? currentRunningEntry)
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

        // Local method to edit project and task
        void EditProjectAndTask()
        {
            console.WriteLine();
            console.MarkupLine("[bold]Selecting New Project and Task[/]");
            console.WriteLine();

            var result = ProjectListHelper.PromptForProjectAndTaskFromLists(console, projects, allTasks);
            if (result == null)
            {
                console.MarkupLine("[yellow]Project/task selection cancelled or no projects available.[/]");
                return;
            }
            var (selectedProject, selectedTask) = result.Value;
            var tempNewProjectId = selectedProject.Id;
            var tempNewTaskId = selectedTask?.TaskId;

            // Check if this is actually a change
            if (newProjectId != tempNewProjectId || newTaskId != tempNewTaskId)
            {
                newProjectId = tempNewProjectId;
                newTaskId = tempNewTaskId;
                hasChanges = true;
                var taskName = selectedTask?.TaskName ?? "(No Task)";
                console.MarkupLine($"[green]✓[/] Project/Task will be changed to: [cyan]{Markup.Escape(selectedProject.Name)}[/] - [yellow]{Markup.Escape(taskName)}[/]");
            }
            else
            {
                console.MarkupLine("[dim]Project/Task unchanged (same as current).[/]");
            }
        }

        // Menu-based editing loop
        while (true)
        {
            console.WriteLine();
            var editOption = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to edit?")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "Change project/task",
                        "Change times",
                        "Change description",
                        "Done (apply changes and exit)"
                    })
                    .UseConverter(choice => choice switch
                    {
                        "Done (apply changes and exit)" => hasChanges ? "[green]Done (apply changes and exit)[/]" : "[dim]Done (no changes to apply)[/]",
                        _ => choice
                    }));

            switch (editOption)
            {
                case "Change project/task":
                    EditProjectAndTask();
                    break;

                case "Change times":
                    EditTimes();
                    break;

                case "Change description":
                    EditDescription();
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
        var newProject = projects.FirstOrDefault(p => p.Id == newProjectId);
        var newTask = allTasks.FirstOrDefault(t => t.TaskId == newTaskId);
        var newProjectName = newProject?.Name ?? "Unknown Project";
        var newTaskName = newTask?.TaskName ?? "No Task";

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
