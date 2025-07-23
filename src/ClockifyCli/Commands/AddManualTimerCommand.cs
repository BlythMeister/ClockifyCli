using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class AddManualTimerCommand : BaseCommand
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;
    private readonly IClock clock;

    // Constructor for dependency injection (now required)
    public AddManualTimerCommand(IClockifyClient clockifyClient, IAnsiConsole console, IClock clock)
    {
        this.clockifyClient = clockifyClient;
        this.console = console;
        this.clock = clock;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await AddManualTimeEntry(clockifyClient, console, clock);
        return 0;
    }

    private async Task AddManualTimeEntry(IClockifyClient clockifyClient, IAnsiConsole console, IClock clock)
    {
        console.MarkupLine("[bold]Add Manual Time Entry[/]");
        console.WriteLine();

        var user = await clockifyClient.GetLoggedInUser();
        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
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
                                               .Title("Select a [green]task[/] for the time entry:")
                                               .PageSize(15)
                                               .AddChoices(allOptions)
                                               .UseConverter(task => task.SafeDisplayName));

        // Prompt for optional description
        var description = console.Prompt(new TextPrompt<string>("[blue]Description[/] (optional):").AllowEmpty());

        // Prompt for start time
        var startTimeInput = console.Prompt(
            new TextPrompt<string>("[blue]Start time[/] (HH:mm or HH:mm:ss):")
                .Validate(input =>
                {
                    if (TimeSpan.TryParse(input, out var time))
                    {
                        var proposedStartTime = clock.Today.Add(time);
                        if (proposedStartTime <= clock.Now)
                        {
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("Start time cannot be in the future");
                    }
                    return ValidationResult.Error("Please enter a valid time format (HH:mm or HH:mm:ss)");
                }));

        DateTime startTime = clock.Today;
        if (TimeSpan.TryParse(startTimeInput, out var parsedStartTime))
        {
            startTime = clock.Today.Add(parsedStartTime);
            // If the time is after current time, assume it's for yesterday
            if (startTime > clock.Now)
            {
                startTime = startTime.AddDays(-1);
            }
        }

        // Prompt for end time
        var endTimeInput = console.Prompt(
            new TextPrompt<string>("[blue]End time[/] (HH:mm or HH:mm:ss):")
                .Validate(input =>
                {
                    if (TimeSpan.TryParse(input, out var time))
                    {
                        var proposedEndTime = clock.Today.Add(time);

                        // Handle case where end time might be next day
                        if (time < startTime.TimeOfDay)
                        {
                            proposedEndTime = proposedEndTime.AddDays(1);
                        }

                        if (proposedEndTime <= clock.Now)
                        {
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("End time cannot be in the future");
                    }
                    return ValidationResult.Error("Please enter a valid time format (HH:mm or HH:mm:ss)");
                }));

        DateTime endTime = clock.Today;
        if (TimeSpan.TryParse(endTimeInput, out var parsedEndTime))
        {
            endTime = startTime.Date.Add(parsedEndTime);

            // If end time is before start time, assume it's the next day
            if (endTime <= startTime)
            {
                endTime = endTime.AddDays(1);
            }
        }

        // Validate that end time is after start time
        if (endTime <= startTime)
        {
            console.MarkupLine("[red]Error: End time must be after start time![/]");
            return;
        }

        // Calculate and display duration
        var duration = endTime - startTime;
        var durationDisplay = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";

        // Show confirmation
        var projectName = Markup.Escape(selectedOption.ProjectName);
        var taskName = selectedOption.TaskName == "No specific task"
                           ? "[dim]No specific task[/]"
                           : Markup.Escape(selectedOption.TaskName);
        var descriptionDisplay = string.IsNullOrWhiteSpace(description)
                                     ? "[dim]No description[/]"
                                     : Markup.Escape(description);

        console.MarkupLine($"[yellow]About to add time entry for:[/]");
        console.MarkupLine($"  [bold]Project:[/] {projectName}");
        console.MarkupLine($"  [bold]Task:[/] {taskName}");
        console.MarkupLine($"  [bold]Description:[/] {descriptionDisplay}");
        console.MarkupLine($"  [bold]Start time:[/] {startTime:HH:mm:ss}");
        console.MarkupLine($"  [bold]End time:[/] {endTime:HH:mm:ss}");
        console.MarkupLine($"  [bold]Duration:[/] {durationDisplay}");
        console.WriteLine();

        if (console.Confirm("Add this time entry?"))
        {
            // Add the time entry (inside Status block for feedback)
            await console.Status()
                         .StartAsync("Adding time entry...", async ctx =>
                                                                {
                                                                    var taskId = string.IsNullOrEmpty(selectedOption.TaskId) ? null : selectedOption.TaskId;
                                                                    var finalDescription = string.IsNullOrWhiteSpace(description) ? null : description;

                                                                    var addedEntry = await clockifyClient.AddTimeEntry(
                                                                                                                       workspace,
                                                                                                                       selectedOption.ProjectId,
                                                                                                                       taskId,
                                                                                                                       finalDescription,
                                                                                                                       startTime,
                                                                                                                       endTime);
                                                                });

            console.MarkupLine("[green]:check_mark: Time entry added successfully![/]");
            console.MarkupLine($"[dim]Duration: {durationDisplay} ({startTime:HH:mm:ss} - {endTime:HH:mm:ss})[/]");
            console.MarkupLine("[dim]Use 'clockify-cli week-view' to see your time entries.[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Time entry cancelled.[/]");
        }
    }
}
