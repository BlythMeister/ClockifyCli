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

        // Use ProjectListHelper to select project and task
        var projectAndTask = await ProjectListHelper.PromptForProjectAndTaskAsync(clockifyClient, console, workspace);
        if (projectAndTask == null)
        {
            return;
        }
        var (selectedProject, selectedTask) = projectAndTask.Value;

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
                    return ValidationResult.Error($"Please enter a valid time format");
                }));

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
                    return ValidationResult.Error($"Please enter a valid time format");
                }));

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
        console.MarkupLine($"  [bold]Start time:[/] {startTime:HH:mm}");
        console.MarkupLine($"  [bold]End time:[/] {endTime:HH:mm}");
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
            console.MarkupLine($"[dim]Duration: {durationDisplay} ({startTime:HH:mm} - {endTime:HH:mm})[/]");
            console.MarkupLine("[dim]Use 'clockify-cli week-view' to see your time entries.[/]");
        }
        else
        {
            console.MarkupLine("[yellow]Time entry cancelled.[/]");
        }
    }

    private static string PromptForTimeWithConfirmation(IAnsiConsole console, string timeType, DateTime contextTime, bool isStartTime, IClock clock)
    {
        var input = console.Prompt(
            new TextPrompt<string>($"[blue]{timeType}[/] (24-hour format, e.g., 09:30, 14:30, 23:45):")
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
                    return ValidationResult.Error($"Please enter a valid time in 24-hour format (e.g., 09:30, 14:30, 23:45)");
                }));

        return input;
    }

    /// <summary>
}
