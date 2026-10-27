using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Contact;
using Portfolio.Api.Functions;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactFunctionTests
{
    [Fact]
    public async Task Post_returns_501_until_the_pipeline_exists()
    {
        var function = new ContactFunction(new NotImplementedContactSubmitter());
        var context = new DefaultHttpContext();

        IActionResult result = await function.Run(context.Request, CancellationToken.None);

        ObjectResult body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, body.StatusCode);
        ContactErrorBody error = Assert.IsType<ContactErrorBody>(body.Value);
        Assert.Equal("not_implemented", error.Error);
    }

    [Fact]
    public void Procedure_codes_match_the_database_contract()
    {
        // db/README.md. These are the numbers usp_SubmitContact returns.
        // HTTP 501 from the stub is a different thing and is not in this list.
        Assert.Equal(0, ContactProcedureCodes.Accepted);
        Assert.Equal(1, ContactProcedureCodes.RateLimitedIp);
        Assert.Equal(2, ContactProcedureCodes.RateLimitedGlobal);
        Assert.Equal(3, ContactProcedureCodes.Invalid);
        Assert.Equal(5, ContactProcedureCodes.Busy);
    }
}
