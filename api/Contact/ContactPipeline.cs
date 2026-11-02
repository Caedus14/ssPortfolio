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
/// The submitter runs only after every check has passed, including Turnstile.
/// A stored message is a 200. The in-memory limit and a missing IP hash key
/// both stop the request before siteverify.
/// </summary>
public sealed class ContactPipeline : IContactPipeline
{
    private readonly IFormTokenService _tokens;
    private readonly IIpAddressHasher _hasher;
    private readonly IContactRateLimiter _rateLimiter;
    private readonly ITurnstileVerifier _turnstile;
    private readonly IContactSubmitter _submitter;
    private readonly ILogger<ContactPipeline> _logger;

    public ContactPipeline(
        IFormTokenService tokens,
        IIpAddressHasher hasher,
        IContactRateLimiter rateLimiter,
        ITurnstileVerifier turnstile,
        IContactSubmitter submitter,
        ILogger<ContactPipeline> logger
    )
    {
        _tokens = tokens;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _turnstile = turnstile;
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
        // The in-memory window and the SQL rate limits are what stop a replayed token.
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

        ValidatedContact contact = validation.Contact!;

        // Hash before the in-memory limit and before siteverify. A missing key
        // cannot be allowed through, and a request with no visible client IP
        // still hashes the shared "unknown" label so it cannot skip the limit.
        string material = IpAddressNormalizer.ForHash(ClientIp.From(request));
        byte[]? ipHash = _hasher.TryHash(material);
        if (ipHash is null)
        {
            _logger.LogError(
                "IP_HASH_KEY is missing or shorter than {MinimumLength} characters. Refusing to store the message.",
                ContactLimits.MinimumIpHashKeyLength
            );
            return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
        }

        // Counts this attempt. A refusal here does not call siteverify.
        // The window is per instance. SQL is the limit that is shared.
        ContactSubmitResult? limited = await _rateLimiter.AdmitAsync(ipHash, cancellationToken);
        if (limited is not null)
        {
            return limited;
        }

        if (string.IsNullOrEmpty(contact.TurnstileToken))
        {
            return TurnstileField(
                "turnstile_required",
                "Complete the check and try again."
            );
        }

        if (contact.TurnstileToken.Length > ContactLimits.TurnstileTokenMaxLength)
        {
            return TurnstileField(
                "turnstile_failed",
                "Complete the check again and try again."
            );
        }

        TurnstileVerdict turnstile = await _turnstile.VerifyAsync(
            contact.TurnstileToken,
            ClientIp.From(request),
            cancellationToken
        );
        switch (turnstile.Outcome)
        {
            case TurnstileOutcome.Passed:
                break;
            case TurnstileOutcome.Unavailable:
                return new ContactSubmitResult(ContactSubmitStatus.TurnstileUnavailable, "turnstile_unavailable");
            case TurnstileOutcome.Retry:
                return TurnstileField(
                    "turnstile_timeout",
                    "The check expired or was already used. Complete it again and try again."
                );
            default:
                return TurnstileField(
                    "turnstile_failed",
                    "Complete the check again and try again."
                );
        }

        return await _submitter.SubmitAsync(contact, ipHash, cancellationToken);
    }

    private static ContactSubmitResult TurnstileField(string error, string message)
    {
        return new ContactSubmitResult(
            ContactSubmitStatus.TurnstileRejected,
            error,
            new Dictionary<string, string> { ["turnstileToken"] = message }
        );
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
