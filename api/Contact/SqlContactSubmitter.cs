using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

/// <summary>
/// Calls the procedure and retries the errors that mean the free serverless
/// database is paused or waking. The budget stays well under the 45-second
/// function limit. A missing connection string does not open a connection.
/// </summary>
public sealed class SqlContactSubmitter : IContactSubmitter
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
    ];

    private readonly IOptions<ContactApiOptions> _options;
    private readonly IContactProcedureExecutor _procedures;
    private readonly IAsyncDelay _delay;
    private readonly TimeProvider _time;
    private readonly ILogger<SqlContactSubmitter> _logger;
    private readonly TimeSpan _budget;

    public SqlContactSubmitter(
        IOptions<ContactApiOptions> options,
        IContactProcedureExecutor procedures,
        IAsyncDelay delay,
        TimeProvider time,
        ILogger<SqlContactSubmitter> logger,
        TimeSpan? retryBudget = null
    )
    {
        _options = options;
        _procedures = procedures;
        _delay = delay;
        _time = time;
        _logger = logger;
        _budget = retryBudget ?? TimeSpan.FromMilliseconds(ContactLimits.SqlRetryBudgetMilliseconds);
    }

    public async Task<ContactSubmitResult> SubmitAsync(
        ValidatedContact contact,
        byte[] ipHash,
        CancellationToken cancellationToken
    )
    {
        if (ipHash is null || ipHash.Length != 32)
        {
            _logger.LogError("Refusing to store a contact message without a 32-byte IP hash.");
            return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
        }

        if (string.IsNullOrWhiteSpace(_options.Value.SqlConnectionString))
        {
            _logger.LogError("SQL_CONNECTION_STRING is missing. Refusing to store the message.");
            return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
        }

        DateTimeOffset started = _time.GetUtcNow();
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_time.GetUtcNow() - started >= _budget)
            {
                _logger.LogError("Contact SQL retry budget was exhausted.");
                return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
            }

            ContactProcedureOutcome outcome = await _procedures.ExecuteAsync(contact, ipHash, cancellationToken);
            if (outcome.Completed)
            {
                if (outcome.ResultCode == ContactProcedureCodes.Accepted)
                {
                    _logger.LogInformation("Stored contact message {MessageId}.", outcome.MessageId);
                }
                else
                {
                    _logger.LogInformation(
                        "Contact procedure returned {ResultCode}.",
                        outcome.ResultCode
                    );
                }

                return ContactProcedureMapper.Map(outcome.ResultCode, outcome.RetryAfterSeconds);
            }

            if (!outcome.Transient || attempt >= Delays.Length)
            {
                _logger.LogError(
                    "Contact SQL failed with error number {ErrorNumber}.",
                    outcome.ErrorNumber
                );
                return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
            }

            TimeSpan delay = Delays[attempt];
            if (_time.GetUtcNow() + delay - started >= _budget)
            {
                _logger.LogError(
                    "Contact SQL was unavailable after retries ({ErrorNumber}).",
                    outcome.ErrorNumber
                );
                return ContactSubmitResult.ServiceUnavailable(ContactLimits.UnavailableRetryAfterSeconds);
            }

            attempt++;
            await _delay.DelayAsync(delay, cancellationToken);
        }
    }
}
