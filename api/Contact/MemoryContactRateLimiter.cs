using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

/// <summary>
/// Per-instance sliding window, keyed by the IP hash. It runs before Turnstile.
/// Every attempt that reaches this method is counted, including ones that are then refused.
/// The window is not shared across instances and it is empty after a cold start.
/// dbo.usp_SubmitContact is the limit that survives both of those.
/// </summary>
public interface IContactRateLimiter
{
    ValueTask<ContactSubmitResult?> AdmitAsync(byte[] ipHash, CancellationToken cancellationToken);
}

public sealed class MemoryContactRateLimiter : IContactRateLimiter
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly IOptions<ContactApiOptions> _options;
    private readonly TimeProvider _time;

    public MemoryContactRateLimiter(IOptions<ContactApiOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public ValueTask<ContactSubmitResult?> AdmitAsync(byte[] ipHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int perMinute = Positive(_options.Value.MemoryIpPerMinute, ContactLimits.MemoryDefaultPerMinute);
        int perHour = Positive(_options.Value.MemoryIpPerHour, ContactLimits.MemoryDefaultPerHour);
        int maxEntries = Positive(_options.Value.MemoryMaxEntries, ContactLimits.MemoryDefaultMaxEntries);
        long now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        string key = Convert.ToHexString(ipHash);

        lock (_gate)
        {
            EvictStale(now);
            Bucket bucket = GetOrAdd(key, now, maxEntries);
            bucket.LastSeenMs = now;
            Prune(bucket, now);
            bucket.Hits.Add(now);
            while (bucket.Hits.Count > ContactLimits.MemoryMaxTimestampsPerEntry)
            {
                bucket.Hits.RemoveAt(0);
            }

            int? minuteWait = WaitSeconds(bucket.Hits, now, 60_000, perMinute);
            int? hourWait = WaitSeconds(bucket.Hits, now, 3_600_000, perHour);
            if (minuteWait is null && hourWait is null)
            {
                return new ValueTask<ContactSubmitResult?>((ContactSubmitResult?)null);
            }

            int wait = Math.Max(minuteWait ?? 1, hourWait ?? 1);
            return new ValueTask<ContactSubmitResult?>(ContactSubmitResult.RateLimited(wait));
        }
    }

    private Bucket GetOrAdd(string key, long now, int maxEntries)
    {
        if (_buckets.TryGetValue(key, out Bucket? existing))
        {
            return existing;
        }

        while (_buckets.Count >= maxEntries)
        {
            string? oldest = null;
            long oldestSeen = long.MaxValue;
            foreach ((string candidate, Bucket bucket) in _buckets)
            {
                if (bucket.LastSeenMs < oldestSeen)
                {
                    oldest = candidate;
                    oldestSeen = bucket.LastSeenMs;
                }
            }

            if (oldest is null)
            {
                break;
            }

            _buckets.Remove(oldest);
        }

        var created = new Bucket { LastSeenMs = now };
        _buckets[key] = created;
        return created;
    }

    private void EvictStale(long now)
    {
        List<string>? stale = null;
        foreach ((string key, Bucket bucket) in _buckets)
        {
            if (now - bucket.LastSeenMs > 3_600_000)
            {
                stale ??= [];
                stale.Add(key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (string key in stale)
        {
            _buckets.Remove(key);
        }
    }

    private static void Prune(Bucket bucket, long now)
    {
        int keep = 0;
        while (keep < bucket.Hits.Count && now - bucket.Hits[keep] > 3_600_000)
        {
            keep++;
        }

        if (keep > 0)
        {
            bucket.Hits.RemoveRange(0, keep);
        }
    }

    /// <summary>
    /// Seconds until enough of the oldest hits age out that one more request would fit.
    /// Null when this window is not over its limit. Matches the slot math in usp_SubmitContact.
    /// </summary>
    private static int? WaitSeconds(List<long> hits, long now, long windowMs, int limit)
    {
        int start = 0;
        while (start < hits.Count && now - hits[start] > windowMs)
        {
            start++;
        }

        int count = hits.Count - start;
        if (count <= limit)
        {
            return null;
        }

        int index = start + (count - limit);
        long remainMs = hits[index] + windowMs - now;
        if (remainMs <= 0)
        {
            return 1;
        }

        return (int)Math.Min(int.MaxValue, (remainMs + 999) / 1000);
    }

    private static int Positive(int? configured, int fallback) =>
        configured is int value and > 0 ? value : fallback;

    private sealed class Bucket
    {
        public List<long> Hits { get; } = [];

        public long LastSeenMs { get; set; }
    }
}
