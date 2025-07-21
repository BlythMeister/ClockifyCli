using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class AddTaskCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();
        var jiraClient = await CreateJiraClientAsync();

        await AddTask(clockifyClient, jiraClient);
        return 0;
    }

    private async Task AddTask(Services.ClockifyClient clockifyClient, Services.JiraClient jiraClient)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        var projects = await clockifyClient.GetProjects(workspace);

        var projectChoice = AnsiConsole.Prompt(
                                               new SelectionPrompt<string>()
                                                   .Title("Select a [green]project[/]:")
                                                   .PageSize(10)
                                                   .AddChoices(projects.Select(p => p.Name)));

        var selectedProject = projects.First(p => p.Name == projectChoice);

        var jiraRefOrUrl = AnsiConsole.Ask<string>("Enter [blue]Jira Ref[/] or [blue]URL[/]:");
        var jiraRef = jiraRefOrUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                          ? jiraRefOrUrl.Substring(jiraRefOrUrl.LastIndexOf("/") + 1)
                          : jiraRefOrUrl;

        // Load Jira issue data first (inside Status block)
        Models.JiraIssue? issue = null;
        await AnsiConsole.Status()
                         .StartAsync($"Finding jira: {jiraRef}...", async ctx => { issue = await jiraClient.GetIssue(jiraRef); });

        // Check if issue was found and has valid data (outside Status block)
        if (issue == null || string.IsNullOrEmpty(issue.Key) || issue.Fields == null || string.IsNullOrEmpty(issue.Fields.Summary))
        {
            AnsiConsole.MarkupLine($"[red]Unknown Issue '{Markup.Escape(jiraRefOrUrl)}' or issue data is incomplete[/]");
            return;
        }

        // Show confirmation (outside Status block)
        var taskName = $"{issue.Key} [{issue.Fields.Summary}]";
        AnsiConsole.MarkupLine($"Will Add Task '[yellow]{Markup.Escape(taskName)}[/]' Into Project '[green]{Markup.Escape(selectedProject.Name)}[/]'");

        if (AnsiConsole.Confirm("Confirm?"))
        {
            // Add the task (inside Status block for feedback)
            await AnsiConsole.Status()
                             .StartAsync("Adding task...", async ctx => { await clockifyClient.AddTask(workspace, selectedProject, taskName); });

            AnsiConsole.MarkupLine("[green]Task added successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
        }
    }
}
