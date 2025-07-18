using ClockifyCli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

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

    // Add the archive-list command
    config.AddCommand<ArchiveListCommand>("archive-list")
        .WithDescription("List tasks that can be archived based on Jira status")
        .WithExample(new[] { "archive-list" });

    // Add the week-view command
    config.AddCommand<WeekViewCommand>("week-view")
        .WithDescription("Display current week's time entries from Clockify")
        .WithExample(new[] { "week-view" });

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
