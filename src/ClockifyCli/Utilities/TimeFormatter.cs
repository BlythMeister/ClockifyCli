using System;

namespace ClockifyCli.Utilities;

public static class TimeFormatter
{
    /// <summary>
    /// Formats a TimeSpan into a human-readable duration string (e.g., "2h 30m 15s")
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        if (totalHours > 0)
        {
            return $"{totalHours}h {minutes}m {seconds}s";
        }
        else if (minutes > 0)
        {
            return $"{minutes}m {seconds}s";
        }
        else
        {
            return $"{seconds}s";
        }
    }

    /// <summary>
    /// Formats a TimeSpan into a compact duration string without seconds (e.g., "2h 30m")
    /// </summary>
    public static string FormatDurationCompact(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        if (totalHours == 0)
            return $"{minutes}m";

        return minutes == 0 ? $"{totalHours}h" : $"{totalHours}h {minutes}m";
    }
}