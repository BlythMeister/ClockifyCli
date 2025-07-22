using System.ComponentModel;
using System.Globalization;
using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class DeleteTimerCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public DeleteTimerCommand(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await DeleteCompletedTimer(clockifyClient, console);
        return 0;
    }

    private async Task DeleteCompletedTimer(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        console.MarkupLine("[bold]Delete Completed Timer[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Get current week dates (Monday to Sunday)
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        var endOfWeek = startOfWeek.AddDays(6);

        console.MarkupLine($"[dim]Looking for completed timers from this week ({Markup.Escape(startOfWeek.ToString("MMM dd"))} - {Markup.Escape(endOfWeek.ToString("MMM dd, yyyy"))})[/]");
        console.WriteLine();

        List<TimeEntry> timeEntries = new();
        List<ProjectInfo> projects = new();
        List<TaskInfo> allTasks = new();

        await console.Status()
            .StartAsync("Loading time entries...", async ctx =>
            {
                ctx.Status("Getting time entries from Clockify...");
                timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startOfWeek, endOfWeek.AddDays(1));

                // Filter out current running entries (they have no end time) and only include entries from this week
                timeEntries = timeEntries
                    .Where(e => !string.IsNullOrEmpty(e.TimeInterval.End) &&
                               e.TimeInterval.StartDate.Date >= startOfWeek &&
                               e.TimeInterval.StartDate.Date <= endOfWeek)
                    .OrderByDescending(e => e.TimeInterval.StartDate) // Newest first
                    .ToList();

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
            console.MarkupLine("[yellow]No completed time entries found for this week.[/]");
            console.MarkupLine("[dim]Only timers created this week can be deleted.[/]");
            return;
        }

        console.MarkupLine($"[green]Found {timeEntries.Count} completed timer(s) from this week[/]");
        console.WriteLine();

        // Select specific time entry to delete
        var selectedEntry = console.Prompt(
            new SelectionPrompt<TimeEntry>()
                .Title("Select a [red]timer to delete[/]:")
                .PageSize(15)
                .AddChoices(timeEntries)
                .UseConverter(entry =>
                {
                    var project = projects.FirstOrDefault(p => p.Id == entry.ProjectId);
                    var task = allTasks.FirstOrDefault(t => t.Id == entry.TaskId);
                    var projectName = project?.Name ?? "Unknown Project";
                    var taskName = task?.Name ?? "No Task";
                    var startTime = entry.TimeInterval.StartDate.ToLocalTime();
                    var endTime = entry.TimeInterval.EndDate.ToLocalTime();
                    var duration = TimeFormatter.FormatDurationCompact(entry.TimeInterval.DurationSpan);
                    var description = string.IsNullOrWhiteSpace(entry.Description) ? "No description" : entry.Description;

                    return Markup.Escape($"{startTime:MMM dd, HH:mm}-{endTime:HH:mm} ({duration}) | {projectName} > {taskName} | {description}");
                }));

        // Show details of the selected entry
        await ShowTimerDetails(selectedEntry, projects, allTasks, clockifyClient, workspace);
    }

    private async Task ShowTimerDetails(TimeEntry timeEntry, List<ProjectInfo> projects, List<TaskInfo> allTasks, Services.IClockifyClient clockifyClient, WorkspaceInfo workspace)
    {
        var project = projects.FirstOrDefault(p => p.Id == timeEntry.ProjectId);
        var task = allTasks.FirstOrDefault(t => t.Id == timeEntry.TaskId);

        console.WriteLine();
        console.MarkupLine("[bold]Timer Details[/]");
        console.WriteLine();

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Value");

        var projectName = project?.Name != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task?.Name != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(timeEntry.Description) ? "No description" : Markup.Escape(timeEntry.Description);
        var startTime = timeEntry.TimeInterval.StartDate.ToLocalTime();
        var endTime = timeEntry.TimeInterval.EndDate.ToLocalTime();
        var duration = TimeFormatter.FormatDurationCompact(timeEntry.TimeInterval.DurationSpan);

        table.AddRow("Project", projectName);
        table.AddRow("Task", taskName);
        table.AddRow("Description", description);
        table.AddRow("Date", startTime.ToString("ddd, MMM dd, yyyy"));
        table.AddRow("Start Time", startTime.ToString("HH:mm"));
        table.AddRow("End Time", endTime.ToString("HH:mm"));
        table.AddRow("Duration", duration);

        console.Write(table);
        console.WriteLine();

        console.MarkupLine("[red]⚠️ WARNING: This will permanently delete the timer![/]");
        console.MarkupLine("[dim]This action cannot be undone.[/]");
        console.WriteLine();

        // Final confirmation
        if (console.Confirm($"Are you sure you want to delete this timer with {duration} of logged time?"))
        {
            await console.Status()
                .StartAsync("Deleting timer...", async ctx =>
                {
                    await clockifyClient.DeleteTimeEntry(workspace, timeEntry);
                });

            console.MarkupLine("[green]✅ Timer deleted successfully![/]");
            console.MarkupLine($"[dim]Deleted timer with {duration} of logged time[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Deletion cancelled.[/]");
        }
    }
}
