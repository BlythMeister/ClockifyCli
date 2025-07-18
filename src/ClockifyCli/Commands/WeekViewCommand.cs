using ClockifyCli.Models;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace ClockifyCli.Commands;

public class WeekViewCommand : BaseCommand<WeekViewCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Include the currently running time entry in the view")]
        [CommandOption("--include-current")]
        [DefaultValue(false)]
        public bool IncludeCurrent { get; init; } = false;

        [Description("Show detailed view with start time, end time, and duration for each entry")]
        [CommandOption("--detailed")]
        [DefaultValue(false)]
        public bool Detailed { get; init; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        await ShowCurrentWeekTimeEntries(clockifyClient, settings.IncludeCurrent, settings.Detailed);
        return 0;
    }

    private async Task ShowCurrentWeekTimeEntries(Services.ClockifyClient clockifyClient, bool includeCurrent, bool detailed)
    {
        AnsiConsole.MarkupLine("[bold]Current Week Time Entries[/]");
        if (includeCurrent)
        {
            AnsiConsole.MarkupLine("[dim]Including in-progress time entry[/]");
        }

        if (detailed)
        {
            AnsiConsole.MarkupLine("[dim]Detailed view with start/end times[/]");
        }

        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Get current week dates (Monday to Sunday)
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        var endOfWeek = startOfWeek.AddDays(6);

        AnsiConsole.MarkupLine($"[dim]Week: {startOfWeek:MMM dd} - {endOfWeek:MMM dd, yyyy}[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
                         .StartAsync("Loading time entries...", async ctx =>
                                                                {
                                                                    ctx.Status("Getting time entries from Clockify...");
                                                                    var timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startOfWeek, endOfWeek.AddDays(1));

                                                                    // Get current running entry if requested
                                                                    TimeEntry? currentEntry = null;
                                                                    if (includeCurrent)
                                                                    {
                                                                        ctx.Status("Getting current running time entry...");
                                                                        currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
                                                                    }

                                                                    ctx.Status("Getting projects and tasks from Clockify...");
                                                                    var projects = await clockifyClient.GetProjects(workspace);
                                                                    var allTasks = new List<TaskInfo>();

                                                                    foreach (var project in projects)
                                                                    {
                                                                        var projectTasks = await clockifyClient.GetTasks(workspace, project);
                                                                        allTasks.AddRange(projectTasks);
                                                                    }

                                                                    // Group entries by date
                                                                    var entriesByDate = timeEntries
                                                                                        .Where(e => e.TimeInterval.StartDate.Date >= startOfWeek && e.TimeInterval.StartDate.Date <= endOfWeek)
                                                                                        .GroupBy(e => e.TimeInterval.StartDate.Date)
                                                                                        .OrderBy(g => g.Key)
                                                                                        .ToList();

                                                                    // Add current entry to today's entries if it exists and is within the week
                                                                    if (currentEntry != null && currentEntry.TimeInterval.StartDate.Date >= startOfWeek && currentEntry.TimeInterval.StartDate.Date <= endOfWeek)
                                                                    {
                                                                        var currentDate = currentEntry.TimeInterval.StartDate.Date;
                                                                        var existingDateGroup = entriesByDate.FirstOrDefault(g => g.Key == currentDate);

                                                                        if (existingDateGroup != null)
                                                                        {
                                                                            // Add to existing date group
                                                                            var updatedEntries = existingDateGroup.ToList();
                                                                            updatedEntries.Add(currentEntry);

                                                                            // Remove old group and add updated one
                                                                            entriesByDate.RemoveAll(g => g.Key == currentDate);
                                                                            entriesByDate.Add(updatedEntries.GroupBy(e => e.TimeInterval.StartDate.Date).First());
                                                                        }
                                                                        else
                                                                        {
                                                                            // Create new date group for current entry
                                                                            var newEntries = new List<TimeEntry> { currentEntry };
                                                                            entriesByDate.Add(newEntries.GroupBy(e => e.TimeInterval.StartDate.Date).First());
                                                                        }

                                                                        // Re-sort by date
                                                                        entriesByDate = entriesByDate.OrderBy(g => g.Key).ToList();
                                                                    }

                                                                    if (!entriesByDate.Any())
                                                                    {
                                                                        if (currentEntry != null && (currentEntry.TimeInterval.StartDate.Date < startOfWeek || currentEntry.TimeInterval.StartDate.Date > endOfWeek))
                                                                        {
                                                                            AnsiConsole.MarkupLine("[yellow]No time entries found for the current week.[/]");
                                                                            AnsiConsole.MarkupLine("[dim]Current running entry is from a different week.[/]");
                                                                        }
                                                                        else
                                                                        {
                                                                            AnsiConsole.MarkupLine("[yellow]No time entries found for the current week.[/]");
                                                                        }

                                                                        return;
                                                                    }

                                                                    // Create table with appropriate columns based on detailed flag
                                                                    var table = new Table();
                                                                    table.AddColumn("Date");
                                                                    table.AddColumn("Project");
                                                                    table.AddColumn("Task");
                                                                    table.AddColumn("Description");

                                                                    if (detailed)
                                                                    {
                                                                        table.AddColumn("Start Time", c => c.RightAligned());
                                                                        table.AddColumn("End Time", c => c.RightAligned());
                                                                    }

                                                                    table.AddColumn("Duration", c => c.RightAligned());
                                                                    table.AddColumn("Status");

                                                                    var weekTotal = TimeSpan.Zero;

                                                                    foreach (var dateGroup in entriesByDate)
                                                                    {
                                                                        var date = dateGroup.Key;
                                                                        var dayEntries = dateGroup.OrderBy(e => e.TimeInterval.StartDate).ToList();
                                                                        var dayTotal = TimeSpan.Zero;

                                                                        for (var i = 0; i < dayEntries.Count; i++)
                                                                        {
                                                                            var entry = dayEntries[i];
                                                                            var project = projects.FirstOrDefault(p => p.Id == entry.ProjectId);
                                                                            var task = allTasks.FirstOrDefault(t => t.Id == entry.TaskId);

                                                                            var isCurrentEntry = currentEntry != null && entry.Id == currentEntry.Id;
                                                                            TimeSpan duration;
                                                                            string status;
                                                                            string startTime;
                                                                            string endTime;

                                                                            if (isCurrentEntry)
                                                                            {
                                                                                // For current entry, calculate elapsed time
                                                                                duration = DateTime.UtcNow - entry.TimeInterval.StartDate;
                                                                                status = "[green]⏱️ Running[/]";
                                                                                startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");
                                                                                endTime = "[green]Running[/]";
                                                                            }
                                                                            else
                                                                            {
                                                                                // For completed entries, use actual duration
                                                                                duration = entry.TimeInterval.DurationSpan;
                                                                                status = "[dim]Completed[/]";
                                                                                startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");
                                                                                endTime = entry.TimeInterval.EndDate.ToLocalTime().ToString("HH:mm");
                                                                            }

                                                                            dayTotal += duration;
                                                                            weekTotal += duration;

                                                                            // Only show date on first entry of the day
                                                                            var dateDisplay = i == 0 ? $"[bold]{date.ToString("ddd, MMM dd", CultureInfo.InvariantCulture)}[/]" : "";

                                                                            // Escape markup characters in dynamic content from Clockify API
                                                                            var projectName = project?.Name != null ? Markup.Escape(project.Name) : "[dim]Unknown Project[/]";
                                                                            var taskName = task?.Name != null ? Markup.Escape(task.Name) : "[dim]No Task[/]";
                                                                            var description = string.IsNullOrWhiteSpace(entry.Description) ? "[dim]No description[/]" : Markup.Escape(entry.Description);

                                                                            // Add row with appropriate number of columns based on detailed flag
                                                                            if (detailed)
                                                                            {
                                                                                table.AddRow(
                                                                                             dateDisplay,
                                                                                             projectName,
                                                                                             taskName,
                                                                                             description,
                                                                                             startTime,
                                                                                             endTime,
                                                                                             TimeFormatter.FormatDurationCompact(duration),
                                                                                             status
                                                                                            );
                                                                            }
                                                                            else
                                                                            {
                                                                                table.AddRow(
                                                                                             dateDisplay,
                                                                                             projectName,
                                                                                             taskName,
                                                                                             description,
                                                                                             TimeFormatter.FormatDurationCompact(duration),
                                                                                             status
                                                                                            );
                                                                            }
                                                                        }

                                                                        // Add day total row
                                                                        if (dayEntries.Count > 1)
                                                                        {
                                                                            if (detailed)
                                                                            {
                                                                                table.AddRow(
                                                                                             "",
                                                                                             "",
                                                                                             "",
                                                                                             $"[bold dim]Day Total[/]",
                                                                                             "",
                                                                                             "",
                                                                                             $"[bold]{TimeFormatter.FormatDurationCompact(dayTotal)}[/]",
                                                                                             ""
                                                                                            );
                                                                            }
                                                                            else
                                                                            {
                                                                                table.AddRow(
                                                                                             "",
                                                                                             "",
                                                                                             "",
                                                                                             $"[bold dim]Day Total[/]",
                                                                                             $"[bold]{TimeFormatter.FormatDurationCompact(dayTotal)}[/]",
                                                                                             ""
                                                                                            );
                                                                            }
                                                                        }

                                                                        // Add separator between days (except for last day)
                                                                        if (dateGroup != entriesByDate.Last())
                                                                        {
                                                                            table.AddEmptyRow();
                                                                        }
                                                                    }

                                                                    AnsiConsole.Write(table);
                                                                    AnsiConsole.WriteLine();
                                                                    AnsiConsole.MarkupLine($"[bold green]Week Total: {TimeFormatter.FormatDurationCompact(weekTotal)}[/]");

                                                                    // Show daily averages
                                                                    var workingDaysWithEntries = entriesByDate.Count;
                                                                    if (workingDaysWithEntries > 0)
                                                                    {
                                                                        var averagePerDay = TimeSpan.FromTicks(weekTotal.Ticks / workingDaysWithEntries);
                                                                        AnsiConsole.MarkupLine($"[dim]Average per day ({workingDaysWithEntries} days): {TimeFormatter.FormatDurationCompact(averagePerDay)}[/]");
                                                                    }

                                                                    // Show note about current entry if included
                                                                    if (includeCurrent && currentEntry != null)
                                                                    {
                                                                        AnsiConsole.WriteLine();
                                                                        AnsiConsole.MarkupLine("[dim]Note: Running timer duration is calculated in real-time and will continue to increase.[/]");
                                                                    }
                                                                });
    }
}
