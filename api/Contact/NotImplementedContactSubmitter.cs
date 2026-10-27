namespace Portfolio.Api.Contact;

/// <summary>
/// Placeholder. It does not open a SQL connection and it does not read secrets.
/// </summary>
public sealed class NotImplementedContactSubmitter : IContactSubmitter
{
    public Task<ContactSubmitResult> SubmitAsync(
        ContactSubmission submission,
        CancellationToken cancellationToken
    )
    {
        _ = submission;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ContactSubmitResult(ContactSubmitStatus.NotImplemented));
    }
}
