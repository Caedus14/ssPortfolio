namespace Portfolio.Api.Contact;

/// <summary>
/// Registered so the dependency is visible. Nothing calls it yet.
/// Calling it throws, which is louder than returning a fake hash.
/// </summary>
public sealed class UnconfiguredIpAddressHasher : IIpAddressHasher
{
    public byte[] Hash(string normalizedAddress)
    {
        _ = normalizedAddress;
        throw new NotSupportedException(
            "IP hashing is not implemented yet. A later change will HMAC the address with IP_HASH_KEY."
        );
    }
}
