using ClockifyCli.Models;
using NUnit.Framework;

namespace ClockifyCli.Tests.Models;

[TestFixture]
public class TimeIntervalTests
{
    [Test]
    public void StartDate_ShouldParseUtcDateCorrectly()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "2024-01-15T10:30:00.000Z");

        // Act
        var startDate = timeInterval.StartDate;

        // Assert
        Assert.That(startDate, Is.EqualTo(new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc)));
        Assert.That(startDate.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void EndDate_ShouldParseUtcDateCorrectly()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "2024-01-15T10:30:00.000Z");

        // Act
        var endDate = timeInterval.EndDate;

        // Assert
        Assert.That(endDate, Is.EqualTo(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)));
        Assert.That(endDate.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void DurationSpan_WithValidEndTime_ShouldCalculateCorrectDuration()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "2024-01-15T10:45:30.000Z");

        // Act
        var duration = timeInterval.DurationSpan;

        // Assert
        Assert.That(duration, Is.EqualTo(new TimeSpan(2, 15, 30)));
    }

    [Test]
    public void DurationSpan_WithNullEndTime_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", null!);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = timeInterval.DurationSpan);
        Assert.That(ex.Message, Is.EqualTo("Cannot calculate duration for a running time entry. Use DateTime.UtcNow - StartDate instead."));
    }

    [Test]
    public void DurationSpan_WithEmptyEndTime_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = timeInterval.DurationSpan);
        Assert.That(ex.Message, Is.EqualTo("Cannot calculate duration for a running time entry. Use DateTime.UtcNow - StartDate instead."));
    }

    [Test]
    public void IsRunning_WithNullEndTime_ShouldReturnTrue()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", null!);

        // Act & Assert
        Assert.That(timeInterval.IsRunning, Is.True);
    }

    [Test]
    public void IsRunning_WithEmptyEndTime_ShouldReturnTrue()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "");

        // Act & Assert
        Assert.That(timeInterval.IsRunning, Is.True);
    }

    [Test]
    public void IsRunning_WithValidEndTime_ShouldReturnFalse()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "2024-01-15T10:30:00.000Z");

        // Act & Assert
        Assert.That(timeInterval.IsRunning, Is.False);
    }

    [Test]
    public void StartDate_WithDifferentTimeZoneFormats_ShouldParseCorrectly()
    {
        // Arrange & Act & Assert
        var utcInterval = new TimeInterval("2024-01-15T08:30:00.000Z", "2024-01-15T10:30:00.000Z");
        Assert.That(utcInterval.StartDate, Is.EqualTo(new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc)));

        var offsetInterval = new TimeInterval("2024-01-15T08:30:00.000+00:00", "2024-01-15T10:30:00.000+00:00");
        Assert.That(offsetInterval.StartDate, Is.EqualTo(new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void DurationSpan_WithSameDateDifferentTimes_ShouldCalculateCorrectly()
    {
        // Arrange
        var timeInterval = new TimeInterval("2024-01-15T23:45:00.000Z", "2024-01-16T01:15:00.000Z");

        // Act
        var duration = timeInterval.DurationSpan;

        // Assert
        Assert.That(duration, Is.EqualTo(new TimeSpan(1, 30, 0)));
    }
}
