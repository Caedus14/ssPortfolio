using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ClientIpTests
{
    [Fact]
    public void No_forwarded_header_omits_the_address()
    {
        var context = ContactTestSupport.Json("{}");
        context.Request.Headers["client-ip"] = "203.0.113.8:4000";

        Assert.Null(ClientIp.From(context.Request));
    }

    [Fact]
    public void A_single_forwarded_address_is_used_and_the_port_is_stripped()
    {
        var context = ContactTestSupport.Json("{}");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10:54421";

        Assert.Equal("203.0.113.10", ClientIp.From(context.Request));
    }

    [Fact]
    public void The_address_just_left_of_the_last_proxy_is_the_visitor()
    {
        var context = ContactTestSupport.Json("{}");
        context.Request.Headers["X-Forwarded-For"] = "1.1.1.1, 203.0.113.10:54421, 20.205.82.199:46830";

        Assert.Equal("203.0.113.10", ClientIp.From(context.Request));
    }

    [Fact]
    public void X_Azure_ClientIP_wins_over_a_forwarded_list()
    {
        var context = ContactTestSupport.Json("{}");
        context.Request.Headers["X-Forwarded-For"] = "1.1.1.1";
        context.Request.Headers["X-Azure-ClientIP"] = "203.0.113.9";

        Assert.Equal("203.0.113.9", ClientIp.From(context.Request));
    }

    [Fact]
    public void An_ipv6_address_with_a_bracketed_port_parses()
    {
        var context = ContactTestSupport.Json("{}");
        context.Request.Headers["X-Forwarded-For"] = "[2001:db8::10]:443";

        Assert.Equal("2001:db8::10", ClientIp.From(context.Request));
    }
}
