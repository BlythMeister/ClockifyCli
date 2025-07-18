using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClockifyCli.Commands;

public class TimerMonitorCommand : BaseCommand<TimerMonitorCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Silent mode - suppress console output (useful for scheduled tasks)")]
        [CommandOption("-s|--silent")]
        [DefaultValue(false)]
        public bool Silent { get; init; } = false;

        [Description("Show notification even when timer is running (for status updates)")]
        [CommandOption("--always-notify")]
        [DefaultValue(false)]
        public bool AlwaysNotify { get; init; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        return await MonitorTimer(clockifyClient, settings);
    }

    private async Task<int> MonitorTimer(ClockifyClient clockifyClient, Settings settings)
    {
        if (!settings.Silent)
        {
            AnsiConsole.MarkupLine("[bold]Clockify Timer Monitor[/]");
            AnsiConsole.WriteLine();
        }

        // Check if running on Windows for notification support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !settings.Silent)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ Toast notifications are only supported on Windows[/]");
            AnsiConsole.MarkupLine("[dim]Running in console-only mode...[/]");
            AnsiConsole.WriteLine();
        }

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            if (!settings.Silent)
            {
                AnsiConsole.MarkupLine("[red]No workspace found![/]");
            }

            return 1;
        }

        Models.TimeEntry? currentEntry = null;
        Models.ProjectInfo? project = null;
        Models.TaskInfo? task = null;

        if (!settings.Silent)
        {
            await AnsiConsole.Status()
                             .StartAsync("Checking timer status...", async ctx =>
                                                                     {
                                                                         currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                                                                         if (currentEntry != null)
                                                                         {
                                                                             ctx.Status("Getting project and task details...");
                                                                             var projects = await clockifyClient.GetProjects(workspace);
                                                                             project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                                                                             task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : null;
                                                                         }
                                                                     });
        }
        else
        {
            // Silent mode - no status display
            currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

            if (currentEntry != null)
            {
                var projects = await clockifyClient.GetProjects(workspace);
                project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : null;
            }
        }

        if (currentEntry == null)
        {
            // No timer running - show reminder notification
            if (!settings.Silent)
            {
                AnsiConsole.MarkupLine("[yellow]⏸️ No timer is currently running[/]");
                AnsiConsole.MarkupLine("[dim]Showing reminder notification...[/]");
            }

            // Show Windows toast notification
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NotificationService.ShowTimerReminderNotification();
            }

            if (!settings.Silent)
            {
                AnsiConsole.MarkupLine("[green]✓ Notification sent successfully[/]");
                AnsiConsole.MarkupLine("[dim]Start a timer with 'clockify-cli start'[/]");
            }

            return 2; // Special exit code indicating no timer running
        }
        else
        {
            // Timer is running
            var projectName = project?.Name ?? "Unknown Project";
            var taskName = task?.Name ?? "No Task";
            var startTime = currentEntry.TimeInterval.StartDate;
            var elapsed = DateTime.UtcNow - startTime;

            if (!settings.Silent)
            {
                AnsiConsole.MarkupLine("[green]✅ Timer is running[/]");
                AnsiConsole.MarkupLine($"[bold]Project:[/] {Markup.Escape(projectName)}");
                AnsiConsole.MarkupLine($"[bold]Task:[/] {Markup.Escape(taskName)}");
                AnsiConsole.MarkupLine($"[bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
            }

            // Show notification if always-notify is enabled
            if (settings.AlwaysNotify && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NotificationService.ShowTimerRunningNotification(projectName, taskName, elapsed);

                if (!settings.Silent)
                {
                    AnsiConsole.MarkupLine("[green]✓ Status notification sent[/]");
                }
            }

            return 0; // Success - timer is running
        }
    }
}
