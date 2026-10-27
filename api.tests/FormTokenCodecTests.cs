using System.Security.Cryptography;
using System.Text;
using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class FormTokenCodecTests
{
    private const string Key = ContactTestSupport.Key;
    private const long IssuedMs = 1_700_000_000_000;

    [Fact]
    public void A_token_is_valid_at_3_seconds_and_at_exactly_2_hours()
    {
        string token = Issue(IssuedMs);

        Assert.Equal(FormTokenVerdict.Valid, Verify(token, IssuedMs + ContactLimits.MinimumAgeMilliseconds));
        Assert.Equal(FormTokenVerdict.Valid, Verify(token, IssuedMs + ContactLimits.MaximumAgeMilliseconds));
    }

    [Fact]
    public void A_token_one_millisecond_under_3_seconds_is_too_fast()
    {
        string token = Issue(IssuedMs);

        Assert.Equal(
            FormTokenVerdict.TooFast,
            Verify(token, IssuedMs + ContactLimits.MinimumAgeMilliseconds - 1)
        );
    }

    [Fact]
    public void A_token_one_millisecond_over_2_hours_is_expired()
    {
        string token = Issue(IssuedMs);

        Assert.Equal(
            FormTokenVerdict.Expired,
            Verify(token, IssuedMs + ContactLimits.MaximumAgeMilliseconds + 1)
        );
    }

    [Fact]
    public void The_same_token_can_be_checked_twice_until_it_expires()
    {
        string token = Issue(IssuedMs);
        long at = IssuedMs + 10_000;

        Assert.Equal(FormTokenVerdict.Valid, Verify(token, at));
        Assert.Equal(FormTokenVerdict.Valid, Verify(token, at));
    }

    [Fact]
    public void A_tampered_payload_is_invalid()
    {
        string token = Issue(IssuedMs);
        int dot = token.IndexOf('.');
        char flipped = token[0] == 'A' ? 'B' : 'A';
        string tampered = flipped + token[1..];

        Assert.NotEqual(token, tampered);
        Assert.Equal(FormTokenVerdict.Invalid, Verify(tampered, IssuedMs + 10_000));
        Assert.Contains('.', tampered);
        Assert.Equal(dot, tampered.IndexOf('.'));
    }

    [Fact]
    public void A_tampered_signature_is_invalid()
    {
        string token = Issue(IssuedMs);
        int dot = token.IndexOf('.');
        char flipped = token[dot + 1] == 'A' ? 'B' : 'A';
        string tampered = token[..(dot + 1)] + flipped + token[(dot + 2)..];

        Assert.Equal(FormTokenVerdict.Invalid, Verify(tampered, IssuedMs + 10_000));
    }

    [Fact]
    public void A_token_signed_with_another_key_is_invalid()
    {
        Assert.True(FormTokenCodec.TryIssue("aaaaaaaaaaaaaaaa", At(IssuedMs), out string forged));

        Assert.Equal(FormTokenVerdict.Invalid, Verify(forged, IssuedMs + 10_000));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_token_does_not_need_the_key(string? token)
    {
        Assert.Equal(FormTokenVerdict.Missing, FormTokenCodec.Verify(null, token, At(IssuedMs)));
        Assert.Equal(FormTokenVerdict.Missing, FormTokenCodec.Verify("short", token, At(IssuedMs)));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("abc.")]
    [InlineData(".abc")]
    [InlineData("a.b.c")]
    [InlineData("AAAA.YQ")]
    public void A_bad_shape_or_a_short_signature_is_invalid_and_does_not_throw(string token)
    {
        FormTokenVerdict verdict = FormTokenCodec.Verify(Key, token, At(IssuedMs));

        Assert.Equal(FormTokenVerdict.Invalid, verdict);
    }

    [Fact]
    public void A_non_numeric_payload_with_a_valid_mac_is_invalid()
    {
        string payload = Base64Url("not-a-time");
        string signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(payload)));

        Assert.Equal(FormTokenVerdict.Invalid, FormTokenCodec.Verify(Key, payload + "." + signature, At(IssuedMs)));
    }

    [Fact]
    public void More_than_30_seconds_in_the_future_is_invalid()
    {
        string token = Issue(IssuedMs + ContactLimits.FutureSkewMilliseconds + 1);

        Assert.Equal(FormTokenVerdict.Invalid, Verify(token, IssuedMs));
    }

    [Fact]
    public void Thirty_seconds_of_future_skew_is_too_fast()
    {
        string token = Issue(IssuedMs + ContactLimits.FutureSkewMilliseconds);

        Assert.Equal(FormTokenVerdict.TooFast, Verify(token, IssuedMs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fifteen-chars!!")]
    public void A_missing_or_short_key_cannot_issue_or_verify(string? key)
    {
        Assert.False(FormTokenCodec.IsUsableKey(key));
        Assert.False(FormTokenCodec.TryIssue(key, At(IssuedMs), out string token));
        Assert.Equal("", token);
        Assert.Equal(FormTokenVerdict.Unavailable, FormTokenCodec.Verify(key, "present", At(IssuedMs)));
    }

    [Fact]
    public void The_key_is_used_as_stored_including_a_trailing_space()
    {
        const string spaced = "0123456789abcdef ";
        Assert.True(FormTokenCodec.TryIssue(spaced, At(IssuedMs), out string token));

        Assert.Equal(FormTokenVerdict.Valid, FormTokenCodec.Verify(spaced, token, At(IssuedMs + 5_000)));
        Assert.Equal(FormTokenVerdict.Invalid, FormTokenCodec.Verify(spaced.Trim(), token, At(IssuedMs + 5_000)));
    }

    private static string Issue(long unixMilliseconds)
    {
        Assert.True(FormTokenCodec.TryIssue(Key, At(unixMilliseconds), out string token));
        return token;
    }

    private static FormTokenVerdict Verify(string token, long unixMilliseconds) =>
        FormTokenCodec.Verify(Key, token, At(unixMilliseconds));

    private static DateTimeOffset At(long unixMilliseconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);

    private static string Base64Url(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
