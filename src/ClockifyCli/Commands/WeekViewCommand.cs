using System.Globalization;
using ClockifyCli.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class WeekViewCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();
        
        await ShowCurrentWeekTimeEntries(clockifyClient);
        return 0;
    }

    private async Task ShowCurrentWeekTimeEntries(ClockifyCli.Services.ClockifyClient clockifyClient)
    {
        AnsiConsole.MarkupLine("[bold]Current Week Time Entries[/]");
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

                if (!entriesByDate.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No time entries found for the current week.[/]");
                    return;
                }

                // Create table
                var table = new Table();
                table.AddColumn("Date");
                table.AddColumn("Project");
                table.AddColumn("Task");
                table.AddColumn("Description");
                table.AddColumn("Duration", c => c.RightAligned());

                var weekTotal = TimeSpan.Zero;

                foreach (var dateGroup in entriesByDate)
                {
                    var date = dateGroup.Key;
                    var dayEntries = dateGroup.OrderBy(e => e.TimeInterval.StartDate).ToList();
                    var dayTotal = TimeSpan.Zero;

                    for (int i = 0; i < dayEntries.Count; i++)
                    {
                        var entry = dayEntries[i];
                        var project = projects.FirstOrDefault(p => p.Id == entry.ProjectId);
                        var task = allTasks.FirstOrDefault(t => t.Id == entry.TaskId);
                        var duration = entry.TimeInterval.DurationSpan;

                        dayTotal += duration;
                        weekTotal += duration;

                        // Only show date on first entry of the day
                        var dateDisplay = i == 0 ? $"[bold]{date.ToString("ddd, MMM dd", CultureInfo.InvariantCulture)}[/]" : "";
                        
                        // Escape markup characters in dynamic content from Clockify API
                        var projectName = project?.Name != null ? Markup.Escape(project.Name) : "[dim]Unknown Project[/]";
                        var taskName = task?.Name != null ? Markup.Escape(task.Name) : "[dim]No Task[/]";
                        var description = string.IsNullOrWhiteSpace(entry.Description) ? "[dim]No description[/]" : Markup.Escape(entry.Description);

                        table.AddRow(
                            dateDisplay,
                            projectName,
                            taskName,
                            description,
                            FormatDuration(duration)
                        );
                    }

                    // Add day total row
                    if (dayEntries.Count > 1)
                    {
                        table.AddRow(
                            "",
                            "",
                            "",
                            $"[bold dim]Day Total[/]",
                            $"[bold]{FormatDuration(dayTotal)}[/]"
                        );
                    }

                    // Add separator between days (except for last day)
                    if (dateGroup != entriesByDate.Last())
                    {
                        table.AddEmptyRow();
                    }
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold green]Week Total: {FormatDuration(weekTotal)}[/]");
                
                // Show daily averages
                var workingDaysWithEntries = entriesByDate.Count;
                if (workingDaysWithEntries > 0)
                {
                    var averagePerDay = TimeSpan.FromTicks(weekTotal.Ticks / workingDaysWithEntries);
                    AnsiConsole.MarkupLine($"[dim]Average per day ({workingDaysWithEntries} days): {FormatDuration(averagePerDay)}[/]");
                }
            });
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        
        if (totalHours == 0)
            return $"{minutes}m";
        
        return minutes == 0 ? $"{totalHours}h" : $"{totalHours}h {minutes}m";
    }
}