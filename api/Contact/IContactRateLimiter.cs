using Microsoft.AspNetCore.Http;

namespace Portfolio.Api.Contact;

/// <summary>
/// The step-7 in-memory per-instance limit. It runs after the cheap checks
/// and before the Turnstile siteverify call, so a flood does not spend
/// Cloudflare requests. The current class allows every request.
/// Step 7 replaces the registration. It does not reorder the pipeline.
/// </summary>
public interface IContactRateLimiter
{
    ValueTask<ContactSubmitResult?> AdmitAsync(
        HttpRequest request,
        ValidatedContact contact,
        CancellationToken cancellationToken
    );
}

/// <summary>Placeholder. A non-null result would stop the pipeline before Turnstile.</summary>
public sealed class PassThroughContactRateLimiter : IContactRateLimiter
{
    public ValueTask<ContactSubmitResult?> AdmitAsync(
        HttpRequest request,
        ValidatedContact contact,
        CancellationToken cancellationToken
    )
    {
        _ = request;
        _ = contact;
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<ContactSubmitResult?>((ContactSubmitResult?)null);
    }
}
