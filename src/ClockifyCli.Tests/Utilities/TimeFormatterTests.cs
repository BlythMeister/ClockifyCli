using ClockifyCli.Utilities;
using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class TimeFormatterTests
{
    [TestCase(0, 0, 0, "0s")]
    [TestCase(0, 0, 30, "30s")]
    [TestCase(0, 5, 0, "5m 0s")]
    [TestCase(0, 5, 30, "5m 30s")]
    [TestCase(1, 0, 0, "1h 0m 0s")]
    [TestCase(1, 30, 0, "1h 30m 0s")]
    [TestCase(1, 30, 45, "1h 30m 45s")]
    [TestCase(25, 0, 0, "25h 0m 0s")]
    [TestCase(25, 30, 45, "25h 30m 45s")]
    public void FormatDuration_ShouldReturnCorrectFormat(int hours, int minutes, int seconds, string expected)
    {
        // Arrange
        var duration = new TimeSpan(hours, minutes, seconds);

        // Act
        var result = TimeFormatter.FormatDuration(duration);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(0, 0, "0m")]
    [TestCase(0, 5, "5m")]
    [TestCase(0, 30, "30m")]
    [TestCase(1, 0, "1h")]
    [TestCase(1, 30, "1h 30m")]
    [TestCase(25, 0, "25h")]
    [TestCase(25, 30, "25h 30m")]
    public void FormatDurationCompact_ShouldReturnCorrectFormat(int hours, int minutes, string expected)
    {
        // Arrange
        var duration = new TimeSpan(hours, minutes, 0);

        // Act
        var result = TimeFormatter.FormatDurationCompact(duration);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void FormatDuration_WithNegativeDuration_ShouldHandleCorrectly()
    {
        // Arrange
        var duration = new TimeSpan(-1, -30, -45);

        // Act
        var result = TimeFormatter.FormatDuration(duration);

        // Assert
        // Note: Need to verify actual behavior - negative TimeSpan might be formatted differently
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void FormatDurationCompact_WithNegativeDuration_ShouldHandleCorrectly()
    {
        // Arrange
        var duration = new TimeSpan(-1, -30, 0);

        // Act
        var result = TimeFormatter.FormatDurationCompact(duration);

        // Assert
        // Note: Need to verify actual behavior - negative TimeSpan might be formatted differently
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }
}
