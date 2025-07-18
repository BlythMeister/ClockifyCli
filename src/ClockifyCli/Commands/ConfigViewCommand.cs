using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public class ConfigViewCommand : ConfigCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var configService = new ConfigurationService();
        var config = await configService.LoadConfigurationAsync();

        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddColumn("Status");

        // Show masked values for security
        table.AddRow("Clockify API Key", MaskSecret(config.ClockifyApiKey), GetStatus(config.ClockifyApiKey));
        table.AddRow("Jira Username", config.JiraUsername ?? "[dim]Not set[/]", GetStatus(config.JiraUsername));
        table.AddRow("Jira API Token", MaskSecret(config.JiraApiToken), GetStatus(config.JiraApiToken));
        table.AddRow("Tempo API Key", MaskSecret(config.TempoApiKey), GetStatus(config.TempoApiKey));

        AnsiConsole.MarkupLine("[bold]Current Configuration[/]");
        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"\nConfiguration file: [dim]{configService.GetConfigurationPath()}[/]");

        if (config.IsComplete())
        {
            AnsiConsole.MarkupLine("\n[green]? All configuration values are set[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[yellow]? Some configuration values are missing[/]");
            AnsiConsole.MarkupLine("Use '[green]config set[/]' to configure missing values.");
        }

        return 0;
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[dim]Not set[/]";

        if (value.Length <= 8)
            return new string('*', value.Length);

        return $"{value[..4]}{"*".PadRight(value.Length - 8, '*')}{value[^4..]}";
    }

    private static string GetStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "[red]Missing[/]" : "[green]Set[/]";
    }
}