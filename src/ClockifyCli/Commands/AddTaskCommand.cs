using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class AddTaskCommand : BaseCommand<AddTaskCommand.Settings>
{
    private readonly IClockifyClient clockifyClient;
    private readonly IJiraClient jiraClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public AddTaskCommand(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient;
        this.jiraClient = jiraClient;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await AddTask(clockifyClient, jiraClient, console);
        return 0;
    }

    private async Task AddTask(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        var projects = await clockifyClient.GetProjects(workspace);

        var projectChoice = console.Prompt(
                                               new SelectionPrompt<string>()
                                                   .Title("Select a [green]project[/]:")
                                                   .PageSize(10)
                                                   .AddChoices(projects.Select(p => p.Name)));

        var selectedProject = projects.First(p => p.Name == projectChoice);

        var jiraRefOrUrl = console.Ask<string>("Enter [blue]Jira Ref[/] or [blue]URL[/]:");
        var jiraRef = jiraRefOrUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                          ? jiraRefOrUrl.Substring(jiraRefOrUrl.LastIndexOf("/") + 1)
                          : jiraRefOrUrl;

        // Load Jira issue data first (inside Status block)
        Models.JiraIssue? issue = null;
        await console.Status()
                         .StartAsync($"Finding jira: {jiraRef}...", async ctx => { issue = await jiraClient.GetIssue(jiraRef); });

        // Check if issue was found and has valid data (outside Status block)
        if (issue == null || string.IsNullOrEmpty(issue.Key) || issue.Fields == null || string.IsNullOrEmpty(issue.Fields.Summary))
        {
            console.MarkupLine($"[red]Unknown Issue '{Markup.Escape(jiraRefOrUrl)}' or issue data is incomplete[/]");
            return;
        }

        // Show confirmation (outside Status block)
        var taskName = $"{issue.Key} [{issue.Fields.Summary}]";
        console.MarkupLine($"Will Add Task '[yellow]{Markup.Escape(taskName)}[/]' Into Project '[green]{Markup.Escape(selectedProject.Name)}[/]'");

        if (console.Confirm("Confirm?"))
        {
            // Add the task (inside Status block for feedback)
            await console.Status()
                             .StartAsync("Adding task...", async ctx => { await clockifyClient.AddTask(workspace, selectedProject, taskName); });

            console.MarkupLine("[green]Task added successfully![/]");
        }
        else
        {
            console.MarkupLine("[yellow]Operation cancelled.[/]");
        }
    }

    public class Settings : CommandSettings
    {
        // No settings for this command currently
    }
}
