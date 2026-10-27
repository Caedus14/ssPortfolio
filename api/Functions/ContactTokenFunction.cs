using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Contact;

namespace Portfolio.Api.Functions;

/// <summary>Body of GET /api/contact/token. The form sends formToken back on submit.</summary>
public sealed record FormTokenResponse(string FormToken);

/// <summary>
/// GET /api/contact/token. Returns an HMAC of the current time.
/// The key never leaves the server, so a client cannot mint an old timestamp.
/// When FORM_TOKEN_KEY is missing this returns 503. It does not throw.
/// </summary>
public sealed class ContactTokenFunction
{
    private readonly IFormTokenService _tokens;
    private readonly ILogger<ContactTokenFunction> _logger;

    public ContactTokenFunction(IFormTokenService tokens, ILogger<ContactTokenFunction> logger)
    {
        _tokens = tokens;
        _logger = logger;
    }

    [Function(nameof(ContactTokenFunction))]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "contact/token")] HttpRequest request
    )
    {
        _ = request;
        string? token = _tokens.Issue();
        if (token is null)
        {
            _logger.LogError(
                "FORM_TOKEN_KEY is missing or shorter than {MinimumLength} characters. Not issuing a form token.",
                ContactLimits.MinimumFormTokenKeyLength
            );
            return ContactResponses.Unavailable();
        }

        return new OkObjectResult(new FormTokenResponse(token));
    }
}
