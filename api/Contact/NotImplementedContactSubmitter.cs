namespace Portfolio.Api.Contact;

/// <summary>
/// Placeholder behind the checks. It does not open a SQL connection and it does not read secrets.
/// </summary>
public sealed class NotImplementedContactSubmitter : IContactSubmitter
{
    public Task<ContactSubmitResult> SubmitAsync(ValidatedContact contact, CancellationToken cancellationToken)
    {
        _ = contact;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ContactSubmitResult.NotImplemented());
    }
}
