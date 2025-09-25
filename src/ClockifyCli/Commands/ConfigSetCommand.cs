/// <summary>
/// Command to set configuration values for ClockifyCli.
/// Prompts for:
/// - Clockify API Key
/// - Jira Username
/// - Jira API Token
/// - Tempo API Key
/// - Recent Tasks Count (number of recent tasks to show in project/task selection lists)
/// - Recent Tasks Days (how many days back to consider for recent tasks)
/// 
/// Leave fields blank to keep current values.
/// </summary>
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ConfigSetCommand : ConfigCommand
{
    private readonly ConfigurationService configService;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public ConfigSetCommand(ConfigurationService configService, IAnsiConsole console)
    {
        this.configService = configService;
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var currentConfig = await configService.LoadConfigurationAsync();

        console.MarkupLine("[bold]Configuration Setup[/]");
        console.MarkupLine("Leave fields blank to keep current values.\n");

        // Clockify API Key
        var clockifyApiKey = PromptForSecret(
                                             "Clockify API Key",
                                             currentConfig.ClockifyApiKey,
                                             "Get this from Clockify → Profile Settings → API");

        // Jira Username
        var jiraUsername = console.Prompt(
                                              new TextPrompt<string>("Jira Username (email):")
                                                  .DefaultValue(currentConfig.JiraUsername ?? "")
                                                  .AllowEmpty());

        // Jira API Token
        var jiraApiToken = PromptForSecret(
                                           "Jira API Token",
                                           currentConfig.JiraApiToken,
                                           "Get this from Atlassian → Account Settings → Security → API tokens");

        // Tempo API Key
        var tempoApiKey = PromptForSecret(
                                          "Tempo API Key",
                                          currentConfig.TempoApiKey,
                                          "Get this from Tempo → Settings → API Integration");

        // Recent Tasks Count
        var recentTasksCount = PromptForInt(
            "Recent Tasks Count",
            currentConfig.RecentTasksCount,
            "Number of recent tasks to show in project/task selection lists (default: 5)",
            min: 1, max: 50);

        // Recent Tasks Days
        var recentTasksDays = PromptForInt(
            "Recent Tasks Days",
            currentConfig.RecentTasksDays,
            "How many days back to consider for recent tasks (default: 7)",
            min: 1, max: 90);

        // Update configuration
        try
        {
            var updatedConfig = await configService.UpdateConfigurationAsync(
                string.IsNullOrWhiteSpace(clockifyApiKey) ? null : clockifyApiKey,
                string.IsNullOrWhiteSpace(jiraUsername) ? null : jiraUsername,
                string.IsNullOrWhiteSpace(jiraApiToken) ? null : jiraApiToken,
                string.IsNullOrWhiteSpace(tempoApiKey) ? null : tempoApiKey,
                recentTasksCount,
                recentTasksDays
            );

            console.MarkupLine("\n[green]:check_mark: Configuration saved successfully![/]");

            if (updatedConfig.IsComplete())
            {
                console.MarkupLine("[green]:check_mark: All required configuration values are now set[/]");
            }
            else
            {
                console.MarkupLine("[yellow]:warning: Some configuration values are still missing[/]");
                console.MarkupLine("Run '[green]config view[/]' to see what's missing.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]:cross_mark: Failed to save configuration: {ex.Message}[/]");
            return 1;
        }
    }

    private int PromptForInt(string fieldName, int currentValue, string helpText, int min, int max)
    {
        console.MarkupLine($"[dim]{helpText}[/]");
        var prompt = new TextPrompt<int>($"{fieldName}:")
            .DefaultValue(currentValue)
            .AllowEmpty()
            .ValidationErrorMessage("[red]Please enter a valid number.[/]")
            .Validate(val => val >= min && val <= max);
        return console.Prompt(prompt);
    }

    private string PromptForSecret(string fieldName, string? currentValue, string helpText)
    {
        var hasCurrentValue = !string.IsNullOrWhiteSpace(currentValue);
        var prompt = new TextPrompt<string>($"{fieldName}:")
                     .Secret()
                     .AllowEmpty();

        if (hasCurrentValue)
        {
            prompt.DefaultValue(""); // Don't show the actual value as default
            console.MarkupLine($"[dim]{helpText}[/]");
            console.MarkupLine($"[dim]Current value: {MaskValue(currentValue!)} (leave blank to keep)[/]");
        }
        else
        {
            console.MarkupLine($"[dim]{helpText}[/]");
        }

        return console.Prompt(prompt);
    }

    private static string MaskValue(string value)
    {
        if (value.Length <= 8)
        {
            return new string('*', value.Length);
        }

        return $"{value[..4]}{"*".PadRight(value.Length - 8, '*')}{value[^4..]}";
    }
}
