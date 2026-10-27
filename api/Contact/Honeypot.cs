namespace Portfolio.Api.Contact;

/// <summary>
/// A filled website field means a bot. Whitespace alone does not count:
/// a hidden input can submit an empty string or a few spaces.
/// </summary>
public static class Honeypot
{
    public static bool IsFilled(string? website) => !string.IsNullOrWhiteSpace(website);
}
