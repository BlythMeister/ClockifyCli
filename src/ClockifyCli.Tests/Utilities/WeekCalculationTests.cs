using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class WeekCalculationTests
{
    // Test data for week calculation - format: weekStartDay, referenceDate, expectedWeekStart, expectedWeekEnd
    private static readonly object[] WeekCalculationTestCases =
    {
        // Monday as week start
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 21), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Monday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 22), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Tuesday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Wednesday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 24), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Thursday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 25), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Friday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Saturday
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 27), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Sunday

        // Sunday as week start
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 20), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Sunday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 21), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Monday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 22), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Tuesday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Wednesday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 24), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Thursday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 25), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Friday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Saturday

        // Wednesday as week start
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Wednesday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 24), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Thursday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 25), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Friday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Saturday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 27), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Sunday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 28), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Monday
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 29), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Tuesday

        // Friday as week start
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 25), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Friday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Saturday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 27), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Sunday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 28), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Monday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 29), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Tuesday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 30), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Wednesday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 31), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Thursday

        // Saturday as week start
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Saturday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 27), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Sunday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 28), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Monday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 29), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Tuesday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 30), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Wednesday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 31), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Thursday
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 8, 1), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Friday
    };

    [TestCaseSource(nameof(WeekCalculationTestCases))]
    public void CalculateWeekRange_WithDifferentWeekStartDays_ReturnsCorrectRange(
        DayOfWeek weekStartDay, DateTime referenceDate, DateTime expectedWeekStart, DateTime expectedWeekEnd)
    {
        // Act
        var (actualWeekStart, actualWeekEnd) = CalculateWeekRange(referenceDate, weekStartDay);

        // Assert
        Assert.That(actualWeekStart.Date, Is.EqualTo(expectedWeekStart.Date), 
            $"Week start incorrect for {weekStartDay} week starting from {referenceDate:yyyy-MM-dd dddd}");
        Assert.That(actualWeekEnd.Date, Is.EqualTo(expectedWeekEnd.Date), 
            $"Week end incorrect for {weekStartDay} week starting from {referenceDate:yyyy-MM-dd dddd}");
    }

    [Test]
    public void CalculateWeekRange_EdgeCaseMonthBoundary_HandlesCorrectly()
    {
        // Arrange - Test month boundary (July 31 to August 1)
        var referenceDate = new DateTime(2025, 7, 31); // Thursday
        var weekStartDay = DayOfWeek.Tuesday;

        // Act
        var (weekStart, weekEnd) = CalculateWeekRange(referenceDate, weekStartDay);

        // Assert
        Assert.That(weekStart.Date, Is.EqualTo(new DateTime(2025, 7, 29).Date)); // Previous Tuesday
        Assert.That(weekEnd.Date, Is.EqualTo(new DateTime(2025, 8, 4).Date)); // Next Monday
    }

    [Test]
    public void CalculateWeekRange_EdgeCaseYearBoundary_HandlesCorrectly()
    {
        // Arrange - Test year boundary (December 31 to January 1)
        var referenceDate = new DateTime(2025, 12, 31); // Wednesday
        var weekStartDay = DayOfWeek.Monday;

        // Act
        var (weekStart, weekEnd) = CalculateWeekRange(referenceDate, weekStartDay);

        // Assert
        Assert.That(weekStart.Date, Is.EqualTo(new DateTime(2025, 12, 29).Date)); // Previous Monday
        Assert.That(weekEnd.Date, Is.EqualTo(new DateTime(2026, 1, 4).Date)); // Next Sunday
    }

    /// <summary>
    /// Helper method that replicates the week calculation logic from WeekViewCommand
    /// This should match the implementation in the actual command
    /// </summary>
    private static (DateTime weekStart, DateTime weekEnd) CalculateWeekRange(DateTime referenceDate, DayOfWeek weekStartDay)
    {
        var today = referenceDate.Date;
        var daysSinceWeekStart = ((int)today.DayOfWeek - (int)weekStartDay + 7) % 7;
        var weekStart = today.AddDays(-daysSinceWeekStart);
        var weekEnd = weekStart.AddDays(6);

        return (weekStart, weekEnd);
    }

    // Test data for day name conversion
    private static readonly object[] DayNameTestCases =
    {
        new object[] { DayOfWeek.Monday, "Sunday" },
        new object[] { DayOfWeek.Tuesday, "Monday" },
        new object[] { DayOfWeek.Wednesday, "Tuesday" },
        new object[] { DayOfWeek.Thursday, "Wednesday" },
        new object[] { DayOfWeek.Friday, "Thursday" },
        new object[] { DayOfWeek.Saturday, "Friday" },
        new object[] { DayOfWeek.Sunday, "Saturday" }
    };

    [TestCaseSource(nameof(DayNameTestCases))]
    public void GetEndDayName_WithDifferentWeekStartDays_ReturnsCorrectEndDay(DayOfWeek weekStartDay, string expectedEndDayName)
    {
        // Act
        var actualEndDayName = GetEndDayName(weekStartDay);

        // Assert
        Assert.That(actualEndDayName, Is.EqualTo(expectedEndDayName));
    }

    /// <summary>
    /// Helper method that replicates the GetEndDayName logic from WeekViewCommand
    /// This should match the implementation in the actual command
    /// </summary>
    private static string GetEndDayName(DayOfWeek weekStartDay)
    {
        var endDayOfWeek = (DayOfWeek)(((int)weekStartDay + 6) % 7);
        return endDayOfWeek.ToString();
    }
}
