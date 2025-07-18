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

    private async Task StopCurrentTimer(ClockifyCli.Services.ClockifyClient clockifyClient)
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

        await AnsiConsole.Status()
            .StartAsync("Checking current time entry...", async ctx =>
            {
                var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                if (currentEntry == null)
                {
                    AnsiConsole.MarkupLine("[yellow]??  No time entry is currently running[/]");
                    AnsiConsole.MarkupLine("[dim]There's nothing to stop.[/]");
                    return;
                }

                // Get project and task details for display
                ctx.Status("Getting project and task details...");
                var projects = await clockifyClient.GetProjects(workspace);
                var project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                var task = project != null ? 
                    (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : 
                    null;

                // Calculate elapsed time before stopping
                var startTime = currentEntry.TimeInterval.StartDate;
                var elapsed = DateTime.UtcNow - startTime;

                // Show what will be stopped
                var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
                var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
                var description = string.IsNullOrWhiteSpace(currentEntry.Description) ? "No description" : Markup.Escape(currentEntry.Description);

                AnsiConsole.MarkupLine($"[yellow]Currently running:[/]");
                AnsiConsole.MarkupLine($"  [bold]Project:[/] {projectName}");
                AnsiConsole.MarkupLine($"  [bold]Task:[/] {taskName}");
                AnsiConsole.MarkupLine($"  [bold]Description:[/] {description}");
                AnsiConsole.MarkupLine($"  [bold]Elapsed:[/] {FormatDuration(elapsed)}");
                AnsiConsole.WriteLine();

                if (AnsiConsole.Confirm("Stop this timer?"))
                {
                    ctx.Status("Stopping timer...");
                    var stoppedEntry = await clockifyClient.StopCurrentTimeEntry(workspace, user);
                    
                    // Calculate final duration
                    var finalDuration = stoppedEntry.TimeInterval.DurationSpan;
                    
                    AnsiConsole.MarkupLine("[green]? Timer stopped successfully![/]");
                    AnsiConsole.MarkupLine($"[dim]Final duration: {FormatDuration(finalDuration)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Timer stop cancelled.[/]");
                }
            });
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;
        
        if (totalHours > 0)
        {
            return $"{totalHours}h {minutes}m {seconds}s";
        }
        else if (minutes > 0)
        {
            return $"{minutes}m {seconds}s";
        }
        else
        {
            return $"{seconds}s";
        }
    }
}