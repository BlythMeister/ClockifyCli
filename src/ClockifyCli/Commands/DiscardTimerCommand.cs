using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class DiscardTimerCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public DiscardTimerCommand(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await DiscardCurrentTimer(clockifyClient, console);
        return 0;
    }

    private async Task DiscardCurrentTimer(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        console.MarkupLine("[bold]Discard Current Timer[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        TimeEntry? currentEntry = null;
        ProjectInfo? project = null;
        TaskInfo? task = null;
        TimeSpan elapsed = TimeSpan.Zero;

        await console.Status()
            .StartAsync("Checking for running timer...", async ctx =>
            {
                currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                if (currentEntry != null)
                {
                    ctx.Status("Getting timer details...");
                    var projects = await clockifyClient.GetProjects(workspace);
                    project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                    task = project != null ? 
                        (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : 
                        null;
                    
                    var startTime = currentEntry.TimeInterval.StartDate;
                    elapsed = DateTime.UtcNow - startTime;
                }
            });

        // Check if there's a timer running
        if (currentEntry == null)
        {
            console.MarkupLine("[yellow]?? No time entry is currently running[/]");
            console.MarkupLine("[dim]There's nothing to discard.[/]");
            return;
        }

        // Show what will be discarded
        var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(currentEntry.Description) ? "No description" : Markup.Escape(currentEntry.Description);

        console.MarkupLine($"[yellow]Currently running timer:[/]");
        console.MarkupLine($"  [bold]Project:[/] {projectName}");
        console.MarkupLine($"  [bold]Task:[/] {taskName}");
        console.MarkupLine($"  [bold]Description:[/] {description}");
        console.MarkupLine($"  [bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
        console.WriteLine();

        console.MarkupLine("[red]?? WARNING: This will permanently delete the running timer![/]");
        console.MarkupLine("[dim]All elapsed time will be lost and cannot be recovered.[/]");
        console.WriteLine();

        // User confirmation
        if (console.Confirm($"Are you sure you want to discard this timer with {TimeFormatter.FormatDuration(elapsed)} of elapsed time?"))
        {
            // Delete the timer
            await console.Status()
                .StartAsync("Discarding timer...", async ctx =>
                {
                    await clockifyClient.DeleteTimeEntry(workspace, currentEntry);
                });

            console.MarkupLine("[green]? Timer discarded successfully![/]");
            console.MarkupLine($"[dim]Discarded {TimeFormatter.FormatDuration(elapsed)} of elapsed time[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Discard cancelled.[/]");
            console.MarkupLine("[dim]Timer is still running.[/]");
        }
    }
}
