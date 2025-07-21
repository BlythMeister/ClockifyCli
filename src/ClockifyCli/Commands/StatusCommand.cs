using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StatusCommand : BaseCommand
{
    private readonly ClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public StatusCommand(ClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await ShowCurrentStatus(clockifyClient, console);
        return 0;
    }

    private async Task ShowCurrentStatus(ClockifyClient clockifyClient, IAnsiConsole console)
    {
        console.MarkupLine("[bold]Current Clockify Status[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        await console.Status()
                     .StartAsync("Getting current time entry...", async ctx =>
                                                                  {
                                                                      var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);

                                                                      if (currentEntry == null)
                                                                      {
                                                                          console.MarkupLine("[yellow]⏸️  No time entry currently running[/]");
                                                                          console.MarkupLine("[dim]Start a new time entry by running 'clockify-cli start' to see it here.[/]");
                                                                          return;
                                                                      }

                                                                      // Get project and task details
                                                                      ctx.Status("Getting project and task details...");
                                                                      var projects = await clockifyClient.GetProjects(workspace);
                                                                      var project = projects.FirstOrDefault(p => p.Id == currentEntry.ProjectId);

                                                                      var task = project != null ? (await clockifyClient.GetTasks(workspace, project)).FirstOrDefault(t => t.Id == currentEntry.TaskId) : null;

                                                                      // Calculate elapsed time
                                                                      var startTime = currentEntry.TimeInterval.StartDate;
                                                                      var elapsed = DateTime.UtcNow - startTime;

                                                                      // Create status panel
                                                                      var panel = new Panel(CreateStatusContent(currentEntry, project, task, startTime, elapsed))
                                                                                  .Header("[green]⏱️  Currently Running[/]")
                                                                                  .BorderColor(Color.Green)
                                                                                  .Padding(1, 1);

                                                                      console.Write(panel);
                                                                  });
    }

    private static string CreateStatusContent(
        Models.TimeEntry entry,
        Models.ProjectInfo? project,
        Models.TaskInfo? task,
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
        content.AppendLine($"[bold]Elapsed:[/] {TimeFormatter.FormatDuration(elapsed)}");

        return content.ToString().TrimEnd();
    }
}
