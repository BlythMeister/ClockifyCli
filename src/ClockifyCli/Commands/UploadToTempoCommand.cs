using ClockifyCli.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace ClockifyCli.Commands;

public class UploadToTempoCommand : BaseCommand<UploadToTempoCommand.Settings>
{
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
        var clockifyClient = await CreateClockifyClientAsync();
        var jiraClient = await CreateJiraClientAsync();
        var tempoClient = await CreateTempoClientAsync(jiraClient);

        await UploadTimeEntriesToTempo(clockifyClient, tempoClient, settings.Days, settings.CleanupOrphaned);
        return 0;
    }

    private async Task UploadTimeEntriesToTempo(Services.ClockifyClient clockifyClient, Services.TempoClient tempoClient, int days, bool cleanupOrphaned)
    {
        AnsiConsole.MarkupLine($"[bold]Uploading time entries from Clockify to Tempo[/]");
        AnsiConsole.MarkupLine($"[dim]Processing last {days} days...[/]");
        if (cleanupOrphaned)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Orphaned entry cleanup is enabled[/]");
        }
        AnsiConsole.WriteLine();

        var today = DateTime.UtcNow.Date;
        DateTime endDate = today.AddDays(days);
        DateTime startDate = today.AddDays(days * -1);
        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        var successCount = 0;
        var errorCount = 0;
        var results = new List<(string EntryId, string Date, bool Success, string? ErrorMessage)>();

        await AnsiConsole.Status()
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
                            AnsiConsole.MarkupLine($"[red]Deleted orphaned entry {exportedTime.TempoWorklogId}[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[green]✓ No orphaned entries found[/]");
                    }
                }

                // Upload new time entries
                var entriesToUpload = timeEntries.Where(e =>
                    !exportedTimes.Any(et =>
                        et.Description.Contains($"[cid:{e.Id}]") &&
                        et.StartDateTimeUtc.Date == e.TimeInterval.StartDate.Date))
                    .OrderBy(x => x.TimeInterval.StartDate)
                    .ToList();

                if (!entriesToUpload.Any())
                {
                    ctx.Status("No new time entries to upload.");
                    AnsiConsole.MarkupLine("[green]✓ All time entries are already up to date in Tempo[/]");
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
                AnsiConsole.MarkupLine($"[green]✓ Uploaded entry {entryId}[/] [dim]({date})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to upload entry {entryId}: {Markup.Escape(errorMessage ?? "Unknown error")}[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Upload Summary:[/]");
        AnsiConsole.MarkupLine($"[green]✓ Successfully uploaded: {successCount} entries[/]");
        if (errorCount > 0)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to upload: {errorCount} entries[/]");
        }
    }
}
