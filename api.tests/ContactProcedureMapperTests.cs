using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactProcedureMapperTests
{
    [Fact]
    public void Every_procedure_code_maps_to_the_http_contract()
    {
        Assert.Equal(ContactSubmitStatus.Accepted, ContactProcedureMapper.Map(0, null).Status);

        ContactSubmitResult ip = ContactProcedureMapper.Map(1, 42);
        Assert.Equal(ContactSubmitStatus.RateLimited, ip.Status);
        Assert.Equal("rate_limited", ip.Error);
        Assert.Equal(42, ip.RetryAfterSeconds);

        ContactSubmitResult global = ContactProcedureMapper.Map(2, 600);
        Assert.Equal(ContactSubmitStatus.RateLimited, global.Status);
        Assert.Equal(600, global.RetryAfterSeconds);

        ContactSubmitResult invalid = ContactProcedureMapper.Map(3, null);
        Assert.Equal(ContactSubmitStatus.InvalidFields, invalid.Status);
        Assert.Equal("invalid", invalid.Error);
        Assert.Null(invalid.RetryAfterSeconds);

        ContactSubmitResult busy = ContactProcedureMapper.Map(5, 5);
        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, busy.Status);
        Assert.Equal("unavailable", busy.Error);
        Assert.Equal(5, busy.RetryAfterSeconds);

        ContactSubmitResult unknown = ContactProcedureMapper.Map(9, null);
        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, unknown.Status);
        Assert.Equal(ContactLimits.UnavailableRetryAfterSeconds, unknown.RetryAfterSeconds);
    }

    [Fact]
    public void A_missing_retry_uses_the_fallback()
    {
        Assert.Equal(ContactLimits.RateLimitFallbackRetryAfterSeconds, ContactProcedureMapper.Map(1, null).RetryAfterSeconds);
        Assert.Equal(ContactLimits.RateLimitFallbackRetryAfterSeconds, ContactProcedureMapper.Map(2, null).RetryAfterSeconds);
        Assert.Equal(ContactLimits.UnavailableRetryAfterSeconds, ContactProcedureMapper.Map(5, null).RetryAfterSeconds);
    }

    [Theory]
    [InlineData(40613, true)]
    [InlineData(40197, true)]
    [InlineData(40501, true)]
    [InlineData(49918, true)]
    [InlineData(49919, true)]
    [InlineData(49920, true)]
    [InlineData(4060, true)]
    [InlineData(-2, true)]
    [InlineData(18456, false)]
    [InlineData(2627, false)]
    public void Transient_sql_numbers_are_the_wake_up_and_timeout_set(int number, bool transientFailure)
    {
        Assert.Equal(transientFailure, SqlTransientErrors.IsTransient(number));
    }

    [Fact]
    public void Rate_limit_and_unavailable_responses_carry_retry_after()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        IActionResult limited = ContactResponses.From(ContactSubmitResult.RateLimited(30));
        IActionResult unavailable = ContactResponses.From(
            ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds)
        );

        ContactObjectResult limitedResult = Assert.IsType<ContactObjectResult>(limited);
        ContactObjectResult unavailableResult = Assert.IsType<ContactObjectResult>(unavailable);
        Assert.Equal(StatusCodes.Status429TooManyRequests, limitedResult.StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailableResult.StatusCode);

        var http = new DefaultHttpContext();
        limitedResult.ApplyHeaders(http.Response);
        Assert.Equal("30", http.Response.Headers.RetryAfter.ToString());

        Assert.Equal(
            "{\"error\":\"rate_limited\",\"retryAfterSeconds\":30}",
            JsonSerializer.Serialize(limitedResult.Value, options)
        );
        Assert.Equal(
            "{\"error\":\"unavailable\",\"retryAfterSeconds\":15}",
            JsonSerializer.Serialize(unavailableResult.Value, options)
        );
        ObjectResult invalid = Assert.IsAssignableFrom<ObjectResult>(
            ContactResponses.From(ContactProcedureMapper.Map(3, null))
        );
        Assert.Equal(
            "{\"error\":\"invalid\"}",
            JsonSerializer.Serialize(invalid.Value, options)
        );
    }
}
