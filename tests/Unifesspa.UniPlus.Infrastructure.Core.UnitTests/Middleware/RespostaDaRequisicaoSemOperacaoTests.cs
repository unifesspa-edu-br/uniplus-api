namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public sealed class RespostaDaRequisicaoSemOperacaoTests
{
    private static readonly Endpoint EndpointComum = new(static _ => Task.CompletedTask, EndpointMetadataCollection.Empty, "comum");

    private static readonly RouteEndpoint EndpointDeRota = new(
        static _ => Task.CompletedTask, RoutePatternFactory.Parse("/rota"), order: 0, EndpointMetadataCollection.Empty, "rota");

    [Fact(DisplayName = "O 404 sem operação escreve o código do caminho inexistente")]
    public async Task Status404SemOperacao_EscreveCaminhoInexistente()
    {
        DefaultHttpContext contexto = await ExecutarAsync(StatusCodes.Status404NotFound, endpoint: null);

        using JsonDocument corpo = Ler(contexto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be(ErrosDaFronteiraHttp.CaminhoInexistente.Code);
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        contexto.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact(DisplayName = "O 405 sem operação escreve o código do método não aceito e preserva o Allow")]
    public async Task Status405SemOperacao_EscreveMetodoNaoAceitoEPreservaAllow()
    {
        DefaultHttpContext contexto = await ExecutarAsync(StatusCodes.Status405MethodNotAllowed, EndpointComum, allow: "GET, HEAD");

        using JsonDocument corpo = Ler(contexto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be(ErrosDaFronteiraHttp.MetodoNaoAceito.Code);
        corpo.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status405MethodNotAllowed);
        corpo.RootElement.GetProperty("type").GetString().Should().EndWith($"/erros/{ErrosDaFronteiraHttp.MetodoNaoAceito.Code}");
        contexto.Response.Headers.Allow.ToString().Should().Be("GET, HEAD");
    }

    [Theory(DisplayName = "Com a operação selecionada, o 404 e o 405 ficam como a operação os deixou")]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed)]
    public async Task ComOperacao_NaoEscreve(int status)
    {
        DefaultHttpContext contexto = await ExecutarAsync(status, EndpointDeRota);

        contexto.Response.Body.Length.Should().Be(0);
    }

    [Fact(DisplayName = "O 401 sem operação não é escrito aqui")]
    public async Task Status401SemOperacao_NaoEscreve()
    {
        DefaultHttpContext contexto = await ExecutarAsync(StatusCodes.Status401Unauthorized, endpoint: null);

        contexto.Response.Body.Length.Should().Be(0);
    }

    [Theory(DisplayName = "Só o endpoint de rota conta como operação alcançada")]
    [InlineData(null, false)]
    [InlineData("comum", false)]
    [InlineData("rota", true)]
    public void ChegouAUmaOperacao_SoParaEndpointDeRota(string? tipo, bool esperado)
    {
        DefaultHttpContext contexto = new();
        contexto.SetEndpoint(tipo switch { "comum" => EndpointComum, "rota" => EndpointDeRota, _ => null });

        contexto.ChegouAUmaOperacao().Should().Be(esperado);
    }

    private static Task<DefaultHttpContext> ExecutarAsync(int status, Endpoint? endpoint, string? allow = null) =>
        PipelineDaRequisicaoSemOperacao.ExecutarAsync(status, endpoint, allow);

    private static JsonDocument Ler(HttpContext contexto)
    {
        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        return JsonDocument.Parse(contexto.Response.Body);
    }
}
