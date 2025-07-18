using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public abstract class BaseCommand : AsyncCommand
{
    private AppConfiguration? _configuration;

    protected async Task<AppConfiguration> GetConfigurationAsync()
    {
        if (_configuration == null)
        {
            var configService = new ConfigurationService();
            _configuration = await configService.LoadConfigurationAsync();

            if (!_configuration.IsComplete())
            {
                ShowConfigurationIncompleteMessage();
                throw new InvalidOperationException("Configuration is incomplete. Please run 'config set' first.");
            }
        }

        return _configuration;
    }

    protected async Task<ClockifyClient> CreateClockifyClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new ClockifyClient(config.ClockifyApiKey);
    }

    protected async Task<JiraClient> CreateJiraClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new JiraClient(config.JiraUsername, config.JiraApiToken);
    }

    protected async Task<TempoClient> CreateTempoClientAsync(JiraClient jiraClient)
    {
        var config = await GetConfigurationAsync();
        return new TempoClient(config.TempoApiKey, jiraClient);
    }

    private static void ShowConfigurationIncompleteMessage()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[red]? Configuration is incomplete![/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("The application requires the following API keys and credentials:");
        AnsiConsole.MarkupLine("• [yellow]Clockify API Key[/] - Get from Clockify ? Profile Settings ? API");
        AnsiConsole.MarkupLine("• [yellow]Jira Username[/] - Your Jira email address");
        AnsiConsole.MarkupLine("• [yellow]Jira API Token[/] - Get from Atlassian ? Account Settings ? Security ? API tokens");
        AnsiConsole.MarkupLine("• [yellow]Tempo API Key[/] - Get from Tempo ? Settings ? API Integration");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("Run '[green]clockify-cli config set[/]' to configure these values.");
        AnsiConsole.MarkupLine("Run '[green]clockify-cli config view[/]' to check current configuration.");
        AnsiConsole.MarkupLine("");
    }

    public override abstract Task<int> ExecuteAsync(CommandContext context);
}

public abstract class BaseCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    private AppConfiguration? _configuration;

    protected async Task<AppConfiguration> GetConfigurationAsync()
    {
        if (_configuration == null)
        {
            var configService = new ConfigurationService();
            _configuration = await configService.LoadConfigurationAsync();

            if (!_configuration.IsComplete())
            {
                ShowConfigurationIncompleteMessage();
                throw new InvalidOperationException("Configuration is incomplete. Please run 'config set' first.");
            }
        }

        return _configuration;
    }

    protected async Task<ClockifyClient> CreateClockifyClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new ClockifyClient(config.ClockifyApiKey);
    }

    protected async Task<JiraClient> CreateJiraClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new JiraClient(config.JiraUsername, config.JiraApiToken);
    }

    protected async Task<TempoClient> CreateTempoClientAsync(JiraClient jiraClient)
    {
        var config = await GetConfigurationAsync();
        return new TempoClient(config.TempoApiKey, jiraClient);
    }

    private static void ShowConfigurationIncompleteMessage()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[red]? Configuration is incomplete![/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("The application requires the following API keys and credentials:");
        AnsiConsole.MarkupLine("• [yellow]Clockify API Key[/] - Get from Clockify ? Profile Settings ? API");
        AnsiConsole.MarkupLine("• [yellow]Jira Username[/] - Your Jira email address");
        AnsiConsole.MarkupLine("• [yellow]Jira API Token[/] - Get from Atlassian ? Account Settings ? Security ? API tokens");
        AnsiConsole.MarkupLine("• [yellow]Tempo API Key[/] - Get from Tempo ? Settings ? API Integration");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("Run '[green]clockify-cli config set[/]' to configure these values.");
        AnsiConsole.MarkupLine("Run '[green]clockify-cli config view[/]' to check current configuration.");
        AnsiConsole.MarkupLine("");
    }

    public override abstract Task<int> ExecuteAsync(CommandContext context, TSettings settings);
}