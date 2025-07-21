using ClockifyCli.Models;
using ClockifyCli.Services;
using NUnit.Framework;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class ConfigurationServiceTests
{
    private string testConfigDirectory = null!;
    private ConfigurationService configService = null!;

    [SetUp]
    public void Setup()
    {
        // Create a unique test directory for each test
        testConfigDirectory = Path.Combine(Path.GetTempPath(), "ClockifyCli.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testConfigDirectory);
        
        // Use the custom directory constructor
        configService = new ConfigurationService(testConfigDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test directory
        if (Directory.Exists(testConfigDirectory))
        {
            Directory.Delete(testConfigDirectory, true);
        }
    }

    [Test]
    public async Task LoadConfigurationAsync_WithNonExistentFile_ShouldReturnEmptyConfiguration()
    {
        // Act
        var config = await configService.LoadConfigurationAsync();

        // Assert
        Assert.That(config, Is.EqualTo(AppConfiguration.Empty));
    }

    [Test]
    public async Task SaveAndLoadConfigurationAsync_ShouldPersistConfiguration()
    {
        // Arrange
        var originalConfig = new AppConfiguration(
            "test-clockify-key",
            "test@example.com",
            "test-jira-token",
            "test-tempo-key"
        );

        // Act
        await configService.SaveConfigurationAsync(originalConfig);
        var loadedConfig = await configService.LoadConfigurationAsync();

        // Assert
        Assert.That(loadedConfig, Is.EqualTo(originalConfig));
    }

    [Test]
    public async Task UpdateConfigurationAsync_WithPartialUpdate_ShouldUpdateOnlySpecifiedFields()
    {
        // Arrange
        var initialConfig = new AppConfiguration(
            "initial-clockify-key",
            "initial@example.com",
            "initial-jira-token",
            "initial-tempo-key"
        );
        await configService.SaveConfigurationAsync(initialConfig);

        // Act
        var updatedConfig = await configService.UpdateConfigurationAsync(
            clockifyApiKey: "updated-clockify-key",
            jiraUsername: "updated@example.com"
        );

        // Assert
        Assert.That(updatedConfig.ClockifyApiKey, Is.EqualTo("updated-clockify-key"));
        Assert.That(updatedConfig.JiraUsername, Is.EqualTo("updated@example.com"));
        Assert.That(updatedConfig.JiraApiToken, Is.EqualTo("initial-jira-token"));
        Assert.That(updatedConfig.TempoApiKey, Is.EqualTo("initial-tempo-key"));
    }

    [Test]
    public async Task UpdateConfigurationAsync_WithNoExistingConfig_ShouldCreateNewConfig()
    {
        // Act - Only update clockify key, leave others as null (which should preserve empty strings from AppConfiguration.Empty)
        var updatedConfig = await configService.UpdateConfigurationAsync(
            clockifyApiKey: "new-clockify-key"
        );

        // Assert
        Assert.That(updatedConfig.ClockifyApiKey, Is.EqualTo("new-clockify-key"));
        // These should remain empty since they weren't provided and no config existed before
        Assert.That(updatedConfig.JiraUsername, Is.EqualTo(string.Empty));
        Assert.That(updatedConfig.JiraApiToken, Is.EqualTo(string.Empty));
        Assert.That(updatedConfig.TempoApiKey, Is.EqualTo(string.Empty));
        
        // Verify the config was actually saved by loading it again
        var reloadedConfig = await configService.LoadConfigurationAsync();
        Assert.That(reloadedConfig, Is.EqualTo(updatedConfig));
    }

    [Test]
    public void ConfigurationExists_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange - Create a test config file
        var config = new AppConfiguration("test", "test", "test", "test");
        configService.SaveConfigurationAsync(config).Wait();

        // Act & Assert
        Assert.That(configService.ConfigurationExists(), Is.True);
    }

    [Test]
    public void ConfigurationExists_WithNonExistentFile_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.That(configService.ConfigurationExists(), Is.False);
    }

    [Test]
    public void GetConfigurationPath_ShouldReturnValidPath()
    {
        // Act
        var path = configService.GetConfigurationPath();

        // Assert
        Assert.That(path, Is.Not.Null);
        Assert.That(path, Is.Not.Empty);
        Assert.That(path, Does.EndWith("clockify-config.dat"));
        Assert.That(path, Does.Contain(testConfigDirectory));
    }

    [Test]
    public void SaveConfigurationAsync_WithValidConfig_ShouldNotThrow()
    {
        // This test verifies that the method handles normal cases correctly
        var config = new AppConfiguration("test", "test", "test", "test");
        
        // Act & Assert - Should not throw for valid config
        Assert.DoesNotThrowAsync(async () => await configService.SaveConfigurationAsync(config));
    }

    [Test]
    public void DefaultConstructor_ShouldUseAppDataDirectory()
    {
        // Arrange & Act
        var defaultConfigService = new ConfigurationService();
        var path = defaultConfigService.GetConfigurationPath();

        // Assert
        var expectedBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClockifyCli");
        Assert.That(path, Does.StartWith(expectedBasePath));
        Assert.That(path, Does.EndWith("clockify-config.dat"));
    }

    [Test]
    public void CustomDirectoryConstructor_ShouldUseProvidedDirectory()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), "custom-test-dir");
        var customConfigService = new ConfigurationService(customDir);

        // Act
        var path = customConfigService.GetConfigurationPath();

        // Assert
        Assert.That(path, Does.StartWith(customDir));
        Assert.That(path, Does.EndWith("clockify-config.dat"));
    }
}
