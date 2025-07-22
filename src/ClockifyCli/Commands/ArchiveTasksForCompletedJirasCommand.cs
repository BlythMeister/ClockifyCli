using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ArchiveTasksForCompletedJirasCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IJiraClient jiraClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public ArchiveTasksForCompletedJirasCommand(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient ?? throw new ArgumentNullException(nameof(clockifyClient));
        this.jiraClient = jiraClient ?? throw new ArgumentNullException(nameof(jiraClient));
        this.console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await ArchiveCompletedTasks(clockifyClient, jiraClient, console);
        return 0;
    }

    private async Task ArchiveCompletedTasks(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        console.MarkupLine("[bold]Scanning for archivable tasks...[/]");
        console.WriteLine();

        var projects = await clockifyClient.GetProjects(workspace);
        var tasksToArchive = new List<(Models.ProjectInfo Project, Models.TaskInfo Task, Models.JiraIssue Issue)>();

        await console.Status()
                         .StartAsync("Checking tasks and Jira status...", async ctx =>
                                                                          {
                                                                              foreach (var project in projects)
                                                                              {
                                                                                  ctx.Status($"Checking project: {project.Name}...");
                                                                                  var projectTasks = await clockifyClient.GetTasks(workspace, project);

                                                                                  foreach (var projectTask in projectTasks.Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase)))
                                                                                  {
                                                                                      var issue = await jiraClient.GetIssue(projectTask);
                                                                                      if (issue != null && issue.Fields.Status.StatusCategory.Name.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                                                                                      {
                                                                                          tasksToArchive.Add((project, projectTask, issue));
                                                                                      }
                                                                                  }
                                                                              }
                                                                          });

        if (tasksToArchive.Count == 0)
        {
            console.MarkupLine("[green]✓ No tasks need to be archived - all tasks are up to date![/]");
            return;
        }

        // Display table of tasks that can be archived
        var table = new Table();
        table.AddColumn("Project");
        table.AddColumn("Task");
        table.AddColumn("Jira Status");

        foreach (var (project, task, issue) in tasksToArchive)
        {
            table.AddRow(
                         Markup.Escape(project.Name),
                         Markup.Escape(task.Name),
                         $"[green]{Markup.Escape(issue.Fields.Status.Name)}[/]"
                        );
        }

        console.Write(table);
        console.WriteLine();

        // Ask if user wants to archive these tasks
        var shouldArchive = console.Confirm($"Archive {tasksToArchive.Count} completed task(s) in Clockify?");

        if (!shouldArchive)
        {
            console.MarkupLine("[yellow]Archive operation cancelled.[/]");
            return;
        }

        // Archive the tasks
        var successCount = 0;
        var failureCount = 0;
        var results = new List<(string TaskName, bool Success, string? ErrorMessage)>();

        await console.Progress()
                         .StartAsync(async ctx =>
                                     {
                                         var progressTask = ctx.AddTask("[green]Archiving tasks...[/]");
                                         progressTask.MaxValue = tasksToArchive.Count;

                                         foreach (var (project, taskInfo, issue) in tasksToArchive)
                                         {
                                             try
                                             {
                                                 await clockifyClient.UpdateTaskStatus(workspace, project, taskInfo, "DONE");
                                                 successCount++;
                                                 results.Add((taskInfo.Name, true, null));
                                             }
                                             catch (Exception ex)
                                             {
                                                 failureCount++;
                                                 results.Add((taskInfo.Name, false, ex.Message));
                                             }

                                             progressTask.Increment(1);
                                         }
                                     });

        // Display results after progress is complete
        foreach (var (taskName, success, errorMessage) in results)
        {
            if (success)
            {
                console.MarkupLine($"[green]✓ Archived:[/] {Markup.Escape(taskName)}");
            }
            else
            {
                console.MarkupLine($"[red]✗ Failed to archive:[/] {Markup.Escape(taskName)} - {Markup.Escape(errorMessage ?? "Unknown error")}");
            }
        }

        console.WriteLine();
        console.MarkupLine($"[bold]Archive Summary:[/]");
        console.MarkupLine($"[green]✓ Successfully archived: {successCount} task(s)[/]");

        if (failureCount > 0)
        {
            console.MarkupLine($"[red]✗ Failed to archive: {failureCount} task(s)[/]");
        }
    }
}
