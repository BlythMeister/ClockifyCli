using System.Globalization;
using System.Text.RegularExpressions;

namespace ClockifyCli.Utilities;

/// <summary>
/// Provides intelligent parsing of time input that supports both 12-hour and 24-hour formats
/// with automatic AM/PM detection based on context.
/// </summary>
public static class IntelligentTimeParser
{
    private static readonly Regex TimeRegex = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(am|pm|AM|PM|a|p|A|P)?$", RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a time string with intelligent AM/PM detection.
    /// </summary>
    /// <param name="input">The time input string (e.g., "1:30", "1:30 PM", "1:30p", "13:30")</param>
    /// <param name="result">The parsed TimeSpan result</param>
    /// <param name="contextTime">Optional context time to help determine AM/PM for ambiguous cases</param>
    /// <param name="isStartTime">True if this is a start time, false if end time (affects AM/PM logic)</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParseTime(string input, out TimeSpan result, DateTime? contextTime = null, bool isStartTime = true)
    {
        result = default;
        
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // Try regex parsing first for comprehensive format detection
        var match = TimeRegex.Match(input);
        if (!match.Success)
        {
            // Fallback to standard parsing for unusual formats
            if (TimeSpan.TryParse(input, out result))
            {
                // Validate that it's a valid time within 24 hours
                if (result >= TimeSpan.Zero && result < TimeSpan.FromDays(1))
                    return true;
            }
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out var hours) ||
            !int.TryParse(match.Groups[2].Value, out var minutes))
            return false;

        var seconds = 0;
        if (match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out seconds))
            return false;

        // Validate ranges
        if (minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return false;

        var amPmSpecified = match.Groups[4].Success;
        var amPmValue = match.Groups[4].Value.ToLowerInvariant();
        var isAm = amPmSpecified && (amPmValue == "am" || amPmValue == "a");
        var isPm = amPmSpecified && (amPmValue == "pm" || amPmValue == "p");

        // Handle explicit AM/PM
        if (amPmSpecified)
        {
            if (hours < 1 || hours > 12)
                return false;

            if (isPm && hours != 12)
                hours += 12;
            else if (isAm && hours == 12)
                hours = 0;
        }
        else
        {
            // Check if it's already in 24-hour format
            // If it has a leading zero (e.g., "09:15"), has seconds, or is 13-23, treat as 24-hour
            var originalHourString = match.Groups[1].Value;
            var hasLeadingZero = originalHourString.Length > 1 && originalHourString.StartsWith("0");
            var hasSeconds = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value);
            
            if (hours >= 13 && hours <= 23)
            {
                // Already 24-hour format (13-23), use as-is
            }
            else if (hours == 0 || hasLeadingZero || hasSeconds)
            {
                // Midnight, leading zero format (e.g., 09:15), or has seconds (e.g., 10:15:30) - use as-is
            }
            else
            {
                // No AM/PM specified and ambiguous (1-12 without leading zero or seconds) - apply intelligent detection
                hours = DetermineHoursWithContext(hours, contextTime, isStartTime);
            }
        }

        // Final validation
        if (hours < 0 || hours > 23)
            return false;

        result = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    /// <summary>
    /// Intelligently determines whether to use AM or PM for ambiguous hour values based on context.
    /// </summary>
    private static int DetermineHoursWithContext(int inputHours, DateTime? contextTime, bool isStartTime)
    {
        // If already in 24-hour format (13-23), return as-is
        if (inputHours >= 13 && inputHours <= 23)
            return inputHours;

        // If 0, treat as midnight (24-hour format)
        if (inputHours == 0)
            return 0;

        // For hours 1-12, we need to determine AM/PM
        if (inputHours < 1 || inputHours > 12)
            return inputHours; // Invalid, but let validation catch it

        // Calculate both AM and PM versions
        var amVersion = inputHours == 12 ? 0 : inputHours;
        var pmVersion = inputHours == 12 ? 12 : inputHours + 12;

        // If no context time, use default business logic
        if (contextTime == null)
        {
            // For business hours (9 AM - 5 PM), default to PM
            // For early hours (1-8), default to AM
            return inputHours >= 9 ? pmVersion : amVersion;
        }

        var contextHour = contextTime.Value.Hour;

        if (isStartTime)
        {
            // For start times: choose the time that makes most sense relative to current context
            
            // For late context (after 6 PM), probably early start next day
            if (contextHour >= 18)
            {
                // For reasonable work start times (6-9), prefer AM (next day start)
                if (inputHours >= 6 && inputHours <= 9)
                    return amVersion; // Early start next day
                // For lunch times (11-1), could be next day
                if (inputHours >= 11 && inputHours <= 1)
                    return pmVersion; // Late night session
                // For other times, use proximity logic
            }
            
            // If context is during business hours (9 AM - 5 PM, but not late evening)
            if (contextHour >= 9 && contextHour <= 17)
            {
                // If the PM version is close to the context time (within 2 hours), prefer PM
                if (Math.Abs(pmVersion - contextHour) <= 2)
                    return pmVersion;
                
                // If the AM version is reasonable for early start and context is later, prefer AM
                if (amVersion >= 6 && amVersion <= 9 && contextHour > amVersion)
                    return amVersion;
                
                // Otherwise for business hours context, prefer PM for reasonable working times
                if (inputHours >= 9)
                    return pmVersion;
            }
            
            // For early context (before 9 AM), prefer AM unless PM makes more sense
            if (contextHour < 9)
            {
                if (amVersion <= contextHour + 2) // Within 2 hours
                    return amVersion;
                if (pmVersion >= 9) // Reasonable work start time
                    return pmVersion;
            }
            
            // Default: choose based on proximity to context
            return Math.Abs(pmVersion - contextHour) <= Math.Abs(amVersion - contextHour) ? pmVersion : amVersion;
        }
        else
        {
            // For end times: ensure reasonable work session duration
            
            // If start time is in morning (6-11 AM), choose AM/PM based on what makes sense
            if (contextHour >= 6 && contextHour <= 11)
            {
                // If AM version would be after start time and create reasonable duration (< 8 hours)
                if (amVersion > contextHour && (amVersion - contextHour) <= 8)
                    return amVersion;
                // Otherwise prefer PM for longer work sessions
                return pmVersion;
            }
            
            // If start time is in early afternoon (12-17), prefer PM if it's after start
            if (contextHour >= 12 && contextHour <= 17)
            {
                if (pmVersion > contextHour)
                    return pmVersion;
                // If PM would be before start, must be next day AM
                return amVersion;
            }
            
            // If start time is late (18+), could be late night work or next day
            if (contextHour >= 18)
            {
                if (pmVersion > contextHour)
                    return pmVersion; // Same day late work
                return amVersion; // Next day
            }
            
            // Default: prefer PM for reasonable work hours
            return pmVersion;
        }
    }

    /// <summary>
    /// Calculates the time difference between two hours, considering wrap-around.
    /// </summary>
    private static int CalculateTimeDifference(int fromHour, int toHour, bool allowPast)
    {
        var diff = toHour - fromHour;
        
        if (!allowPast && diff < 0)
            diff += 24; // Next day
            
        return Math.Abs(diff);
    }

    /// <summary>
    /// Parses time input with context for start time validation.
    /// </summary>
    public static bool TryParseStartTime(string input, out TimeSpan result, DateTime currentTime)
    {
        return TryParseTime(input, out result, currentTime, isStartTime: true);
    }

    /// <summary>
    /// Parses time input with context for end time validation.
    /// </summary>
    public static bool TryParseEndTime(string input, out TimeSpan result, DateTime startTime)
    {
        return TryParseTime(input, out result, startTime, isStartTime: false);
    }

    /// <summary>
    /// Validates that a parsed time makes sense in context and provides user-friendly feedback.
    /// </summary>
    public static bool ValidateTimeInContext(TimeSpan parsedTime, DateTime? contextTime, bool isStartTime, out string errorMessage)
    {
        errorMessage = "";

        if (contextTime == null)
            return true; // No context to validate against

        var contextDate = contextTime.Value.Date;
        var proposedTime = contextDate.Add(parsedTime);

        if (isStartTime)
        {
            // Start time validation
            if (proposedTime > contextTime.Value.AddHours(1))
            {
                errorMessage = "Start time cannot be more than 1 hour in the future.";
                return false;
            }

            if (proposedTime < contextTime.Value.AddHours(-12))
            {
                errorMessage = "Start time cannot be more than 12 hours in the past.";
                return false;
            }
        }
        else
        {
            // End time validation (contextTime is start time in this case)
            var startTime = contextTime.Value.TimeOfDay;
            var endTime = parsedTime;
            
            var duration = endTime - startTime;
            
            // If end time appears to be before start time, check if it makes sense as next day
            if (duration < TimeSpan.Zero)
            {
                // If the time difference is small (< 4 hours back), likely an error, not next day
                if (Math.Abs(duration.TotalHours) < 4)
                {
                    errorMessage = "End time must be after start time.";
                    return false;
                }
                
                // Otherwise, treat as next day
                duration = duration.Add(TimeSpan.FromDays(1));
            }
            
            if (duration <= TimeSpan.Zero)
            {
                errorMessage = "End time must be after start time.";
                return false;
            }

            if (duration > TimeSpan.FromHours(16))
            {
                errorMessage = "Work session cannot be longer than 16 hours.";
                return false;
            }
        }

        return true;
    }
}
