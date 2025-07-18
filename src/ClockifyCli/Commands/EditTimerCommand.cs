using System.ComponentModel;
using System.Globalization;
using ClockifyCli.Models;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class EditTimerCommand : BaseCommand<EditTimerCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Number of days back to look for time entries")]
        [CommandOption("-d|--days")]
        [DefaultValue(7)]
        public int Days { get; init; } = 7;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        await EditTimeEntry(clockifyClient, settings.Days);
        return 0;
    }

    private async Task EditTimeEntry(Services.ClockifyClient clockifyClient, int daysBack)
    {
        AnsiConsole.MarkupLine("[bold]Edit Time Entry[/]");
        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Get date range for entries
        var endDate = DateTime.Today.AddDays(1);
        var startDate = DateTime.Today.AddDays(-daysBack);

        AnsiConsole.MarkupLine($"[dim]Looking for entries from {Markup.Escape(startDate.ToString("MMM dd"))} to {Markup.Escape(DateTime.Today.ToString("MMM dd, yyyy"))}[/]");
        AnsiConsole.WriteLine();

        List<TimeEntry> timeEntries = new();
        List<ProjectInfo> projects = new();
        List<TaskInfo> allTasks = new();

        await AnsiConsole.Status()
            .StartAsync("Loading time entries...", async ctx =>
            {
                ctx.Status("Getting time entries from Clockify...");
                timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startDate, endDate);

                // Filter out current running entries (they have no end time)
                timeEntries = timeEntries.Where(e => !string.IsNullOrEmpty(e.TimeInterval.End)).ToList();

                ctx.Status("Getting projects and tasks from Clockify...");
                projects = await clockifyClient.GetProjects(workspace);

                foreach (var project in projects)
                {
                    var projectTasks = await clockifyClient.GetTasks(workspace, project);
                    allTasks.AddRange(projectTasks);
                }
            });

        if (!timeEntries.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No completed time entries found in the specified date range.[/]");
            AnsiConsole.MarkupLine("[dim]Try increasing the number of days with --days option.[/]");
            return;
        }

        // Step 1: Select date
        var entriesByDate = timeEntries
            .GroupBy(e => e.TimeInterval.StartDate.Date)
            .OrderByDescending(g => g.Key)
            .ToList();

        var selectedDate = AnsiConsole.Prompt(
            new SelectionPrompt<DateTime>()
                .Title("Select a [green]date[/] to edit entries from:")
                .PageSize(10)
                .AddChoices(entriesByDate.Select(g => g.Key))
                .UseConverter(date => Markup.Escape($"{date:ddd, MMM dd, yyyy} ({entriesByDate.First(g => g.Key == date).Count()} entries)")));

        // Step 2: Select specific time entry from that date
        var entriesForDate = entriesByDate.First(g => g.Key == selectedDate).OrderBy(e => e.TimeInterval.StartDate).ToList();

        var selectedEntry = AnsiConsole.Prompt(
            new SelectionPrompt<TimeEntry>()
                .Title($"Select a [green]time entry[/] from {Markup.Escape(selectedDate.ToString("MMM dd"))} to edit:")
                .PageSize(15)
                .AddChoices(entriesForDate)
                .UseConverter(entry =>
                {
                    var project = projects.FirstOrDefault(p => p.Id == entry.ProjectId);
                    var task = allTasks.FirstOrDefault(t => t.Id == entry.TaskId);
                    var projectName = project?.Name ?? "Unknown Project";
                    var taskName = task?.Name ?? "No Task";
                    var startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");
                    var endTime = entry.TimeInterval.EndDate.ToLocalTime().ToString("HH:mm");
                    var duration = TimeFormatter.FormatDurationCompact(entry.TimeInterval.DurationSpan);
                    var description = string.IsNullOrWhiteSpace(entry.Description) ? "No description" : entry.Description;
                    
                    return Markup.Escape($"{startTime}-{endTime} ({duration}) | {projectName} > {taskName} | {description}");
                }));

        // Step 3: Show current details and edit
        await EditSelectedEntry(clockifyClient, workspace, selectedEntry, projects, allTasks);
    }

    private async Task EditSelectedEntry(Services.ClockifyClient clockifyClient, WorkspaceInfo workspace, TimeEntry timeEntry, List<ProjectInfo> projects, List<TaskInfo> allTasks)
    {
        var project = projects.FirstOrDefault(p => p.Id == timeEntry.ProjectId);
        var task = allTasks.FirstOrDefault(t => t.Id == timeEntry.TaskId);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Current Time Entry Details[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Current Value");

        var projectName = project?.Name != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task?.Name != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(timeEntry.Description) ? "No description" : Markup.Escape(timeEntry.Description);
        var currentStartTime = timeEntry.TimeInterval.StartDate.ToLocalTime();
        var currentEndTime = timeEntry.TimeInterval.EndDate.ToLocalTime();
        var currentDuration = TimeFormatter.FormatDurationCompact(timeEntry.TimeInterval.DurationSpan);

        table.AddRow("Project", projectName);
        table.AddRow("Task", taskName);
        table.AddRow("Description", description);
        table.AddRow("Start Time", currentStartTime.ToString("MMM dd, yyyy HH:mm"));
        table.AddRow("End Time", currentEndTime.ToString("MMM dd, yyyy HH:mm"));
        table.AddRow("Duration", currentDuration);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Get new start time
        var newStartTimeStr = AnsiConsole.Ask<string>(
            $"Enter new [green]start time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentStartTime.ToString("HH:mm"))}):",
            defaultValue: string.Empty);

        DateTime newStartTime = currentStartTime;
        if (!string.IsNullOrWhiteSpace(newStartTimeStr))
        {
            if (TimeSpan.TryParseExact(newStartTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out var startTimeSpan))
            {
                newStartTime = currentStartTime.Date.Add(startTimeSpan);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Invalid time format. Keeping original start time.[/]");
            }
        }

        // Get new end time
        var newEndTimeStr = AnsiConsole.Ask<string>(
            $"Enter new [green]end time[/] (HH:mm format, or leave blank to keep {Markup.Escape(currentEndTime.ToString("HH:mm"))}):",
            defaultValue: string.Empty);

        DateTime newEndTime = currentEndTime;
        if (!string.IsNullOrWhiteSpace(newEndTimeStr))
        {
            if (TimeSpan.TryParseExact(newEndTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out var endTimeSpan))
            {
                newEndTime = newStartTime.Date.Add(endTimeSpan);
                
                // Handle case where end time is next day
                if (newEndTime <= newStartTime)
                {
                    newEndTime = newEndTime.AddDays(1);
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Invalid time format. Keeping original end time.[/]");
            }
        }

        // Validate times
        if (newEndTime <= newStartTime)
        {
            AnsiConsole.MarkupLine("[red]End time must be after start time. Operation cancelled.[/]");
            return;
        }

        // Get new description (optional)
        var newDescription = AnsiConsole.Ask<string>(
            "Enter new [green]description[/] (or leave blank to keep current):",
            defaultValue: string.Empty);

        if (string.IsNullOrWhiteSpace(newDescription))
        {
            newDescription = timeEntry.Description;
        }

        // Show summary of changes
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary of Changes[/]");

        var summaryTable = new Table();
        summaryTable.AddColumn("Field");
        summaryTable.AddColumn("Current");
        summaryTable.AddColumn("New");

        var newDuration = TimeFormatter.FormatDurationCompact(newEndTime - newStartTime);

        summaryTable.AddRow(
            "Start Time",
            currentStartTime.ToString("HH:mm"),
            newStartTime.ToString("HH:mm"));
        summaryTable.AddRow(
            "End Time",
            currentEndTime.ToString("HH:mm"),
            newEndTime.ToString("HH:mm"));
        summaryTable.AddRow(
            "Duration",
            currentDuration,
            newDuration);
        summaryTable.AddRow(
            "Description",
            string.IsNullOrWhiteSpace(timeEntry.Description) ? "[dim]No description[/]" : Markup.Escape(timeEntry.Description),
            string.IsNullOrWhiteSpace(newDescription) ? "[dim]No description[/]" : Markup.Escape(newDescription));

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        // Confirm changes
        if (AnsiConsole.Confirm("Apply these changes?"))
        {
            await AnsiConsole.Status()
                .StartAsync("Updating time entry...", async ctx =>
                {
                    await clockifyClient.UpdateTimeEntry(workspace, timeEntry, newStartTime.ToUniversalTime(), newEndTime.ToUniversalTime(), newDescription);
                });

            AnsiConsole.MarkupLine("[green]? Time entry updated successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Changes cancelled.[/]");
        }
    }
}
