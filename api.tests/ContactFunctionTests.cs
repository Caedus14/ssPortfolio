using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Contact;
using Portfolio.Api.Functions;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactFunctionTests
{
    [Fact]
    public async Task Post_returns_501_after_the_checks_pass()
    {
        var time = new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
        var submitter = new RecordingSubmitter();
        var function = new ContactFunction(ContactTestSupport.Pipeline(ContactTestSupport.Key, time, submitter));
        Assert.True(FormTokenCodec.TryIssue(ContactTestSupport.Key, time.UtcNow, out string token));
        time.UtcNow += TimeSpan.FromSeconds(3);

        IActionResult result = await function.Run(
            ContactTestSupport.Json(ContactTestSupport.ValidJson(token)).Request,
            CancellationToken.None
        );

        ObjectResult body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, body.StatusCode);
        ContactErrorBody error = Assert.IsType<ContactErrorBody>(body.Value);
        Assert.Equal("not_implemented", error.Error);
        Assert.Null(error.Fields);
        Assert.Equal(1, submitter.Calls);
    }

    [Fact]
    public async Task Post_maps_a_honeypot_to_200()
    {
        var function = new ContactFunction(
            ContactTestSupport.Pipeline(key: null, TimeProvider.System, new RecordingSubmitter())
        );

        IActionResult result = await function.Run(
            ContactTestSupport.Json("{\"website\":\"filled\"}").Request,
            CancellationToken.None
        );

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(ContactAcceptedBody.Instance, ok.Value);
    }

    [Fact]
    public void Procedure_codes_match_the_database_contract()
    {
        // db/README.md. These are the numbers usp_SubmitContact returns.
        // HTTP 501 from the placeholder is a different thing and is not in this list.
        Assert.Equal(0, ContactProcedureCodes.Accepted);
        Assert.Equal(1, ContactProcedureCodes.RateLimitedIp);
        Assert.Equal(2, ContactProcedureCodes.RateLimitedGlobal);
        Assert.Equal(3, ContactProcedureCodes.Invalid);
        Assert.Equal(5, ContactProcedureCodes.Busy);
    }
}
