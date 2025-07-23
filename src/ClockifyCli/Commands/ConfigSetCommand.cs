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

        // Update configuration
        try
        {
            var updatedConfig = await configService.UpdateConfigurationAsync(
                                                                             string.IsNullOrWhiteSpace(clockifyApiKey) ? null : clockifyApiKey,
                                                                             string.IsNullOrWhiteSpace(jiraUsername) ? null : jiraUsername,
                                                                             string.IsNullOrWhiteSpace(jiraApiToken) ? null : jiraApiToken,
                                                                             string.IsNullOrWhiteSpace(tempoApiKey) ? null : tempoApiKey
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
