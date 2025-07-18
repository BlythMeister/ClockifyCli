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

    private async Task AddTask(ClockifyCli.Services.ClockifyClient clockifyClient, ClockifyCli.Services.JiraClient jiraClient)
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

        await AnsiConsole.Status()
            .StartAsync($"Finding jira: {jiraRef}...", async ctx =>
            {
                var issue = await jiraClient.GetIssue(jiraRef);

                if (issue == null)
                {
                    AnsiConsole.MarkupLine($"[red]Unknown Issue '{jiraRefOrUrl}'[/]");
                    return;
                }

                var taskName = $"{issue.Key} [{issue.Fields.Summary}]";

                AnsiConsole.MarkupLine($"Will Add Task '[yellow]{taskName}[/]' Into Project '[green]{selectedProject.Name}[/]'");

                if (AnsiConsole.Confirm("Confirm?"))
                {
                    await clockifyClient.AddTask(workspace, selectedProject, taskName);
                    AnsiConsole.MarkupLine("[green]Task added successfully![/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                }
            });
    }
}