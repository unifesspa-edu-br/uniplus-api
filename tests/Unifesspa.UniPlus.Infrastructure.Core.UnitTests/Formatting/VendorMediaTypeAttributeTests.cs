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
    public void OnActionExecuting_AcceptComQZeroEmJson_RetornaProblemDetails406()
    {
        // RFC 9110 §12.5.1: q=0 explicitamente marca o media range como
        // inaceitável. Não pode cair no fallback application/json mesmo
        // quando ele aparece na lista.
        ActionExecutingContext context = CreateContext(
            "application/vnd.uniplus.edital.v9+json;q=1, application/json;q=0");

        Attribute.OnActionExecuting(context);

        ObjectResult result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status406NotAcceptable);
        ProblemDetails problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["available_versions"].Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void OnActionExecuting_AcceptComQZeroEmJsonComV1NaLista_AceitaV1()
    {
        // q=0 em json é respeitado; a versão suportada na mesma lista vence.
        ActionExecutingContext context = CreateContext(
            "application/vnd.uniplus.edital.v1+json, application/json;q=0");

        Attribute.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("Application/Vnd.Uniplus.Edital.V1+Json")]
    [InlineData("APPLICATION/VND.UNIPLUS.EDITAL.V1+JSON")]
    [InlineData("Application/JSON")]
    public void OnActionExecuting_AcceptComCasingDiferente_AceitaCaseInsensitive(string accept)
    {
        // RFC 9110 §8.3.1: media type tokens são case-insensitive.
        ActionExecutingContext context = CreateContext(accept);

        Attribute.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_MultiplasVersoesComQDiferente_AceitaMaiorQ()
    {
        // RFC 9110 §12.5.1: maior q-value vence sobre ordem do header.
        // Cliente lista v1 antes de v2 mas atribui q maior a v2 — deve aceitar v2.
        VendorMediaTypeAttribute attr = new() { Resource = "edital", Versions = [1, 2] };
        ActionExecutingContext context = CreateContext(
            "application/vnd.uniplus.edital.v1+json;q=0.5, application/vnd.uniplus.edital.v2+json;q=0.9");

        attr.OnActionExecuting(context);

        context.Result.Should().BeNull();
        context.HttpContext.Items["__UniPlusVendorMediaTypeAccepted"].Should().Be(2);
    }

    [Fact]
    public void OnActionExecuting_QZeroEmV1ComWildcard_QuandoV1ELatest_Retorna406()
    {
        // Cliente exclui v1 explicitamente e aceita */* — como v1 é a única
        // versão suportada, o wildcard deveria resolver para v1, mas v1 está
        // excluída. Resultado correto: 406 (RFC 9110 §12.5.1).
        ActionExecutingContext context = CreateContext(
            "application/vnd.uniplus.edital.v1+json;q=0, */*;q=1");

        Attribute.OnActionExecuting(context);

        ObjectResult result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status406NotAcceptable);
    }

    [Fact]
    public void OnActionExecuting_QZeroEmV1ComWildcard_QuandoExisteV2_AceitaV2()
    {
        // Mesma exclusão de v1, mas com v2 disponível: wildcard resolve
        // para latest=v2, que não está excluída.
        VendorMediaTypeAttribute attr = new() { Resource = "edital", Versions = [1, 2] };
        ActionExecutingContext context = CreateContext(
            "application/vnd.uniplus.edital.v1+json;q=0, */*;q=1");

        attr.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnResultExecuting_RespostaProblemDetails_NaoSobrescreveContentType()
    {
        // RFC 9457: erros mantêm application/problem+json mesmo após
        // negociação bem-sucedida (ex.: cursor inválido em endpoint paginado).
        ActionExecutingContext executing = CreateContext("application/vnd.uniplus.edital.v1+json");
        Attribute.OnActionExecuting(executing);

        ObjectResult problemResult = new(new ProblemDetails { Status = StatusCodes.Status400BadRequest })
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };

        ResultExecutingContext resultCtx = new(
            executing,
            [],
            problemResult,
            controller: new object());

        Attribute.OnResultExecuting(resultCtx);

        resultCtx.HttpContext.Response.ContentType.Should().BeNull();
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
