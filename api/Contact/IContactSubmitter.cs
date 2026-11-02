namespace Portfolio.Api.Contact;

/// <summary>
/// What happens after the cheap checks, the rate-limit slot, and Turnstile.
/// The current implementation returns 501 and stores nothing.
/// A later change should hash the client IP and call dbo.usp_SubmitContact.
/// The in-memory rate limit is <see cref="IContactRateLimiter" />. It runs
/// before Turnstile, not inside this type.
/// The procedure parameters are @Name, @Email, @Message, and @IpHash.
/// Its result codes live in <see cref="ContactProcedureCodes" />.
/// </summary>
public interface IContactSubmitter
{
    Task<ContactSubmitResult> SubmitAsync(ValidatedContact contact, CancellationToken cancellationToken);
}
