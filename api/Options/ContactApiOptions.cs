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

    /// <summary>
    /// Cloudflare Turnstile secret for siteverify. The site key is public and lives in
    /// src/config/site.ts. Leave this empty until it is set in Azure. An empty value
    /// is a 503 on the Turnstile check. Do not use Cloudflare's always-pass test secret here
    /// in Azure. That secret accepts every dummy token.
    /// </summary>
    public string? TurnstileSecret { get; set; }

    /// <summary>
    /// Comma-separated hostnames added to the built-in allow list.
    /// Empty means only the built-in names. See <see cref="Portfolio.Api.Contact.TurnstileHostnames" />.
    /// </summary>
    public string? TurnstileAllowedHosts { get; set; }

    /// <summary>
    /// Widget action the token must carry. Empty means "contact", which the form will set.
    /// </summary>
    public string? TurnstileExpectedAction { get; set; }

    /// <summary>HMAC key for IIpAddressHasher. Treat it like a password.</summary>
    public string? IpHashKey { get; set; }

    /// <summary>
    /// HMAC key for form tokens only. This is a different secret from
    /// <see cref="IpHashKey" />. The hash key can test an IP address
    /// against a stored hash. The token key only proves when a form was loaded.
    /// A leak of one secret should not hand over the other, and they rotate
    /// on different days: changing the hash key makes old hashes unsearchable,
    /// while changing this key only asks open forms to reload.
    /// </summary>
    public string? FormTokenKey { get; set; }
}

public static class ContactApiOptionsBinder
{
    public static void Bind(ContactApiOptions options, IConfiguration configuration)
    {
        options.SqlConnectionString = configuration["SQL_CONNECTION_STRING"];
        options.TurnstileSecret = configuration["TURNSTILE_SECRET"];
        options.TurnstileAllowedHosts = configuration["TURNSTILE_ALLOWED_HOSTS"];
        options.TurnstileExpectedAction = configuration["TURNSTILE_EXPECTED_ACTION"];
        options.IpHashKey = configuration["IP_HASH_KEY"];
        options.FormTokenKey = configuration["FORM_TOKEN_KEY"];
    }
}
