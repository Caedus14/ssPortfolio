using System.Net;
using Microsoft.AspNetCore.Http;

namespace Portfolio.Api.Contact;

/// <summary>
/// The visitor address to send to Turnstile as remoteip, when one can be named safely.
/// A missing result means "do not send remoteip". Siteverify still checks the token.
/// Sending a forged address is worse than sending none: Cloudflare can fold that
/// address into the verdict, and the caller would have chosen it.
/// </summary>
public static class ClientIp
{
    /// <summary>
    /// Azure Front Door overwrites this header. A value the browser sent does not survive that product.
    /// Managed Static Web Apps captures often omit it. When it is present and parses, it wins.
    /// </summary>
    public const string AzureClientIpHeader = "X-Azure-ClientIP";

    public static string? From(HttpRequest request)
    {
        if (TryFirstAddress(request.Headers[AzureClientIpHeader].ToString(), out string? azure))
        {
            return azure;
        }

        // X-Forwarded-For is not a secret the browser is forbidden to set.
        // A client can open the request with "X-Forwarded-For: 1.2.3.4".
        // If a proxy appends instead of replacing, the leftmost value is the attacker's.
        // On a managed function the chain looks like "visitor, azure-edge"
        // (a capture: "133.226.1.27:63381, 20.205.82.199:46830").
        // The rightmost address is the last proxy. The visitor that proxy saw
        // is the address just to its left. A single address is used as-is,
        // which is the local host and a proxy that replaced the header.
        //
        // The "client-ip" header on these functions is a private hop inside Azure
        // (a capture showed 10.0.0.12). It is not the visitor, so it is ignored.
        List<string> forwarded = ParseList(request.Headers["X-Forwarded-For"].ToString());
        if (forwarded.Count == 0)
        {
            return null;
        }

        if (forwarded.Count == 1)
        {
            return forwarded[0];
        }

        return forwarded[^2];
    }

    private static bool TryFirstAddress(string raw, out string? address)
    {
        address = null;
        List<string> parsed = ParseList(raw);
        if (parsed.Count == 0)
        {
            return false;
        }

        address = parsed[0];
        return true;
    }

    private static List<string> ParseList(string raw)
    {
        var parsed = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return parsed;
        }

        foreach (string part in raw.Split(','))
        {
            if (TryParseAddress(part, out string address))
            {
                parsed.Add(address);
            }
        }

        return parsed;
    }

    private static bool TryParseAddress(string raw, out string address)
    {
        address = "";
        string text = raw.Trim().Trim('"');
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
            // One colon is host:port. More than one colon is an IPv6 address.
            if (colon > 0 && text.IndexOf(':') == colon && text[(colon + 1)..].All(char.IsDigit))
            {
                text = text[..colon];
            }
        }

        if (!IPAddress.TryParse(text, out IPAddress? ip))
        {
            return false;
        }

        address = ip.ToString();
        return true;
    }
}
