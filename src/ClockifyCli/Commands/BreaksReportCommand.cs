using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace ClockifyCli.Commands;

public class BreaksReportCommand : BaseCommand<BreaksReportCommand.Settings>
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;
    private readonly IClock clock;

    // Constructor for dependency injection (now required)
    public BreaksReportCommand(IClockifyClient clockifyClient, IAnsiConsole console, IClock clock)
    {
        this.clockifyClient = clockifyClient ?? throw new ArgumentNullException(nameof(clockifyClient));
        this.console = console ?? throw new ArgumentNullException(nameof(console));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public class Settings : CommandSettings
    {
        [Description("Number of days to look back for breaks")]
        [CommandOption("-d|--days")]
        [DefaultValue(14)]
        public int Days { get; init; } = 14;

        [Description("Include the currently running break entry in the view")]
        [CommandOption("--include-current")]
        [DefaultValue(false)]
        public bool IncludeCurrent { get; init; } = false;

        [Description("Show detailed view with start/end times")]
        [CommandOption("--detailed")]
        [DefaultValue(false)]
        public bool Detailed { get; init; } = false;

        [Description("First day of the week (Sunday, Monday, etc.)")]
        [CommandOption("--week-start")]
        [DefaultValue(DayOfWeek.Monday)]
        public DayOfWeek WeekStartDay { get; init; } = DayOfWeek.Monday;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await ShowBreaksReport(clockifyClient, console, settings.Days, settings.IncludeCurrent, settings.Detailed, settings.WeekStartDay);
        return 0;
    }

    private async Task ShowBreaksReport(IClockifyClient clockifyClient, IAnsiConsole console, int days, bool includeCurrent, bool detailed, DayOfWeek weekStartDay)
    {
        console.MarkupLine("[bold]☕ Breaks Report[/]");
        console.MarkupLine($"[dim]Showing breaks from the last {days} days...[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        var endDate = clock.Today.AddDays(1);
        var startDate = clock.Today.AddDays(-days);

        List<TimeEntry> breakEntries = [];
        List<ProjectInfo> projects = [];
        List<TaskInfo> allTasks = [];
        TimeEntry? currentBreakEntry = null;

        await console.Status()
                     .StartAsync("Loading breaks data from Clockify...", async ctx =>
                     {
                         ctx.Status("Getting time entries from Clockify...");
                         var timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startDate, endDate);

                         ctx.Status("Getting projects and tasks from Clockify...");
                         projects = await clockifyClient.GetProjects(workspace);
                         
                         foreach (var project in projects)
                         {
                             var projectTasks = await clockifyClient.GetTasks(workspace, project);
                             allTasks.AddRange(projectTasks);
                         }

                         // Get current running entry if requested
                         if (includeCurrent)
                         {
                             ctx.Status("Getting current running time entry...");
                             currentBreakEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
                         }

                         ctx.Status("Filtering break entries...");

                         // Find "Breaks" project
                         var breaksProject = projects.FirstOrDefault(p => 
                             string.Equals(p.Name, "Breaks", StringComparison.OrdinalIgnoreCase));

                         // Filter entries to include only:
                         // 1. Entries from "Breaks" project, OR
                         // 2. Entries with Type = "BREAK" (case-insensitive)
                         breakEntries = timeEntries.Where(entry =>
                         {
                             // Include if from "Breaks" project
                             if (breaksProject != null && entry.ProjectId == breaksProject.Id)
                                 return true;

                             // Include if Type is "BREAK" (case-insensitive)
                             if (string.Equals(entry.Type, "BREAK", StringComparison.OrdinalIgnoreCase))
                                 return true;

                             return false;
                         }).ToList();

                         // Also check current running entry
                         if (currentBreakEntry != null)
                         {
                             var isCurrentBreak = false;
                             
                             // Check if current entry is from "Breaks" project
                             if (breaksProject != null && currentBreakEntry.ProjectId == breaksProject.Id)
                                 isCurrentBreak = true;

                             // Check if current entry Type is "BREAK"
                             if (string.Equals(currentBreakEntry.Type, "BREAK", StringComparison.OrdinalIgnoreCase))
                                 isCurrentBreak = true;

                             if (!isCurrentBreak)
                                 currentBreakEntry = null; // Not a break entry
                         }
                     });

        // Combine completed break entries with current running break (if any)
        var allBreakEntries = new List<TimeEntry>(breakEntries);
        if (currentBreakEntry != null)
        {
            allBreakEntries.Add(currentBreakEntry);
        }

        if (!allBreakEntries.Any())
        {
            console.MarkupLine("[yellow]No break entries found in the specified date range.[/]");
            console.MarkupLine("[dim]Break entries are identified by:[/]");
            console.MarkupLine("[dim]  • Entries in a project named 'Breaks'[/]");
            console.MarkupLine("[dim]  • Entries with Type = 'BREAK'[/]");
            return;
        }

        // Group entries by date
        var entriesByDate = allBreakEntries
            .GroupBy(e => e.TimeInterval.StartDate.Date)
            .OrderByDescending(g => g.Key)
            .ToList();

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

        var totalBreakTime = TimeSpan.Zero;
        string? lastDateDisplay = null;

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

                var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
                var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
                var description = string.IsNullOrWhiteSpace(entry.Description) ? "[dim]No description[/]" : Markup.Escape(entry.Description);

                // Check if this is the running break entry
                var isRunning = currentBreakEntry != null && entry.Id == currentBreakEntry.Id;
                
                TimeSpan duration;
                var status = "";
                var endTime = "";

                if (isRunning)
                {
                    duration = clock.UtcNow - entry.TimeInterval.StartDate;
                    status = "[green]RUNNING[/]";
                    endTime = "[green]NOW[/]";
                }
                else
                {
                    duration = entry.TimeInterval.DurationSpan;
                    status = "Completed";
                    endTime = entry.TimeInterval.EndDate.ToLocalTime().ToString("HH:mm");
                }

                dayTotal += duration;

                var startTime = entry.TimeInterval.StartDate.ToLocalTime().ToString("HH:mm");

                // Only show date for first entry of each day or if it's different from previous
                var dateDisplay = lastDateDisplay != date.ToString("ddd, MMM dd") ? date.ToString("ddd, MMM dd") : "";
                lastDateDisplay = date.ToString("ddd, MMM dd");

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

            // Add day total row if there are multiple entries for the day
            if (dayEntries.Count > 1)
            {
                if (detailed)
                {
                    table.AddRow(
                        "",
                        "",
                        "",
                        "[bold]Day Total[/]",
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
                        "[bold]Day Total[/]",
                        $"[bold]{TimeFormatter.FormatDurationCompact(dayTotal)}[/]",
                        ""
                    );
                }
            }

            totalBreakTime += dayTotal;

            // Add spacing between days if not the last day
            if (dateGroup != entriesByDate.Last())
            {
                // Add empty row for visual separation
                var emptyRow = detailed ? 
                    new string[] { "", "", "", "", "", "", "", "" } :
                    new string[] { "", "", "", "", "", "" };
                table.AddRow(emptyRow);
            }
        }

        console.Write(table);
        console.WriteLine();

        // Summary
        console.MarkupLine("[bold]Break Summary:[/]");
        console.MarkupLine($"[green]Total break time:[/] {TimeFormatter.FormatDurationCompact(totalBreakTime)}");
        console.MarkupLine($"[dim]Period:[/] {startDate:MMM dd} - {clock.Today:MMM dd, yyyy}");
        console.MarkupLine($"[dim]Total entries:[/] {allBreakEntries.Count}");
        
        if (currentBreakEntry != null)
        {
            console.MarkupLine("[yellow]⚠ A break is currently running[/]");
        }
    }
}
