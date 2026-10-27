using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Portfolio.Api.Contact;

namespace Portfolio.Api.Functions;

/// <summary>
/// POST /api/contact. This class is the HTTP edge. The rules live in
/// <see cref="ContactPipeline" />, so a test can run each rule without the host.
/// AuthorizationLevel.Anonymous: the browser does not send a function key.
/// </summary>
public sealed class ContactFunction
{
    private readonly IContactPipeline _pipeline;

    public ContactFunction(IContactPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    [Function(nameof(ContactFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        ContactSubmitResult result = await _pipeline.RunAsync(request, cancellationToken);
        return ContactResponses.From(result);
    }
}
