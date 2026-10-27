namespace Portfolio.Api.Contact;

/// <summary>
/// The contact pipeline behind POST /api/contact.
/// The current implementation always returns not-implemented.
/// A later change should do these steps in order, and stop at the first failure:
/// validate the body, reject a filled honeypot or a too-fast submit,
/// check the Origin header, apply an in-memory rate limit,
/// verify the Turnstile token, hash the client IP, then call dbo.usp_SubmitContact.
/// The procedure parameters are @Name, @Email, @Message, and @IpHash.
/// Its result codes live in <see cref="ContactProcedureCodes" />.
/// </summary>
public interface IContactSubmitter
{
    Task<ContactSubmitResult> SubmitAsync(
        ContactSubmission submission,
        CancellationToken cancellationToken
    );
}
