using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ConfigSetCommand : ConfigCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var configService = new ConfigurationService();
        var currentConfig = await configService.LoadConfigurationAsync();

        AnsiConsole.MarkupLine("[bold]Configuration Setup[/]");
        AnsiConsole.MarkupLine("Leave fields blank to keep current values.\n");

        // Clockify API Key
        var clockifyApiKey = PromptForSecret(
                                             "Clockify API Key",
                                             currentConfig.ClockifyApiKey,
                                             "Get this from Clockify → Profile Settings → API");

        // Jira Username
        var jiraUsername = AnsiConsole.Prompt(
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

            AnsiConsole.MarkupLine("\n[green]✓ Configuration saved successfully![/]");

            if (updatedConfig.IsComplete())
            {
                AnsiConsole.MarkupLine("[green]✓ All required configuration values are now set[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Some configuration values are still missing[/]");
                AnsiConsole.MarkupLine("Run '[green]config view[/]' to see what's missing.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to save configuration: {ex.Message}[/]");
            return 1;
        }
    }

    private static string PromptForSecret(string fieldName, string? currentValue, string helpText)
    {
        var hasCurrentValue = !string.IsNullOrWhiteSpace(currentValue);
        var prompt = new TextPrompt<string>($"{fieldName}:")
                     .Secret()
                     .AllowEmpty();

        if (hasCurrentValue)
        {
            prompt.DefaultValue(""); // Don't show the actual value as default
            AnsiConsole.MarkupLine($"[dim]{helpText}[/]");
            AnsiConsole.MarkupLine($"[dim]Current value: {MaskValue(currentValue!)} (leave blank to keep)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]{helpText}[/]");
        }

        return AnsiConsole.Prompt(prompt);
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
