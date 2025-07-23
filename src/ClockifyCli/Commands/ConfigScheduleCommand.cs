using System.ComponentModel;
using System.Runtime.InteropServices;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ConfigScheduleCommand : AsyncCommand<ConfigScheduleCommand.Settings>
{
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public ConfigScheduleCommand(IAnsiConsole console)
    {
        this.console = console;
    }

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
            console.MarkupLine("[red]Scheduled tasks are only supported on Windows[/]");
            console.MarkupLine("[dim]On macOS/Linux, consider using cron jobs instead:[/]");
            console.MarkupLine("[dim]# Add to crontab (crontab -e)[/]");
            console.MarkupLine("[dim]*/60 * * * * clockify-cli timer-monitor --silent[/]");
            return 1;
        }

        console.MarkupLine("[bold]Clockify CLI Scheduled Task Setup[/]");
        console.WriteLine();

        // Handle removal request
        if (settings.Remove)
        {
            return await HandleTaskRemoval(taskName);
        }

        // Check if tool is installed as global tool
        if (!ScheduledTaskService.IsToolInstalledAsGlobalTool())
        {
            console.MarkupLine("[red]:cross_mark: ClockifyCli is not installed as a global .NET tool[/]");
            console.WriteLine();
            console.MarkupLine("[yellow]To install as a global tool, run:[/]");
            console.MarkupLine("[green]dotnet tool install --global ClockifyCli[/]");
            console.WriteLine();
            console.MarkupLine("[dim]After installation, run this command again to set up the scheduled task.[/]");
            return 1;
        }

        console.MarkupLine("[green]:check_mark: ClockifyCli is installed as a global tool[/]");

        // Check if task already exists
        if (ScheduledTaskService.TaskExists(taskName))
        {
            console.MarkupLine($"[yellow]:warning: Scheduled task '{taskName}' already exists[/]");

            if (!console.Confirm("Do you want to replace the existing task?"))
            {
                console.MarkupLine("[yellow]Setup cancelled[/]");
                return 0;
            }

            // Delete existing task
            console.MarkupLine("[yellow]Removing existing task...[/]");
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
        console.WriteLine();
        console.MarkupLine($"[bold]Creating scheduled task...[/]");
        console.MarkupLine($"[dim]Task Name: {taskName}[/]");
        console.MarkupLine($"[dim]Interval: {ScheduledTaskService.GetIntervalDescription(intervalMinutes.ToString())}[/]");
        console.MarkupLine($"[dim]Command: clockify-cli timer-monitor --silent[/]");
        console.WriteLine();

        var success = await ScheduledTaskService.CreateScheduledTask(taskName, intervalMinutes);

        if (success)
        {
            console.MarkupLine("[green]:check_mark: Scheduled task created successfully![/]");
            console.WriteLine();
            console.MarkupLine("[bold]Task Details:[/]");
            console.MarkupLine($"• [green]Name:[/] {taskName}");
            console.MarkupLine($"• [green]Frequency:[/] {ScheduledTaskService.GetIntervalDescription(intervalMinutes.ToString())}");
            console.MarkupLine($"• [green]Action:[/] Check timer status and show notification if no timer is running");
            console.WriteLine();
            console.MarkupLine("[dim]You can view/modify this task in Windows Task Scheduler[/]");
            console.MarkupLine("[dim]To remove the task, run: clockify-cli config schedule-monitor --remove[/]");
        }
        else
        {
            console.MarkupLine("[red]:cross_mark: Failed to create scheduled task[/]");
            console.MarkupLine("[yellow]This may require administrator privileges[/]");
            console.MarkupLine("[dim]Try running the command as administrator[/]");
            return 1;
        }

        return 0;
    }

    private async Task<int> HandleTaskRemoval(string taskName)
    {
        if (!ScheduledTaskService.TaskExists(taskName))
        {
            console.MarkupLine($"[yellow]No scheduled task named '{taskName}' found[/]");
            return 0;
        }

        console.MarkupLine($"[yellow]Found scheduled task: '{taskName}'[/]");

        if (!console.Confirm("Are you sure you want to remove this scheduled task?"))
        {
            console.MarkupLine("[yellow]Removal cancelled[/]");
            return 0;
        }

        console.MarkupLine("[yellow]Removing scheduled task...[/]");
        var success = await ScheduledTaskService.DeleteScheduledTask(taskName);

        if (success)
        {
            console.MarkupLine("[green]:check_mark: Scheduled task removed successfully[/]");
        }
        else
        {
            console.MarkupLine("[red]:cross_mark: Failed to remove scheduled task[/]");
            console.MarkupLine("[yellow]This may require administrator privileges[/]");
            return 1;
        }

        return 0;
    }

    private int GetIntervalFromUser()
    {
        console.MarkupLine("[bold]Select monitoring interval:[/]");

        var choices = ScheduledTaskService.GetValidIntervals()
                                          .Select(interval => $"{ScheduledTaskService.GetIntervalDescription(interval)} ({interval} minutes)")
                                          .ToList();

        var selection = console.Prompt(
                                           new SelectionPrompt<string>()
                                               .Title("Choose how often to check for running timers:")
                                               .AddChoices(choices)
                                          );

        // Extract the interval value from the selection
        var intervalStr = selection.Split('(')[1].Split(' ')[0];
        return int.Parse(intervalStr);
    }
}
