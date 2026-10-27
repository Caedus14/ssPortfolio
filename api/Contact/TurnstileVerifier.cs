using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

public enum TurnstileOutcome
{
    Passed,
    Unavailable,
    Retry,
    Rejected,
}

/// <summary>
/// Outcome of one siteverify call. <see cref="Reason" /> is safe to log:
/// it is an error code or a short label, never the token and never the secret.
/// </summary>
public sealed record TurnstileVerdict(TurnstileOutcome Outcome, string Reason)
{
    public static TurnstileVerdict Passed() => new(TurnstileOutcome.Passed, "passed");
}

public interface ITurnstileVerifier
{
    Task<TurnstileVerdict> VerifyAsync(
        string token,
        string? remoteIp,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Cloudflare's published test keys. They are not secrets.
/// The always-pass secret accepts every dummy token. Do not put it in Azure.
/// Production uses TURNSTILE_SECRET from the Static Web App settings.
/// </summary>
public static class TurnstileTestKeys
{
    public const string AlwaysPassSecret = "1x0000000000000000000000000000000AA";
    public const string AlwaysFailSecret = "2x0000000000000000000000000000000AA";
    public const string TokenAlreadySpentSecret = "3x0000000000000000000000000000000AA";

    public const string AlwaysPassSiteKey = "1x00000000000000000000AA";
    public const string AlwaysBlockSiteKey = "2x00000000000000000000AB";
    public const string ForceInteractiveSiteKey = "3x00000000000000000000FF";

    public const string DummyToken = "XXXX.DUMMY.TOKEN.XXXX";
}

public sealed class SiteverifyResponse
{
    public bool Success { get; set; }

    [JsonPropertyName("challenge_ts")]
    public DateTimeOffset? ChallengeTimestamp { get; set; }

    public string? Hostname { get; set; }

    public string? Action { get; set; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; set; }

    public SiteverifyMetadata? Metadata { get; set; }
}

public sealed class SiteverifyMetadata
{
    [JsonPropertyName("result_with_testing_key")]
    public bool ResultWithTestingKey { get; set; }
}

/// <summary>
/// POST https://challenges.cloudflare.com/turnstile/v0/siteverify.
/// The browser token is not proof until Cloudflare says so. Tokens last 300 seconds
/// and each one can be redeemed once.
/// </summary>
public sealed class TurnstileVerifier : ITurnstileVerifier
{
    public const string SiteverifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IOptions<ContactApiOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TurnstileVerifier> _logger;

    public TurnstileVerifier(
        HttpClient http,
        IOptions<ContactApiOptions> options,
        TimeProvider time,
        ILogger<TurnstileVerifier> logger
    )
    {
        _http = http;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<TurnstileVerdict> VerifyAsync(
        string token,
        string? remoteIp,
        CancellationToken cancellationToken
    )
    {
        string? secret = _options.Value.TurnstileSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("TURNSTILE_SECRET is missing. Refusing to call siteverify.");
            return new TurnstileVerdict(TurnstileOutcome.Unavailable, "missing_secret");
        }

        var body = new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token,
            // A retried POST of this same call can send this key again and Cloudflare
            // returns the first verdict instead of timeout-or-duplicate.
            // This step does not retry. A second submit from the browser is a new
            // call with a new key, so a spent token still fails.
            ["idempotency_key"] = Guid.NewGuid().ToString("D"),
        };
        if (!string.IsNullOrEmpty(remoteIp))
        {
            body["remoteip"] = remoteIp;
        }

        SiteverifyResponse? parsed;
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                SiteverifyUrl,
                body,
                cancellationToken
            );
            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Turnstile siteverify returned HTTP {StatusCode}.",
                    (int)response.StatusCode
                );
                return new TurnstileVerdict(TurnstileOutcome.Unavailable, "http_" + (int)response.StatusCode);
            }

            parsed = JsonSerializer.Deserialize<SiteverifyResponse>(json, JsonOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            _logger.LogError(ex, "Turnstile siteverify could not be completed.");
            return new TurnstileVerdict(TurnstileOutcome.Unavailable, "siteverify_unreachable");
        }

        if (parsed is null)
        {
            _logger.LogError("Turnstile siteverify returned an empty body.");
            return new TurnstileVerdict(TurnstileOutcome.Unavailable, "empty_body");
        }

        return Judge(parsed);
    }

    private TurnstileVerdict Judge(SiteverifyResponse parsed)
    {
        string[] codes = parsed.ErrorCodes ?? [];
        if (codes.Any(code => code is "invalid-input-secret" or "missing-input-secret"))
        {
            _logger.LogError(
                "TURNSTILE_SECRET was rejected by Cloudflare ({ErrorCodes}).",
                string.Join(",", codes)
            );
            return new TurnstileVerdict(TurnstileOutcome.Unavailable, "invalid_secret");
        }

        if (!parsed.Success)
        {
            if (codes.Contains("timeout-or-duplicate", StringComparer.Ordinal))
            {
                return new TurnstileVerdict(TurnstileOutcome.Retry, "timeout-or-duplicate");
            }

            if (codes.Any(code => code is "internal-error" or "bad-request"))
            {
                _logger.LogError("Turnstile siteverify failed ({ErrorCodes}).", string.Join(",", codes));
                return new TurnstileVerdict(TurnstileOutcome.Unavailable, string.Join(",", codes));
            }

            _logger.LogWarning("Turnstile rejected the token ({ErrorCodes}).", string.Join(",", codes));
            return new TurnstileVerdict(TurnstileOutcome.Rejected, codes.Length == 0 ? "rejected" : string.Join(",", codes));
        }

        bool testingKey = parsed.Metadata?.ResultWithTestingKey == true;
        if (!TurnstileHostnames.IsAllowed(parsed.Hostname, _options.Value.TurnstileAllowedHosts))
        {
            bool dummyHost = testingKey
                && TurnstileHostnames.TryNormalize(parsed.Hostname, out string host)
                && host == TurnstileHostnames.TestingKeyHostname;
            if (!dummyHost)
            {
                _logger.LogWarning("Turnstile hostname {Hostname} is not allowed.", parsed.Hostname);
                return new TurnstileVerdict(TurnstileOutcome.Rejected, "hostname");
            }
        }

        string expected = ExpectedAction();
        bool testingOmittedAction = testingKey && string.IsNullOrEmpty(parsed.Action);
        if (!testingOmittedAction && !string.Equals(parsed.Action, expected, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Turnstile action {Action} did not match {ExpectedAction}.",
                parsed.Action,
                expected
            );
            return new TurnstileVerdict(TurnstileOutcome.Rejected, "action");
        }

        if (parsed.ChallengeTimestamp is not DateTimeOffset solved)
        {
            _logger.LogWarning("Turnstile success response had no challenge_ts.");
            return new TurnstileVerdict(TurnstileOutcome.Rejected, "missing_challenge_ts");
        }

        long age = _time.GetUtcNow().ToUnixTimeMilliseconds() - solved.ToUnixTimeMilliseconds();
        if (age < -ContactLimits.FutureSkewMilliseconds)
        {
            return new TurnstileVerdict(TurnstileOutcome.Rejected, "challenge_ts_future");
        }

        if (age > ContactLimits.TurnstileMaxAgeMilliseconds)
        {
            return new TurnstileVerdict(TurnstileOutcome.Retry, "challenge_ts_expired");
        }

        return TurnstileVerdict.Passed();
    }

    private string ExpectedAction()
    {
        string? configured = _options.Value.TurnstileExpectedAction;
        return string.IsNullOrWhiteSpace(configured) ? "contact" : configured.Trim();
    }
}
