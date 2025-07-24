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

        // Get new start time
        var newStartTimeStr = console.Prompt(
            new TextPrompt<string>($"Enter new [green]start time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentStartTime.ToString("HH:mm"))}):")
                .AllowEmpty());

        var newStartTime = currentStartTime;
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

        // Get new end time
        DateTime? newEndTime = null;

        if (!isRunning)
        {
            var currentEndTime = selectedEntry.TimeInterval.EndDate.ToLocalTime();
            var newEndTimeStr = console.Prompt(
                new TextPrompt<string>($"Enter new [green]end time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentEndTime.ToString("HH:mm"))}):")
                    .AllowEmpty());

            newEndTime = currentEndTime;
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

        // Get new description (optional)
        var newDescription = console.Prompt(
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

        // Show summary of changes
        console.WriteLine();
        console.MarkupLine("[bold]Summary of Changes[/]");

        var summaryTable = new Table();
        summaryTable.AddColumn("Field");
        summaryTable.AddColumn("Current");
        summaryTable.AddColumn("New");

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

        // Confirm changes
        if (console.Confirm("Apply these changes?"))
        {
            await console.Status()
                             .StartAsync("Updating time entry...", async ctx =>
                             {
                                 if (isRunning)
                                 {
                                     await clockifyClient.UpdateRunningTimeEntry(workspace, selectedEntry, newStartTime.ToUniversalTime(), newDescription);
                                 }
                                 else
                                 {
                                     await clockifyClient.UpdateTimeEntry(workspace, selectedEntry, newStartTime.ToUniversalTime(), newEndTime!.Value.ToUniversalTime(), newDescription);
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
