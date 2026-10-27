using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Contact;

/// <summary>
/// The fields a later parser will take from the JSON body.
/// NotParsed is the skeleton value: the function has not looked at the body.
/// </summary>
public sealed record ContactSubmission(string? Name, string? Email, string? Message, string? TurnstileToken)
{
    public static ContactSubmission NotParsed { get; } = new(null, null, null, null);
}

public enum ContactSubmitStatus
{
    NotImplemented,
}

public sealed record ContactSubmitResult(ContactSubmitStatus Status);

/// <summary>JSON body for an error response. The property serializes as "error".</summary>
public sealed record ContactErrorBody(string Error);

/// <summary>
/// Result codes returned by dbo.usp_SubmitContact. See db/README.md.
/// These are not HTTP status codes. The SQL call that produces them is a later change.
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
        if (result.Status == ContactSubmitStatus.NotImplemented)
        {
            return new ObjectResult(new ContactErrorBody("not_implemented"))
            {
                StatusCode = StatusCodes.Status501NotImplemented,
            };
        }

        return new StatusCodeResult(StatusCodes.Status500InternalServerError);
    }
}
