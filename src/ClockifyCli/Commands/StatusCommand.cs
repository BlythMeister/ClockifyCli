using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StatusCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();
        
        await ShowCurrentStatus(clockifyClient);
        return 0;
    }

    private async Task ShowCurrentStatus(ClockifyCli.Services.ClockifyClient clockifyClient)
    {
        AnsiConsole.MarkupLine("[bold]Current Clockify Status[/]");
        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        await AnsiConsole.Status()
            .StartAsync("Getting current time entry...", async ctx =>
            {
                var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                if (currentEntry == null)
                {
                    AnsiConsole.MarkupLine("[yellow]??  No time entry currently running[/]");
                    AnsiConsole.MarkupLine("[dim]Start a new time entry in Clockify to see it here.[/]");
                    return;
                }

                // Get project and task details
                ctx.Status("Getting project and task details...");
                var projects = await clockifyClient.GetProjects(workspace);
                var project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);
                
                var task = project != null ? 
                    (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : 
                    null;

                // Calculate elapsed time
                var startTime = currentEntry.TimeInterval.StartDate;
                var elapsed = DateTime.UtcNow - startTime;

                // Create status panel
                var panel = new Panel(CreateStatusContent(currentEntry, project, task, startTime, elapsed))
                    .Header("[green]??  Currently Running[/]")
                    .BorderColor(Color.Green)
                    .Padding(1, 1);

                AnsiConsole.Write(panel);
            });
    }

    private static string CreateStatusContent(
        ClockifyCli.Models.TimeEntry entry, 
        ClockifyCli.Models.ProjectInfo? project, 
        ClockifyCli.Models.TaskInfo? task,
        DateTime startTime,
        TimeSpan elapsed)
    {
        var content = new System.Text.StringBuilder();
        
        // Project and Task
        content.AppendLine($"[bold]Project:[/] {(project != null ? Markup.Escape(project.Name) : "[dim]Unknown Project[/]")}");
        content.AppendLine($"[bold]Task:[/] {(task != null ? Markup.Escape(task.Name) : "[dim]No Task[/]")}");
        content.AppendLine();
        
        // Description
        var description = string.IsNullOrWhiteSpace(entry.Description) ? "[dim]No description[/]" : Markup.Escape(entry.Description);
        content.AppendLine($"[bold]Description:[/] {description}");
        content.AppendLine();
        
        // Timing information
        content.AppendLine($"[bold]Started:[/] {startTime.ToLocalTime():HH:mm:ss (ddd, MMM dd)}");
        content.AppendLine($"[bold]Elapsed:[/] {FormatDuration(elapsed)}");
        
        return content.ToString().TrimEnd();
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