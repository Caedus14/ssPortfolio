using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactPipelineTests
{
    private static readonly DateTimeOffset Opened = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    [Fact]
    public async Task A_filled_honeypot_is_accepted_and_stores_nothing()
    {
        var submitter = new RecordingSubmitter();
        var logger = new ListLogger<ContactPipeline>();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(key: null, new FixedTimeProvider(Opened), submitter, logger);
        DefaultHttpContext context = ContactTestSupport.Json(
            "{\"website\":\"https://spam.example\",\"formToken\":\"forged\",\"email\":\"not-an-email\"}"
        );

        ContactSubmitResult result = await pipeline.RunAsync(context.Request, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.Accepted, result.Status);
        Assert.Equal(0, submitter.Calls);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
        Assert.DoesNotContain("spam.example", logger.Entries[0].Message);
    }

    [Fact]
    public async Task Whitespace_in_the_honeypot_is_not_a_hit()
    {
        var submitter = new RecordingSubmitter();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(key: null, new FixedTimeProvider(Opened), submitter);
        DefaultHttpContext context = ContactTestSupport.Json("{\"website\":\"   \"}");

        ContactSubmitResult result = await pipeline.RunAsync(context.Request, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.FormTokenRejected, result.Status);
        Assert.Equal("form_token_required", result.Error);
        Assert.Equal(0, submitter.Calls);
    }

    [Fact]
    public async Task A_missing_token_asks_for_a_reload_without_the_key()
    {
        var logger = new ListLogger<ContactPipeline>();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(
            key: null,
            new FixedTimeProvider(Opened),
            new RecordingSubmitter(),
            logger
        );

        ContactSubmitResult result = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"name\":\"Ada\"}").Request,
            CancellationToken.None
        );

        Assert.Equal("form_token_required", result.Error);
        Assert.Equal("Reload the page and try again.", result.FieldErrors!["formToken"]);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_present_token_without_a_key_is_503()
    {
        var logger = new ListLogger<ContactPipeline>();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(
            key: "too-short",
            new FixedTimeProvider(Opened),
            new RecordingSubmitter(),
            logger
        );

        ContactSubmitResult result = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"formToken\":\"anything\"}").Request,
            CancellationToken.None
        );

        Assert.Equal(ContactSubmitStatus.FormTokenUnavailable, result.Status);
        Assert.Equal("form_token_unavailable", result.Error);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        Assert.Contains("FORM_TOKEN_KEY", logger.Entries[0].Message);
    }

    [Fact]
    public async Task Too_fast_and_expired_tokens_are_400_and_do_not_reach_field_checks()
    {
        var time = new FixedTimeProvider(Opened);
        var submitter = new RecordingSubmitter();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(ContactTestSupport.Key, time, submitter);
        Assert.True(FormTokenCodec.TryIssue(ContactTestSupport.Key, Opened, out string token));

        time.UtcNow = Opened.AddMilliseconds(ContactLimits.MinimumAgeMilliseconds - 1);
        ContactSubmitResult tooFast = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"formToken\":\"" + token + "\",\"email\":\"nope\"}").Request,
            CancellationToken.None
        );

        time.UtcNow = Opened.AddMilliseconds(ContactLimits.MaximumAgeMilliseconds + 1);
        ContactSubmitResult expired = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"formToken\":\"" + token + "\"}").Request,
            CancellationToken.None
        );

        Assert.Equal("form_token_too_fast", tooFast.Error);
        Assert.Equal("Please wait a few seconds and try again.", tooFast.FieldErrors!["formToken"]);
        Assert.Equal("form_token_expired", expired.Error);
        Assert.Equal(
            "This form has been open too long. Reload the page and try again.",
            expired.FieldErrors!["formToken"]
        );
        Assert.Equal(0, submitter.Calls);
    }

    [Fact]
    public async Task A_forged_token_is_invalid()
    {
        var time = new FixedTimeProvider(Opened.AddSeconds(10));
        ContactPipeline pipeline = ContactTestSupport.Pipeline(
            ContactTestSupport.Key,
            time,
            new RecordingSubmitter()
        );
        Assert.True(FormTokenCodec.TryIssue("other-key-16chars", Opened, out string forged));

        ContactSubmitResult result = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"formToken\":\"" + forged + "\"}").Request,
            CancellationToken.None
        );

        Assert.Equal("form_token_invalid", result.Error);
        Assert.Equal("Reload the page and try again.", result.FieldErrors!["formToken"]);
    }

    [Fact]
    public async Task Field_errors_stop_before_the_submitter()
    {
        var submitter = new RecordingSubmitter();
        (ContactPipeline pipeline, string token) = ReadyPipeline(submitter);

        ContactSubmitResult result = await pipeline.RunAsync(
            ContactTestSupport.Json("{\"name\":\"\",\"email\":\"nope\",\"message\":\"short\",\"formToken\":\"" + token + "\"}").Request,
            CancellationToken.None
        );

        Assert.Equal(ContactSubmitStatus.InvalidFields, result.Status);
        Assert.Equal("invalid", result.Error);
        Assert.Equal(3, result.FieldErrors!.Count);
        Assert.Equal(0, submitter.Calls);
    }

    [Fact]
    public async Task A_valid_request_reaches_the_submitter_and_returns_501()
    {
        var submitter = new RecordingSubmitter();
        (ContactPipeline pipeline, string token) = ReadyPipeline(submitter);

        ContactSubmitResult result = await pipeline.RunAsync(
            ContactTestSupport.Json(ContactTestSupport.ValidJson(token)).Request,
            CancellationToken.None
        );

        Assert.Equal(ContactSubmitStatus.NotImplemented, result.Status);
        Assert.Equal("not_implemented", result.Error);
        Assert.Equal(1, submitter.Calls);
        Assert.Equal("Seth", submitter.Received!.Name);
        Assert.Equal("seth@example.com", submitter.Received.Email);
        Assert.Equal("Hello from the form.", submitter.Received.Message);
        Assert.Equal("ts-1", submitter.Received.TurnstileToken);
    }

    [Theory]
    [InlineData("text/plain", ContactSubmitStatus.UnsupportedMediaType)]
    [InlineData("application/json", ContactSubmitStatus.PayloadTooLarge)]
    public async Task Early_rejections_do_not_call_the_submitter(
        string contentType,
        ContactSubmitStatus expected
    )
    {
        var submitter = new RecordingSubmitter();
        ContactPipeline pipeline = ContactTestSupport.Pipeline(
            ContactTestSupport.Key,
            new FixedTimeProvider(Opened),
            submitter
        );
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.ContentLength = ContactLimits.MaxBodyBytes + 50;
        context.Request.Body = new ThrowOnReadStream();

        ContactSubmitResult result = await pipeline.RunAsync(context.Request, CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Equal(0, submitter.Calls);
    }

    private static (ContactPipeline Pipeline, string Token) ReadyPipeline(IContactSubmitter submitter)
    {
        var time = new FixedTimeProvider(Opened);
        Assert.True(FormTokenCodec.TryIssue(ContactTestSupport.Key, Opened, out string token));
        time.UtcNow = Opened.AddSeconds(3);
        return (ContactTestSupport.Pipeline(ContactTestSupport.Key, time, submitter), token);
    }
}
