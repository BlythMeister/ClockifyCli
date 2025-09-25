using ClockifyCli.Services;

namespace ClockifyCli.Tests.Mocks;

/// <summary>
/// Mock changelog reader for testing different scenarios
/// </summary>
public class MockChangelogReader : IChangelogReader
{
    private readonly string? changelogContent;

    public MockChangelogReader(string? changelogContent)
    {
        this.changelogContent = changelogContent;
    }

    public Task<string?> ReadChangelogAsync()
    {
        return Task.FromResult(changelogContent);
    }
}
