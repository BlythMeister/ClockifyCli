using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StopCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        await StopCurrentTimer(clockifyClient);
        return 0;
    }

    private async Task StopCurrentTimer(Services.ClockifyClient clockifyClient)
    {
        AnsiConsole.MarkupLine("[bold]Stop Current Timer[/]");
        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Load current time entry data first (inside Status block)
        Models.TimeEntry? currentEntry = null;
        Models.ProjectInfo? project = null;
        Models.TaskInfo? task = null;
        TimeSpan elapsed = TimeSpan.Zero;

        await AnsiConsole.Status()
            .StartAsync("Checking current time entry...", async ctx =>
            {
                currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                if (currentEntry == null)
                {
                    return;
                }

                // Get project and task details for display
                ctx.Status("Getting project and task details...");
                var projects = await clockifyClient.GetProjects(workspace);
                project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                task = project != null ?
                    (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) :
                    null;

                // Calculate elapsed time before stopping
                var startTime = currentEntry.TimeInterval.StartDate;
                elapsed = DateTime.UtcNow - startTime;
            });

        // Check if there's a timer running (outside Status block)
        if (currentEntry == null)
        {
            AnsiConsole.MarkupLine("[yellow]⏸️  No time entry is currently running[/]");
            AnsiConsole.MarkupLine("[dim]There's nothing to stop.[/]");
            return;
        }

        // Show what will be stopped (outside Status block)
        var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(currentEntry.Description) ? "No description" : Markup.Escape(currentEntry.Description);

        AnsiConsole.MarkupLine($"[yellow]Currently running:[/]");
        AnsiConsole.MarkupLine($"  [bold]Project:[/] {projectName}");
        AnsiConsole.MarkupLine($"  [bold]Task:[/] {taskName}");
        AnsiConsole.MarkupLine($"  [bold]Description:[/] {description}");
        AnsiConsole.MarkupLine($"  [bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
        AnsiConsole.WriteLine();

        // User confirmation (outside Status block)
        if (AnsiConsole.Confirm("Stop this timer?"))
        {
            // Stop the timer (inside Status block for feedback)
            await AnsiConsole.Status()
                .StartAsync("Stopping timer...", async ctx =>
                {
                    var stoppedEntry = await clockifyClient.StopCurrentTimeEntry(workspace, user);
                });

            AnsiConsole.MarkupLine("[green]✓ Timer stopped successfully![/]");
            AnsiConsole.MarkupLine($"[dim]Final duration: {TimeFormatter.FormatDuration(elapsed)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Timer stop cancelled.[/]");
        }
    }
}
