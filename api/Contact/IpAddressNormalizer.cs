using System.Net;
using System.Net.Sockets;

namespace Portfolio.Api.Contact;

/// <summary>
/// The string that is HMAC'd. See db/README.md.
/// IPv4 is the canonical dotted form. IPv6 is the /64 prefix as four lowercase
/// hextets with leading zeros removed. An IPv4-mapped IPv6 address is treated
/// as IPv4 so it does not collapse into the all-zero prefix and does not
/// disagree with the dotted form of the same visitor.
/// A missing address becomes the fixed label <see cref="UnknownSentinel" />.
/// Every request that shares that label shares one rate-limit bucket.
/// </summary>
public static class IpAddressNormalizer
{
    public const string UnknownSentinel = "unknown";

    public static string ForHash(string? parsedAddress)
    {
        if (string.IsNullOrWhiteSpace(parsedAddress))
        {
            return UnknownSentinel;
        }

        if (!IPAddress.TryParse(parsedAddress.Trim(), out IPAddress? address))
        {
            return UnknownSentinel;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address.ToString();
        }

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != 16)
        {
            return UnknownSentinel;
        }

        var parts = new string[4];
        for (int i = 0; i < 4; i++)
        {
            int value = (bytes[i * 2] << 8) | bytes[i * 2 + 1];
            parts[i] = value.ToString("x");
        }

        return string.Join(':', parts);
    }
}
