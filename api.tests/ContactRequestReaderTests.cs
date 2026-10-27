using System.Text;
using Microsoft.AspNetCore.Http;
using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactRequestReaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/plain")]
    [InlineData("application/json-patch+json")]
    [InlineData("text/json")]
    public async Task Wrong_content_type_is_415_and_the_body_is_not_read(string? contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new ThrowOnReadStream();

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.UnsupportedMediaType, result.Status);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("APPLICATION/JSON")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/json;charset=UTF-8")]
    public async Task Json_content_type_is_accepted(string contentType)
    {
        DefaultHttpContext context = ContactTestSupport.Json("{\"name\":\"Ada\"}", contentType);

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.Ok, result.Status);
        Assert.Equal("Ada", result.Body!.Name);
    }

    [Fact]
    public async Task Content_length_over_the_cap_does_not_read_the_body()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = ContactLimits.MaxBodyBytes + 1;
        context.Request.Body = new ThrowOnReadStream();

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.PayloadTooLarge, result.Status);
    }

    [Fact]
    public async Task A_lying_small_content_length_still_stops_at_the_cap()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(new string('{', ContactLimits.MaxBodyBytes + 1));
        DefaultHttpContext context = ContactTestSupport.Bytes(bytes, "application/json", contentLength: 2);

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.PayloadTooLarge, result.Status);
    }

    [Fact]
    public async Task A_body_of_exactly_8_kib_is_accepted()
    {
        DefaultHttpContext context = ContactTestSupport.Json(PaddedObject(ContactLimits.MaxBodyBytes));

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.Ok, result.Status);
        Assert.Equal(ContactLimits.MaxBodyBytes, context.Request.ContentLength);
    }

    [Fact]
    public async Task One_byte_over_the_cap_is_rejected_when_content_length_is_missing()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(PaddedObject(ContactLimits.MaxBodyBytes + 1));
        DefaultHttpContext context = ContactTestSupport.Bytes(bytes, "application/json", contentLength: null);

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.PayloadTooLarge, result.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"name\":1}")]
    [InlineData("{\"name\":{\"nested\":true}}")]
    [InlineData("{\"name\":\"Ada\",}")]
    [InlineData("{/* note */\"name\":\"Ada\"}")]
    public async Task Bad_json_is_400(string json)
    {
        DefaultHttpContext context = ContactTestSupport.Json(json);

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.InvalidJson, result.Status);
    }

    [Fact]
    public async Task Invalid_utf8_is_invalid_json()
    {
        DefaultHttpContext context = ContactTestSupport.Bytes([0xFF], "application/json", contentLength: 1);

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.InvalidJson, result.Status);
    }

    [Fact]
    public async Task Unknown_fields_are_ignored()
    {
        DefaultHttpContext context = ContactTestSupport.Json(
            "{\"name\":\"Ada\",\"extra\":1,\"website\":\"\"}"
        );

        ContactReadResult result = await ContactRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None
        );

        Assert.Equal(ContactReadStatus.Ok, result.Status);
        Assert.Equal("Ada", result.Body!.Name);
        Assert.Equal("", result.Body.Website);
    }

    private static string PaddedObject(int length)
    {
        const string prefix = "{\"pad\":\"";
        const string suffix = "\"}";
        return prefix + new string(' ', length - prefix.Length - suffix.Length) + suffix;
    }
}
