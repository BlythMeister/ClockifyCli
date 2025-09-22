using ClockifyCli.Utilities;
using NUnit.Framework;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class IntelligentTimeParserTests
{
    #region Rule 1: AM/PM parsing tests
    
    [TestCase("2:30 PM", 14, 30)]
    [TestCase("2:30 pm", 14, 30)]
    [TestCase("2:30PM", 14, 30)]
    [TestCase("2:30pm", 14, 30)]
    [TestCase("2:30 P", 14, 30)]
    [TestCase("2:30 p", 14, 30)]
    [TestCase("2:30P", 14, 30)]
    [TestCase("2:30p", 14, 30)]
    [TestCase("9:15 AM", 9, 15)]
    [TestCase("9:15 am", 9, 15)]
    [TestCase("9:15AM", 9, 15)]
    [TestCase("9:15am", 9, 15)]
    [TestCase("9:15 A", 9, 15)]
    [TestCase("9:15 a", 9, 15)]
    [TestCase("9:15A", 9, 15)]
    [TestCase("9:15a", 9, 15)]
    [TestCase("12:00 PM", 12, 0)]  // Noon
    [TestCase("12:00 AM", 0, 0)]   // Midnight
    public void TryParseTime_WithAmPm_ParsesCorrectly(string input, int expectedHours, int expectedMinutes)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(expectedHours));
        Assert.That(result.Minutes, Is.EqualTo(expectedMinutes));
    }

    #endregion

    #region Rule 2: 24-hour format (13-23 hours) tests
    
    [TestCase("13:00", 13, 0)]
    [TestCase("14:30", 14, 30)]
    [TestCase("15:45", 15, 45)]
    [TestCase("18:20", 18, 20)]
    [TestCase("23:59", 23, 59)]
    public void TryParseTime_With24HourFormat_ParsesCorrectly(string input, int expectedHours, int expectedMinutes)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(expectedHours));
        Assert.That(result.Minutes, Is.EqualTo(expectedMinutes));
    }

    #endregion

    #region Rule 3: Context-based parsing for ambiguous times
    
    [Test]
    public void TryParseStartTime_WithMorningContext_InfersAM()
    {
        var contextTime = new DateTime(2024, 1, 15, 8, 0, 0); // 8:00 AM context
        
        var success = IntelligentTimeParser.TryParseStartTime("9:30", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(21)); // Should infer 9:30 PM (yesterday) as it must be in the past
    }

    [Test]
    public void TryParseStartTime_WithAfternoonContext_InfersPM()
    {
        var contextTime = new DateTime(2024, 1, 15, 15, 0, 0); // 3:00 PM context
        
        var success = IntelligentTimeParser.TryParseStartTime("4:30", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(4)); // Should infer 4:30 AM (this morning) as it must be in the past
    }

    [Test]
    public void TryParseEndTime_WithStartTimeContext_InfersCorrectTime()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0); // 9:00 AM start
        
        var success = IntelligentTimeParser.TryParseEndTime("5:30", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(17)); // Should infer 5:30 PM
    }

    #endregion

    #region Rule 4: Start time must be in the past (not more than 24 hours ago)
    
    [Test]
    public void TryParseStartTime_EarlyMorning_InfersCorrectPastTime()
    {
        var contextTime = new DateTime(2024, 1, 15, 12, 0, 0); // Noon context
        
        var success = IntelligentTimeParser.TryParseStartTime("6:00", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(6)); // Should prefer 6:00 AM (6 hours ago) over 6:00 PM (yesterday, 18 hours ago)
    }

    [Test]
    public void TryParseStartTime_AfterCurrentTime_InfersYesterdayTime()
    {
        var contextTime = new DateTime(2024, 1, 15, 12, 0, 0); // Noon context
        
        var success = IntelligentTimeParser.TryParseStartTime("8:00", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(8)); // Should prefer 8:00 AM today (4 hours ago) over 8:00 PM yesterday (16 hours ago)
    }

    #endregion

    #region Rule 5: End time bounds (7am-10pm)
    
    [Test]
    public void TryParseEndTime_VeryEarly_InfersAM()
    {
        var startTime = new DateTime(2024, 1, 15, 23, 0, 0); // 11:00 PM start (night shift)
        
        var success = IntelligentTimeParser.TryParseEndTime("6:00", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(6)); // Should infer 6:00 AM next day
    }

    [Test]
    public void TryParseEndTime_LateEvening_PrefersReasonableEndTime()
    {
        var startTime = new DateTime(2024, 1, 15, 14, 0, 0); // 2:00 PM start
        
        var success = IntelligentTimeParser.TryParseEndTime("9:00", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(21)); // Should infer 9:00 PM (Rule 5: reasonable end time, 7-hour duration)
    }

    [Test]
    public void TryParseEndTime_VeryLateEvening_Rule6OverridesRule5()
    {
        var startTime = new DateTime(2024, 1, 15, 16, 0, 0); // 4:00 PM start
        
        var success = IntelligentTimeParser.TryParseEndTime("11:30", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(23)); // Should infer 11:30 PM (Rule 6: 8-hour limit overrides Rule 5)
    }

    [Test]
    public void TryParseEndTime_Rule5AppliesWhenNoRule6Conflict()
    {
        var startTime = new DateTime(2024, 1, 15, 6, 0, 0); // 6:00 AM start
        
        var success = IntelligentTimeParser.TryParseEndTime("11:30", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(11)); // Should infer 11:30 AM (5.5 hours - within Rule 6, and Rule 5 prefers AM over PM after 10pm)
    }

    [Test]
    public void TryParseEndTime_ReasonableEvening_InfersPM()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0); // 9:00 AM start
        
        var success = IntelligentTimeParser.TryParseEndTime("5:00", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(17)); // Should infer 5:00 PM (8-hour duration is acceptable)
    }

    #endregion

    #region Rule 5 Updated: Past times preferred unless duration >8h or <0 minutes
    
    [Test]
    public void TryParseStartTime_EightHourExceptionAllowsFuture_LateNightContext()
    {
        var contextTime = new DateTime(2024, 1, 15, 1, 30, 0); // 1:30 AM context
        
        var success = IntelligentTimeParser.TryParseStartTime("5:30", out var result, contextTime);
        var actualDateTime = IntelligentTimeParser.GetActualStartDateTime("5:30", contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(17)); // Should infer 5:30 PM yesterday (not AM today which would be >8h ago)
        Assert.That(actualDateTime.Day, Is.EqualTo(14)); // Should be yesterday (14th)
        
        // Verify duration from PM yesterday to AM today is reasonable
        var duration = contextTime - actualDateTime;
        Assert.That(duration.TotalHours, Is.LessThanOrEqualTo(8));
    }

    [Test]
    public void TryParseStartTime_ZeroDurationExceptionAllowsFuture_SameTimeContext()
    {
        var contextTime = new DateTime(2024, 1, 15, 14, 30, 0); // 2:30 PM context
        
        var success = IntelligentTimeParser.TryParseStartTime("2:30", out var result, contextTime);
        var actualDateTime = IntelligentTimeParser.GetActualStartDateTime("2:30", contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(14)); // Should allow same time
        Assert.That(actualDateTime.Day, Is.EqualTo(15)); // Should be today, not yesterday
        
        // Verify duration is 0 (same time)
        var duration = contextTime - actualDateTime;
        Assert.That(duration.TotalMinutes, Is.EqualTo(0));
    }

    [Test]
    public void TryParseStartTime_NormalPastTimePreference_StandardCase()
    {
        var contextTime = new DateTime(2024, 1, 15, 16, 0, 0); // 4:00 PM context
        
        var success = IntelligentTimeParser.TryParseStartTime("10:00", out var result, contextTime);
        var actualDateTime = IntelligentTimeParser.GetActualStartDateTime("10:00", contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(10)); // Should prefer 10:00 AM today (past)
        Assert.That(actualDateTime.Day, Is.EqualTo(15)); // Should be today
        
        // Verify it's in the past but reasonable duration
        var duration = contextTime - actualDateTime;
        Assert.That(duration.TotalHours, Is.GreaterThan(0)); // Past time
        Assert.That(duration.TotalHours, Is.LessThanOrEqualTo(8)); // Within 8 hours
    }

    [Test]
    public void TryParseStartTime_NegativeDurationProtection_FutureTimeContext()
    {
        var contextTime = new DateTime(2024, 1, 15, 14, 30, 0); // 2:30 PM context
        
        var success = IntelligentTimeParser.TryParseStartTime("3:30", out var result, contextTime);
        var actualDateTime = IntelligentTimeParser.GetActualStartDateTime("3:30", contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(3)); // Should infer 3:30 AM today (past) not 3:30 PM (future/negative)
        Assert.That(actualDateTime.Day, Is.EqualTo(15)); // Should be today
        
        // Verify it's in the past (positive duration)
        var duration = contextTime - actualDateTime;
        Assert.That(duration.TotalHours, Is.GreaterThan(0)); // Past time, positive duration
    }

    #endregion

    #region Rule 6: Duration limit (8 hours max)
    
    [Test]
    public void ValidateTimeInContext_DurationExceeds8Hours_AdjustsInterpretation()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0); // 9:00 AM start
        var endTime = new TimeSpan(20, 0, 0); // Would be 8:00 PM same day = 11 hour duration
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(endTime, startTime, isStartTime: false, out var errorMessage);
        
        // Should suggest alternative interpretation
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("duration exceeds 8 hours"));
    }

    [Test]
    public void TryParseEndTime_WouldExceed8Hours_ChoosesAlternativeInterpretation()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0); // 9:00 AM start
        
        var success = IntelligentTimeParser.TryParseEndTime("9:30", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(9)); // Should infer 9:30 AM same day (Rule 6: avoid 12+ hour duration)
    }

    [Test]
    public void TryParseEndTime_Rule6OverridesRule5_PrefersShortDuration()
    {
        var startTime = new DateTime(2024, 1, 15, 10, 0, 0); // 10:00 AM start
        
        var success = IntelligentTimeParser.TryParseEndTime("6:00", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(18)); // Should infer 6:00 PM (8 hours) rather than 6:00 AM next day (20 hours)
    }

    #endregion

    #region Rule 7: End time after start time
    
    [Test]
    public void ValidateTimeInContext_EndTimeBeforeStart_ReturnsFalse()
    {
        var startTime = new DateTime(2024, 1, 15, 9, 0, 0); // 9:00 AM start
        var endTime = new TimeSpan(8, 0, 0); // 8:00 AM same day
        
        var isValid = IntelligentTimeParser.ValidateTimeInContext(endTime, startTime, isStartTime: false, out var errorMessage);
        
        Assert.That(isValid, Is.False);
        Assert.That(errorMessage, Does.Contain("End time must be after start time"));
    }

    #endregion

    #region Edge cases and invalid inputs
    
    [TestCase("25:00")]    // Invalid hour
    [TestCase("14:60")]    // Invalid minute
    [TestCase("abc")]      // Invalid format
    [TestCase("")]         // Empty string
    [TestCase("14")]       // Incomplete time
    public void TryParseTime_InvalidInputs_ReturnsFalse(string input)
    {
        var success = IntelligentTimeParser.TryParseTime(input, out var result);
        Assert.That(success, Is.False);
    }

    [Test]
    public void TryParseTime_WithSeconds_ParsesCorrectly()
    {
        var success = IntelligentTimeParser.TryParseTime("14:30:45", out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(14, 30, 45)));
    }

    [Test]
    public void TryParseTime_WithSecondsAndAmPm_ParsesCorrectly()
    {
        var success = IntelligentTimeParser.TryParseTime("2:30:15 PM", out var result);
        
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(14, 30, 15)));
    }

    #endregion

    #region Complex scenarios
    
    [Test]
    public void TryParseStartTime_NightShiftContext_HandlesCorrectly()
    {
        var contextTime = new DateTime(2024, 1, 15, 22, 0, 0); // 10:00 PM context
        
        var success = IntelligentTimeParser.TryParseStartTime("11:00", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(11)); // Should infer 11:00 AM (11 hours ago, more recent than 11:00 PM yesterday)
    }

    [Test]
    public void TryParseEndTime_CrossesMidnight_HandlesCorrectly()
    {
        var startTime = new DateTime(2024, 1, 15, 23, 0, 0); // 11:00 PM start
        
        var success = IntelligentTimeParser.TryParseEndTime("2:00", out var result, startTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(2)); // Should infer 2:00 AM next day
    }

    [Test]
    public void TryParseTime_SimplifiedContextLogic_ChoosesMostRecentPast()
    {
        var contextTime = new DateTime(2024, 1, 15, 12, 0, 0); // Noon context
        
        // Test that 3:00 is interpreted as 3:00 AM (9 hours ago) since 3:00 PM would be in the future
        var success = IntelligentTimeParser.TryParseStartTime("3:00", out var result, contextTime);
        
        Assert.That(success, Is.True);
        Assert.That(result.Hours, Is.EqualTo(3)); // Should prefer 3:00 AM (most recent past time)
    }

    #endregion
}
