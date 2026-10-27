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

// Registered now so a later pull request can take these from the constructor.
// Nothing reads them yet, so /api/health works before the secrets exist.
builder.Services.AddSingleton<IIpAddressHasher, UnconfiguredIpAddressHasher>();
builder.Services.AddSingleton<IContactSubmitter, NotImplementedContactSubmitter>();
builder.Services.AddOptions<ContactApiOptions>().Configure<IConfiguration>(ContactApiOptionsBinder.Bind);

builder.Build().Run();
