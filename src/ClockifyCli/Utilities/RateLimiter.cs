using System.Collections.Concurrent;
using System.Diagnostics;

namespace ClockifyCli.Utilities;

/// <summary>
/// A thread-safe rate limiter that ensures requests don't exceed a specified rate.
/// Uses a sliding window approach to track request timestamps.
/// </summary>
public class RateLimiter
{
    private readonly int maxRequests;
    private readonly TimeSpan timeWindow;
    private readonly ConcurrentQueue<DateTime> requestTimestamps;
    private readonly object lockObject = new object();

    /// <summary>
    /// Initializes a new instance of the RateLimiter class.
    /// </summary>
    /// <param name="maxRequests">Maximum number of requests allowed in the time window</param>
    /// <param name="timeWindow">Time window for rate limiting</param>
    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        this.maxRequests = maxRequests;
        this.timeWindow = timeWindow;
        this.requestTimestamps = new ConcurrentQueue<DateTime>();
    }

    /// <summary>
    /// Creates a rate limiter configured for Clockify's API limits.
    /// Conservative approach: 10 requests per second to avoid "Too Many Requests" errors.
    /// </summary>
    /// <returns>A RateLimiter configured for Clockify API</returns>
    public static RateLimiter ForClockifyApi()
    {
        // Use a conservative rate limit: 10 requests per second
        // This ensures we never hit rate limits during pagination or bulk operations
        return new RateLimiter(10, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Waits if necessary to ensure the next request doesn't exceed the rate limit.
    /// This method should be called before making each API request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>A task that completes when it's safe to make the next request</returns>
    public async Task WaitIfNeededAsync(CancellationToken cancellationToken = default)
    {
        TimeSpan waitTime = TimeSpan.Zero;

        lock (lockObject)
        {
            var now = DateTime.UtcNow;

            // Remove old timestamps that are outside our time window
            while (requestTimestamps.TryPeek(out var oldestTimestamp) &&
                   (now - oldestTimestamp) > timeWindow)
            {
                requestTimestamps.TryDequeue(out _);
            }

            // Check if we're at the rate limit
            if (requestTimestamps.Count >= maxRequests)
            {
                // Calculate how long we need to wait
                if (requestTimestamps.TryPeek(out var oldestRequest))
                {
                    waitTime = timeWindow - (now - oldestRequest);
                }
            }
        }

        // Wait outside the lock to avoid blocking other threads
        if (waitTime > TimeSpan.Zero)
        {
            await Task.Delay(waitTime, cancellationToken);
        }

        lock (lockObject)
        {
            // Record this request timestamp
            requestTimestamps.Enqueue(DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Gets the current number of requests in the sliding window.
    /// </summary>
    public int CurrentRequestCount
    {
        get
        {
            lock (lockObject)
            {
                var now = DateTime.UtcNow;

                // Remove old timestamps
                while (requestTimestamps.TryPeek(out var oldestTimestamp) &&
                       (now - oldestTimestamp) > timeWindow)
                {
                    requestTimestamps.TryDequeue(out _);
                }

                return requestTimestamps.Count;
            }
        }
    }

    /// <summary>
    /// Gets the estimated time until the next request can be made without waiting.
    /// </summary>
    public TimeSpan EstimatedWaitTime
    {
        get
        {
            lock (lockObject)
            {
                var now = DateTime.UtcNow;

                // Remove old timestamps
                while (requestTimestamps.TryPeek(out var oldestTimestamp) &&
                       (now - oldestTimestamp) > timeWindow)
                {
                    requestTimestamps.TryDequeue(out _);
                }

                if (requestTimestamps.Count < maxRequests)
                {
                    return TimeSpan.Zero;
                }

                if (requestTimestamps.TryPeek(out var oldestRequest))
                {
                    var waitTime = timeWindow - (now - oldestRequest);
                    return waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero;
                }

                return TimeSpan.Zero;
            }
        }
    }
}
