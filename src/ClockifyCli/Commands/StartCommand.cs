using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class StartCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;
    private readonly IClock clock;

    // Constructor for dependency injection (now required)
    public StartCommand(IClockifyClient clockifyClient, IAnsiConsole console, IClock clock)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
        this.clock = clock;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await StartNewTimer(clockifyClient, console, clock);
        return 0;
    }

    private async Task StartNewTimer(IClockifyClient clockifyClient, IAnsiConsole console, IClock clock)
    {
        console.MarkupLine("[bold]Start New Timer[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Check if there's already a running timer - but don't stop it yet
        var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
        bool hasRunningTimer = currentEntry != null;
        bool shouldReplaceTimer = false;
        
        if (hasRunningTimer)
        {
            console.MarkupLine("[yellow]:warning:  A timer is already running![/]");
            console.MarkupLine($"[dim]Current timer: {currentEntry!.Description}[/]");
            console.WriteLine();

            shouldReplaceTimer = console.Confirm("Do you want to stop the current timer and start a new one?", false);
            if (!shouldReplaceTimer)
            {
                console.MarkupLine("[dim]Timer start cancelled. Use 'clockify-cli status' to see what's running.[/]");
                return;
            }
            
            console.MarkupLine("[dim]Collecting new timer details first...[/]");
            console.WriteLine();
        }

        // First, load projects
        List<ProjectInfo> projects = new();
        await console.Status()
                     .StartAsync("Loading projects...", async ctx =>
                                                        {
                                                            ctx.Status("Getting projects from Clockify...");
                                                            projects = await clockifyClient.GetProjects(workspace);
                                                        });

        if (!projects.Any())
        {
            console.MarkupLine("[yellow]No projects found![/]");
            console.MarkupLine("[dim]Create some projects in Clockify first.[/]");
            return;
        }

        ProjectInfo selectedProject;
        TaskInfo selectedTask;
        
        // Loop to allow going back from task selection to project selection
        while (true)
        {
            // Let user select a project
            selectedProject = console.Prompt(
                                                new SelectionPrompt<ProjectInfo>()
                                                    .Title("Select a [green]project[/]:")
                                                    .PageSize(15)
                                                    .AddChoices(projects.OrderBy(p => p.Name))
                                                    .UseConverter(project => Markup.Escape(project.Name)));

            // Now load tasks for the selected project
            List<TaskInfo> availableTasks = new();
            await console.Status()
                         .StartAsync($"Loading tasks for {selectedProject.Name}...", async ctx =>
                                                                                      {
                                                                                          ctx.Status($"Getting tasks from {selectedProject.Name}...");
                                                                                          var projectTasks = await clockifyClient.GetTasks(workspace, selectedProject);
                                                                                          availableTasks = projectTasks
                                                                                                          .Where(t => !t.Status.Equals("Done", StringComparison.InvariantCultureIgnoreCase))
                                                                                                          .OrderBy(t => t.Name)
                                                                                                          .ToList();
                                                                                      });

            if (!availableTasks.Any())
            {
                console.MarkupLine($"[yellow]No active tasks found for project '{selectedProject.Name}'![/]");
                console.MarkupLine("[dim]Add some tasks to this project first using 'clockify-cli add-task-from-jira'.[/]");
                return;
            }

            // Only add "Back" option if there are multiple projects
            var taskChoices = new List<TaskInfo>(availableTasks);
            if (projects.Count > 1)
            {
                var backOption = new TaskInfo("__BACK__", "← Back to project selection", "Back");
                taskChoices.Add(backOption);
            }

            // Let user select a task or go back
            var selectedTaskOrBack = console.Prompt(
                                             new SelectionPrompt<TaskInfo>()
                                                 .Title($"Select a [green]task[/] from '{Markup.Escape(selectedProject.Name)}':")
                                                 .PageSize(15)
                                                 .AddChoices(taskChoices)
                                                 .UseConverter(task => task.Id == "__BACK__" ? $"[dim]{Markup.Escape(task.Name)}[/]" : Markup.Escape(task.Name)));

            // Check if user selected "Back"
            if (selectedTaskOrBack.Id == "__BACK__")
            {
                continue; // Go back to project selection
            }

            selectedTask = selectedTaskOrBack;
            break; // Exit the loop when a task is selected
        }

        // Create TaskWithProject for compatibility with existing code
        var selectedOption = new TaskWithProject(
                                                selectedTask.Id,
                                                selectedTask.Name,
                                                selectedProject.Id,
                                                selectedProject.Name
                                               );

        // Prompt for optional description
        var description = console.Prompt(new TextPrompt<string>("[blue]Description[/] (optional):").AllowEmpty());

        // Prompt for start time
        var startTimeOption = console.Prompt(
            new SelectionPrompt<string>()
                .Title("When do you want to [green]start[/] the timer?")
                .AddChoices("Now", "Earlier time"));

        DateTime? startTime = null;
        if (startTimeOption == "Earlier time")
        {
            var timeInput = console.Prompt(
                new TextPrompt<string>("[blue]Start time[/] (e.g., 9:30, 2:30 PM, 2:30p, 14:30):")
                    .Validate(input =>
                    {
                        if (IntelligentTimeParser.TryParseStartTime(input, out var time, clock.Now))
                        {
                            var proposedStartTime = clock.Today.Add(time);
                            DateTime actualStartTime;
                            
                            // If the time is in the future (later today), we'll assume they mean yesterday
                            if (proposedStartTime > clock.Now)
                            {
                                actualStartTime = proposedStartTime.AddDays(-1);
                            }
                            else
                            {
                                // Time is earlier today
                                actualStartTime = proposedStartTime;
                            }
                            
                            // Validate that the actual start time is within 10 hours of now
                            if (actualStartTime < clock.Now.AddHours(-10))
                            {
                                return ValidationResult.Error("Start time cannot be more than 10 hours ago");
                            }
                            
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("Please enter a valid time format (e.g., 9:30, 2:30 PM, 2:30p, 14:30)");
                    }));

            if (IntelligentTimeParser.TryParseStartTime(timeInput, out var parsedTime, clock.Now))
            {
                startTime = clock.Today.Add(parsedTime);
                // If the time is after current time, assume it's for yesterday
                if (startTime > clock.Now)
                {
                    startTime = startTime.Value.AddDays(-1);
                }
            }
        }

        // Show confirmation
        var projectName = Markup.Escape(selectedOption.ProjectName);
        var taskName = selectedOption.TaskName == "No specific task"
                           ? "[dim]No specific task[/]"
                           : Markup.Escape(selectedOption.TaskName);
        var descriptionDisplay = string.IsNullOrWhiteSpace(description)
                                     ? "[dim]No description[/]"
                                     : Markup.Escape(description);
        
        var startTimeDisplay = "Now";
        if (startTime.HasValue)
        {
            if (startTime.Value.Date == clock.Today)
            {
                startTimeDisplay = startTime.Value.ToString("HH:mm:ss");
            }
            else
            {
                startTimeDisplay = $"{startTime.Value.ToString("HH:mm:ss")} (yesterday)";
            }
        }

        console.MarkupLine($"[yellow]About to start timer for:[/]");
        console.MarkupLine($"  [bold]Project:[/] {projectName}");
        console.MarkupLine($"  [bold]Task:[/] {taskName}");
        console.MarkupLine($"  [bold]Description:[/] {descriptionDisplay}");
        console.MarkupLine($"  [bold]Start time:[/] {startTimeDisplay}");
        console.WriteLine();

        if (console.Confirm("Start this timer?"))
        {
            // If we need to replace a running timer, stop it first
            if (shouldReplaceTimer && hasRunningTimer)
            {
                console.MarkupLine("[dim]Stopping current timer...[/]");
                await clockifyClient.StopCurrentTimeEntry(workspace, user);
                console.MarkupLine("[green]:check_mark: Current timer stopped[/]");
                console.WriteLine();
            }
            
            // Start the timer (inside Status block for feedback)
            await console.Status()
                         .StartAsync("Starting timer...", async ctx =>
                                                              {
                                                                  var taskId = string.IsNullOrEmpty(selectedOption.TaskId) ? null : selectedOption.TaskId;
                                                                  var finalDescription = string.IsNullOrWhiteSpace(description) ? null : description;

                                                                  var startedEntry = await clockifyClient.StartTimeEntry(
                                                                                                                         workspace,
                                                                                                                         selectedOption.ProjectId,
                                                                                                                         taskId,
                                                                                                                         finalDescription,
                                                                                                                         startTime);
                                                              });

            var displayTime = startTime?.ToString("HH:mm:ss") ?? clock.Now.ToString("HH:mm:ss");
            console.MarkupLine("[green]:check_mark: Timer started successfully![/]");
            console.MarkupLine($"[dim]Started at: {displayTime}[/]");
            console.MarkupLine("[dim]Use 'clockify-cli status' to see the running timer or 'clockify-cli stop' to stop it.[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Timer start cancelled.[/]");
            if (shouldReplaceTimer && hasRunningTimer)
            {
                console.MarkupLine("[dim]Your original timer is still running.[/]");
            }
        }
    }
}
