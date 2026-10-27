using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Portfolio.Api.Functions;

/// <summary>
/// A public record so the JSON shape is obvious and the test can read it.
/// status is "ok". service names this API, not the React site.
/// </summary>
public sealed record HealthReport(string Status, string Service)
{
    public static HealthReport Create() => new("ok", "ssportfolio-api");
}

/// <summary>
/// GET /api/health.
/// The route property is "health". The Functions host adds the /api prefix,
/// which is also the only prefix Static Web Apps will forward to a managed function.
/// AuthorizationLevel.Anonymous is required: the browser does not send a function key,
/// and Static Web Apps calls the function through its own front door.
/// </summary>
public sealed class HealthFunction
{
    [Function(nameof(HealthFunction))]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request
    )
    {
        // The trigger binding requires the request argument. This endpoint does not read it.
        _ = request;
        return new OkObjectResult(HealthReport.Create());
    }
}
