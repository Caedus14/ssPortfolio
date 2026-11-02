using Portfolio.Api.Contact;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class IpAddressHasherTests
{
    private const string Key = "0123456789abcdef";

    // HMAC-SHA256 of the UTF-8 key over the normalized text.
    private const string Ipv4Vector = "018d919e15c851ddaedd01834def6f1e6725c5dcb63ef815fec28f3de948f674";
    private const string Ipv6Vector = "d50eccabeb0cfaaaf7ab5a670255cd114332dab8cf0dac3d3e56b3c7f268196d";
    private const string UnknownVector = "56f0946b9ce68cb4dd69c0e257f9790765dba4c5e291124ddc99101c2efb4cf3";

    [Fact]
    public void IPv4_matches_the_known_vector_and_is_deterministic()
    {
        HmacIpAddressHasher hasher = Hasher(Key);

        byte[] first = hasher.TryHash(IpAddressNormalizer.ForHash("203.0.113.8"))!;
        byte[] second = hasher.TryHash("203.0.113.8")!;

        Assert.Equal(Ipv4Vector, Convert.ToHexString(first).ToLowerInvariant());
        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);
    }

    [Fact]
    public void IPv4_mapped_IPv6_uses_the_dotted_address()
    {
        HmacIpAddressHasher hasher = Hasher(Key);

        string mapped = IpAddressNormalizer.ForHash("::ffff:203.0.113.8");
        string dotted = IpAddressNormalizer.ForHash("203.0.113.8");

        Assert.Equal("203.0.113.8", mapped);
        Assert.Equal(dotted, mapped);
        Assert.Equal(hasher.TryHash(mapped), hasher.TryHash(dotted));
    }

    [Fact]
    public void IPv6_is_grouped_by_the_64_prefix()
    {
        Assert.Equal("2001:db8:abcd:12", IpAddressNormalizer.ForHash("2001:0db8:abcd:0012:ffff::1"));
        Assert.Equal("2001:db8:abcd:12", IpAddressNormalizer.ForHash("2001:db8:abcd:12::9"));
        Assert.Equal("0:0:0:0", IpAddressNormalizer.ForHash("::1"));

        HmacIpAddressHasher hasher = Hasher(Key);
        byte[] prefix = hasher.TryHash(IpAddressNormalizer.ForHash("2001:0db8:abcd:0012:ffff::1"))!;
        byte[] same = hasher.TryHash(IpAddressNormalizer.ForHash("2001:db8:abcd:12::9"))!;
        byte[] other = hasher.TryHash(IpAddressNormalizer.ForHash("2001:db8:abcd:13::1"))!;

        Assert.Equal(Ipv6Vector, Convert.ToHexString(prefix).ToLowerInvariant());
        Assert.Equal(prefix, same);
        Assert.NotEqual(prefix, other);
    }

    [Fact]
    public void A_missing_address_uses_one_shared_sentinel()
    {
        Assert.Equal(IpAddressNormalizer.UnknownSentinel, IpAddressNormalizer.ForHash(null));
        Assert.Equal(IpAddressNormalizer.UnknownSentinel, IpAddressNormalizer.ForHash("not-an-ip"));

        byte[] hash = Hasher(Key).TryHash(IpAddressNormalizer.ForHash(null))!;
        Assert.Equal(UnknownVector, Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fifteen-chars!!")]
    public void A_missing_or_short_key_does_not_hash(string? key)
    {
        Assert.Null(Hasher(key).TryHash("203.0.113.8"));
    }

    [Fact]
    public void A_different_key_produces_a_different_hash()
    {
        byte[] one = Hasher(Key).TryHash("203.0.113.8")!;
        byte[] two = Hasher("fedcba9876543210").TryHash("203.0.113.8")!;

        Assert.NotEqual(one, two);
    }

    private static HmacIpAddressHasher Hasher(string? key) =>
        new(Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { IpHashKey = key }));
}
