using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portfolio.Api.Contact;
using Portfolio.Api.Options;

// Isolated worker: this process is separate from the Functions host.
// ConfigureFunctionsWebApplication turns on the ASP.NET Core HTTP types
// (HttpRequest, IActionResult). It does not turn this into a normal
// ASP.NET Core site. Static Web Apps managed functions still only allow
// HTTP triggers, and every route is served under /api.
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// The clock is injected so a test can move time without waiting.
// FORM_TOKEN_KEY is read when a token is issued or checked.
// A missing key is a 503 on those paths. Startup does not throw,
// so /api/health still answers before that setting exists in Azure.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFormTokenService, FormTokenService>();
builder.Services.AddSingleton<IContactRateLimiter, PassThroughContactRateLimiter>();
builder.Services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>(client =>
{
    client.Timeout = TurnstileVerifier.Timeout;
});
builder.Services.AddSingleton<IContactPipeline, ContactPipeline>();

// The hasher and the SQL call are still later steps.
builder.Services.AddSingleton<IIpAddressHasher, UnconfiguredIpAddressHasher>();
builder.Services.AddSingleton<IContactSubmitter, NotImplementedContactSubmitter>();
builder.Services.AddOptions<ContactApiOptions>().Configure<IConfiguration>(ContactApiOptionsBinder.Bind);

builder.Build().Run();
