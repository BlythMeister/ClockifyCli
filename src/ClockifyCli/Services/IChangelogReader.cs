namespace ClockifyCli.Services;

/// <summary>
/// Interface for reading changelog content
/// </summary>
public interface IChangelogReader
{
    /// <summary>
    /// Reads the changelog content asynchronously
    /// </summary>
    /// <returns>The changelog content as a string, or null if not found</returns>
    Task<string?> ReadChangelogAsync();
}