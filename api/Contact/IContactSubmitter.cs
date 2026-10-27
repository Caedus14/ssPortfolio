namespace Portfolio.Api.Contact;

/// <summary>
/// What happens after the cheap checks pass.
/// The current implementation returns 501 and stores nothing.
/// A later change should check Origin, apply an in-memory rate limit,
/// verify Turnstile, hash the client IP, then call dbo.usp_SubmitContact.
/// The procedure parameters are @Name, @Email, @Message, and @IpHash.
/// Its result codes live in <see cref="ContactProcedureCodes" />.
/// </summary>
public interface IContactSubmitter
{
    Task<ContactSubmitResult> SubmitAsync(ValidatedContact contact, CancellationToken cancellationToken);
}
