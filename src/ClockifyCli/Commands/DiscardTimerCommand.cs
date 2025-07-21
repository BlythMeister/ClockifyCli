using ClockifyCli.Models;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class DiscardTimerCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        await DiscardCurrentTimer(clockifyClient);
        return 0;
    }

    private async Task DiscardCurrentTimer(Services.ClockifyClient clockifyClient)
    {
        AnsiConsole.MarkupLine("[bold]Discard Current Timer[/]");
        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        TimeEntry? currentEntry = null;
        ProjectInfo? project = null;
        TaskInfo? task = null;
        TimeSpan elapsed = TimeSpan.Zero;

        await AnsiConsole.Status()
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
            AnsiConsole.MarkupLine("[yellow]?? No time entry is currently running[/]");
            AnsiConsole.MarkupLine("[dim]There's nothing to discard.[/]");
            return;
        }

        // Show what will be discarded
        var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(currentEntry.Description) ? "No description" : Markup.Escape(currentEntry.Description);

        AnsiConsole.MarkupLine($"[yellow]Currently running timer:[/]");
        AnsiConsole.MarkupLine($"  [bold]Project:[/] {projectName}");
        AnsiConsole.MarkupLine($"  [bold]Task:[/] {taskName}");
        AnsiConsole.MarkupLine($"  [bold]Description:[/] {description}");
        AnsiConsole.MarkupLine($"  [bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[red]?? WARNING: This will permanently delete the running timer![/]");
        AnsiConsole.MarkupLine("[dim]All elapsed time will be lost and cannot be recovered.[/]");
        AnsiConsole.WriteLine();

        // User confirmation
        if (AnsiConsole.Confirm($"Are you sure you want to discard this timer with {TimeFormatter.FormatDuration(elapsed)} of elapsed time?"))
        {
            // Delete the timer
            await AnsiConsole.Status()
                .StartAsync("Discarding timer...", async ctx =>
                {
                    await clockifyClient.DeleteTimeEntry(workspace, currentEntry);
                });

            AnsiConsole.MarkupLine("[green]? Timer discarded successfully![/]");
            AnsiConsole.MarkupLine($"[dim]Discarded {TimeFormatter.FormatDuration(elapsed)} of elapsed time[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Discard cancelled.[/]");
            AnsiConsole.MarkupLine("[dim]Timer is still running.[/]");
        }
    }
}
