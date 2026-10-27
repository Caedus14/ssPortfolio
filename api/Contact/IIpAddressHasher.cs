namespace Portfolio.Api.Contact;

/// <summary>
/// Turns a normalized IP string into the 32-byte HMAC-SHA256 that
/// dbo.ContactMessage.IpHash stores. The raw address never goes to SQL.
/// IPv6 addresses are grouped by their /64 prefix before hashing.
/// The HMAC key is the IP_HASH_KEY app setting, not a column in the database.
/// </summary>
public interface IIpAddressHasher
{
    byte[] Hash(string normalizedAddress);
}
