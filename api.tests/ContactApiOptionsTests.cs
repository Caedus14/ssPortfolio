using Microsoft.Extensions.Configuration;
using Portfolio.Api.Options;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactApiOptionsTests
{
    [Fact]
    public void Binder_reads_the_app_setting_names()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SQL_CONNECTION_STRING"] = "Server=example;",
                    ["TURNSTILE_SECRET"] = "turnstile-example",
                    ["TURNSTILE_ALLOWED_HOSTS"] = "localhost",
                    ["TURNSTILE_EXPECTED_ACTION"] = "contact",
                    ["IP_HASH_KEY"] = "hash-key-example",
                    ["FORM_TOKEN_KEY"] = "form-token-example",
                }
            )
            .Build();
        var options = new ContactApiOptions();

        ContactApiOptionsBinder.Bind(options, configuration);

        Assert.Equal("Server=example;", options.SqlConnectionString);
        Assert.Equal("turnstile-example", options.TurnstileSecret);
        Assert.Equal("localhost", options.TurnstileAllowedHosts);
        Assert.Equal("contact", options.TurnstileExpectedAction);
        Assert.Equal("hash-key-example", options.IpHashKey);
        Assert.Equal("form-token-example", options.FormTokenKey);
    }
}
