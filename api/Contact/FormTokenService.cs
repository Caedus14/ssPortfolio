using Microsoft.Extensions.Options;
using Portfolio.Api.Options;

namespace Portfolio.Api.Contact;

public interface IFormTokenService
{
    /// <summary>Null when FORM_TOKEN_KEY is missing or too short. Does not throw.</summary>
    string? Issue();

    FormTokenVerdict Verify(string? token);
}

public sealed class FormTokenService : IFormTokenService
{
    private readonly IOptions<ContactApiOptions> _options;
    private readonly TimeProvider _time;

    public FormTokenService(IOptions<ContactApiOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public string? Issue()
    {
        if (!FormTokenCodec.TryIssue(_options.Value.FormTokenKey, _time.GetUtcNow(), out string token))
        {
            return null;
        }

        return token;
    }

    public FormTokenVerdict Verify(string? token) =>
        FormTokenCodec.Verify(_options.Value.FormTokenKey, token, _time.GetUtcNow());
}
