using ClockifyCli.Utilities;
using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class IntelligentTimeParserTests
{
    [TestCase("14:30", ExpectedResult = true)]
    [TestCase("09:15", ExpectedResult = true)]
    [TestCase("23:45", ExpectedResult = true)]
    [TestCase("00:00", ExpectedResult = true)]
    [TestCase("12:00", ExpectedResult = true)]
    public bool TryParseTime_ValidTimes_ReturnsTrue(string input)
    {
        return IntelligentTimeParser.TryParseTime(input, out var result);
    }

    [TestCase("25:00", ExpectedResult = false)]
    [TestCase("14:60", ExpectedResult = false)]
    [TestCase("14:30:60", ExpectedResult = false)]
    [TestCase("abc", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase("14", ExpectedResult = false)]
    [TestCase("2:30 PM", ExpectedResult = false)] // No AM/PM allowed in simplified version
    public bool TryParseTime_InvalidTimes_ReturnsFalse(string input)
    {
        return IntelligentTimeParser.TryParseTime(input, out var result);
    }

    [Test]
    public void TryParseTime_ValidTime_ReturnsCorrectTimeSpan()
    {
        var success = IntelligentTimeParser.TryParseTime("14:30", out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(14, 30, 0)));
    }

    [Test]
    public void TryParseTime_WithSeconds_ReturnsCorrectTimeSpan()
    {
        var success = IntelligentTimeParser.TryParseTime("14:30:45", out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(14, 30, 45)));
    }

    [Test]
    public void ValidateTimeInContext_EndTimeAfterStart_ReturnsTrue()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
        var endTime = new TimeSpan(17, 0, 0);
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.True);
        Assert.That(errorMessage, Is.Empty);
    }

    [Test]
    public void ValidateTimeInContext_EndTimeBeforeStart_ReturnsFalse()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
        var endTime = new TimeSpan(8, 0, 0);
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("End time must be after start time"));
    }

    [Test]
    public void IsAmbiguousTime_AlwaysReturnsFalse()
    {
        Assert.That(IntelligentTimeParser.IsAmbiguousTime("14:30"), Is.False);
        Assert.That(IntelligentTimeParser.IsAmbiguousTime("9:15"), Is.False);
        Assert.That(IntelligentTimeParser.IsAmbiguousTime("2:30 PM"), Is.False);
    }
}
