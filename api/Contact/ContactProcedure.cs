namespace Portfolio.Api.Contact;

/// <summary>
/// One call to dbo.usp_SubmitContact. The SQL class is the production implementation.
/// Tests supply a fake so the retry rules do not need a database.
/// </summary>
public interface IContactProcedureExecutor
{
    Task<ContactProcedureOutcome> ExecuteAsync(
        ValidatedContact contact,
        byte[] ipHash,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Either a result row from the procedure, or a failure that may be retried.
/// ErrorNumber is a SQL error number such as 40613. It is safe to log.
/// The connection string, the email, and the message are not on this type.
/// </summary>
public sealed record ContactProcedureOutcome(
    bool Completed,
    int ResultCode,
    int? RetryAfterSeconds,
    long? MessageId,
    int? ErrorNumber,
    bool Transient
)
{
    public static ContactProcedureOutcome Success(int resultCode, int? retryAfterSeconds, long? messageId) =>
        new(true, resultCode, retryAfterSeconds, messageId, null, false);

    public static ContactProcedureOutcome Fault(int? errorNumber, bool transientFailure) =>
        new(false, 0, null, null, errorNumber, transientFailure);
}

public static class ContactProcedureMapper
{
    public static ContactSubmitResult Map(int resultCode, int? retryAfterSeconds) =>
        resultCode switch
        {
            ContactProcedureCodes.Accepted => ContactSubmitResult.Accepted(),
            ContactProcedureCodes.RateLimitedIp => ContactSubmitResult.RateLimited(
                retryAfterSeconds ?? ContactLimits.RateLimitFallbackRetryAfterSeconds
            ),
            ContactProcedureCodes.RateLimitedGlobal => ContactSubmitResult.RateLimited(
                retryAfterSeconds ?? ContactLimits.RateLimitFallbackRetryAfterSeconds
            ),
            ContactProcedureCodes.Invalid => new ContactSubmitResult(ContactSubmitStatus.InvalidFields, "invalid"),
            ContactProcedureCodes.Busy => ContactSubmitResult.ServiceUnavailable(
                retryAfterSeconds ?? ContactLimits.UnavailableRetryAfterSeconds
            ),
            _ => ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds),
        };
}

/// <summary>
/// Numbers Azure SQL uses while a serverless database is paused, waking, or too busy.
/// -2 is the client-side connection or command timeout.
/// </summary>
public static class SqlTransientErrors
{
    public static bool IsTransient(int errorNumber) =>
        errorNumber is 40613 or 40197 or 40501 or 49918 or 49919 or 49920 or 4060 or -2;
}
