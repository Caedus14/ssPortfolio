using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Contact;
using Portfolio.Api.Functions;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactTokenFunctionTests
{
    [Fact]
    public void Get_returns_a_token_the_submit_check_accepts()
    {
        var time = new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
        var function = new ContactTokenFunction(Tokens(ContactTestSupport.Key, time), NullLogger<ContactTokenFunction>.Instance);

        IActionResult result = function.Run(new DefaultHttpContext().Request);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        FormTokenResponse body = Assert.IsType<FormTokenResponse>(ok.Value);
        time.UtcNow = time.UtcNow.AddSeconds(3);
        Assert.Equal(FormTokenVerdict.Valid, Tokens(ContactTestSupport.Key, time).Verify(body.FormToken));
    }

    [Fact]
    public void Get_without_a_key_is_503_and_does_not_throw()
    {
        var logger = new ListLogger<ContactTokenFunction>();
        var function = new ContactTokenFunction(
            Tokens(key: null, new FixedTimeProvider(DateTimeOffset.UnixEpoch)),
            logger
        );

        IActionResult result = function.Run(new DefaultHttpContext().Request);

        ObjectResult body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, body.StatusCode);
        ContactErrorBody error = Assert.IsType<ContactErrorBody>(body.Value);
        Assert.Equal("form_token_unavailable", error.Error);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
    }

    [Fact]
    public void Error_and_accepted_bodies_serialize_in_camel_case()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Equal(
            "{\"status\":\"accepted\"}",
            JsonSerializer.Serialize(ContactAcceptedBody.Instance, options)
        );
        Assert.Equal(
            "{\"error\":\"not_implemented\"}",
            JsonSerializer.Serialize(new ContactErrorBody("not_implemented"), options)
        );
        Assert.Equal(
            "{\"error\":\"invalid\",\"fields\":{\"name\":\"Name must be 1 to 100 characters.\"}}",
            JsonSerializer.Serialize(
                new ContactErrorBody(
                    "invalid",
                    new Dictionary<string, string> { ["name"] = "Name must be 1 to 100 characters." }
                ),
                options
            )
        );
        Assert.Equal(
            "{\"formToken\":\"abc.def\"}",
            JsonSerializer.Serialize(new FormTokenResponse("abc.def"), options)
        );
    }

    private static FormTokenService Tokens(string? key, TimeProvider time) =>
        new(Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { FormTokenKey = key }), time);
}
