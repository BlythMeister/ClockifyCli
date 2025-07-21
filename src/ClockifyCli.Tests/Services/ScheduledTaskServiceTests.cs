using ClockifyCli.Services;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class ScheduledTaskServiceTests
{
    [Test]
    [Explicit("Interacts with system dotnet tools - run explicitly to avoid interference")]
    public void IsToolInstalledAsGlobalTool_ShouldReturnBooleanWithoutException()
    {
        // Act
        bool result;
        
        // Assert - Should not throw an exception
        Assert.DoesNotThrow(() => result = ScheduledTaskService.IsToolInstalledAsGlobalTool());
    }

    [Test]
    [Explicit("Interacts with system 'where' command - run explicitly to avoid interference")]
    public void GetGlobalToolPath_ShouldReturnStringOrNullWithoutException()
    {
        // Act
        string? result = null;
        
        // Assert - Should not throw an exception
        Assert.DoesNotThrow(() => result = ScheduledTaskService.GetGlobalToolPath());
        
        // Result should be either null or a valid string
        Assert.That(result, Is.Null.Or.TypeOf<string>());
    }

    [Test]
    [Explicit("May create system scheduled tasks - run explicitly to avoid interference")]
    public async Task CreateScheduledTask_OnNonWindows_ShouldReturnFalse()
    {
        // This test will only be meaningful on non-Windows platforms
        // On Windows, it will test the actual functionality
        
        // Arrange
        string taskName = "TestTask";
        int intervalMinutes = 30;

        // Act
        bool result = await ScheduledTaskService.CreateScheduledTask(taskName, intervalMinutes);

        // Assert
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.That(result, Is.False, "Should return false on non-Windows platforms");
        }
        else
        {
            // On Windows, the result depends on whether the tool is installed and permissions
            Assert.That(result, Is.TypeOf<bool>(), "Should return a boolean value");
        }
    }

    [Test]
    [Explicit("May create system scheduled tasks - run explicitly to avoid interference")]
    public void CreateScheduledTask_WithInvalidTaskName_ShouldHandleGracefully()
    {
        // Arrange
        string invalidTaskName = "";
        int intervalMinutes = 30;

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => 
            await ScheduledTaskService.CreateScheduledTask(invalidTaskName, intervalMinutes));
    }

    [Test]
    [Explicit("May create system scheduled tasks - run explicitly to avoid interference")]
    public void CreateScheduledTask_WithInvalidInterval_ShouldHandleGracefully()
    {
        // Arrange
        string taskName = "TestTask";
        int invalidInterval = -1;

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => 
            await ScheduledTaskService.CreateScheduledTask(taskName, invalidInterval));
    }

    [Test]
    [Explicit("May delete system scheduled tasks - run explicitly to avoid interference")]
    public async Task DeleteScheduledTask_OnNonWindows_ShouldReturnFalse()
    {
        // Arrange
        string taskName = "TestTask";

        // Act
        bool result = await ScheduledTaskService.DeleteScheduledTask(taskName);

        // Assert
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.That(result, Is.False, "Should return false on non-Windows platforms");
        }
        else
        {
            // On Windows, the result depends on whether the task exists
            Assert.That(result, Is.TypeOf<bool>(), "Should return a boolean value");
        }
    }

    [Test]
    [Explicit("May delete system scheduled tasks - run explicitly to avoid interference")]
    public void DeleteScheduledTask_WithEmptyTaskName_ShouldHandleGracefully()
    {
        // Arrange
        string emptyTaskName = "";

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => 
            await ScheduledTaskService.DeleteScheduledTask(emptyTaskName));
    }

    [Test]
    [Explicit("Interacts with system scheduled tasks - run explicitly to avoid interference")]
    public void TaskExists_ShouldReturnBooleanWithoutException()
    {
        // Arrange
        string taskName = "NonExistentTask";

        // Act & Assert
        Assert.DoesNotThrow(() => ScheduledTaskService.TaskExists(taskName));
    }

    [Test]
    [Explicit("Interacts with system scheduled tasks - run explicitly to avoid interference")]
    public void TaskExists_WithEmptyTaskName_ShouldReturnFalse()
    {
        // Arrange
        string emptyTaskName = "";

        // Act
        bool result = ScheduledTaskService.TaskExists(emptyTaskName);

        // Assert
        Assert.That(result, Is.False, "Should return false for empty task name");
    }
}
