namespace Portfolio.Api.Contact;

/// <summary>
/// What happens after the cheap checks, the in-memory limit, and Turnstile.
/// The SQL implementation calls dbo.usp_SubmitContact. The hash is the same
/// 32 bytes the rate limiter already counted.
/// </summary>
public interface IContactSubmitter
{
    Task<ContactSubmitResult> SubmitAsync(
        ValidatedContact contact,
        byte[] ipHash,
        CancellationToken cancellationToken
    );
}
