using ClockifyCli.Models;
using NUnit.Framework;

namespace ClockifyCli.Tests.Models
{
    [TestFixture]
    public class AppConfigurationTests
    {
        [Test]
        public void RecentTasks_Defaults_ShouldBeCorrect()
        {
            var config = new AppConfiguration("a", "b", "c", "d");
            Assert.That(config.RecentTasksCount, Is.EqualTo(5));
            Assert.That(config.RecentTasksDays, Is.EqualTo(7));
        }

        [Test]
        public void RecentTasks_CustomValues_ShouldBeSettable()
        {
            var config = new AppConfiguration("a", "b", "c", "d", 12, 30);
            Assert.That(config.RecentTasksCount, Is.EqualTo(12));
            Assert.That(config.RecentTasksDays, Is.EqualTo(30));
        }
        public void IsComplete_WithAllValidValues_ShouldReturnTrue()
        {
            var config = new AppConfiguration(
                "clockify-api-key",
                "user@example.com",
                "jira-token",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.True);
        }

        [Test]
        public void IsComplete_WithEmptyClockifyApiKey_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                "",
                "user@example.com",
                "jira-token",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void IsComplete_WithNullClockifyApiKey_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                null!,
                "user@example.com",
                "jira-token",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void IsComplete_WithWhitespaceClockifyApiKey_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                "   ",
                "user@example.com",
                "jira-token",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void IsComplete_WithEmptyJiraUsername_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                "clockify-api-key",
                "",
                "jira-token",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void IsComplete_WithEmptyJiraApiToken_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                "clockify-api-key",
                "user@example.com",
                "",
                "tempo-key"
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void IsComplete_WithEmptyTempoApiKey_ShouldReturnFalse()
        {
            var config = new AppConfiguration(
                "clockify-api-key",
                "user@example.com",
                "jira-token",
                ""
            );
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void Empty_ShouldReturnConfigurationWithEmptyStrings()
        {
            var config = AppConfiguration.Empty;
            Assert.That(config.ClockifyApiKey, Is.EqualTo(string.Empty));
            Assert.That(config.JiraUsername, Is.EqualTo(string.Empty));
            Assert.That(config.JiraApiToken, Is.EqualTo(string.Empty));
            Assert.That(config.TempoApiKey, Is.EqualTo(string.Empty));
            Assert.That(config.IsComplete(), Is.False);
        }

        [Test]
        public void AppConfiguration_ShouldBeRecord_AllowingValueEquality()
        {
            var config1 = new AppConfiguration("key1", "user1", "token1", "tempo1");
            var config2 = new AppConfiguration("key1", "user1", "token1", "tempo1");
            var config3 = new AppConfiguration("key2", "user1", "token1", "tempo1");
            Assert.That(config1, Is.EqualTo(config2));
            Assert.That(config1, Is.Not.EqualTo(config3));
        }
    }
}
