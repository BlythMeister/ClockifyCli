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

        // Ask user what they want to edit with simple Y/N questions
        var changeProject = console.Confirm("Do you want to [green]change the project[/]?", false);
        var changeTimes = console.Confirm("Do you want to [green]change the times[/]?", false);
        var changeDescription = console.Confirm("Do you want to [green]change the description[/]?", false);

        if (!changeProject && !changeTimes && !changeDescription)
        {
            console.MarkupLine("[yellow]No changes selected. Operation cancelled.[/]");
            return;
        }

        console.WriteLine();
        console.MarkupLine("[bold]Editing Selected Fields[/]");
        console.WriteLine();

        // Initialize variables with current values
        var newStartTime = currentStartTime;
        DateTime? newEndTime = isRunning ? null : selectedEntry.TimeInterval.EndDate.ToLocalTime();
        var newDescription = selectedEntry.Description;
        var newProjectId = selectedEntry.ProjectId;
        var newTaskId = selectedEntry.TaskId;

        // Step 2: Edit each selected field
        if (changeProject)
        {
            // Loop to allow going back from task selection to project selection
            while (true)
            {
                var selectedProject = console.Prompt(
                    new SelectionPrompt<ProjectInfo>()
                        .Title("Select new [green]project[/]:")
                        .PageSize(15)
                        .AddChoices(projects.OrderBy(p => p.Name))
                        .UseConverter(p => Markup.Escape(p.Name)));
                
                newProjectId = selectedProject.Id;

                // If project changed, also ask for task within that project
                var projectTasks = allTasks.Where(t => t.ProjectId == selectedProject.Id)
                                          .OrderBy(t => t.TaskName)
                                          .ToList();

                if (projectTasks.Any())
                {
                    // Only add "Back" option if there are multiple projects
                    var taskChoices = new List<TaskWithProject>(projectTasks);
                    if (projects.Count > 1)
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

                    // Check if user selected "Back"
                    if (selectedTaskOrBack.TaskId == "__BACK__")
                    {
                        continue; // Go back to project selection
                    }

                    newTaskId = selectedTaskOrBack.TaskId;
                    break; // Exit the loop when a task is selected
                }
                else
                {
                    console.MarkupLine($"[yellow]No tasks found for project '{Markup.Escape(selectedProject.Name)}'. Task will be cleared.[/]");
                    newTaskId = null;
                    break; // Exit the loop since there are no tasks to select
                }
            }
        }

        if (changeTimes)
        {
            var newStartTimeStr = console.Prompt(
                new TextPrompt<string>($"Enter new [green]start time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentStartTime.ToString("HH:mm"))}):")
                    .AllowEmpty());

            if (!string.IsNullOrWhiteSpace(newStartTimeStr))
            {
                if (TimeSpan.TryParseExact(newStartTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out var startTimeSpan))
                {
                    newStartTime = currentStartTime.Date.Add(startTimeSpan);
                }
                else
                {
                    console.MarkupLine("[red]Invalid time format. Keeping original start time.[/]");
                }
            }
        }

        if (changeTimes && !isRunning)
        {
            var currentEndTime = selectedEntry.TimeInterval.EndDate.ToLocalTime();
            var newEndTimeStr = console.Prompt(
                new TextPrompt<string>($"Enter new [green]end time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentEndTime.ToString("HH:mm"))}):")
                    .AllowEmpty());

            if (!string.IsNullOrWhiteSpace(newEndTimeStr))
            {
                if (TimeSpan.TryParseExact(newEndTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out var endTimeSpan))
                {
                    newEndTime = newStartTime.Date.Add(endTimeSpan);

                    // Handle case where end time is next day
                    if (newEndTime <= newStartTime)
                    {
                        newEndTime = newEndTime.Value.AddDays(1);
                    }
                }
                else
                {
                    console.MarkupLine("[red]Invalid time format. Keeping original end time.[/]");
                }
            }

            // Validate times for completed entries
            if (newEndTime <= newStartTime)
            {
                console.MarkupLine("[red]End time must be after start time. Operation cancelled.[/]");
                return;
            }
        }

        if (changeDescription)
        {
            newDescription = console.Prompt(
                new TextPrompt<string>("Enter new [green]description[/] (leave blank to keep current, or enter [red]-[/] to clear):")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(newDescription))
            {
                newDescription = selectedEntry.Description;
            }
            else if (newDescription.Trim() == "-")
            {
                newDescription = "";
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
