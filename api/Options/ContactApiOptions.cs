using Microsoft.Extensions.Configuration;

namespace Portfolio.Api.Options;

/// <summary>
/// Settings for the contact pipeline. Azure Static Web Apps turns
/// Environment variables into process environment variables, and the
/// Functions host puts those on IConfiguration.
/// Leave the values empty until you set them in the portal.
/// Do not commit them. Managed functions cannot use a managed identity,
/// so SQL_CONNECTION_STRING is a SQL login (the contact_api user), not DefaultAzureCredential.
/// </summary>
public sealed class ContactApiOptions
{
    /// <summary>
    /// ADO.NET connection string for the ssportfolio database.
    /// The login should be contact_api, which can only EXECUTE dbo.usp_SubmitContact.
    /// </summary>
    public string? SqlConnectionString { get; set; }

    /// <summary>Cloudflare Turnstile secret. The site key belongs on the frontend, later.</summary>
    public string? TurnstileSecret { get; set; }

    /// <summary>HMAC key for IIpAddressHasher. Treat it like a password.</summary>
    public string? IpHashKey { get; set; }
}

public static class ContactApiOptionsBinder
{
    public static void Bind(ContactApiOptions options, IConfiguration configuration)
    {
        options.SqlConnectionString = configuration["SQL_CONNECTION_STRING"];
        options.TurnstileSecret = configuration["TURNSTILE_SECRET"];
        options.IpHashKey = configuration["IP_HASH_KEY"];
    }
}
