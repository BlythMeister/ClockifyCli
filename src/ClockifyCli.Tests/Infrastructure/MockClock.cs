using ClockifyCli.Services;

namespace ClockifyCli.Tests.Infrastructure;

/// <summary>
/// Mock clock implementation for testing that allows setting specific times.
/// </summary>
public class MockClock : IClock
{
    private DateTime _now;
    private DateTime _utcNow;
    private DateTime _today;

    /// <summary>
    /// Initializes a new instance of MockClock with the specified time.
    /// </summary>
    /// <param name="now">The current local time to return</param>
    /// <param name="utcNow">The current UTC time to return (optional, defaults to now)</param>
    public MockClock(DateTime now, DateTime? utcNow = null)
    {
        _now = now;
        _utcNow = utcNow ?? now;
        _today = now.Date;
    }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    public DateTime Now => _now;

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    public DateTime UtcNow => _utcNow;

    /// <summary>
    /// Gets the current local date (without time component).
    /// </summary>
    public DateTime Today => _today;

    /// <summary>
    /// Sets the current time for testing purposes.
    /// </summary>
    /// <param name="now">The new current time</param>
    /// <param name="utcNow">The new UTC time (optional, defaults to now)</param>
    public void SetTime(DateTime now, DateTime? utcNow = null)
    {
        _now = now;
        _utcNow = utcNow ?? now;
        _today = now.Date;
    }
}
