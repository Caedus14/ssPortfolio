using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Portfolio.Api.Contact;

public enum ContactReadStatus
{
    Ok,
    UnsupportedMediaType,
    PayloadTooLarge,
    InvalidJson,
}

public sealed record ContactReadResult(ContactReadStatus Status, ContactRequestDto? Body)
{
    public static ContactReadResult Ok(ContactRequestDto body) => new(ContactReadStatus.Ok, body);
}

/// <summary>
/// Reads POST /api/contact without taking more than <see cref="ContactLimits.MaxBodyBytes" />.
/// Content-Type is checked first. A Content-Length over the cap is rejected
/// before the body is read. A missing Content-Length still stops at the cap.
/// </summary>
public static class ContactRequestReader
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    public static async Task<ContactReadResult> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!IsJsonContentType(request.ContentType))
        {
            return new ContactReadResult(ContactReadStatus.UnsupportedMediaType, null);
        }

        if (request.ContentLength is long declared && declared > ContactLimits.MaxBodyBytes)
        {
            return new ContactReadResult(ContactReadStatus.PayloadTooLarge, null);
        }

        byte[] bytes = await ReadAtMostAsync(request.Body, ContactLimits.MaxBodyBytes, cancellationToken);

        if (bytes.Length > ContactLimits.MaxBodyBytes)
        {
            return new ContactReadResult(ContactReadStatus.PayloadTooLarge, null);
        }

        string text;
        try
        {
            text = Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return new ContactReadResult(ContactReadStatus.InvalidJson, null);
        }

        try
        {
            ContactRequestDto? body = JsonSerializer.Deserialize<ContactRequestDto>(text, JsonOptions);
            if (body is null)
            {
                return new ContactReadResult(ContactReadStatus.InvalidJson, null);
            }

            return ContactReadResult.Ok(body);
        }
        catch (JsonException)
        {
            return new ContactReadResult(ContactReadStatus.InvalidJson, null);
        }
    }

    public static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        if (!System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var parsed))
        {
            return false;
        }

        return string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the bytes read. The array is one byte longer than the cap when the body is too large,
    /// so the caller can reject it without reading the rest.
    /// </summary>
    private static async Task<byte[]> ReadAtMostAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
    {
        int budget = maxBytes + 1;
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (output.Length < budget)
        {
            int toRead = (int)Math.Min(buffer.Length, budget - output.Length);
            int read = await body.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
