using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ArchiveCompletedJirasCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();
        var jiraClient = await CreateJiraClientAsync();

        await ArchiveCompletedTasks(clockifyClient, jiraClient);
        return 0;
    }

    private async Task ArchiveCompletedTasks(Services.ClockifyClient clockifyClient, Services.JiraClient jiraClient)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        AnsiConsole.MarkupLine("[bold]Scanning for archivable tasks...[/]");
        AnsiConsole.WriteLine();

        var projects = await clockifyClient.GetProjects(workspace);
        var tasksToArchive = new List<(Models.ProjectInfo Project, Models.TaskInfo Task, Models.JiraIssue Issue)>();

        await AnsiConsole.Status()
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
            AnsiConsole.MarkupLine("[green]✓ No tasks need to be archived - all tasks are up to date![/]");
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

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Ask if user wants to archive these tasks
        var shouldArchive = AnsiConsole.Confirm($"Archive {tasksToArchive.Count} completed task(s) in Clockify?");

        if (!shouldArchive)
        {
            AnsiConsole.MarkupLine("[yellow]Archive operation cancelled.[/]");
            return;
        }

        // Archive the tasks
        var successCount = 0;
        var failureCount = 0;
        var results = new List<(string TaskName, bool Success, string? ErrorMessage)>();

        await AnsiConsole.Progress()
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
                AnsiConsole.MarkupLine($"[green]✓ Archived:[/] {Markup.Escape(taskName)}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to archive:[/] {Markup.Escape(taskName)} - {Markup.Escape(errorMessage ?? "Unknown error")}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Archive Summary:[/]");
        AnsiConsole.MarkupLine($"[green]✓ Successfully archived: {successCount} task(s)[/]");

        if (failureCount > 0)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to archive: {failureCount} task(s)[/]");
        }
    }
}
