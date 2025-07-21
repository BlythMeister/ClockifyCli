using ClockifyCli.Models;
using NUnit.Framework;

namespace ClockifyCli.Tests.Models;

[TestFixture]
public class AppConfigurationTests
{
    [Test]
    public void IsComplete_WithAllValidValues_ShouldReturnTrue()
    {
        // Arrange
        var config = new AppConfiguration(
            "clockify-api-key",
            "user@example.com",
            "jira-token",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.True);
    }

    [Test]
    public void IsComplete_WithEmptyClockifyApiKey_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            "",
            "user@example.com",
            "jira-token",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void IsComplete_WithNullClockifyApiKey_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            null!,
            "user@example.com",
            "jira-token",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void IsComplete_WithWhitespaceClockifyApiKey_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            "   ",
            "user@example.com",
            "jira-token",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void IsComplete_WithEmptyJiraUsername_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            "clockify-api-key",
            "",
            "jira-token",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void IsComplete_WithEmptyJiraApiToken_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            "clockify-api-key",
            "user@example.com",
            "",
            "tempo-key"
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void IsComplete_WithEmptyTempoApiKey_ShouldReturnFalse()
    {
        // Arrange
        var config = new AppConfiguration(
            "clockify-api-key",
            "user@example.com",
            "jira-token",
            ""
        );

        // Act & Assert
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void Empty_ShouldReturnConfigurationWithEmptyStrings()
    {
        // Act
        var config = AppConfiguration.Empty;

        // Assert
        Assert.That(config.ClockifyApiKey, Is.EqualTo(string.Empty));
        Assert.That(config.JiraUsername, Is.EqualTo(string.Empty));
        Assert.That(config.JiraApiToken, Is.EqualTo(string.Empty));
        Assert.That(config.TempoApiKey, Is.EqualTo(string.Empty));
        Assert.That(config.IsComplete(), Is.False);
    }

    [Test]
    public void AppConfiguration_ShouldBeRecord_AllowingValueEquality()
    {
        // Arrange
        var config1 = new AppConfiguration("key1", "user1", "token1", "tempo1");
        var config2 = new AppConfiguration("key1", "user1", "token1", "tempo1");
        var config3 = new AppConfiguration("key2", "user1", "token1", "tempo1");

        // Act & Assert
        Assert.That(config1, Is.EqualTo(config2));
        Assert.That(config1, Is.Not.EqualTo(config3));
    }
}
