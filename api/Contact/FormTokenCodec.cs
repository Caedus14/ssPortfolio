using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Api.Contact;

/// <summary>
/// Issued-at token. The form cannot mint one, because the HMAC key stays on the server.
/// Format: base64url(utf8(unix-milliseconds)) + "." + base64url(hmac-sha256).
/// The signature covers the first part exactly as it appears in the token.
/// </summary>
public static class FormTokenCodec
{
    public static bool IsUsableKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length >= ContactLimits.MinimumFormTokenKeyLength;

    public static bool TryIssue(string? key, DateTimeOffset now, out string token)
    {
        token = "";
        if (!IsUsableKey(key))
        {
            return false;
        }

        string issuedAt = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        string payload = Base64Url.Encode(Encoding.UTF8.GetBytes(issuedAt));
        string signature = Base64Url.Encode(Sign(key!, payload));
        token = payload + "." + signature;
        return true;
    }

    public static FormTokenVerdict Verify(string? key, string? token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return FormTokenVerdict.Missing;
        }

        if (!IsUsableKey(key))
        {
            return FormTokenVerdict.Unavailable;
        }

        int dot = token.IndexOf('.');
        if (dot <= 0 || token.LastIndexOf('.') != dot)
        {
            return FormTokenVerdict.Invalid;
        }

        string payload = token[..dot];
        string signatureText = token[(dot + 1)..];
        if (!Base64Url.TryDecode(signatureText, out byte[] signature))
        {
            return FormTokenVerdict.Invalid;
        }

        byte[] expected = Sign(key!, payload);
        if (signature.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(signature, expected))
        {
            return FormTokenVerdict.Invalid;
        }

        if (!Base64Url.TryDecode(payload, out byte[] issuedBytes))
        {
            return FormTokenVerdict.Invalid;
        }

        string issuedText;
        try
        {
            issuedText = Encoding.UTF8.GetString(issuedBytes);
        }
        catch (DecoderFallbackException)
        {
            return FormTokenVerdict.Invalid;
        }

        if (!long.TryParse(issuedText, NumberStyles.None, CultureInfo.InvariantCulture, out long issuedAt))
        {
            return FormTokenVerdict.Invalid;
        }

        long age = now.ToUnixTimeMilliseconds() - issuedAt;
        if (age < -ContactLimits.FutureSkewMilliseconds)
        {
            return FormTokenVerdict.Invalid;
        }

        if (age > ContactLimits.MaximumAgeMilliseconds)
        {
            return FormTokenVerdict.Expired;
        }

        if (age < ContactLimits.MinimumAgeMilliseconds)
        {
            return FormTokenVerdict.TooFast;
        }

        return FormTokenVerdict.Valid;
    }

    private static byte[] Sign(string key, string payload) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(payload));
}

/// <summary>base64url without padding. Small on purpose: the token is two parts and a dot.</summary>
internal static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0 || text.Any(c => char.IsWhiteSpace(c)))
        {
            return false;
        }

        string padded = text.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 0:
                break;
            default:
                return false;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
