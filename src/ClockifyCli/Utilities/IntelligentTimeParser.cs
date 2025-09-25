using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ClockifyCli.Utilities;

/// <summary>
/// Intelligent time parser that handles AM/PM, 24-hour format, and context-aware interpretation.
/// 
/// Parsing Rules:
/// 1. If time has AM/PM/A/P indicators, use that to calculate the correct time
/// 2. If time is 24-hour format (13-23 hours), treat as PM automatically
/// 3. Use context time (start/end time) for disambiguation when possible
/// 4. When using starting time as context to establish an end time, timer duration should not exceed 8 hours
/// 5. When using current time as a context to establish a start time, the calculated time should be in the past unless this makes the total duration over 8 hours, or under 0 minutes
/// 6. End time must be after start time
/// 7. When calculating start time, we should also have the end time. If there is no end time, we should assume the end time to be the current time.
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
    /// Gets the actual DateTime that would be used for a start time input,
    /// including the intelligent parser's day selection logic.
    /// </summary>
    public static DateTime GetActualStartDateTime(string input, DateTime currentTime)
    {
        if (!TryParseStartTime(input, out var time, currentTime))
        {
            throw new ArgumentException($"Invalid time format: {input}");
        }

        var proposedStartTime = currentTime.Date.Add(time);

        // Use the same logic as the intelligent parser for day selection
        // Rule 7: When calculating start time, if there is no end time, assume the end time to be the current time
        if (proposedStartTime > currentTime)
        {
            return proposedStartTime.AddDays(-1);
        }
        else
        {
            return proposedStartTime;
        }
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
            // Rule 6: End time must be after start time
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

                // Rule 4: When using starting time as context to establish an end time, timer duration should not exceed 8 hours
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

                // Rule 4: When using starting time as context to establish an end time, timer duration should not exceed 8 hours
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
    /// Determines if a time is ambiguous (could be AM or PM).
    /// </summary>
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

        // If no context, prefer the more likely interpretation based on the hour
        if (contextTime == null)
        {
            // For hours 1-6, prefer PM during day, AM during night
            // For hours 7-11, prefer AM (morning) over PM (evening)
            if (hours >= 1 && hours <= 6)
                return pmTime; // 1PM-6PM more common than 1AM-6AM for manual entry
            else if (hours >= 7 && hours <= 11)
                return amTime; // 7AM-11AM more common than 7PM-11PM
            else // hour 12
                return pmTime; // Default 12:XX to noon rather than midnight
        }

        // Rule 3: Use context time for disambiguation
        if (isStartTime)
        {
            // Rule 5: When using current time as a context to establish a start time, the calculated time should be in the past unless this makes the total duration over 8 hours, or under 0 minutes
            return ChooseStartTimeInPast(amTime, pmTime, contextTime.Value);
        }
        else
        {
            // For end times, choose based on reasonable duration
            return ChooseEndTimeBasedOnDuration(amTime, pmTime, contextTime.Value);
        }
    }

    private static TimeSpan ChooseStartTimeInPast(TimeSpan amTime, TimeSpan pmTime, DateTime contextTime)
    {
        var contextTimeOfDay = contextTime.TimeOfDay;

        // Calculate how long ago each time was
        var amMinutesAgo = CalculateMinutesAgo(contextTimeOfDay, amTime);
        var pmMinutesAgo = CalculateMinutesAgo(contextTimeOfDay, pmTime);

        // Calculate potential durations (from start time to current context time)
        var amDuration = TimeSpan.FromMinutes(amMinutesAgo);
        var pmDuration = TimeSpan.FromMinutes(pmMinutesAgo);

        // Rule 5: The calculated time should be in the past unless this makes the total duration over 8 hours, or under 0 minutes
        // First check if duration would be over 8 hours
        if (amDuration.TotalHours > 8 && pmDuration.TotalHours <= 8)
            return pmTime;
        if (pmDuration.TotalHours > 8 && amDuration.TotalHours <= 8)
            return amTime;

        // Check for negative duration (shouldn't happen with "minutes ago" but safety check)
        if (amMinutesAgo < 0 && pmMinutesAgo >= 0)
            return pmTime;
        if (pmMinutesAgo < 0 && amMinutesAgo >= 0)
            return amTime;

        // If both are reasonable, prefer the more recent one (less time ago)
        return amMinutesAgo < pmMinutesAgo ? amTime : pmTime;
    }

    private static TimeSpan ChooseEndTimeBasedOnDuration(TimeSpan amTime, TimeSpan pmTime, DateTime startTime)
    {
        var startTimeOfDay = startTime.TimeOfDay;

        // Calculate durations for both options
        var amDuration = CalculateDuration(startTimeOfDay, amTime);
        var pmDuration = CalculateDuration(startTimeOfDay, pmTime);

        // Rule 4: When using starting time as context to establish an end time, timer duration should not exceed 8 hours
        if (amDuration.TotalHours > 8 && pmDuration.TotalHours <= 8)
            return pmTime;
        if (pmDuration.TotalHours > 8 && amDuration.TotalHours <= 8)
            return amTime;

        // If both are reasonable, prefer the shorter duration (same day if possible)
        return amDuration.TotalHours < pmDuration.TotalHours ? amTime : pmTime;
    }

    private static double CalculateMinutesAgo(TimeSpan currentTime, TimeSpan pastTime)
    {
        if (currentTime >= pastTime)
            return (currentTime - pastTime).TotalMinutes;
        else
            return (currentTime + TimeSpan.FromDays(1) - pastTime).TotalMinutes; // Past time was yesterday
    }

    private static TimeSpan ChooseStartTimeBasedOnContext(TimeSpan amTime, TimeSpan pmTime, DateTime contextTime)
    {
        // Simple context-based choice - prefer the time that's closest to current context
        var contextHour = contextTime.Hour;

        // If in morning (6 AM - 11 AM), prefer AM for early hours
        if (contextHour >= 6 && contextHour <= 11)
        {
            return amTime.Hours <= 11 ? amTime : pmTime;
        }
        // If in afternoon/evening (noon onwards), prefer PM
        else if (contextHour >= 12)
        {
            return pmTime;
        }
        // Very early morning (midnight to 5 AM), use past time logic
        else
        {
            return ChooseStartTimeInPast(amTime, pmTime, contextTime);
        }
    }

    private static TimeSpan ChooseEndTimeBasedOnContext(TimeSpan amTime, TimeSpan pmTime, DateTime startTime)
    {
        // Simplified: just use duration-based logic
        return ChooseEndTimeBasedOnDuration(amTime, pmTime, startTime);
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
