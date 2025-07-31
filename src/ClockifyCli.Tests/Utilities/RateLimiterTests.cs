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
    public async Task ForClockifyApi_ShouldCreateAppropriateRateLimiter()
    {
        // Arrange & Act
        var rateLimiter = RateLimiter.ForClockifyApi();

        // Assert - Should allow multiple requests quickly
        for (int i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await rateLimiter.WaitIfNeededAsync();
            stopwatch.Stop();

            // First 10 requests should be fast
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50), $"Request {i + 1} should be fast");
        }
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
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await rateLimiter.WaitIfNeededAsync(cancellationTokenSource.Token));
        stopwatch.Stop();

        // Should have been cancelled before the full wait time
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
    }

    [Test]
    public async Task RateLimiter_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var rateLimiter = new RateLimiter(10, TimeSpan.FromSeconds(1));
        var taskCount = 20;
        var tasks = new List<Task>();

        // Act - Start multiple concurrent tasks
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await rateLimiter.WaitIfNeededAsync();
            }));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert - Should not throw exceptions and should complete
        Assert.That(tasks.All(t => t.IsCompletedSuccessfully), Is.True);
    }
}
