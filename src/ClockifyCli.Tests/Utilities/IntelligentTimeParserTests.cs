using System;
using ClockifyCli.Utilities;
using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class IntelligentTimeParserTests
{
    [TestCase("14:30", ExpectedResult = "14:30:00")]
    [TestCase("09:15", ExpectedResult = "09:15:00")]
    [TestCase("23:59", ExpectedResult = "23:59:00")]
    [TestCase("00:00", ExpectedResult = "00:00:00")]
    public string TryParseTime_24HourFormat_ParsesCorrectly(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        Assert.That(success, Is.True);
        return result.ToString();
    }

    [TestCase("2:30 PM", ExpectedResult = "14:30:00")]
    [TestCase("2:30 pm", ExpectedResult = "14:30:00")]
    [TestCase("10:15 AM", ExpectedResult = "10:15:00")]
    [TestCase("12:00 PM", ExpectedResult = "12:00:00")]
    [TestCase("12:00 AM", ExpectedResult = "00:00:00")]
    [TestCase("11:59 PM", ExpectedResult = "23:59:00")]
    public string TryParseTime_12HourFormatWithAmPm_ParsesCorrectly(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        Assert.That(success, Is.True);
        return result.ToString();
    }

    [Test]
    public void TryParseTime_AmbiguousTimeWithWorkHoursContext_ChoosesPmDuringWorkDay()
    {
        // Context: 2:00 PM (14:00)
        var contextTime = new DateTime(2024, 1, 15, 14, 0, 0);
        
        // Input: "3:30" (could be 3:30 AM or 3:30 PM)
        var success = IntelligentTimeParser.TryParseTime("3:30", out var result, contextTime, isStartTime: true);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(15, 30, 0))); // Should choose 3:30 PM
    }

    [Test]
    public void TryParseTime_AmbiguousTimeWithMorningContext_ChoosesAm()
    {
        // Context: 8:00 AM
        var contextTime = new DateTime(2024, 1, 15, 8, 0, 0);
        
        // Input: "7:30" (for start time, should be 7:30 AM as it's before context)
        var success = IntelligentTimeParser.TryParseTime("7:30", out var result, contextTime, isStartTime: true);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(7, 30, 0))); // Should choose 7:30 AM
    }

    [Test]
    public void TryParseTime_EndTimeAfterMorningStart_ChoosesPm()
    {
        // Context: 9:00 AM start time
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
        
        // Input: "5:30" for end time - should be 5:30 PM
        var success = IntelligentTimeParser.TryParseTime("5:30", out var result, startTime, isStartTime: false);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(17, 30, 0))); // Should choose 5:30 PM
    }

    [Test]
    public void TryParseTime_EndTimeAfterAfternoonStart_ChoosesPm()
    {
        // Context: 2:00 PM start time
        var startTime = new DateTime(2024, 1, 15, 14, 0, 0);
        
        // Input: "4:00" for end time - should be 4:00 PM
        var success = IntelligentTimeParser.TryParseTime("4:00", out var result, startTime, isStartTime: false);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(16, 0, 0))); // Should choose 4:00 PM
    }

    [TestCase("1:30")]
    [TestCase("9:15")]
    [TestCase("11:45")]
    public void TryParseTime_NoContext_DefaultsToReasonableHours(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        
        Assert.That(success, Is.True);
        // Without context, should make reasonable assumptions
        Assert.That(result.Hours, Is.InRange(0, 23));
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("25:00")]
    [TestCase("12:60")]
    [TestCase("abc")]
    [TestCase("13:30 PM")] // Invalid - 13 with PM
    [TestCase("0:30 PM")]  // Invalid - 0 with PM
    public void TryParseTime_InvalidInput_ReturnsFalse(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        Assert.That(success, Is.False);
    }

    [Test]
    public void TryParseStartTime_WithCurrentTime_ParsesCorrectly()
    {
        var currentTime = new DateTime(2024, 1, 15, 14, 30, 0);
        
        var success = IntelligentTimeParser.TryParseStartTime("2:00", out var result, currentTime);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(14, 0, 0))); // Should be 2:00 PM
    }

    [Test]
    public void TryParseEndTime_WithStartTime_ParsesCorrectly()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
        
        var success = IntelligentTimeParser.TryParseEndTime("5:30", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(17, 30, 0))); // Should be 5:30 PM
    }

    [Test]
    public void ValidateTimeInContext_StartTimeTooFarInFuture_ReturnsError()
    {
        var currentTime = new DateTime(2024, 1, 15, 14, 0, 0);
        var futureTime = new TimeSpan(16, 0, 0); // 2 hours in future
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(
            futureTime, currentTime, isStartTime: true, out var errorMessage);
        
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("future"));
    }

    [Test]
    public void ValidateTimeInContext_EndTimeBeforeStart_ReturnsError()
    {
        var startTime = new DateTime(2024, 1, 15, 14, 0, 0);
        var endTime = new TimeSpan(13, 0, 0); // Before start time
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(
            endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("after start time"));
    }

    [Test]
    public void ValidateTimeInContext_ReasonableWorkSession_ReturnsTrue()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
        var endTime = new TimeSpan(17, 0, 0); // 8-hour work day
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(
            endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.True);
        Assert.That(errorMessage, Is.Empty);
    }

    [Test]
    public void ValidateTimeInContext_TooLongWorkSession_ReturnsError()
    {
        var startTime = new DateTime(2024, 1, 15, 8, 0, 0);
        var endTime = new TimeSpan(2, 0, 0); // Next day 2 AM (18 hours)
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(
            endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("16 hours"));
    }

    [TestCase("2:30:45 PM", ExpectedResult = "14:30:45")]
    [TestCase("10:15:30", ExpectedResult = "10:15:30")]
    public string TryParseTime_WithSeconds_ParsesCorrectly(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        Assert.That(success, Is.True);
        return result.ToString();
    }

    [Test]
    public void TryParseTime_EarlyMorningWithEveningContext_IntelligentlyChoosesAm()
    {
        // Context: 6:00 PM
        var contextTime = new DateTime(2024, 1, 15, 18, 0, 0);
        
        // Input: "6:00" - for start time, should be 6:00 AM (reasonable start of day)
        var success = IntelligentTimeParser.TryParseTime("6:00", out var result, contextTime, isStartTime: true);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(6, 0, 0))); // Should choose 6:00 AM
    }

    [Test]
    public void TryParseTime_LunchTimeHour_IntelligentlyChoosesPm()
    {
        // Context: 11:00 AM  
        var contextTime = new DateTime(2024, 1, 15, 11, 0, 0);
        
        // Input: "12:30" - should be 12:30 PM (lunch time)
        var success = IntelligentTimeParser.TryParseTime("12:30", out var result, contextTime, isStartTime: false);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(12, 30, 0))); // Should choose 12:30 PM
    }
}
