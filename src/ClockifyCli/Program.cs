using ClockifyCli.Commands;
using ClockifyCli.Infrastructure;
using ClockifyCli.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;

// Ensure console supports Unicode output
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Setup dependency injection
var services = new ServiceCollection();

// Register console
services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

// Register HTTP clients
services.AddHttpClient();

// Register our services
services.AddSingleton<ConfigurationService>();

// Register client factories that will create clients on-demand
services.AddTransient<ClockifyClient>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var configService = provider.GetRequiredService<ConfigurationService>();
    
    // Load config when the client is actually needed
    var config = configService.LoadConfigurationAsync().GetAwaiter().GetResult();
    
    if (string.IsNullOrWhiteSpace(config.ClockifyApiKey))
    {
        throw new InvalidOperationException("Configuration is incomplete. Run 'config set' first to configure your API keys.");
    }
    
    var httpClient = httpClientFactory.CreateClient();
    return new ClockifyClient(httpClient, config.ClockifyApiKey);
});

// Register the interface that points to the same instance
services.AddTransient<IClockifyClient>(provider => provider.GetRequiredService<ClockifyClient>());

services.AddTransient<JiraClient>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var configService = provider.GetRequiredService<ConfigurationService>();
    
    // Load config when the client is actually needed
    var config = configService.LoadConfigurationAsync().GetAwaiter().GetResult();
    
    if (string.IsNullOrWhiteSpace(config.JiraUsername) || string.IsNullOrWhiteSpace(config.JiraApiToken))
    {
        throw new InvalidOperationException("Configuration is incomplete. Run 'config set' first to configure your API keys.");
    }
    
    var httpClient = httpClientFactory.CreateClient();
    return new JiraClient(httpClient, config.JiraUsername, config.JiraApiToken);
});

// Register the interface that points to the same instance
services.AddTransient<IJiraClient>(provider => provider.GetRequiredService<JiraClient>());

services.AddTransient<TempoClient>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var configService = provider.GetRequiredService<ConfigurationService>();
    var jiraClient = provider.GetRequiredService<JiraClient>();
    
    // Load config when the client is actually needed
    var config = configService.LoadConfigurationAsync().GetAwaiter().GetResult();
    
    if (string.IsNullOrWhiteSpace(config.TempoApiKey))
    {
        throw new InvalidOperationException("Configuration is incomplete. Run 'config set' first to configure your API keys.");
    }
    
    var httpClient = httpClientFactory.CreateClient();
    return new TempoClient(httpClient, config.TempoApiKey, jiraClient);
});

// Register the interface that points to the same instance
services.AddTransient<ITempoClient>(provider => provider.GetRequiredService<TempoClient>());

// Create type registrar and command app
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

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
                        .WithExample(new[] { "week-view", "--include-current" })
                        .WithExample(new[] { "week-view", "--detailed" })
                        .WithExample(new[] { "week-view", "--include-current", "--detailed" });

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

                  // Add the edit-timer command
                  config.AddCommand<EditTimerCommand>("edit-timer")
                        .WithDescription("Edit start/end times of existing time entries")
                        .WithExample(new[] { "edit-timer" })
                        .WithExample(new[] { "edit-timer", "--days", "3" })
                        .WithExample(new[] { "edit-timer", "--days", "14" });

                  // Add the discard-timer command
                  config.AddCommand<DiscardTimerCommand>("discard-timer")
                        .WithDescription("Discard the currently running timer (permanently deletes it)")
                        .WithExample(new[] { "discard-timer" });

                  // Add the delete-timer command
                  config.AddCommand<DeleteTimerCommand>("delete-timer")
                        .WithDescription("Delete completed timers from this week")
                        .WithExample(new[] { "delete-timer" });

                  // Add the timer-monitor command
                  config.AddCommand<TimerMonitorCommand>("timer-monitor")
                        .WithDescription("Monitor timer status and show notifications (ideal for scheduled tasks)")
                        .WithExample(new[] { "timer-monitor" })
                        .WithExample(new[] { "timer-monitor", "--silent" })
                        .WithExample(new[] { "timer-monitor", "--always-notify" });

                  // Add the full-view command
                  config.AddCommand<FullViewCommand>("full-view")
                        .WithDescription("Open Clockify web app in your default browser")
                        .WithExample(new[] { "full-view" });

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

                                                 config.AddCommand<ConfigScheduleCommand>("schedule-monitor")
                                                       .WithDescription("Set up scheduled task for timer monitoring")
                                                       .WithExample(new[] { "config", "schedule-monitor" })
                                                       .WithExample(new[] { "config", "schedule-monitor", "--interval", "60" })
                                                       .WithExample(new[] { "config", "schedule-monitor", "--remove" });
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
