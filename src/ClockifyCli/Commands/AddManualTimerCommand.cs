using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Utilities;
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
        var startTimeInput = console.Prompt(
            new TextPrompt<string>("[blue]Start time[/] (e.g., 9:30, 2:30 PM, 2:30p, 14:30):")
                .Validate(timeInput =>
                {
                    if (IntelligentTimeParser.TryParseStartTime(timeInput, out var time, clock.Now))
                    {
                        var proposedDateTime = clock.Today.Add(time);
                        if (proposedDateTime <= clock.Now)
                        {
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("Start time cannot be in the future");
                    }
                    return ValidationResult.Error($"Please enter a valid time format (e.g., 9:30, 2:30p, 14:30)");
                }));

        // Check if start time is ambiguous and confirm with user
        startTimeInput = CheckAndConfirmAmbiguousTime(console, startTimeInput, "start time");

        DateTime startTime = clock.Today;
        if (IntelligentTimeParser.TryParseStartTime(startTimeInput, out var parsedStartTime, clock.Now))
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
            new TextPrompt<string>("[blue]End time[/] (e.g., 10:30, 5:30 PM, 5:30p, 17:30):")
                .Validate(timeInput =>
                {
                    if (IntelligentTimeParser.TryParseEndTime(timeInput, out var time, startTime))
                    {
                        var proposedDateTime = startTime.Date.Add(time);
                        if (proposedDateTime <= clock.Now)
                        {
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("End time cannot be in the future");
                    }
                    return ValidationResult.Error($"Please enter a valid time format (e.g., 10:30, 5:30p, 17:30)");
                }));

        // Check if end time is ambiguous and confirm with user
        endTimeInput = CheckAndConfirmAmbiguousTime(console, endTimeInput, "end time");

        DateTime endTime = clock.Today;
        if (IntelligentTimeParser.TryParseEndTime(endTimeInput, out var parsedEndTime, startTime))
        {
            endTime = startTime.Date.Add(parsedEndTime);

            // If end time is before start time, assume it's the next day
            if (endTime <= startTime)
            {
                endTime = endTime.AddDays(1);
            }
            
            // Validate the time makes sense in context
            if (!IntelligentTimeParser.ValidateTimeInContext(parsedEndTime, startTime, isStartTime: false, out var errorMessage))
            {
                console.MarkupLine($"[red]Error: {errorMessage}[/]");
                return;
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

    private static string PromptForTimeWithConfirmation(IAnsiConsole console, string timeType, DateTime contextTime, bool isStartTime, IClock clock)
    {
        while (true)
        {
            var input = console.Prompt(
                new TextPrompt<string>($"[blue]{timeType}[/] (e.g., 9:30, 2:30 PM, 2:30p, 14:30):")
                    .Validate(timeInput =>
                    {
                        TimeSpan parsedTime;
                        bool parseSuccess = isStartTime ? 
                            IntelligentTimeParser.TryParseStartTime(timeInput, out parsedTime, contextTime) :
                            IntelligentTimeParser.TryParseEndTime(timeInput, out parsedTime, contextTime);

                        if (parseSuccess)
                        {
                            var proposedDateTime = clock.Today.Add(parsedTime);
                            
                            if (isStartTime)
                            {
                                if (proposedDateTime <= clock.Now)
                                {
                                    return ValidationResult.Success();
                                }
                                return ValidationResult.Error("Start time cannot be in the future");
                            }
                            else
                            {
                                // For end times, check if it's reasonable
                                if (proposedDateTime <= clock.Now)
                                {
                                    return ValidationResult.Success();
                                }
                                return ValidationResult.Error("End time cannot be in the future");
                            }
                        }
                        return ValidationResult.Error($"Please enter a valid time format (e.g., 9:30, 2:30p, 14:30)");
                    }));

            // Parse the time to check if it's ambiguous
            TimeSpan finalParsedTime;
            var parseSuccess = isStartTime ? 
                IntelligentTimeParser.TryParseStartTime(input, out finalParsedTime, contextTime) :
                IntelligentTimeParser.TryParseEndTime(input, out finalParsedTime, contextTime);

            if (!parseSuccess)
            {
                continue; // This shouldn't happen due to validation, but just in case
            }

            // Check if the input is ambiguous
            if (IntelligentTimeParser.IsAmbiguousTime(input))
            {
                var (amVersion, pmVersion, display24Hour, displayAmPm) = IntelligentTimeParser.GetAmbiguousTimeOptions(input, finalParsedTime);

                console.MarkupLine($"[yellow]You entered:[/] {input}");
                console.MarkupLine($"[cyan]I interpreted this as:[/] {display24Hour} ({displayAmPm})");

                var isCorrect = console.Confirm("Is this correct?", true);
                
                if (isCorrect)
                {
                    return input; // Return original input since it was interpreted correctly
                }

                // Ask user to clarify AM or PM
                var inputHour = finalParsedTime.Hours > 12 ? finalParsedTime.Hours - 12 : finalParsedTime.Hours;
                if (inputHour == 0) inputHour = 12;

                var amChoice = $"{inputHour}:{finalParsedTime.Minutes:D2} AM";
                var pmChoice = $"{inputHour}:{finalParsedTime.Minutes:D2} PM";

                var amPmChoice = console.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Please clarify - did you mean:[/]")
                        .AddChoices(amChoice, pmChoice));

                // Return the clarified input with explicit AM/PM
                return amPmChoice;
            }

            // Not ambiguous, return as-is
            return input;
        }
    }

    private static string CheckAndConfirmAmbiguousTime(IAnsiConsole console, string timeInput, string timeType)
    {
        // Check if the input is ambiguous
        if (IntelligentTimeParser.IsAmbiguousTime(timeInput))
        {
            // Parse the time to get current interpretation
            TimeSpan parsedTime;
            if (IntelligentTimeParser.TryParseTime(timeInput, out parsedTime))
            {
                var (amVersion, pmVersion, display24Hour, displayAmPm) = IntelligentTimeParser.GetAmbiguousTimeOptions(timeInput, parsedTime);

                console.MarkupLine($"[yellow]You entered:[/] {timeInput} for {timeType}");
                console.MarkupLine($"[cyan]I interpreted this as:[/] {display24Hour} ({displayAmPm})");

                var isCorrect = console.Confirm("Is this correct?", true);
                
                if (isCorrect)
                {
                    return timeInput; // Return original input since it was interpreted correctly
                }

                // Ask user to clarify AM or PM
                var inputHour = parsedTime.Hours > 12 ? parsedTime.Hours - 12 : parsedTime.Hours;
                if (inputHour == 0) inputHour = 12;

                var amChoice = $"{inputHour}:{parsedTime.Minutes:D2} AM";
                var pmChoice = $"{inputHour}:{parsedTime.Minutes:D2} PM";

                var amPmChoice = console.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Please clarify - did you mean:[/]")
                        .AddChoices(amChoice, pmChoice));

                return amPmChoice;
            }
        }

        // Not ambiguous or couldn't parse, return as-is
        return timeInput;
    }
}
