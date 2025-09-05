using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ClockifyCli.Utilities;

/// <summary>
/// Simple time parser that requires 24-hour format (HH:mm or HH:mm:ss).
/// </summary>
public static class IntelligentTimeParser
{
    private static readonly Regex TimeRegex = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a time string in 24-hour format only.
    /// </summary>
    /// <param name="input">The time input string in 24-hour format (e.g., "13:30", "09:15", "23:45")</param>
    /// <param name="result">The parsed TimeSpan result</param>
    /// <param name="contextTime">Not used in simplified version</param>
    /// <param name="isStartTime">Not used in simplified version</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParseTime(string input, out TimeSpan result, DateTime? contextTime = null, bool isStartTime = true)
    {
        result = default;
        
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        var match = TimeRegex.Match(input);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups[1].Value, out var hours) ||
            !int.TryParse(match.Groups[2].Value, out var minutes))
            return false;

        var seconds = 0;
        if (match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out seconds))
            return false;

        // Validate ranges for 24-hour format
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return false;

        result = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    /// <summary>
    /// Parses time input for start time (same as TryParseTime in simplified version).
    /// </summary>
    public static bool TryParseStartTime(string input, out TimeSpan result, DateTime currentTime)
    {
        return TryParseTime(input, out result);
    }

    /// <summary>
    /// Parses time input for end time (same as TryParseTime in simplified version).
    /// </summary>
    public static bool TryParseEndTime(string input, out TimeSpan result, DateTime startTime)
    {
        return TryParseTime(input, out result);
    }

    /// <summary>
    /// Basic validation that ensures end time is after start time.
    /// </summary>
    public static bool ValidateTimeInContext(TimeSpan parsedTime, DateTime? contextTime, bool isStartTime, out string errorMessage)
    {
        errorMessage = "";

        if (contextTime == null)
            return true;

        if (!isStartTime)
        {
            // End time validation - ensure it's after start time
            var startTime = contextTime.Value.TimeOfDay;
            var endTime = parsedTime;
            
            if (endTime <= startTime)
            {
                errorMessage = "End time must be after start time. Use 24-hour format (e.g., 14:30 for 2:30 PM).";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// In simplified version, no times are ambiguous since we require 24-hour format.
    /// </summary>
    public static bool IsAmbiguousTime(string input)
    {
        return false; // No ambiguous times in 24-hour format
    }

    /// <summary>
    /// Not used in simplified version since there are no ambiguous times.
    /// </summary>
    public static (TimeSpan amVersion, TimeSpan pmVersion, string display24Hour, string displayAmPm) GetAmbiguousTimeOptions(string input, TimeSpan interpretedTime)
    {
        // Return the same time for both versions since there's no ambiguity in 24-hour format
        var displayTime = interpretedTime.ToString(@"hh\:mm");
        return (interpretedTime, interpretedTime, displayTime, displayTime);
    }
}
