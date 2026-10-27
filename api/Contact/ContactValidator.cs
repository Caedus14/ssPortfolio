namespace Portfolio.Api.Contact;

public sealed record ContactValidationResult(
    bool IsValid,
    IReadOnlyDictionary<string, string> FieldErrors,
    ValidatedContact? Contact
);

/// <summary>
/// Field rules that mirror dbo.usp_SubmitContact, plus content rules the procedure does not have.
/// Lengths are measured after trim. A failure here is a 400 the form can show next to the input.
/// </summary>
public static class ContactValidator
{
    public static ContactValidationResult Validate(ContactRequestDto request)
    {
        var errors = new Dictionary<string, string>();

        string name = (request.Name ?? "").Trim();
        string email = (request.Email ?? "").Trim();
        string message = (request.Message ?? "").Trim();

        if (name.Length < ContactLimits.NameMinLength || name.Length > ContactLimits.NameMaxLength)
        {
            errors["name"] = "Name must be 1 to 100 characters.";
        }
        else if (HasForbiddenControl(name, allowNewlines: false))
        {
            errors["name"] = "Name cannot include control characters.";
        }

        if (!IsPlausibleEmail(email))
        {
            errors["email"] = "Enter an email address, up to 254 characters, without spaces.";
        }

        if (message.Length < ContactLimits.MessageMinLength || message.Length > ContactLimits.MessageMaxLength)
        {
            errors["message"] = "Message must be 10 to 2000 characters.";
        }
        else if (HasForbiddenControl(message, allowNewlines: true))
        {
            errors["message"] = "Message cannot include control characters.";
        }
        else if (ContainsHtmlTag(message))
        {
            errors["message"] = "Message cannot include HTML.";
        }
        else if (CountLinks(message) > ContactLimits.MaxLinks)
        {
            errors["message"] = "Message can include at most 2 links.";
        }

        if (errors.Count > 0)
        {
            return new ContactValidationResult(false, errors, null);
        }

        string? turnstile = string.IsNullOrWhiteSpace(request.TurnstileToken)
            ? null
            : request.TurnstileToken.Trim();
        return new ContactValidationResult(true, errors, new ValidatedContact(name, email, message, turnstile));
    }

    /// <summary>
    /// One @, a dot in the domain that is not the first or last domain character, no spaces, no controls.
    /// This matches the SQL check (something @ something . something) without a long regex.
    /// </summary>
    public static bool IsPlausibleEmail(string email)
    {
        if (email.Length is < 3 or > ContactLimits.EmailMaxLength)
        {
            return false;
        }

        if (email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return false;
        }

        int at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@'))
        {
            return false;
        }

        string domain = email[(at + 1)..];
        int dot = domain.IndexOf('.');
        // The dot cannot be the first or last character of the domain.
        // "a@b.c" passes. "a@.example.com" and "a@b.c." do not.
        if (dot <= 0 || domain[^1] == '.')
        {
            return false;
        }

        if (domain.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public static bool HasForbiddenControl(string value, bool allowNewlines)
    {
        foreach (char c in value)
        {
            if (!char.IsControl(c))
            {
                continue;
            }

            if (allowNewlines && c is '\n' or '\r' or '\t')
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// A tag is "&lt;" immediately followed by a letter, "&lt;/" and a letter, or "&lt;!" / "&lt;?".
    /// "a &lt; b" has a space and is allowed. A space after "&lt;" is an evasion this check misses.
    /// </summary>
    public static bool ContainsHtmlTag(string value)
    {
        for (int i = 0; i < value.Length - 1; i++)
        {
            if (value[i] != '<')
            {
                continue;
            }

            char next = value[i + 1];
            if (char.IsLetter(next) || next is '!' or '?')
            {
                return true;
            }

            if (next == '/' && i + 2 < value.Length && char.IsLetter(value[i + 2]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts http://, https://, and www. as links. Matching is case-insensitive.
    /// https://www.example.com is one link: the www. sits inside the https token.
    /// </summary>
    public static int CountLinks(string value)
    {
        int count = 0;
        int i = 0;
        while (i < value.Length)
        {
            if (StartsWith(value, i, "https://") || StartsWith(value, i, "http://") || StartsWith(value, i, "www."))
            {
                count++;
                while (i < value.Length && !char.IsWhiteSpace(value[i]))
                {
                    i++;
                }

                continue;
            }

            i++;
        }

        return count;
    }

    private static bool StartsWith(string value, int index, string marker)
    {
        if (index + marker.Length > value.Length)
        {
            return false;
        }

        return value.AsSpan(index, marker.Length).Equals(marker, StringComparison.OrdinalIgnoreCase);
    }
}
