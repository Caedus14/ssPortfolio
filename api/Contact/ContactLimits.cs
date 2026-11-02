namespace Portfolio.Api.Contact;

/// <summary>Numbers shared by the reader, the token check, and the field checks.</summary>
public static class ContactLimits
{
    /// <summary>
    /// Raw body cap. 8 KiB is enough for a normal 2000-character message
    /// and small enough that a client cannot stream a large body into the function.
    /// A message of 2000 characters that are all 4-byte UTF-8 may not fit,
    /// and that request is rejected here before field validation.
    /// </summary>
    public const int MaxBodyBytes = 8 * 1024;

    public const int NameMinLength = 1;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 2000;
    public const int MaxLinks = 2;

    public const long MinimumAgeMilliseconds = 3_000;
    public const long MaximumAgeMilliseconds = 2 * 60 * 60 * 1000;
    public const long FutureSkewMilliseconds = 30_000;

    /// <summary>Cloudflare's maximum token length. Longer values are rejected without a siteverify call.</summary>
    public const int TurnstileTokenMaxLength = 2048;

    /// <summary>A Turnstile token is valid for 300 seconds after Cloudflare solves it.</summary>
    public const long TurnstileMaxAgeMilliseconds = 300_000;

    public const int MinimumIpHashKeyLength = 16;

    /// <summary>How long the caller should wait when SQL is waking or the lock is busy and the procedure did not say.</summary>
    public const int UnavailableRetryAfterSeconds = 15;

    /// <summary>Used when a rate-limit result has no RetryAfterSeconds. One minute is long enough to stop a tight retry loop.</summary>
    public const int RateLimitFallbackRetryAfterSeconds = 60;

    public const int MemoryDefaultPerMinute = 5;
    public const int MemoryDefaultPerHour = 20;
    public const int MemoryDefaultMaxEntries = 4096;
    public const int MemoryMaxTimestampsPerEntry = 64;

    public const int SqlConnectTimeoutSeconds = 5;
    public const int SqlCommandTimeoutSeconds = 5;
    public const long SqlRetryBudgetMilliseconds = 20_000;

    /// <summary>
    /// Shorter keys are treated as missing. A 16-character key is the floor.
    /// Generate a longer one. See api/README.md.
    /// </summary>
    public const int MinimumFormTokenKeyLength = 16;
}
