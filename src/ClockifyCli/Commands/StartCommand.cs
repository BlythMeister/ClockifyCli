using ClockifyCli.Models;
using ClockifyCli.Services;
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

        // Check if there's already a running timer
        var currentEntry = await clockifyClient.GetCurrentTimeEntry(workspace, user);
        if (currentEntry != null)
        {
            console.MarkupLine("[yellow]:warning:  A timer is already running![/]");
            console.MarkupLine($"[dim]Current timer: {currentEntry.Description}[/]");
            console.WriteLine();

            var shouldStop = console.Confirm("Do you want to stop the current timer and start a new one?", false);
            if (!shouldStop)
            {
                console.MarkupLine("[dim]Timer start cancelled. Use 'clockify-cli status' to see what's running.[/]");
                return;
            }

            // Stop the current timer
            console.MarkupLine("[dim]Stopping current timer...[/]");
            await clockifyClient.StopCurrentTimeEntry(workspace, user);
            console.MarkupLine("[green]:check_mark: Current timer stopped[/]");
            console.WriteLine();
        }

        // Load data first (inside Status block)
        List<TaskWithProject> allOptions = new();
        await console.Status()
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
            console.MarkupLine("[yellow]No tasks found![/]");
            console.MarkupLine("[dim]Add some tasks to your projects first using 'clockify-cli add-task-from-jira'.[/]");
            return;
        }

        // Now do user interaction (outside Status block)
        var selectedOption = console.Prompt(
                                           new SelectionPrompt<TaskWithProject>()
                                               .Title("Select a [green]task[/] to start timing:")
                                               .PageSize(15)
                                               .AddChoices(allOptions)
                                               .UseConverter(task => task.SafeDisplayName));

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
                new TextPrompt<string>("[blue]Start time[/] (HH:mm or HH:mm:ss):")
                    .Validate(input =>
                    {
                        if (TimeSpan.TryParse(input, out var time))
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
                        return ValidationResult.Error("Please enter a valid time format (HH:mm or HH:mm:ss)");
                    }));

            if (TimeSpan.TryParse(timeInput, out var parsedTime))
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
        }
    }
}
