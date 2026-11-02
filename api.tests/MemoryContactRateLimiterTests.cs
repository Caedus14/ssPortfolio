using Portfolio.Api.Contact;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class MemoryContactRateLimiterTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T12:00:00Z");

    [Fact]
    public async Task Five_per_minute_is_allowed_and_the_sixth_waits()
    {
        var time = new FixedTimeProvider(Start);
        MemoryContactRateLimiter limiter = Limiter(time, perMinute: 5, perHour: 100);
        byte[] hash = Hash("203.0.113.8");

        for (int i = 0; i < 5; i++)
        {
            Assert.Null(await limiter.AdmitAsync(hash, CancellationToken.None));
        }

        ContactSubmitResult blocked = (await limiter.AdmitAsync(hash, CancellationToken.None))!;
        Assert.Equal(ContactSubmitStatus.RateLimited, blocked.Status);
        Assert.Equal("rate_limited", blocked.Error);
        Assert.Equal(60, blocked.RetryAfterSeconds);

        time.UtcNow = Start.AddSeconds(61);
        Assert.Null(await limiter.AdmitAsync(hash, CancellationToken.None));
    }

    [Fact]
    public async Task Twenty_per_hour_is_enforced_when_the_minute_window_would_allow_it()
    {
        var time = new FixedTimeProvider(Start);
        MemoryContactRateLimiter limiter = Limiter(time, perMinute: 100, perHour: 20);
        byte[] hash = Hash("203.0.113.9");

        for (int i = 0; i < 20; i++)
        {
            time.UtcNow = Start.AddSeconds(i * 10);
            Assert.Null(await limiter.AdmitAsync(hash, CancellationToken.None));
        }

        time.UtcNow = Start.AddMinutes(4);
        ContactSubmitResult blocked = (await limiter.AdmitAsync(hash, CancellationToken.None))!;
        Assert.Equal(ContactSubmitStatus.RateLimited, blocked.Status);
        Assert.InRange(blocked.RetryAfterSeconds!.Value, 1, 3600);
    }

    [Fact]
    public async Task A_rejected_attempt_stays_in_the_window()
    {
        var time = new FixedTimeProvider(Start);
        MemoryContactRateLimiter limiter = Limiter(time, perMinute: 1, perHour: 100);
        byte[] hash = Hash("203.0.113.10");

        Assert.Null(await limiter.AdmitAsync(hash, CancellationToken.None));
        time.UtcNow = Start.AddSeconds(30);
        Assert.NotNull(await limiter.AdmitAsync(hash, CancellationToken.None));

        // The first hit has aged out. The rejected hit at 30s has not,
        // so this attempt is still over the limit.
        time.UtcNow = Start.AddSeconds(61);
        Assert.NotNull(await limiter.AdmitAsync(hash, CancellationToken.None));
    }

    [Fact]
    public async Task The_entry_cap_evicts_the_least_recently_seen_hash()
    {
        var time = new FixedTimeProvider(Start);
        MemoryContactRateLimiter limiter = Limiter(time, perMinute: 1, perHour: 100, maxEntries: 2);

        Assert.Null(await limiter.AdmitAsync(Hash("203.0.113.1"), CancellationToken.None));
        Assert.Null(await limiter.AdmitAsync(Hash("203.0.113.2"), CancellationToken.None));
        Assert.Null(await limiter.AdmitAsync(Hash("203.0.113.3"), CancellationToken.None));

        // 203.0.113.1 was the least recent, so its bucket was dropped.
        // A fresh bucket is allowed even though the minute limit is 1.
        Assert.Null(await limiter.AdmitAsync(Hash("203.0.113.1"), CancellationToken.None));
        Assert.NotNull(await limiter.AdmitAsync(Hash("203.0.113.1"), CancellationToken.None));
    }

    [Fact]
    public async Task Entries_older_than_an_hour_are_evicted()
    {
        var time = new FixedTimeProvider(Start);
        MemoryContactRateLimiter limiter = Limiter(time, perMinute: 1, perHour: 1, maxEntries: 1);
        byte[] first = Hash("203.0.113.1");
        byte[] second = Hash("203.0.113.2");

        Assert.Null(await limiter.AdmitAsync(first, CancellationToken.None));
        time.UtcNow = Start.AddHours(2);
        Assert.Null(await limiter.AdmitAsync(second, CancellationToken.None));
        Assert.Null(await limiter.AdmitAsync(first, CancellationToken.None));
    }

    private static MemoryContactRateLimiter Limiter(
        TimeProvider time,
        int perMinute,
        int perHour,
        int maxEntries = 100
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new ContactApiOptions
                {
                    MemoryIpPerMinute = perMinute,
                    MemoryIpPerHour = perHour,
                    MemoryMaxEntries = maxEntries,
                }
            ),
            time
        );

    private static byte[] Hash(string address)
    {
        var hasher = new HmacIpAddressHasher(
            Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { IpHashKey = ContactTestSupport.Key })
        );
        return hasher.TryHash(IpAddressNormalizer.ForHash(address))!;
    }
}
