namespace ClockifyCli.Models;

public record AppConfiguration(
    string ClockifyApiKey,
    string JiraUsername,
    string JiraApiToken,
    string TempoApiKey
)
{
    public bool IsComplete()
    {
        return !string.IsNullOrWhiteSpace(ClockifyApiKey) &&
               !string.IsNullOrWhiteSpace(JiraUsername) &&
               !string.IsNullOrWhiteSpace(JiraApiToken) &&
               !string.IsNullOrWhiteSpace(TempoApiKey);
    }

    public static AppConfiguration Empty => new(string.Empty, string.Empty, string.Empty, string.Empty);
}