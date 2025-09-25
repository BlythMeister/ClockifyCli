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
                            // Use the intelligent parser to get the actual interpreted date/time
                            var actualStartTime = IntelligentTimeParser.GetActualStartDateTime(input, clock.Now);

                            // Validate that the actual start time is within 24 hours of now
                            if (actualStartTime < clock.Now.AddHours(-24))
                            {
                                return ValidationResult.Error("Start time cannot be more than 24 hours ago");
                            }

                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error("Please enter a valid time format");
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
                startTimeDisplay = startTime.Value.ToString("HH:mm");
            }
            else
            {
                startTimeDisplay = $"{startTime.Value.ToString("HH:mm")} (yesterday)";
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

            var displayTime = startTime?.ToString("HH:mm") ?? clock.Now.ToString("HH:mm");
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

    /// <summary>
    /// Rule 8: Check for ambiguous times and prompt user for clarification if needed.
    /// </summary>
    /// <param name="input">The original time input</param>
}
