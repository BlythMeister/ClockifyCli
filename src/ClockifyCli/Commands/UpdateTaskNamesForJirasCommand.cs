using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class UpdateTaskNamesForJirasCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IJiraClient jiraClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public UpdateTaskNamesForJirasCommand(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient ?? throw new ArgumentNullException(nameof(clockifyClient));
        this.jiraClient = jiraClient ?? throw new ArgumentNullException(nameof(jiraClient));
        this.console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await UpdateTaskNames(clockifyClient, jiraClient, console);
        return 0;
    }

    private async Task UpdateTaskNames(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        console.MarkupLine("[bold]Scanning for tasks to update...[/]");
        console.WriteLine();

        var projects = await clockifyClient.GetProjects(workspace);
        var tasksToUpdate = new List<(Models.ProjectInfo Project, Models.TaskInfo Task, Models.JiraIssue Issue, string NewName)>();

        await console.Status()
                         .StartAsync("Checking tasks and Jira data...", async ctx =>
                                                                          {
                                                                              foreach (var project in projects)
                                                                              {
                                                                                  ctx.Status($"Checking project: {project.Name}...");
                                                                                  var projectTasks = await clockifyClient.GetTasks(workspace, project);

                                                                                  foreach (var projectTask in projectTasks.Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase)))
                                                                                  {
                                                                                      var issue = await jiraClient.GetIssue(projectTask);
                                                                                      if (issue != null && issue.Fields != null)
                                                                                      {
                                                                                          var newTaskName = TaskNameFormatter.FormatTaskName(issue);
                                                                                          
                                                                                          // Normalize both names for comparison to avoid whitespace-only differences
                                                                                          var normalizedCurrentName = NormalizeWhitespace(projectTask.Name);
                                                                                          var normalizedNewName = NormalizeWhitespace(newTaskName);
                                                                                          
                                                                                          // Only include if the name would actually change (ignoring whitespace differences)
                                                                                          if (!normalizedCurrentName.Equals(normalizedNewName, StringComparison.Ordinal))
                                                                                          {
                                                                                              tasksToUpdate.Add((project, projectTask, issue, newTaskName));
                                                                                          }
                                                                                      }
                                                                                  }
                                                                              }
                                                                          });

        if (tasksToUpdate.Count == 0)
        {
            console.MarkupLine("[green]:check_mark: No tasks need to be updated - all task names are current![/]");
            return;
        }

        // Display table of tasks that will be updated
        var table = new Table();
        table.AddColumn("Project");
        table.AddColumn("Current Name");
        table.AddColumn("New Name");

        foreach (var (project, task, issue, newName) in tasksToUpdate)
        {
            table.AddRow(
                         Markup.Escape(project.Name),
                         Markup.Escape(task.Name),
                         Markup.Escape(newName)
                        );
        }

        console.Write(table);
        console.WriteLine();

        // Ask if user wants to update these tasks
        var shouldUpdate = console.Confirm($"Update {tasksToUpdate.Count} task name(s) in Clockify?");

        if (!shouldUpdate)
        {
            console.MarkupLine("[yellow]Update operation cancelled.[/]");
            return;
        }

        // Update the tasks
        var successCount = 0;
        var failureCount = 0;
        var results = new List<(string OldName, string NewName, bool Success, string? ErrorMessage)>();

        await console.Progress()
                         .StartAsync(async ctx =>
                                     {
                                         var progressTask = ctx.AddTask("[green]Updating task names...[/]");
                                         progressTask.MaxValue = tasksToUpdate.Count;

                                         foreach (var (project, taskInfo, issue, newName) in tasksToUpdate)
                                         {
                                             try
                                             {
                                                 await clockifyClient.UpdateTaskName(workspace, project, taskInfo, newName);
                                                 successCount++;
                                                 results.Add((taskInfo.Name, newName, true, null));
                                             }
                                             catch (Exception ex)
                                             {
                                                 failureCount++;
                                                 results.Add((taskInfo.Name, newName, false, ex.Message));
                                             }

                                             progressTask.Increment(1);
                                         }
                                     });

        // Display results after progress is complete
        foreach (var (oldName, newName, success, errorMessage) in results)
        {
            if (success)
            {
                console.MarkupLine($"[green]:check_mark: Updated:[/] {Markup.Escape(oldName)} → {Markup.Escape(newName)}");
            }
            else
            {
                console.MarkupLine($"[red]:cross_mark: Failed to update:[/] {Markup.Escape(oldName)} - {Markup.Escape(errorMessage ?? "Unknown error")}");
            }
        }

        console.WriteLine();
        console.MarkupLine($"[bold]Update Summary:[/]");
        console.MarkupLine($"[green]:check_mark: Successfully updated: {successCount} task(s)[/]");

        if (failureCount > 0)
        {
            console.MarkupLine($"[red]:cross_mark: Failed to update: {failureCount} task(s)[/]");
        }
    }

    /// <summary>
    /// Normalizes whitespace in a string by trimming and collapsing multiple spaces into single spaces
    /// </summary>
    private static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Trim and collapse multiple spaces into single spaces
        var normalized = System.Text.RegularExpressions.Regex.Replace(input.Trim(), @"\s+", " ");
        
        // Also remove spaces before closing brackets (common formatting inconsistency)
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+\]", "]");
        
        return normalized;
    }
}
