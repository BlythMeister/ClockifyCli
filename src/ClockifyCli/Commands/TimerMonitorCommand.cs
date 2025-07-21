using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClockifyCli.Commands;

public class TimerMonitorCommand : BaseCommand<TimerMonitorCommand.Settings>
{
    private readonly ClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public TimerMonitorCommand(ClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

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
        return await MonitorTimer(clockifyClient, console, settings);
    }

    private async Task<int> MonitorTimer(ClockifyClient clockifyClient, IAnsiConsole console, Settings settings)
    {
        if (!settings.Silent)
        {
            console.MarkupLine("[bold]Clockify Timer Monitor[/]");
            console.WriteLine();
        }

        // Check if running on Windows for notification support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !settings.Silent)
        {
            console.MarkupLine("[yellow]⚠️ Toast notifications are only supported on Windows[/]");
            console.MarkupLine("[dim]Running in console-only mode...[/]");
            console.WriteLine();
        }

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            if (!settings.Silent)
            {
                console.MarkupLine("[red]No workspace found![/]");
            }

            return 1;
        }

        Models.TimeEntry? currentEntry = null;
        Models.ProjectInfo? project = null;
        Models.TaskInfo? task = null;

        if (!settings.Silent)
        {
            await console.Status()
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
                console.MarkupLine("[yellow]⏸️ No timer is currently running[/]");
                console.MarkupLine("[dim]Showing reminder notification...[/]");
            }

            // Show Windows toast notification
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NotificationService.ShowTimerReminderNotification();
            }

            if (!settings.Silent)
            {
                console.MarkupLine("[green]✓ Notification sent successfully[/]");
                console.MarkupLine("[dim]Start a timer with 'clockify-cli start'[/]");
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
                console.MarkupLine("[green]✅ Timer is running[/]");
                console.MarkupLine($"[bold]Project:[/] {Markup.Escape(projectName)}");
                console.MarkupLine($"[bold]Task:[/] {Markup.Escape(taskName)}");
                console.MarkupLine($"[bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
            }

            // Show notification if always-notify is enabled
            if (settings.AlwaysNotify && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NotificationService.ShowTimerRunningNotification(projectName, taskName, elapsed);

                if (!settings.Silent)
                {
                    console.MarkupLine("[green]✓ Status notification sent[/]");
                }
            }

            return 0; // Success - timer is running
        }
    }
}
