namespace ClockifyCli.Services;

/// <summary>
/// Interface for providing current date and time.
/// This allows for testable time-dependent code by enabling time mocking in tests.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local date (without time component).
    /// </summary>
    DateTime Today { get; }
}
