using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClockifyCli.Commands;

public class ConfigScheduleCommand : AsyncCommand<ConfigScheduleCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Interval in minutes for the scheduled task (15, 30, 60, 120, 240)")]
        [CommandOption("-i|--interval")]
        public int? Interval { get; init; }

        [Description("Remove/delete the scheduled task instead of creating it")]
        [CommandOption("-r|--remove")]
        [DefaultValue(false)]
        public bool Remove { get; init; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        const string taskName = "ClockifyCli Timer Monitor";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine("[red]Scheduled tasks are only supported on Windows[/]");
            AnsiConsole.MarkupLine("[dim]On macOS/Linux, consider using cron jobs instead:[/]");
            AnsiConsole.MarkupLine("[dim]# Add to crontab (crontab -e)[/]");
            AnsiConsole.MarkupLine("[dim]*/60 * * * * clockify-cli timer-monitor --silent[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[bold]Clockify CLI Scheduled Task Setup[/]");
        AnsiConsole.WriteLine();

        // Handle removal request
        if (settings.Remove)
        {
            return await HandleTaskRemoval(taskName);
        }

        // Check if tool is installed as global tool
        if (!ScheduledTaskService.IsToolInstalledAsGlobalTool())
        {
            AnsiConsole.MarkupLine("[red]✗ ClockifyCli is not installed as a global .NET tool[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]To install as a global tool, run:[/]");
            AnsiConsole.MarkupLine("[green]dotnet tool install --global ClockifyCli[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]After installation, run this command again to set up the scheduled task.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]✓ ClockifyCli is installed as a global tool[/]");

        // Check if task already exists
        if (ScheduledTaskService.TaskExists(taskName))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Scheduled task '{taskName}' already exists[/]");

            if (!AnsiConsole.Confirm("Do you want to replace the existing task?"))
            {
                AnsiConsole.MarkupLine("[yellow]Setup cancelled[/]");
                return 0;
            }

            // Delete existing task
            AnsiConsole.MarkupLine("[yellow]Removing existing task...[/]");
            await ScheduledTaskService.DeleteScheduledTask(taskName);
        }

        // Get interval from user if not provided
        int intervalMinutes;
        if (settings.Interval.HasValue && ScheduledTaskService.GetValidIntervals().Contains(settings.Interval.Value.ToString()))
        {
            intervalMinutes = settings.Interval.Value;
        }
        else
        {
            intervalMinutes = GetIntervalFromUser();
        }

        // Create the scheduled task
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Creating scheduled task...[/]");
        AnsiConsole.MarkupLine($"[dim]Task Name: {taskName}[/]");
        AnsiConsole.MarkupLine($"[dim]Interval: {ScheduledTaskService.GetIntervalDescription(intervalMinutes.ToString())}[/]");
        AnsiConsole.MarkupLine($"[dim]Command: clockify-cli timer-monitor --silent[/]");
        AnsiConsole.WriteLine();

        var success = await ScheduledTaskService.CreateScheduledTask(taskName, intervalMinutes);

        if (success)
        {
            AnsiConsole.MarkupLine("[green]✓ Scheduled task created successfully![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Task Details:[/]");
            AnsiConsole.MarkupLine($"• [green]Name:[/] {taskName}");
            AnsiConsole.MarkupLine($"• [green]Frequency:[/] {ScheduledTaskService.GetIntervalDescription(intervalMinutes.ToString())}");
            AnsiConsole.MarkupLine($"• [green]Action:[/] Check timer status and show notification if no timer is running");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]You can view/modify this task in Windows Task Scheduler[/]");
            AnsiConsole.MarkupLine("[dim]To remove the task, run: clockify-cli config schedule-monitor --remove[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ Failed to create scheduled task[/]");
            AnsiConsole.MarkupLine("[yellow]This may require administrator privileges[/]");
            AnsiConsole.MarkupLine("[dim]Try running the command as administrator[/]");
            return 1;
        }

        return 0;
    }

    private async Task<int> HandleTaskRemoval(string taskName)
    {
        if (!ScheduledTaskService.TaskExists(taskName))
        {
            AnsiConsole.MarkupLine($"[yellow]No scheduled task named '{taskName}' found[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[yellow]Found scheduled task: '{taskName}'[/]");

        if (!AnsiConsole.Confirm("Are you sure you want to remove this scheduled task?"))
        {
            AnsiConsole.MarkupLine("[yellow]Removal cancelled[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[yellow]Removing scheduled task...[/]");
        var success = await ScheduledTaskService.DeleteScheduledTask(taskName);

        if (success)
        {
            AnsiConsole.MarkupLine("[green]✓ Scheduled task removed successfully[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ Failed to remove scheduled task[/]");
            AnsiConsole.MarkupLine("[yellow]This may require administrator privileges[/]");
            return 1;
        }

        return 0;
    }

    private int GetIntervalFromUser()
    {
        AnsiConsole.MarkupLine("[bold]Select monitoring interval:[/]");

        var choices = ScheduledTaskService.GetValidIntervals()
            .Select(interval => $"{ScheduledTaskService.GetIntervalDescription(interval)} ({interval} minutes)")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose how often to check for running timers:")
                .AddChoices(choices)
        );

        // Extract the interval value from the selection
        var intervalStr = selection.Split('(')[1].Split(' ')[0];
        return int.Parse(intervalStr);
    }
}