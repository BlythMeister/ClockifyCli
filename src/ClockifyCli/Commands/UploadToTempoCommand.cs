using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace ClockifyCli.Commands;

public class UploadToTempoCommand : BaseCommand<UploadToTempoCommand.Settings>
{
    private readonly ClockifyClient clockifyClient;
    private readonly TempoClient tempoClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public UploadToTempoCommand(ClockifyClient clockifyClient, TempoClient tempoClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.tempoClient = tempoClient;
        this.console = console;
    }

    public class Settings : CommandSettings
    {
        [Description("Number of days to upload time entries for")]
        [CommandOption("-d|--days")]
        [DefaultValue(14)]
        public int Days { get; init; } = 14;

        [Description("Clean up orphaned entries without Clockify IDs (use with caution)")]
        [CommandOption("--cleanup-orphaned")]
        [DefaultValue(false)]
        public bool CleanupOrphaned { get; init; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await UploadTimeEntriesToTempo(clockifyClient, tempoClient, console, settings.Days, settings.CleanupOrphaned);
        return 0;
    }

    private async Task UploadTimeEntriesToTempo(ClockifyClient clockifyClient, TempoClient tempoClient, IAnsiConsole console, int days, bool cleanupOrphaned)
    {
        console.MarkupLine($"[bold]Uploading time entries from Clockify to Tempo[/]");
        console.MarkupLine($"[dim]Processing last {days} days...[/]");
        if (cleanupOrphaned)
        {
            console.MarkupLine("[yellow]⚠ Orphaned entry cleanup is enabled[/]");
        }

        console.WriteLine();

        var today = DateTime.UtcNow.Date;
        var endDate = today.AddDays(days);
        var startDate = today.AddDays(days * -1);
        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Check for running timer and warn user
        await console.Status()
                         .StartAsync("Checking for running timer...", async ctx =>
                                                                      {
                                                                          var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                                                                          if (currentEntry != null)
                                                                          {
                                                                              // Get project and task details for display
                                                                              ctx.Status("Getting timer details...");
                                                                              var projects = await clockifyClient.GetProjects(workspace);
                                                                              var project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                                                                              var task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : null;

                                                                              // Calculate elapsed time
                                                                              var startTime = currentEntry.TimeInterval.StartDate;
                                                                              var elapsed = DateTime.UtcNow - startTime;

                                                                              // Show running timer details outside Status block
                                                                              ctx.Status("Timer running - showing details...");
                                                                          }
                                                                      });

        // Handle running timer warning outside Status block
        var runningEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
        if (runningEntry != null)
        {
            var projects = await clockifyClient.GetProjects(workspace);
            var project = projects.FirstOrDefault(p => p.Id == runningEntry.ProjectId);
            var task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == runningEntry.TaskId) : null;

            var startTime = runningEntry.TimeInterval.StartDate;
            var elapsed = DateTime.UtcNow - startTime;

            console.MarkupLine("[yellow]⚠️  Warning: A timer is currently running![/]");
            console.WriteLine();

            var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
            var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
            var description = string.IsNullOrWhiteSpace(runningEntry.Description) ? "No description" : Markup.Escape(runningEntry.Description);

            console.MarkupLine($"[bold]Currently running timer:[/]");
            console.MarkupLine($"  [bold]Project:[/] {projectName}");
            console.MarkupLine($"  [bold]Task:[/] {taskName}");
            console.MarkupLine($"  [bold]Description:[/] {description}");
            console.MarkupLine($"  [bold]Elapsed:[/] {Utilities.TimeFormatter.FormatDuration(elapsed)}");
            console.WriteLine();

            console.MarkupLine("[yellow]Uploading while a timer is running may cause incomplete time entries to be uploaded.[/]");
            console.MarkupLine("[dim]Consider stopping the timer first with 'clockify-cli stop' for accurate time tracking.[/]");
            console.WriteLine();

            if (!console.Confirm("Do you want to proceed with the upload anyway?"))
            {
                console.MarkupLine("[yellow]Upload cancelled.[/]");
                return;
            }

            console.WriteLine();
        }

        var successCount = 0;
        var errorCount = 0;
        var results = new List<(string EntryId, string Date, bool Success, string? ErrorMessage)>();

        await console.Status()
                         .StartAsync("Loading data from Clockify and Tempo...", async ctx =>
                                                                                {
                                                                                    ctx.Status("Getting time entries from Clockify...");
                                                                                    var timeEntries = await clockifyClient.GetTimeEntries(workspace, user, startDate, endDate);

                                                                                    ctx.Status("Getting projects and tasks from Clockify...");
                                                                                    var projects = await clockifyClient.GetProjects(workspace);
                                                                                    var tasks = new List<TaskInfo>();

                                                                                    foreach (var project in projects)
                                                                                    {
                                                                                        var projectTasks = await clockifyClient.GetTasks(workspace, project);
                                                                                        tasks.AddRange(projectTasks);
                                                                                    }

                                                                                    ctx.Status("Getting existing time entries from Tempo...");
                                                                                    var exportedTimes = await tempoClient.GetCurrentTime(startDate, endDate);

                                                                                    // Clean up orphaned entries in Tempo (entries without [cid:] identifier) only if explicitly requested
                                                                                    if (cleanupOrphaned)
                                                                                    {
                                                                                        var orphanedEntries = exportedTimes.Where(et =>
                                                                                                                                      !et.Description.Contains("[cid:") &&
                                                                                                                                      et.StartDateTimeUtc >= startDate).ToList(); // Use the same date range as the query

                                                                                        if (orphanedEntries.Any())
                                                                                        {
                                                                                            ctx.Status($"Cleaning up {orphanedEntries.Count} orphaned entries in Tempo...");
                                                                                            foreach (var exportedTime in orphanedEntries)
                                                                                            {
                                                                                                await tempoClient.Delete(exportedTime);
                                                                                                console.MarkupLine($"[red]Deleted orphaned entry {exportedTime.TempoWorklogId}[/]");
                                                                                            }
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            console.MarkupLine("[green]✓ No orphaned entries found[/]");
                                                                                        }
                                                                                    }

                                                                                    // Upload new time entries (exclude running timers)
                                                                                    var entriesToUpload = timeEntries.Where(e =>
                                                                                                                                !string.IsNullOrEmpty(e.TimeInterval.End) && // Exclude running timers
                                                                                                                                !exportedTimes.Any(et =>
                                                                                                                                                       et.Description.Contains($"[cid:{e.Id}]") &&
                                                                                                                                                       et.StartDateTimeUtc.Date == e.TimeInterval.StartDate.Date))
                                                                                                                     .OrderBy(x => x.TimeInterval.StartDate)
                                                                                                                     .ToList();

                                                                                    if (!entriesToUpload.Any())
                                                                                    {
                                                                                        ctx.Status("No new time entries to upload.");
                                                                                        console.MarkupLine("[green]✓ All time entries are already up to date in Tempo[/]");
                                                                                        return;
                                                                                    }

                                                                                    ctx.Status($"Uploading {entriesToUpload.Count} time entries to Tempo...");

                                                                                    foreach (var timeEntry in entriesToUpload)
                                                                                    {
                                                                                        try
                                                                                        {
                                                                                            var task = tasks.FirstOrDefault(x => x.Id == timeEntry.TaskId);
                                                                                            if (task == null)
                                                                                            {
                                                                                                errorCount++;
                                                                                                results.Add((timeEntry.Id, timeEntry.TimeInterval.StartDate.ToString("yyyy-MM-dd"), false, "Unknown TaskId"));
                                                                                                continue;
                                                                                            }

                                                                                            await tempoClient.ExportTimeEntry(timeEntry, task);
                                                                                            successCount++;
                                                                                            results.Add((timeEntry.Id, timeEntry.TimeInterval.StartDate.ToString("yyyy-MM-dd"), true, null));
                                                                                        }
                                                                                        catch (Exception e)
                                                                                        {
                                                                                            errorCount++;
                                                                                            results.Add((timeEntry.Id, timeEntry.TimeInterval.StartDate.ToString("yyyy-MM-dd"), false, e.Message));
                                                                                        }
                                                                                    }

                                                                                    ctx.Status("Upload completed.");
                                                                                });

        // Display results after Status block
        foreach (var (entryId, date, success, errorMessage) in results)
        {
            if (success)
            {
                console.MarkupLine($"[green]✓ Uploaded entry {entryId}[/] [dim]({date})[/]");
            }
            else
            {
                console.MarkupLine($"[red]✗ Failed to upload entry {entryId}: {Markup.Escape(errorMessage ?? "Unknown error")}[/]");
            }
        }

        console.WriteLine();
        console.MarkupLine($"[bold]Upload Summary:[/]");
        console.MarkupLine($"[green]✓ Successfully uploaded: {successCount} entries[/]");
        if (errorCount > 0)
        {
            console.MarkupLine($"[red]✗ Failed to upload: {errorCount} entries[/]");
        }
    }
}
