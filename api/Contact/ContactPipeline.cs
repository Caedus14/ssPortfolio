using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Portfolio.Api.Contact;

public interface IContactPipeline
{
    Task<ContactSubmitResult> RunAsync(HttpRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The checks in front of <see cref="IContactSubmitter" />.
/// Order is cheapest first. A honeypot hit returns before the token check,
/// so a bot does not learn whether its token was valid.
/// The submitter runs only after every check has passed. Today that submitter
/// stores nothing and returns 501.
/// </summary>
public sealed class ContactPipeline : IContactPipeline
{
    private readonly IFormTokenService _tokens;
    private readonly IContactSubmitter _submitter;
    private readonly ILogger<ContactPipeline> _logger;

    public ContactPipeline(
        IFormTokenService tokens,
        IContactSubmitter submitter,
        ILogger<ContactPipeline> logger
    )
    {
        _tokens = tokens;
        _submitter = submitter;
        _logger = logger;
    }

    public async Task<ContactSubmitResult> RunAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ContactReadResult read = await ContactRequestReader.ReadAsync(request, cancellationToken);
        switch (read.Status)
        {
            case ContactReadStatus.UnsupportedMediaType:
                return new ContactSubmitResult(ContactSubmitStatus.UnsupportedMediaType, "unsupported_media_type");
            case ContactReadStatus.PayloadTooLarge:
                return new ContactSubmitResult(ContactSubmitStatus.PayloadTooLarge, "payload_too_large");
            case ContactReadStatus.InvalidJson:
                return new ContactSubmitResult(ContactSubmitStatus.InvalidJson, "invalid_json");
        }

        ContactRequestDto body = read.Body!;
        if (Honeypot.IsFilled(body.Website))
        {
            _logger.LogDebug("Contact honeypot was filled. Returning accepted and storing nothing.");
            return ContactSubmitResult.Accepted();
        }

        // The same token stays valid until it expires. A used-token list would
        // need storage shared by every instance, and this plan has none.
        // Later per-IP and SQL rate limits are what stop a replayed token.
        FormTokenVerdict verdict = _tokens.Verify(body.FormToken);
        if (verdict == FormTokenVerdict.Unavailable)
        {
            _logger.LogError(
                "FORM_TOKEN_KEY is missing or shorter than {MinimumLength} characters. Refusing to check the form token.",
                ContactLimits.MinimumFormTokenKeyLength
            );
            return ContactSubmitResult.Unavailable();
        }

        if (verdict != FormTokenVerdict.Valid)
        {
            return TokenRejection(verdict);
        }

        ContactValidationResult validation = ContactValidator.Validate(body);
        if (!validation.IsValid)
        {
            return new ContactSubmitResult(ContactSubmitStatus.InvalidFields, "invalid", validation.FieldErrors);
        }

        return await _submitter.SubmitAsync(validation.Contact!, cancellationToken);
    }

    private static ContactSubmitResult TokenRejection(FormTokenVerdict verdict)
    {
        (string error, string message) = verdict switch
        {
            FormTokenVerdict.Missing => ("form_token_required", "Reload the page and try again."),
            FormTokenVerdict.Expired => (
                "form_token_expired",
                "This form has been open too long. Reload the page and try again."
            ),
            FormTokenVerdict.TooFast => ("form_token_too_fast", "Please wait a few seconds and try again."),
            _ => ("form_token_invalid", "Reload the page and try again."),
        };

        return new ContactSubmitResult(
            ContactSubmitStatus.FormTokenRejected,
            error,
            new Dictionary<string, string> { ["formToken"] = message }
        );
    }
}
