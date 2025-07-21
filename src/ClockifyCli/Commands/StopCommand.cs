using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StopCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public StopCommand(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await StopCurrentTimer(clockifyClient, console);
        return 0;
    }

    private async Task StopCurrentTimer(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        console.MarkupLine("[bold]Stop Current Timer[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Load current time entry data first (inside Status block)
        Models.TimeEntry? currentEntry = null;
        Models.ProjectInfo? project = null;
        Models.TaskInfo? task = null;
        var elapsed = TimeSpan.Zero;

        await console.Status()
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
                                                                       task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : null;

                                                                       // Calculate elapsed time before stopping
                                                                       var startTime = currentEntry.TimeInterval.StartDate;
                                                                       elapsed = DateTime.UtcNow - startTime;
                                                                   });

        // Check if there's a timer running (outside Status block)
        if (currentEntry == null)
        {
            console.MarkupLine("[yellow]⏸️  No time entry is currently running[/]");
            console.MarkupLine("[dim]There's nothing to stop.[/]");
            return;
        }

        // Show what will be stopped (outside Status block)
        var projectName = project != null ? Markup.Escape(project.Name) : "Unknown Project";
        var taskName = task != null ? Markup.Escape(task.Name) : "No Task";
        var description = string.IsNullOrWhiteSpace(currentEntry.Description) ? "No description" : Markup.Escape(currentEntry.Description);

        console.MarkupLine($"[yellow]Currently running:[/]");
        console.MarkupLine($"  [bold]Project:[/] {projectName}");
        console.MarkupLine($"  [bold]Task:[/] {taskName}");
        console.MarkupLine($"  [bold]Description:[/] {description}");
        console.MarkupLine($"  [bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");
        console.WriteLine();

        // User confirmation (outside Status block)
        if (console.Confirm("Stop this timer?"))
        {
            // Stop the timer (inside Status block for feedback)
            await console.Status()
                         .StartAsync("Stopping timer...", async ctx =>
                                                          {
                                                              var stoppedEntry = await clockifyClient.StopCurrentTimeEntry(workspace, user);
                                                          });

            console.MarkupLine("[green]✓ Timer stopped successfully![/]");
            console.MarkupLine($"[dim]Final duration: {TimeFormatter.FormatDuration(elapsed)}[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Timer stop cancelled.[/]");
        }
    }
}
