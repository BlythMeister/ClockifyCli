using ClockifyCli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;

// Ensure console supports Unicode output
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("clockify-cli");
    config.SetApplicationVersion("1.0.0");
    config.UseAssemblyInformationalVersion();

    // Add the upload-to-tempo command with proper settings
    config.AddCommand<UploadToTempoCommand>("upload-to-tempo")
        .WithDescription("Upload time entries from Clockify to Tempo")
        .WithExample(new[] { "upload-to-tempo" })
        .WithExample(new[] { "upload-to-tempo", "--days", "7" })
        .WithExample(new[] { "upload-to-tempo", "--days", "30", "--cleanup-orphaned" });

    // Add the add-task command
    config.AddCommand<AddTaskCommand>("add-task")
        .WithDescription("Add a new task to Clockify from a Jira issue")
        .WithExample(new[] { "add-task" });

    // Add the archive-completed-jiras command
    config.AddCommand<ArchiveCompletedJirasCommand>("archive-completed-jiras")
        .WithDescription("Archive tasks in Clockify that have completed Jira status")
        .WithExample(new[] { "archive-completed-jiras" });

    // Add the week-view command
    config.AddCommand<WeekViewCommand>("week-view")
        .WithDescription("Display current week's time entries from Clockify")
        .WithExample(new[] { "week-view" })
        .WithExample(new[] { "week-view", "--include-current" });

    // Add the status command
    config.AddCommand<StatusCommand>("status")
        .WithDescription("Display current in-progress time entry from Clockify")
        .WithExample(new[] { "status" });

    // Add the stop command
    config.AddCommand<StopCommand>("stop")
        .WithDescription("Stop the currently running time entry in Clockify")
        .WithExample(new[] { "stop" });

    // Add the start command
    config.AddCommand<StartCommand>("start")
        .WithDescription("Start a new time entry by selecting from available tasks")
        .WithExample(new[] { "start" });

    // Add the timer-monitor command
    config.AddCommand<TimerMonitorCommand>("timer-monitor")
        .WithDescription("Monitor timer status and show notifications (ideal for scheduled tasks)")
        .WithExample(new[] { "timer-monitor" })
        .WithExample(new[] { "timer-monitor", "--silent" })
        .WithExample(new[] { "timer-monitor", "--always-notify" });

    // Add config branch with subcommands
    config.AddBranch("config", config =>
    {
        config.SetDescription("Configuration management commands");

        config.AddCommand<ConfigSetCommand>("set")
            .WithDescription("Set API keys and credentials (required first step)")
            .WithExample(new[] { "config", "set" });

        config.AddCommand<ConfigViewCommand>("view")
            .WithDescription("View current configuration")
            .WithExample(new[] { "config", "view" });
    });

    // Customize help and error messages
    config.SetExceptionHandler((ex, resolver) =>
    {
        if (ex is InvalidOperationException && ex.Message.Contains("Configuration is incomplete"))
        {
            // Don't show the full exception for configuration errors
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return -1;
        }

        AnsiConsole.WriteException(ex);
        return -1;
    });

    // Add validation for examples
    config.ValidateExamples();
});

try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return -1;
}
