using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class AddTaskFromJiraCommand : BaseCommand<AddTaskFromJiraCommand.Settings>
{
    private readonly IClockifyClient clockifyClient;
    private readonly IJiraClient jiraClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public AddTaskFromJiraCommand(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console)
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

        // Ask user whether they want to enter a single Jira issue or a JQL filter
        var inputType = console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to add?")
                .AddChoices(new[] { "Single Jira Issue", "JQL Filter (Multiple Issues)" }));

        if (inputType == "Single Jira Issue")
        {
            await AddSingleJiraTask(clockifyClient, jiraClient, console, workspace, selectedProject);
        }
        else
        {
            await AddTasksFromJqlFilter(clockifyClient, jiraClient, console, workspace, selectedProject);
        }
    }

    private async Task AddSingleJiraTask(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console, WorkspaceInfo workspace, ProjectInfo selectedProject)
    {
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

    private async Task AddTasksFromJqlFilter(IClockifyClient clockifyClient, IJiraClient jiraClient, IAnsiConsole console, WorkspaceInfo workspace, ProjectInfo selectedProject)
    {
        var jqlQuery = console.Ask<string>("Enter [blue]JQL Filter[/] (e.g., 'project = ABC AND status = \"In Progress\"'):");

        console.MarkupLine($"[dim]Searching with JQL: {Markup.Escape(jqlQuery)}[/]");
        console.WriteLine();

        // Search for issues using JQL
        Models.JiraSearchResult? searchResult = null;
        await console.Status()
                         .StartAsync("Searching Jira issues...", async ctx => 
                         { 
                             searchResult = await jiraClient.SearchIssues(jqlQuery, 100); // Limit to 100 results
                         });

        if (searchResult == null || !searchResult.Issues.Any())
        {
            console.MarkupLine("[yellow]No issues found matching the JQL filter.[/]");
            return;
        }

        // Display found issues
        console.MarkupLine($"[green]Found {searchResult.Issues.Count} issue(s):[/]");
        console.WriteLine();

        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Summary");
        table.AddColumn("Status");

        foreach (var issue in searchResult.Issues)
        {
            var status = issue.Fields?.Status?.Name ?? "Unknown";
            table.AddRow(
                Markup.Escape(issue.Key ?? "Unknown"), 
                Markup.Escape(issue.Fields?.Summary ?? "No summary"), 
                Markup.Escape(status)
            );
        }

        console.Write(table);
        console.WriteLine();

        // Confirm addition
        console.MarkupLine($"Will add {searchResult.Issues.Count} task(s) to project '[green]{Markup.Escape(selectedProject.Name)}[/]'");
        console.MarkupLine("[dim]Note: Duplicate tasks (by name) will be skipped automatically.[/]");

        if (!console.Confirm("Proceed with adding all these tasks?"))
        {
            console.MarkupLine("[yellow]Operation cancelled.[/]");
            return;
        }

        // Add all tasks with duplicate checking
        var successCount = 0;
        var errorCount = 0;
        var skippedCount = 0;

        // Get existing tasks for the selected project to check for duplicates
        List<TaskInfo> existingTasks = new List<TaskInfo>();
        await console.Status()
            .StartAsync("Loading existing tasks...", async ctx =>
            {
                existingTasks = await clockifyClient.GetTasks(workspace, selectedProject);
            });

        await console.Status()
            .StartAsync("Adding tasks...", async ctx =>
            {
                foreach (var issue in searchResult.Issues)
                {
                    if (issue.Key != null && issue.Fields?.Summary != null)
                    {
                        try
                        {
                            var taskName = $"{issue.Key} [{issue.Fields.Summary}]";
                            
                            // Check if task already exists (case-insensitive comparison)
                            if (existingTasks.Any(t => t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase)))
                            {
                                ctx.Status($"Skipping {issue.Key} (already exists)...");
                                skippedCount++;
                                continue;
                            }

                            ctx.Status($"Adding {issue.Key}...");
                            await clockifyClient.AddTask(workspace, selectedProject, taskName);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            console.MarkupLine($"[red]Failed to add {issue.Key}: {Markup.Escape(ex.Message)}[/]");
                            errorCount++;
                        }
                    }
                    else
                    {
                        console.MarkupLine($"[red]Skipped invalid issue: missing key or summary[/]");
                        errorCount++;
                    }
                }
            });

        console.WriteLine();
        console.MarkupLine($"[bold]Task Addition Summary:[/]");
        console.MarkupLine($"[green]:check_mark: Successfully added: {successCount} tasks[/]");
        if (skippedCount > 0)
        {
            console.MarkupLine($"[yellow]:warning: Skipped (already exist): {skippedCount} tasks[/]");
        }
        if (errorCount > 0)
        {
            console.MarkupLine($"[red]:cross_mark: Failed to add: {errorCount} tasks[/]");
        }
    }

    public class Settings : CommandSettings
    {
        // No settings for this command currently
    }
}
