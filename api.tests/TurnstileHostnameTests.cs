using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class TurnstileHostnameTests
{
    [Theory]
    [InlineData("sethstearns.dev")]
    [InlineData("WWW.sethstearns.dev")]
    [InlineData("sethstearns.dev:443")]
    [InlineData("nice-rock-033c92210.2.azurestaticapps.net")]
    [InlineData("nice-rock-033c92210-8.centralus.2.azurestaticapps.net")]
    public void Production_and_this_apps_preview_hosts_are_allowed(string hostname)
    {
        Assert.True(TurnstileHostnames.IsAllowed(hostname, extraHosts: null));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("evil.azurestaticapps.net")]
    [InlineData("nice-rock-033c92210-8.centralus.2.azurestaticapps.net.evil.com")]
    [InlineData("localhost")]
    public void Other_hosts_are_rejected_unless_configured(string hostname)
    {
        Assert.False(TurnstileHostnames.IsAllowed(hostname, extraHosts: null));
    }

    [Fact]
    public void An_extra_host_is_added_and_does_not_remove_the_defaults()
    {
        Assert.True(TurnstileHostnames.IsAllowed("localhost", "localhost, 127.0.0.1"));
        Assert.True(TurnstileHostnames.IsAllowed("sethstearns.dev", "localhost"));
    }
}
