using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

/// <summary>
/// HMAC-SHA256 of the normalized address. The database stores the raw 32 bytes.
/// Returns null when IP_HASH_KEY is missing or shorter than 16 characters.
/// The key is the UTF-8 bytes of the setting, used exactly as stored.
/// </summary>
public interface IIpAddressHasher
{
    byte[]? TryHash(string normalizedAddress);
}

public sealed class HmacIpAddressHasher : IIpAddressHasher
{
    private readonly IOptions<ContactApiOptions> _options;

    public HmacIpAddressHasher(IOptions<ContactApiOptions> options)
    {
        _options = options;
    }

    public byte[]? TryHash(string normalizedAddress)
    {
        string? key = _options.Value.IpHashKey;
        if (string.IsNullOrWhiteSpace(key) || key.Length < ContactLimits.MinimumIpHashKeyLength)
        {
            return null;
        }

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(normalizedAddress));
    }
}
