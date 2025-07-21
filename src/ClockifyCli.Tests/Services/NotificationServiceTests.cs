using ClockifyCli.Services;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace ClockifyCli.Tests.Services;

[TestFixture]
public class NotificationServiceTests
{
    [Test]
    public void ShowTimerReminderNotification_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw even if PowerShell fails
        Assert.DoesNotThrow(() => NotificationService.ShowTimerReminderNotification());
    }

    [Test]
    public void ShowTimerRunningNotification_WithValidParameters_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw even if PowerShell fails
        Assert.DoesNotThrow(() => NotificationService.ShowTimerRunningNotification(
            "Test Project",
            "Test Task",
            TimeSpan.FromMinutes(30)
        ));
    }

    [Test]
    public void ShowTimerRunningNotification_WithLongElapsedTime_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw even if PowerShell fails
        Assert.DoesNotThrow(() => NotificationService.ShowTimerRunningNotification(
            "Test Project",
            "Test Task",
            TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(45))
        ));
    }

    [Test]
    public void ShowTimerRunningNotification_WithSpecialCharacters_ShouldNotThrowException()
    {
        // Act & Assert - Should handle special characters without throwing
        Assert.DoesNotThrow(() => NotificationService.ShowTimerRunningNotification(
            "Project with 'quotes' & symbols",
            "Task-123: Fix bug (urgent)",
            TimeSpan.FromMinutes(15)
        ));
    }

    // Note: FormatElapsedTime is private, so we can't test it directly.
    // The behavior is tested through the public methods above.
}
