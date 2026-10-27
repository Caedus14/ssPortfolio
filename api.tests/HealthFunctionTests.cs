using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Functions;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class HealthFunctionTests
{
    [Fact]
    public void Report_names_the_api_and_says_ok()
    {
        HealthReport report = HealthReport.Create();

        Assert.Equal("ok", report.Status);
        Assert.Equal("ssportfolio-api", report.Service);
    }

    [Fact]
    public void Get_returns_200_with_that_report()
    {
        var function = new HealthFunction();
        var context = new DefaultHttpContext();

        IActionResult result = function.Run(context.Request);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        HealthReport report = Assert.IsType<HealthReport>(ok.Value);
        Assert.Equal(HealthReport.Create(), report);
    }
}
