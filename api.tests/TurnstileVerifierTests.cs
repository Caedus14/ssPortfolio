using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Contact;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class TurnstileVerifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-27T21:00:00Z");

    [Fact]
    public async Task A_fresh_contact_token_for_an_allowed_host_passes_and_sends_the_documented_fields()
    {
        StubHandler handler = Ok(SuccessBody(Now, "sethstearns.dev", "contact"));
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync(
            TurnstileTestKeys.DummyToken,
            "203.0.113.10",
            CancellationToken.None
        );

        Assert.Equal(TurnstileOutcome.Passed, verdict.Outcome);
        Dictionary<string, string> sent = Sent(handler);
        Assert.Equal(TurnstileTestKeys.AlwaysPassSecret, sent["secret"]);
        Assert.Equal(TurnstileTestKeys.DummyToken, sent["response"]);
        Assert.Equal("203.0.113.10", sent["remoteip"]);
        Assert.True(Guid.TryParse(sent["idempotency_key"], out _));
        Assert.Equal(TurnstileVerifier.SiteverifyUrl, handler.Last!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Remote_ip_is_omitted_when_the_caller_has_none()
    {
        StubHandler handler = Ok(SuccessBody(Now, "sethstearns.dev", "contact"));
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        await verifier.VerifyAsync(TurnstileTestKeys.DummyToken, remoteIp: null, CancellationToken.None);

        Assert.False(Sent(handler).ContainsKey("remoteip"));
    }

    [Fact]
    public async Task The_always_fail_test_shape_is_a_400_rejection()
    {
        StubHandler handler = Ok("""{"success":false,"error-codes":["invalid-input-response"]}""");
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysFailSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Rejected, verdict.Outcome);
        Assert.Equal("invalid-input-response", verdict.Reason);
    }

    [Fact]
    public async Task The_already_spent_test_shape_asks_for_a_retry()
    {
        StubHandler handler = Ok("""{"success":false,"error-codes":["timeout-or-duplicate"]}""");
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.TokenAlreadySpentSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Retry, verdict.Outcome);
        Assert.Equal("timeout-or-duplicate", verdict.Reason);
    }

    [Fact]
    public async Task An_invalid_secret_is_unavailable_and_does_not_echo_the_secret()
    {
        var logger = new ListLogger<TurnstileVerifier>();
        StubHandler handler = Ok("""{"success":false,"error-codes":["invalid-input-secret"]}""");
        TurnstileVerifier verifier = Verifier("not-the-real-secret", handler, Now, logger);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
        Assert.Equal("invalid_secret", verdict.Reason);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        Assert.DoesNotContain("not-the-real-secret", logger.Entries[0].Message);
    }

    [Fact]
    public async Task A_missing_secret_does_not_call_siteverify()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("called"));
        TurnstileVerifier verifier = Verifier(secret: "  ", handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
        Assert.Equal("missing_secret", verdict.Reason);
        Assert.Null(handler.Last);
    }

    [Theory]
    [InlineData("internal-error")]
    [InlineData("bad-request")]
    public async Task A_cloudflare_side_error_code_is_unavailable(string code)
    {
        StubHandler handler = Ok("{\"success\":false,\"error-codes\":[\"" + code + "\"]}");
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
        Assert.Equal(code, verdict.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task A_cloudflare_http_error_is_unavailable(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("nope"),
        });
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
    }

    [Fact]
    public async Task A_timeout_or_network_error_is_unavailable_and_does_not_throw()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("down"));
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
        Assert.Equal("siteverify_unreachable", verdict.Reason);
    }

    [Fact]
    public async Task The_caller_cancelling_is_not_turned_into_a_verdict()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            verifier.VerifyAsync("token", null, canceled.Token)
        );
    }

    [Fact]
    public async Task A_client_timeout_is_unavailable()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Unavailable, verdict.Outcome);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("example.com")]
    public async Task A_production_success_for_another_host_is_rejected(string hostname)
    {
        StubHandler handler = Ok(SuccessBody(Now, hostname, "contact"));
        TurnstileVerifier verifier = Verifier("production-secret", handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Rejected, verdict.Outcome);
        Assert.Equal("hostname", verdict.Reason);
    }

    [Fact]
    public async Task A_wrong_action_is_rejected()
    {
        StubHandler handler = Ok(SuccessBody(Now, "sethstearns.dev", "login"));
        TurnstileVerifier verifier = Verifier("production-secret", handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Rejected, verdict.Outcome);
        Assert.Equal("action", verdict.Reason);
    }

    [Fact]
    public async Task Challenge_ts_is_accepted_at_300_seconds_and_rejected_one_millisecond_later()
    {
        DateTimeOffset solved = Now.AddMilliseconds(-ContactLimits.TurnstileMaxAgeMilliseconds);
        TurnstileVerifier fresh = Verifier(
            "production-secret",
            Ok(SuccessBody(solved, "sethstearns.dev", "contact")),
            Now
        );
        TurnstileVerifier stale = Verifier(
            "production-secret",
            Ok(SuccessBody(solved.AddMilliseconds(-1), "sethstearns.dev", "contact")),
            Now
        );

        Assert.Equal(TurnstileOutcome.Passed, (await fresh.VerifyAsync("token", null, CancellationToken.None)).Outcome);
        TurnstileVerdict expired = await stale.VerifyAsync("token", null, CancellationToken.None);
        Assert.Equal(TurnstileOutcome.Retry, expired.Outcome);
        Assert.Equal("challenge_ts_expired", expired.Reason);
    }

    [Fact]
    public async Task A_challenge_more_than_30_seconds_in_the_future_is_rejected()
    {
        StubHandler handler = Ok(
            SuccessBody(Now.AddMilliseconds(ContactLimits.FutureSkewMilliseconds + 1), "sethstearns.dev", "contact")
        );
        TurnstileVerifier verifier = Verifier("production-secret", handler, Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Rejected, verdict.Outcome);
        Assert.Equal("challenge_ts_future", verdict.Reason);
    }

    [Fact]
    public async Task The_published_pass_key_shape_allows_example_com_without_an_action()
    {
        const string body = """
            {"success":true,"error-codes":[],"hostname":"example.com","challenge_ts":"2026-10-27T21:00:00.000Z","metadata":{"result_with_testing_key":true}}
            """;
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, Ok(body), Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync(
            TurnstileTestKeys.DummyToken,
            null,
            CancellationToken.None
        );

        Assert.Equal(TurnstileOutcome.Passed, verdict.Outcome);
    }

    [Fact]
    public async Task A_testing_key_response_for_a_different_host_is_still_rejected()
    {
        const string body = """
            {"success":true,"error-codes":[],"hostname":"evil.example","action":"contact","challenge_ts":"2026-10-27T21:00:00.000Z","metadata":{"result_with_testing_key":true}}
            """;
        TurnstileVerifier verifier = Verifier(TurnstileTestKeys.AlwaysPassSecret, Ok(body), Now);

        TurnstileVerdict verdict = await verifier.VerifyAsync("token", null, CancellationToken.None);

        Assert.Equal(TurnstileOutcome.Rejected, verdict.Outcome);
        Assert.Equal("hostname", verdict.Reason);
    }

    [Fact]
    public void The_http_timeout_is_eight_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(8), TurnstileVerifier.Timeout);
    }

    private static TurnstileVerifier Verifier(
        string? secret,
        StubHandler handler,
        DateTimeOffset now,
        ILogger<TurnstileVerifier>? logger = null
    )
    {
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        return new TurnstileVerifier(
            http,
            Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { TurnstileSecret = secret }),
            new FixedTimeProvider(now),
            logger ?? NullLogger<TurnstileVerifier>.Instance
        );
    }

    private static StubHandler Ok(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    private static string SuccessBody(DateTimeOffset solved, string hostname, string action) =>
        "{\"success\":true,\"error-codes\":[],\"hostname\":\""
        + hostname
        + "\",\"action\":\""
        + action
        + "\",\"challenge_ts\":\""
        + solved.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        + "\"}";

    private static Dictionary<string, string> Sent(StubHandler handler)
    {
        Assert.NotNull(handler.Body);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(handler.Body!)!;
    }
}

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public HttpRequestMessage? Last { get; private set; }

    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Last = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return _respond(request);
    }
}
