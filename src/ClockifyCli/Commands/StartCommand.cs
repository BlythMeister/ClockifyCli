using ClockifyCli.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StartCommand : BaseCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var clockifyClient = await CreateClockifyClientAsync();

        await StartNewTimer(clockifyClient);
        return 0;
    }

    private async Task StartNewTimer(Services.ClockifyClient clockifyClient)
    {
        AnsiConsole.MarkupLine("[bold]Start New Timer[/]");
        AnsiConsole.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            AnsiConsole.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Check if there's already a running timer
        var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
        if (currentEntry != null)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  A timer is already running![/]");
            AnsiConsole.MarkupLine("[dim]Stop the current timer first with 'clockify-cli stop' or use 'clockify-cli status' to see what's running.[/]");
            return;
        }

        // Load data first (inside Status block)
        List<TaskWithProject> allOptions = new();
        await AnsiConsole.Status()
            .StartAsync("Loading projects and tasks...", async ctx =>
            {
                ctx.Status("Getting projects from Clockify...");
                var projects = await clockifyClient.GetProjects(workspace);

                if (!projects.Any())
                {
                    return;
                }

                // Collect all tasks with their project information
                ctx.Status("Getting tasks from all projects...");
                var tasksWithProjects = new List<TaskWithProject>();

                foreach (var project in projects)
                {
                    var projectTasks = await clockifyClient.GetTasks(workspace, project);
                    foreach (var task in projectTasks.Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        tasksWithProjects.Add(new TaskWithProject(
                            task.Id,
                            task.Name,
                            project.Id,
                            project.Name
                        ));
                    }
                }

                allOptions.AddRange(tasksWithProjects
                    .OrderBy(t => t.ProjectName)
                    .ThenBy(t => t.TaskName));
            });

        // Check if we have options after loading
        if (!allOptions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No tasks found![/]");
            AnsiConsole.MarkupLine("[dim]Add some tasks to your projects first using 'clockify-cli add-task'.[/]");
            return;
        }

        // Now do user interaction (outside Status block)
        var selectedOption = AnsiConsole.Prompt(
            new SelectionPrompt<TaskWithProject>()
                .Title("Select a [green]task[/] to start timing:")
                .PageSize(15)
                .AddChoices(allOptions)
                .UseConverter(task => task.SafeDisplayName));

        // Prompt for optional description
        var description = AnsiConsole.Ask<string>(
            "[blue]Description[/] (optional):",
            defaultValue: string.Empty);

        // Show confirmation
        var projectName = Markup.Escape(selectedOption.ProjectName);
        var taskName = selectedOption.TaskName == "No specific task"
            ? "[dim]No specific task[/]"
            : Markup.Escape(selectedOption.TaskName);
        var descriptionDisplay = string.IsNullOrWhiteSpace(description)
            ? "[dim]No description[/]"
            : Markup.Escape(description);

        AnsiConsole.MarkupLine($"[yellow]About to start timer for:[/]");
        AnsiConsole.MarkupLine($"  [bold]Project:[/] {projectName}");
        AnsiConsole.MarkupLine($"  [bold]Task:[/] {taskName}");
        AnsiConsole.MarkupLine($"  [bold]Description:[/] {descriptionDisplay}");
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("Start this timer?"))
        {
            // Start the timer (inside Status block for feedback)
            await AnsiConsole.Status()
                .StartAsync("Starting timer...", async ctx =>
                {
                    var taskId = string.IsNullOrEmpty(selectedOption.TaskId) ? null : selectedOption.TaskId;
                    var finalDescription = string.IsNullOrWhiteSpace(description) ? null : description;

                    var startedEntry = await clockifyClient.StartTimeEntry(
                        workspace,
                        selectedOption.ProjectId,
                        taskId,
                        finalDescription);
                });

            AnsiConsole.MarkupLine("[green]✓ Timer started successfully![/]");
            AnsiConsole.MarkupLine($"[dim]Started at: {DateTime.Now:HH:mm:ss}[/]");
            AnsiConsole.MarkupLine("[dim]Use 'clockify-cli status' to see the running timer or 'clockify-cli stop' to stop it.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Timer start cancelled.[/]");
        }
    }
}
