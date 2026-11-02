using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Contact;

/// <summary>
/// JSON body of POST /api/contact. Unknown properties are ignored.
/// Every property is a string. A number or an object in one of these
/// fields is rejected as invalid JSON.
/// </summary>
public sealed class ContactRequestDto
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Message { get; set; }

    /// <summary>
    /// Honeypot. The real form hides this input. A person leaves it empty.
    /// A bot that fills every input puts something here.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>Signed issued-at token from GET /api/contact/token.</summary>
    public string? FormToken { get; set; }

    /// <summary>Token from the Turnstile widget. The server checks it with siteverify.</summary>
    public string? TurnstileToken { get; set; }
}

/// <summary>
/// Values that passed the field checks. The later SQL call receives these,
/// not the raw JSON. Nothing in this pull request stores them.
/// </summary>
public sealed record ValidatedContact(string Name, string Email, string Message, string? TurnstileToken);

public enum ContactSubmitStatus
{
    Accepted,
    UnsupportedMediaType,
    PayloadTooLarge,
    InvalidJson,
    FormTokenUnavailable,
    FormTokenRejected,
    InvalidFields,
    TurnstileRejected,
    TurnstileUnavailable,
    RateLimited,
    ServiceUnavailable,
}

public enum FormTokenVerdict
{
    Valid,
    Missing,
    Invalid,
    Expired,
    TooFast,
    Unavailable,
}

public sealed record ContactSubmitResult(
    ContactSubmitStatus Status,
    string? Error = null,
    IReadOnlyDictionary<string, string>? FieldErrors = null,
    int? RetryAfterSeconds = null
)
{
    public static ContactSubmitResult Accepted() => new(ContactSubmitStatus.Accepted);

    public static ContactSubmitResult Unavailable() =>
        new(ContactSubmitStatus.FormTokenUnavailable, "form_token_unavailable");

    public static ContactSubmitResult RateLimited(int retryAfterSeconds) =>
        new(
            ContactSubmitStatus.RateLimited,
            "rate_limited",
            RetryAfterSeconds: AtLeastOne(retryAfterSeconds)
        );

    public static ContactSubmitResult ServiceUnavailable(int retryAfterSeconds) =>
        new(
            ContactSubmitStatus.ServiceUnavailable,
            "unavailable",
            RetryAfterSeconds: AtLeastOne(retryAfterSeconds)
        );

    private static int AtLeastOne(int seconds) => seconds < 1 ? 1 : seconds;
}

/// <summary>200 body for a honeypot hit, and later for a real accept. Same shape on purpose.</summary>
public sealed record ContactAcceptedBody(string Status)
{
    public static ContactAcceptedBody Instance { get; } = new("accepted");
}

/// <summary>Error body. Fields is omitted from JSON when this response has no per-field messages.</summary>
public sealed record ContactErrorBody(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyDictionary<string, string>? Fields = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? RetryAfterSeconds = null
);

/// <summary>
/// Result codes returned by dbo.usp_SubmitContact. See db/README.md.
/// These are not HTTP status codes. ContactProcedureMapper turns them into HTTP.
/// </summary>
public static class ContactProcedureCodes
{
    public const int Accepted = 0;
    public const int RateLimitedIp = 1;
    public const int RateLimitedGlobal = 2;
    public const int Invalid = 3;
    public const int Busy = 5;
}

public static class ContactResponses
{
    public static IActionResult From(ContactSubmitResult result)
    {
        switch (result.Status)
        {
            case ContactSubmitStatus.Accepted:
                return new OkObjectResult(ContactAcceptedBody.Instance);
            case ContactSubmitStatus.UnsupportedMediaType:
                return Error(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type");
            case ContactSubmitStatus.PayloadTooLarge:
                return Error(StatusCodes.Status413PayloadTooLarge, "payload_too_large");
            case ContactSubmitStatus.InvalidJson:
                return Error(StatusCodes.Status400BadRequest, "invalid_json");
            case ContactSubmitStatus.FormTokenUnavailable:
                return Error(StatusCodes.Status503ServiceUnavailable, "form_token_unavailable");
            case ContactSubmitStatus.FormTokenRejected:
            case ContactSubmitStatus.InvalidFields:
            case ContactSubmitStatus.TurnstileRejected:
                return Error(
                    StatusCodes.Status400BadRequest,
                    result.Error ?? "invalid",
                    result.FieldErrors
                );
            case ContactSubmitStatus.TurnstileUnavailable:
                return Error(StatusCodes.Status503ServiceUnavailable, "turnstile_unavailable");
            case ContactSubmitStatus.RateLimited:
                return Error(
                    StatusCodes.Status429TooManyRequests,
                    "rate_limited",
                    retryAfterSeconds: result.RetryAfterSeconds
                );
            case ContactSubmitStatus.ServiceUnavailable:
                return Error(
                    StatusCodes.Status503ServiceUnavailable,
                    "unavailable",
                    retryAfterSeconds: result.RetryAfterSeconds
                );
            default:
                return Error(StatusCodes.Status500InternalServerError, "error");
        }
    }

    public static IActionResult Unavailable() =>
        From(ContactSubmitResult.Unavailable());

    private static ObjectResult Error(
        int statusCode,
        string error,
        IReadOnlyDictionary<string, string>? fields = null,
        int? retryAfterSeconds = null
    )
    {
        if (retryAfterSeconds is int)
        {
            return new ContactObjectResult(
                new ContactErrorBody(error, fields, retryAfterSeconds),
                statusCode,
                retryAfterSeconds
            );
        }

        return new ObjectResult(new ContactErrorBody(error, fields)) { StatusCode = statusCode };
    }
}

/// <summary>
/// Sets Retry-After when the result carries a wait. The header is delay-seconds, not a date.
/// </summary>
public sealed class ContactObjectResult : ObjectResult
{
    public ContactObjectResult(object body, int statusCode, int? retryAfterSeconds)
        : base(body)
    {
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int? RetryAfterSeconds { get; }

    public void ApplyHeaders(HttpResponse response)
    {
        if (RetryAfterSeconds is int seconds)
        {
            response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        ApplyHeaders(context.HttpContext.Response);
        return base.ExecuteResultAsync(context);
    }
}
