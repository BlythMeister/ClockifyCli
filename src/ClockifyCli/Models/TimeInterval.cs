namespace ClockifyCli.Models;

public record TimeInterval(string Start, string End)
{
    public DateTime StartDate => DateTime.Parse(Start, null, System.Globalization.DateTimeStyles.RoundtripKind);
    public DateTime EndDate => DateTime.Parse(End, null, System.Globalization.DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Gets the duration of this time interval. 
    /// Only use this for completed time entries where End is not null/empty.
    /// For running timers, calculate duration manually using DateTime.UtcNow - StartDate.
    /// </summary>
    public TimeSpan DurationSpan
    {
        get
        {
            if (string.IsNullOrEmpty(End))
            {
                throw new InvalidOperationException("Cannot calculate duration for a running time entry. Use DateTime.UtcNow - StartDate instead.");
            }
            return EndDate.Subtract(StartDate);
        }
    }

    /// <summary>
    /// Indicates whether this time entry is currently running (End is null or empty).
    /// </summary>
    public bool IsRunning => string.IsNullOrEmpty(End);
}
