namespace Unifesspa.UniPlus.Portal.IntegrationTests.Cors;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

/// <summary>
/// <b>Todo header que o contrato do Portal promete ao cliente é legível de fato por uma aplicação
/// web de outra origem.</b>
/// </summary>
/// <remarks>
/// Sem constar de <c>Access-Control-Expose-Headers</c>, o <c>Retry-After</c> do <c>503</c> chega ao
/// navegador e <c>response.headers.get('Retry-After')</c> devolve nulo: a tela não sabe quando
/// tentar de novo, e nada no servidor denuncia.
/// </remarks>
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class HeaderDeclaradoEAlcancavelNoPortalTests : IClassFixture<PortalApiFactory>
{
    /// <summary>Origem declarada na configuração de desenvolvimento, que o host de teste usa.</summary>
    private const string OrigemDoSpa = "http://localhost:4200";

    private readonly PortalApiFactory _factory;

    public HeaderDeclaradoEAlcancavelNoPortalTests(PortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Todo header declarado nos endpoints do Portal está exposto pelo CORS")]
    public void Contrato_QuandoEndpointDoPortalDeclaraHeader_DeveExpoLoPeloCors()
    {
        IReadOnlyCollection<string> declarados = HeadersDoContrato.Declarados(_factory.Services);

        declarados.Should().NotBeEmpty(
            "sem endpoint declarando header próprio, este teste não estaria verificando nada");

        IReadOnlyCollection<string> expostos = HeadersDoContrato.ExpostosPelaPoliticaPadrao(_factory.Services);

        declarados.Should().BeSubsetOf(expostos,
            "um header emitido e documentado que o CORS não expõe é ilegível na SPA, sem erro que denuncie");
    }

    [Fact(DisplayName = "A resposta de outra origem expõe o Retry-After do Portal ao JavaScript")]
    public async Task Responder_QuandoRequisicaoDeOutraOrigem_DeveExporRetryAfter()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Get, new Uri("/api/portal/ping", UriKind.Relative));
        requisicao.Headers.Add("Origin", OrigemDoSpa);

        using HttpResponseMessage resposta = await client.SendAsync(requisicao, CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        HeadersExpostos(resposta).Should().Contain("Retry-After", "o 503 do certame indica nele quando tentar de novo");
    }

    private static IReadOnlyList<string> HeadersExpostos(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Access-Control-Expose-Headers", out IEnumerable<string>? valores)
            ? [.. valores.SelectMany(static v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))]
            : [];
}
