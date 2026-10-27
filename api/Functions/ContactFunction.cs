using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Portfolio.Api.Contact;

namespace Portfolio.Api.Functions;

/// <summary>
/// POST /api/contact.
/// This pull request does not read the body. Validation, the honeypot,
/// the minimum-time check, the origin check, the in-memory rate limit,
/// Turnstile, and the call to dbo.usp_SubmitContact belong in later changes.
/// They should go through <see cref="IContactSubmitter" /> so this class
/// stays the HTTP edge.
/// </summary>
public sealed class ContactFunction
{
    private readonly IContactSubmitter _submitter;

    public ContactFunction(IContactSubmitter submitter)
    {
        _submitter = submitter;
    }

    [Function(nameof(ContactFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        // The body stays unread on purpose. Parsing it here would look like
        // validation, and this skeleton does not validate yet.
        _ = request;
        ContactSubmitResult result = await _submitter.SubmitAsync(
            ContactSubmission.NotParsed,
            cancellationToken
        );
        return ContactResponses.From(result);
    }
}
