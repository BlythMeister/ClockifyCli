using ClockifyCli.Models;

namespace ClockifyCli.Services;

public class ClientFactory
{
    private readonly ConfigurationService configurationService;

    public ClientFactory() : this(new ConfigurationService())
    {
    }

    public ClientFactory(ConfigurationService configurationService)
    {
        this.configurationService = configurationService;
    }

    public async Task<ClockifyClient> CreateClockifyClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new ClockifyClient(config.ClockifyApiKey);
    }

    public async Task<JiraClient> CreateJiraClientAsync()
    {
        var config = await GetConfigurationAsync();
        return new JiraClient(config.JiraUsername, config.JiraApiToken);
    }

    public async Task<TempoClient> CreateTempoClientAsync(JiraClient jiraClient)
    {
        var config = await GetConfigurationAsync();
        return new TempoClient(config.TempoApiKey, jiraClient);
    }

    private async Task<AppConfiguration> GetConfigurationAsync()
    {
        var configuration = await configurationService.LoadConfigurationAsync();

        if (!configuration.IsComplete())
        {
            ShowConfigurationIncompleteMessage();
            throw new InvalidOperationException("Configuration is incomplete. Please run 'config set' first.");
        }

        return configuration;
    }

    private static void ShowConfigurationIncompleteMessage()
    {
        Console.WriteLine();
        Console.WriteLine("Configuration is incomplete!");
        Console.WriteLine();
        Console.WriteLine("The application requires the following API keys and credentials:");
        Console.WriteLine("• Clockify API Key - Get from Clockify → Profile Settings → API");
        Console.WriteLine("• Jira Username - Your Jira email address");
        Console.WriteLine("• Jira API Token - Get from Atlassian → Account Settings → Security → API tokens");
        Console.WriteLine("• Tempo API Key - Get from Tempo → Settings → API Integration");
        Console.WriteLine();
        Console.WriteLine("Run 'clockify-cli config set' to configure these values.");
        Console.WriteLine("Run 'clockify-cli config view' to check current configuration.");
        Console.WriteLine();
    }
}
