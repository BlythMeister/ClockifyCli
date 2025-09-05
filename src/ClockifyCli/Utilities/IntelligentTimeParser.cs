using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ClockifyCli.Utilities;

/// <summary>
/// Intelligent time parser that handles AM/PM, 24-hour format, and context-aware interpretation.
/// </summary>
public static class IntelligentTimeParser
{
    // Regex patterns for different time formats
    private static readonly Regex TimeWithAmPmRegex = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(AM|PM|A|P)$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex SimpleTimeRegex = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", 
        RegexOptions.Compiled);

    /// <summary>
    /// Parse result with ambiguity information
    /// </summary>
    public class ParseResult
    {
        public TimeSpan Time { get; set; }
        public bool IsAmbiguous { get; set; }
        public string OriginalInput { get; set; } = "";
        public string ReasonForInterpretation { get; set; } = "";
    }

    /// <summary>
    /// Attempts to parse a time string using intelligent rules.
    /// </summary>
    /// <param name="input">The time input string</param>
    /// <param name="result">The parsed TimeSpan result</param>
    /// <param name="contextTime">Context time for disambiguation</param>
    /// <param name="isStartTime">Whether this is a start time (affects working hours bias)</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParseTime(string input, out TimeSpan result, DateTime? contextTime = null, bool isStartTime = true)
    {
        result = default;
        
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // Rule 1: If time has AM/PM/A/P, use that to calculate the correct time
        var amPmMatch = TimeWithAmPmRegex.Match(input);
        if (amPmMatch.Success)
        {
            return TryParseWithAmPm(amPmMatch, out result);
        }

        // Check if it's a simple time format
        var simpleMatch = SimpleTimeRegex.Match(input);
        if (!simpleMatch.Success)
            return false;

        if (!int.TryParse(simpleMatch.Groups[1].Value, out var hours) ||
            !int.TryParse(simpleMatch.Groups[2].Value, out var minutes))
            return false;

        var seconds = 0;
        if (simpleMatch.Groups[3].Success && !int.TryParse(simpleMatch.Groups[3].Value, out seconds))
            return false;

        // Validate ranges
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return false;

        // Rule 2: If hours are 13-23, we know this is 24-hour format (PM)
        if (hours >= 13 && hours <= 23)
        {
            result = new TimeSpan(hours, minutes, seconds);
            return true;
        }

        // For hours 0-12, we need to use context and working hours logic
        result = DetermineAmOrPm(hours, minutes, seconds, contextTime, isStartTime);
        return true;
    }

    /// <summary>
    /// Parses time input specifically for start times with context awareness.
    /// </summary>
    public static bool TryParseStartTime(string input, out TimeSpan result, DateTime currentTime)
    {
        return TryParseTime(input, out result, currentTime, isStartTime: true);
    }

    /// <summary>
    /// Parses time input specifically for end times with start time context.
    /// </summary>
    public static bool TryParseEndTime(string input, out TimeSpan result, DateTime startTime)
    {
        return TryParseTime(input, out result, startTime, isStartTime: false);
    }

    /// <summary>
    /// Validates parsed time in context and checks for logical issues.
    /// </summary>
    public static bool ValidateTimeInContext(TimeSpan parsedTime, DateTime? contextTime, bool isStartTime, out string errorMessage)
    {
        errorMessage = "";

        if (contextTime == null)
            return true;

        if (!isStartTime)
        {
            // Rule 7: Timer cannot end before it has started
            var startTime = contextTime.Value.TimeOfDay;
            var endTime = parsedTime;
            
            // Handle next-day scenarios (e.g., start at 11 PM, end at 2 AM)
            if (endTime < startTime)
            {
                // Check if this could be next day and if duration is reasonable
                var nextDayDuration = (TimeSpan.FromDays(1) - startTime) + endTime;
                
                // If this would be a very long duration (like 23 hours), it's likely an error
                if (nextDayDuration.TotalHours > 16)
                {
                    errorMessage = "End time must be after start time. Please check your time input.";
                    return false;
                }
                
                // Rule 6: Duration should not exceed 8 hours
                if (nextDayDuration.TotalHours > 8)
                {
                    errorMessage = $"Timer duration exceeds 8 hours ({nextDayDuration.TotalHours:F1} hours). " +
                                  "This suggests the wrong AM/PM interpretation. Please specify AM/PM or use 24-hour format.";
                    return false;
                }
                
                // If duration is reasonable for next-day, allow it
                return true;
            }
            else
            {
                // Same day - check duration
                var duration = endTime - startTime;
                
                // If duration is very short (like 0), this might be an error
                if (duration.TotalMinutes < 1)
                {
                    errorMessage = "End time must be after start time. Please check your time input.";
                    return false;
                }
                
                // Rule 6: Duration should not exceed 8 hours
                if (duration.TotalHours > 8)
                {
                    errorMessage = $"Timer duration exceeds 8 hours ({duration.TotalHours:F1} hours). " +
                                  "This suggests the wrong AM/PM interpretation. Please specify AM/PM or use 24-hour format.";
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Rule 8: Determines if a time is ambiguous (could be AM or PM).
    /// </summary>
    public static bool IsAmbiguousTime(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // Not ambiguous if it has AM/PM markers
        if (TimeWithAmPmRegex.IsMatch(input))
            return false;

        // Check if it's a simple time format
        var simpleMatch = SimpleTimeRegex.Match(input);
        if (!simpleMatch.Success)
            return false;

        if (!int.TryParse(simpleMatch.Groups[1].Value, out var hours))
            return false;

        // Not ambiguous if hours are 13-23 (clearly 24-hour format)
        if (hours >= 13 && hours <= 23)
            return false;

        // Hours 0-12 are potentially ambiguous
        return true;
    }

    /// <summary>
    /// Gets both AM and PM versions of an ambiguous time for user selection.
    /// </summary>
    public static (TimeSpan amVersion, TimeSpan pmVersion, string display24Hour, string displayAmPm) GetAmbiguousTimeOptions(string input, TimeSpan interpretedTime)
    {
        var simpleMatch = SimpleTimeRegex.Match(input.Trim());
        if (!simpleMatch.Success)
            return (interpretedTime, interpretedTime, interpretedTime.ToString(@"hh\:mm"), interpretedTime.ToString(@"hh\:mm"));

        if (!int.TryParse(simpleMatch.Groups[1].Value, out var hours) ||
            !int.TryParse(simpleMatch.Groups[2].Value, out var minutes))
            return (interpretedTime, interpretedTime, interpretedTime.ToString(@"hh\:mm"), interpretedTime.ToString(@"hh\:mm"));

        var seconds = 0;
        if (simpleMatch.Groups[3].Success)
            int.TryParse(simpleMatch.Groups[3].Value, out seconds);

        // Create AM version (handle midnight special case)
        var amHours = hours == 12 ? 0 : hours;
        var amVersion = new TimeSpan(amHours, minutes, seconds);

        // Create PM version (handle noon special case)
        var pmHours = hours == 12 ? 12 : hours + 12;
        var pmVersion = new TimeSpan(pmHours, minutes, seconds);

        // Format displays
        var display24Hour = $"{interpretedTime.Hours:D2}:{interpretedTime.Minutes:D2}";
        var displayAmPm = interpretedTime.Hours >= 12 
            ? $"{(interpretedTime.Hours > 12 ? interpretedTime.Hours - 12 : interpretedTime.Hours)}:{interpretedTime.Minutes:D2} PM"
            : $"{(interpretedTime.Hours == 0 ? 12 : interpretedTime.Hours)}:{interpretedTime.Minutes:D2} AM";

        return (amVersion, pmVersion, display24Hour, displayAmPm);
    }

    #region Private helper methods

    private static bool TryParseWithAmPm(Match match, out TimeSpan result)
    {
        result = default;

        if (!int.TryParse(match.Groups[1].Value, out var hours) ||
            !int.TryParse(match.Groups[2].Value, out var minutes))
            return false;

        var seconds = 0;
        if (match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out seconds))
            return false;

        // Validate ranges
        if (hours < 1 || hours > 12 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return false;

        var amPmIndicator = match.Groups[4].Value.ToUpperInvariant();
        bool isPm = amPmIndicator.StartsWith("P");

        // Convert to 24-hour format
        if (isPm && hours != 12)
            hours += 12;
        else if (!isPm && hours == 12)
            hours = 0;

        result = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    private static TimeSpan DetermineAmOrPm(int hours, int minutes, int seconds, DateTime? contextTime, bool isStartTime)
    {
        // Create both AM and PM versions
        var amHours = hours == 12 ? 0 : hours;
        var pmHours = hours == 12 ? 12 : hours + 12;
        
        var amTime = new TimeSpan(amHours, minutes, seconds);
        var pmTime = new TimeSpan(pmHours, minutes, seconds);

        // If no context, use working hours bias
        if (contextTime == null)
        {
            return ApplyWorkingHoursBias(hours, minutes, seconds, isStartTime);
        }

        // Rule 3: Use context time for disambiguation
        if (isStartTime)
        {
            // For start times, consider both context proximity and working hours
            var contextHour = contextTime.Value.Hour;
            
            // Morning context (6-11 AM) - prefer AM for early hours
            if (contextHour >= 6 && contextHour <= 11)
            {
                if (hours <= 6) return amTime;  // 6:00 in morning context = 6:00 AM
                if (hours >= 7 && hours <= 11) return amTime;  // Morning work hours
                return pmTime;  // Afternoon hours
            }
            // Afternoon context (12-5 PM) - be smart about interpretation
            else if (contextHour >= 12 && contextHour <= 17)
            {
                if (hours <= 6) return amTime;  // Very early hours still AM
                if (hours >= 7 && hours <= 11) return amTime;  // Morning hours still AM
                return pmTime;  // Default PM for ambiguous afternoon times
            }
            // Evening context (6-11 PM) - prefer PM
            else if (contextHour >= 18 && contextHour <= 23)
            {
                return pmTime;  // Evening context prefers PM
            }
            else
            {
                return ApplyWorkingHoursBias(hours, minutes, seconds, true);
            }
        }
        else
        {
            // For end times, focus on creating reasonable duration
            return ChooseEndTimeBasedOnContext(amTime, pmTime, contextTime.Value);
        }
    }

    private static TimeSpan ApplyWorkingHoursBias(int hours, int minutes, int seconds, bool isStartTime)
    {
        var amHours = hours == 12 ? 0 : hours;
        var pmHours = hours == 12 ? 12 : hours + 12;
        
        var amTime = new TimeSpan(amHours, minutes, seconds);
        var pmTime = new TimeSpan(pmHours, minutes, seconds);

        if (isStartTime)
        {
            // Rule 4: Start times - bias towards working day (7am-7pm)
            if (hours <= 6)
                return amTime; // Early morning
            else if (hours >= 7 && hours <= 11)
                return amTime; // Morning work hours
            else if (hours >= 1 && hours <= 7)
                return pmTime; // Afternoon work hours
            else
                return hours == 12 ? pmTime : amTime; // Noon is PM, others prefer AM
        }
        else
        {
            // Rule 5: End times - most work ends in PM
            // For end times, strongly prefer PM for any reasonable hour
            if (hours >= 1 && hours <= 11)
                return pmTime; // Strongly prefer PM for end times
            else
                return hours == 12 ? pmTime : amTime; // Noon is PM, midnight is AM
        }
    }

    private static TimeSpan ChooseStartTimeBasedOnContext(TimeSpan amTime, TimeSpan pmTime, DateTime contextTime)
    {
        var contextHour = contextTime.Hour;
        
        // Simple logic based on context time and working hour preferences
        if (contextHour >= 6 && contextHour <= 11)
        {
            // Morning context (6 AM - 11 AM)
            // Strongly prefer AM for early hours like 6:00
            if (amTime.Hours <= 7)
                return amTime; // Early morning hours - strong AM preference
            else if (amTime.Hours >= 8 && amTime.Hours <= 11)
                return amTime; // Morning work hours - prefer AM  
            else
                return pmTime; // Afternoon hours - prefer PM
        }
        else if (contextHour >= 12 && contextHour <= 17)
        {
            // Afternoon context (noon to 5 PM)
            // Even in afternoon context, 6:00 should still be AM due to working hours bias
            if (amTime.Hours <= 6)
                return amTime; // Very early hours - prefer AM even in afternoon context
            else if (pmTime.Hours >= 13 && pmTime.Hours <= 19)
                return pmTime; // Afternoon work hours - prefer PM
            else
                return pmTime; // Default to PM in afternoon context
        }
        else if (contextHour >= 18 && contextHour <= 23)
        {
            // Evening context (6 PM to 11 PM)
            return pmTime; // In evening context, prefer PM
        }
        else
        {
            // Very early morning context (midnight to 5 AM)
            return ApplyWorkingHoursBias(amTime.Hours, amTime.Minutes, amTime.Seconds, isStartTime: true);
        }
    }

    private static TimeSpan ChooseEndTimeBasedOnContext(TimeSpan amTime, TimeSpan pmTime, DateTime startTime)
    {
        var startTimeOfDay = startTime.TimeOfDay;
        
        // Calculate durations for both options
        var amDuration = CalculateDuration(startTimeOfDay, amTime);
        var pmDuration = CalculateDuration(startTimeOfDay, pmTime);
        
        // Rule 6: Heavily prefer the option that doesn't exceed 8 hours
        if (amDuration.TotalHours > 8 && pmDuration.TotalHours <= 8)
            return pmTime;
        if (pmDuration.TotalHours > 8 && amDuration.TotalHours <= 8)
            return amTime;
        
        // If both are reasonable, prefer the most logical option
        // For normal day work (start time 6 AM - 6 PM), prefer PM end times
        if (startTime.Hour >= 6 && startTime.Hour <= 18)
        {
            // Normal work day - strongly prefer PM end times
            // Example: 9:00 AM start with "9:30" should be 9:30 PM
            return pmTime;
        }
        else
        {
            // Night shift or very early/late start - could end in AM
            return amTime;
        }
    }

    private static TimeSpan CalculateDuration(TimeSpan start, TimeSpan end)
    {
        if (end >= start)
            return end - start;
        else
            return (TimeSpan.FromDays(1) - start) + end; // Next day
    }

    #endregion
}
