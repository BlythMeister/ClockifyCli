using ClockifyCli.Utilities;
using NUnit.Framework;
using System.Diagnostics;

namespace ClockifyCli.Tests.Utilities;

[TestFixture]
public class RateLimiterTests
{
    [Test]
    public async Task WaitIfNeededAsync_WithinRateLimit_ShouldNotWait()
    {
        // Arrange
        var rateLimiter = new RateLimiter(50, TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        // Act
        await rateLimiter.WaitIfNeededAsync();

        // Assert
        stopwatch.Stop();
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10), "Should not wait when under rate limit");
    }

    [Test]
    [CancelAfter(1000)] // 1 second timeout
    public async Task WaitIfNeededAsync_ExceedingRateLimit_ShouldWait()
    {
        // Arrange
        var rateLimiter = new RateLimiter(2, TimeSpan.FromMilliseconds(500));

        // Act - Make requests to exceed the limit
        await rateLimiter.WaitIfNeededAsync(); // Request 1
        await rateLimiter.WaitIfNeededAsync(); // Request 2

        var stopwatch = Stopwatch.StartNew();
        await rateLimiter.WaitIfNeededAsync(); // Request 3 - should wait
        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(400), "Should wait when exceeding rate limit");
    }

    [Test]
    public async Task CurrentRequestCount_ShouldReflectActiveRequests()
    {
        // Arrange
        var rateLimiter = new RateLimiter(5, TimeSpan.FromSeconds(1));

        // Act
        Assert.That(rateLimiter.CurrentRequestCount, Is.EqualTo(0));

        await rateLimiter.WaitIfNeededAsync();
        Assert.That(rateLimiter.CurrentRequestCount, Is.EqualTo(1));

        await rateLimiter.WaitIfNeededAsync();
        Assert.That(rateLimiter.CurrentRequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task EstimatedWaitTime_WhenUnderLimit_ShouldBeZero()
    {
        // Arrange
        var rateLimiter = new RateLimiter(5, TimeSpan.FromSeconds(1));

        // Act & Assert
        Assert.That(rateLimiter.EstimatedWaitTime, Is.EqualTo(TimeSpan.Zero));

        await rateLimiter.WaitIfNeededAsync();
        Assert.That(rateLimiter.EstimatedWaitTime, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task EstimatedWaitTime_WhenAtLimit_ShouldIndicateWaitTime()
    {
        // Arrange
        var rateLimiter = new RateLimiter(2, TimeSpan.FromMilliseconds(500));

        // Act - Fill up the rate limit
        await rateLimiter.WaitIfNeededAsync();
        await rateLimiter.WaitIfNeededAsync();

        // Assert - Should need to wait
        Assert.That(rateLimiter.EstimatedWaitTime, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(rateLimiter.EstimatedWaitTime, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(500)));
    }

    [Test]
    [CancelAfter(3000)] // 3 second timeout to prevent hanging
    public async Task ForClockifyApi_ShouldCreateAppropriateRateLimiter()
    {
        // Arrange & Act
        var rateLimiter = RateLimiter.ForClockifyApi();

        // Assert - Should allow the first request quickly
        var stopwatch = Stopwatch.StartNew();
        await rateLimiter.WaitIfNeededAsync();
        stopwatch.Stop();

        // First request should be fast
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50), "First request should be fast");

        // Second request should wait since ForClockifyApi uses 1 request per second
        stopwatch.Restart();
        await rateLimiter.WaitIfNeededAsync();
        stopwatch.Stop();

        // Second request should take approximately 1 second
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(900), "Second request should wait ~1 second");
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1200), "Second request should not wait much more than 1 second");
    }

    [Test]
    public async Task RateLimiter_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var rateLimiter = new RateLimiter(1, TimeSpan.FromSeconds(2));
        var cancellationTokenSource = new CancellationTokenSource();

        // Fill the rate limit
        await rateLimiter.WaitIfNeededAsync();

        // Act & Assert
        cancellationTokenSource.CancelAfter(100); // Cancel after 100ms

        var stopwatch = Stopwatch.StartNew();
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await rateLimiter.WaitIfNeededAsync(cancellationTokenSource.Token));
        stopwatch.Stop();

        // Should have been cancelled before the full wait time
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
    }

    [Test]
    [CancelAfter(5000)] // 5 second timeout for concurrent test
    public async Task RateLimiter_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange - Use a higher rate limit to avoid long waits
        var rateLimiter = new RateLimiter(20, TimeSpan.FromSeconds(1));
        var taskCount = 15; // Less than the rate limit
        var tasks = new List<Task>();

        // Act - Start multiple concurrent tasks
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await rateLimiter.WaitIfNeededAsync();
            }));
        }

        // Wait for all tasks to complete with a reasonable timeout
        var completedWithinTimeout = await Task.WhenAll(tasks).ContinueWith(t => t.IsCompletedSuccessfully, TaskScheduler.Default);

        // Assert - Should not throw exceptions and should complete quickly
        Assert.That(tasks.All(t => t.IsCompletedSuccessfully), Is.True);
    }
}
