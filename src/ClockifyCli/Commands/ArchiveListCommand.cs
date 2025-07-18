using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ArchiveListCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();
        var jiraClient = await CreateJiraClientAsync();

        await PrintTaskArchiveList(clockifyClient, jiraClient);
        return 0;
    }

    private async Task PrintTaskArchiveList(ClockifyCli.Services.ClockifyClient clockifyClient, ClockifyCli.Services.JiraClient jiraClient)
    {
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        var projects = await clockifyClient.GetProjects(workspace);
        var table = new Table();
        table.AddColumn("Project");
        table.AddColumn("Task");
        table.AddColumn("Status");

        foreach (var project in projects)
        {
            var projectTasks = await clockifyClient.GetTasks(workspace, project);
            foreach (var projectTask in projectTasks.Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase)))
            {
                var issue = await jiraClient.GetIssue(projectTask);
                if (issue is null)
                {
                    table.AddRow(Markup.Escape(project.Name), Markup.Escape(projectTask.Name), "[red]Unknown Jira[/]");
                }
                else if (issue.Fields.Status.StatusCategory.Name.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                {
                    table.AddRow(Markup.Escape(project.Name), Markup.Escape(projectTask.Name), "[yellow]Ready to Archive[/]");
                }
            }
        }

        AnsiConsole.Write(table);
    }
}