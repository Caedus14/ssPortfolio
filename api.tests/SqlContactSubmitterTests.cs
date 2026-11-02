using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Contact;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class SqlContactSubmitterTests
{
    private static readonly ValidatedContact Contact = new("Ada", "ada@example.com", "Hello from the form.", "token");
    private static readonly byte[] Hash = new byte[32];

    [Fact]
    public async Task A_missing_connection_string_is_503_and_does_not_call_sql()
    {
        var procedures = new ScriptedProcedure();
        SqlContactSubmitter submitter = Submitter(procedures, connectionString: "  ");

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, result.Status);
        Assert.Equal("unavailable", result.Error);
        Assert.Equal(ContactLimits.UnavailableRetryAfterSeconds, result.RetryAfterSeconds);
        Assert.Equal(0, procedures.Calls);
    }

    [Fact]
    public async Task An_accepted_row_is_200_and_does_not_retry()
    {
        var procedures = new ScriptedProcedure();
        procedures.Enqueue(ContactProcedureOutcome.Success(0, null, 17));
        var delay = new RecordingDelay();
        SqlContactSubmitter submitter = Submitter(procedures, delay: delay);

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.Accepted, result.Status);
        Assert.Equal(1, procedures.Calls);
        Assert.Empty(delay.Delays);
        Assert.Equal(Contact, procedures.Contact);
        Assert.Equal(Hash, procedures.Hash);
    }

    [Fact]
    public async Task A_transient_error_retries_then_returns_the_row()
    {
        var procedures = new ScriptedProcedure();
        procedures.Enqueue(ContactProcedureOutcome.Fault(40613, transientFailure: true));
        procedures.Enqueue(ContactProcedureOutcome.Fault(-2, transientFailure: true));
        procedures.Enqueue(ContactProcedureOutcome.Success(1, 42, null));
        var delay = new RecordingDelay();
        SqlContactSubmitter submitter = Submitter(procedures, delay: delay);

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.RateLimited, result.Status);
        Assert.Equal(42, result.RetryAfterSeconds);
        Assert.Equal(3, procedures.Calls);
        Assert.Equal(2, delay.Delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(250), delay.Delays[0]);
        Assert.Equal(TimeSpan.FromSeconds(1), delay.Delays[1]);
    }

    [Fact]
    public async Task A_non_transient_error_does_not_retry()
    {
        var procedures = new ScriptedProcedure();
        procedures.Enqueue(ContactProcedureOutcome.Fault(18456, transientFailure: false));
        var delay = new RecordingDelay();
        var logger = new ListLogger<SqlContactSubmitter>();
        SqlContactSubmitter submitter = Submitter(procedures, delay: delay, logger: logger);

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, result.Status);
        Assert.Equal(1, procedures.Calls);
        Assert.Empty(delay.Delays);
        Assert.Contains("18456", logger.Entries[0].Message);
        Assert.DoesNotContain("ada@example.com", logger.Entries[0].Message);
        Assert.DoesNotContain("Hello from the form.", logger.Entries[0].Message);
    }

    [Fact]
    public async Task The_retry_budget_stops_a_waking_database()
    {
        var procedures = new ScriptedProcedure();
        procedures.Enqueue(ContactProcedureOutcome.Fault(40613, transientFailure: true));
        var delay = new RecordingDelay();
        SqlContactSubmitter submitter = Submitter(
            procedures,
            delay: delay,
            budget: TimeSpan.FromMilliseconds(100)
        );

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, result.Status);
        Assert.Equal(ContactLimits.UnavailableRetryAfterSeconds, result.RetryAfterSeconds);
        Assert.Equal(1, procedures.Calls);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task Exhausting_the_transient_retries_is_503()
    {
        var procedures = new ScriptedProcedure();
        for (int i = 0; i < 5; i++)
        {
            procedures.Enqueue(ContactProcedureOutcome.Fault(40197, transientFailure: true));
        }

        var delay = new RecordingDelay();
        SqlContactSubmitter submitter = Submitter(procedures, delay: delay);

        ContactSubmitResult result = await submitter.SubmitAsync(Contact, Hash, CancellationToken.None);

        Assert.Equal(ContactSubmitStatus.ServiceUnavailable, result.Status);
        Assert.Equal(4, procedures.Calls);
        Assert.Equal(3, delay.Delays.Count);
    }

    [Fact]
    public void Connect_and_command_timeouts_stay_inside_the_function_limit()
    {
        Assert.Equal(5, ContactLimits.SqlConnectTimeoutSeconds);
        Assert.Equal(5, ContactLimits.SqlCommandTimeoutSeconds);
        Assert.Equal(20_000, ContactLimits.SqlRetryBudgetMilliseconds);
        Assert.True(ContactLimits.SqlRetryBudgetMilliseconds < 45_000);
    }

    private static SqlContactSubmitter Submitter(
        ScriptedProcedure procedures,
        string connectionString = "Server=example;",
        RecordingDelay? delay = null,
        ILogger<SqlContactSubmitter>? logger = null,
        TimeSpan? budget = null
    )
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-11-02T12:00:00Z"));
        delay ??= new RecordingDelay(time);
        return new SqlContactSubmitter(
            Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { SqlConnectionString = connectionString }),
            procedures,
            delay,
            time,
            logger ?? NullLogger<SqlContactSubmitter>.Instance,
            budget
        );
    }

    private sealed class ScriptedProcedure : IContactProcedureExecutor
    {
        private readonly Queue<ContactProcedureOutcome> _steps = new();

        public int Calls { get; private set; }

        public ValidatedContact? Contact { get; private set; }

        public byte[]? Hash { get; private set; }

        public void Enqueue(ContactProcedureOutcome outcome) => _steps.Enqueue(outcome);

        public Task<ContactProcedureOutcome> ExecuteAsync(
            ValidatedContact contact,
            byte[] ipHash,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            Contact = contact;
            Hash = ipHash;
            return Task.FromResult(_steps.Dequeue());
        }
    }

    private sealed class RecordingDelay : IAsyncDelay
    {
        private readonly FixedTimeProvider? _time;

        public RecordingDelay(FixedTimeProvider? time = null)
        {
            _time = time;
        }

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            if (_time is not null)
            {
                _time.UtcNow += delay;
            }

            return Task.CompletedTask;
        }
    }
}
