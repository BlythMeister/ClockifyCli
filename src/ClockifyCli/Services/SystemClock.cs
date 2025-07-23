namespace ClockifyCli.Services;

/// <summary>
/// System clock implementation that provides real current date and time.
/// </summary>
public class SystemClock : IClock
{
    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    public DateTime Now => DateTime.Now;

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets the current local date (without time component).
    /// </summary>
    public DateTime Today => DateTime.Today;
}
