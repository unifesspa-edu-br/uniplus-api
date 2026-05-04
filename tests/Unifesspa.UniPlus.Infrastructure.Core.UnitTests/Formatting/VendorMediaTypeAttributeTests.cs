namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Formatting;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

public sealed class VendorMediaTypeAttributeTests
{
    private static readonly VendorMediaTypeAttribute Attribute = new()
    {
        Resource = "edital",
        Versions = [1],
    };

    [Theory]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("application/*")]
    [InlineData("application/vnd.uniplus.edital.v1+json")]
    public void OnActionExecuting_AcceptSuportado_NaoSetaResultado(string accept)
    {
        ActionExecutingContext context = CreateContext(accept);

        Attribute.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_AcceptAusente_NaoSetaResultado()
    {
        ActionExecutingContext context = CreateContext(accept: null);

        Attribute.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_VersaoNaoSuportada_RetornaProblemDetails406ComAvailableVersions()
    {
        VendorMediaTypeAttribute attr = new() { Resource = "edital", Versions = [1] };
        ActionExecutingContext context = CreateContext("application/vnd.uniplus.edital.v2+json");

        attr.OnActionExecuting(context);

        ObjectResult result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status406NotAcceptable);

        ProblemDetails problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status406NotAcceptable);
        problem.Type.Should().Be("https://uniplus.unifesspa.edu.br/errors/uniplus.contract.versao_nao_suportada");
        problem.Extensions["code"].Should().Be("uniplus.contract.versao_nao_suportada");
        problem.Extensions["available_versions"].Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void OnActionExecuting_AcceptDeRecursoDiferente_FazFallbackQuandoCoringaPresente()
    {
        ActionExecutingContext context = CreateContext("application/vnd.uniplus.outro.v9+json, */*");

        Attribute.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnResultExecuting_AposAceiteValido_SetaContentTypeNaResposta()
    {
        ActionExecutingContext executing = CreateContext("application/vnd.uniplus.edital.v1+json");
        Attribute.OnActionExecuting(executing);
        executing.Result.Should().BeNull();

        ResultExecutingContext resultCtx = new(
            executing,
            [],
            new OkObjectResult("payload"),
            controller: new object());

        Attribute.OnResultExecuting(resultCtx);

        resultCtx.HttpContext.Response.ContentType.Should().Be("application/vnd.uniplus.edital.v1+json");
    }

    [Fact]
    public void OnResultExecuting_FallbackJson_SetaContentTypeDaUltimaVersao()
    {
        VendorMediaTypeAttribute attr = new() { Resource = "edital", Versions = [1, 2] };
        ActionExecutingContext executing = CreateContext("application/json");
        attr.OnActionExecuting(executing);

        ResultExecutingContext resultCtx = new(
            executing,
            [],
            new OkObjectResult("payload"),
            controller: new object());

        attr.OnResultExecuting(resultCtx);

        resultCtx.HttpContext.Response.ContentType.Should().Be("application/vnd.uniplus.edital.v2+json");
    }

    [Fact]
    public void ProblemDetails_NaoConteimPiiNoCorpo()
    {
        ActionExecutingContext context = CreateContext("application/vnd.uniplus.edital.v9+json");
        Attribute.OnActionExecuting(context);

        ObjectResult result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = (ProblemDetails)result.Value!;
        string serialized = JsonSerializer.Serialize(problem);

        // O corpo só deve conter referências ao recurso e à lista de versões.
        serialized.Should().NotContain("@");
        serialized.Should().NotMatchRegex(@"\d{3}\.\d{3}\.\d{3}-\d{2}");
    }

    private static ActionExecutingContext CreateContext(string? accept)
    {
        DefaultHttpContext httpContext = new();
        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }
}
