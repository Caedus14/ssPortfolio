namespace Portfolio.Api.Contact;

/// <summary>
/// Hostnames a Turnstile token may name. The widget was created for sethstearns.dev.
/// The default Static Web Apps host and this app's preview hosts are included so a
/// token issued there can be checked once that hostname is also on the widget.
/// Anything else needs TURNSTILE_ALLOWED_HOSTS.
/// </summary>
public static class TurnstileHostnames
{
    public const string AppStem = "nice-rock-033c92210";

    /// <summary>Hostname Cloudflare's published test secret reports. It is not a production host.</summary>
    public const string TestingKeyHostname = "example.com";

    public static readonly string[] Defaults =
    [
        "sethstearns.dev",
        "www.sethstearns.dev",
        AppStem + ".2.azurestaticapps.net",
    ];

    public static bool IsAllowed(string? hostname, string? extraHosts)
    {
        if (!TryNormalize(hostname, out string host))
        {
            return false;
        }

        if (Defaults.Contains(host, StringComparer.Ordinal))
        {
            return true;
        }

        foreach (string extra in Split(extraHosts))
        {
            if (extra == host)
            {
                return true;
            }
        }

        return IsPreviewHost(host);
    }

    public static bool TryNormalize(string? hostname, out string host)
    {
        host = "";
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        string text = hostname.Trim().TrimEnd('.');
        int slash = text.IndexOf('/');
        if (slash >= 0)
        {
            text = text[..slash];
        }

        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] == '[')
        {
            int end = text.IndexOf(']');
            if (end <= 1)
            {
                return false;
            }

            text = text[1..end];
        }
        else
        {
            int colon = text.LastIndexOf(':');
            if (colon > 0 && text.IndexOf(':') == colon && text[(colon + 1)..].All(char.IsDigit))
            {
                text = text[..colon];
            }
        }

        host = text.ToLowerInvariant();
        return host.Length > 0;
    }

    /// <summary>
    /// Preview URLs look like nice-rock-033c92210-8.centralus.2.azurestaticapps.net.
    /// The stem is this app's default hostname, so another Static Web App does not match.
    /// </summary>
    public static bool IsPreviewHost(string host)
    {
        string prefix = AppStem + "-";
        const string suffix = ".2.azurestaticapps.net";
        if (!host.StartsWith(prefix, StringComparison.Ordinal) || !host.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string middle = host[prefix.Length..^suffix.Length];
        int dot = middle.IndexOf('.');
        if (dot <= 0 || dot != middle.LastIndexOf('.'))
        {
            return false;
        }

        string pullRequest = middle[..dot];
        string region = middle[(dot + 1)..];
        return pullRequest.Length > 0
            && pullRequest.All(char.IsDigit)
            && region.Length > 0
            && region.All(c => char.IsAsciiLetterLower(c) || c == '-');
    }

    private static IEnumerable<string> Split(string? extraHosts)
    {
        if (string.IsNullOrWhiteSpace(extraHosts))
        {
            yield break;
        }

        foreach (string part in extraHosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryNormalize(part, out string host))
            {
                yield return host;
            }
        }
    }
}
