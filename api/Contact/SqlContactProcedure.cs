using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

/// <summary>
/// Opens SQL_CONNECTION_STRING and executes dbo.usp_SubmitContact.
/// Connect time is capped so a paused database cannot consume the whole function.
/// Pools are cleared after the errors that mean the database is moving, so the
/// next attempt does not reuse a dead connection. Nothing here logs the
/// connection string, the email, or the message.
/// </summary>
public sealed class SqlContactProcedure : IContactProcedureExecutor
{
    private readonly IOptions<ContactApiOptions> _options;

    public SqlContactProcedure(IOptions<ContactApiOptions> options)
    {
        _options = options;
    }

    public async Task<ContactProcedureOutcome> ExecuteAsync(
        ValidatedContact contact,
        byte[] ipHash,
        CancellationToken cancellationToken
    )
    {
        string? raw = _options.Value.SqlConnectionString;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ContactProcedureOutcome.Fault(errorNumber: null, transientFailure: false);
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(raw)
            {
                ConnectTimeout = ContactLimits.SqlConnectTimeoutSeconds,
            };

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_SubmitContact";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.CommandTimeout = ContactLimits.SqlCommandTimeoutSeconds;
            command.Parameters.Add("@Name", System.Data.SqlDbType.NVarChar, 200).Value = contact.Name;
            command.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar, 320).Value = contact.Email;
            command.Parameters.Add("@Message", System.Data.SqlDbType.NVarChar, 4000).Value = contact.Message;
            command.Parameters.Add("@IpHash", System.Data.SqlDbType.VarBinary, 32).Value = ipHash;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ContactProcedureOutcome.Fault(errorNumber: null, transientFailure: false);
            }

            int code = reader.GetInt32(reader.GetOrdinal("ResultCode"));
            int retryOrdinal = reader.GetOrdinal("RetryAfterSeconds");
            int idOrdinal = reader.GetOrdinal("MessageId");
            int? retry = reader.IsDBNull(retryOrdinal) ? null : reader.GetInt32(retryOrdinal);
            long? messageId = reader.IsDBNull(idOrdinal) ? null : reader.GetInt64(idOrdinal);
            return ContactProcedureOutcome.Success(code, retry, messageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException ex)
        {
            if (ex.Number is 40613 or 4060)
            {
                SqlConnection.ClearAllPools();
            }

            return ContactProcedureOutcome.Fault(ex.Number, SqlTransientErrors.IsTransient(ex.Number));
        }
        catch (TimeoutException)
        {
            return ContactProcedureOutcome.Fault(-2, transientFailure: true);
        }
        catch (ArgumentException)
        {
            return ContactProcedureOutcome.Fault(errorNumber: null, transientFailure: false);
        }
    }
}
