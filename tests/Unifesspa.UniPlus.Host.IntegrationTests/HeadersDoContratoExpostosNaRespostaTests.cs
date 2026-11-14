namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// <b>Os headers que os controllers do host declaram no contrato chegam ao JavaScript de outra
/// origem na resposta real.</b>
/// </summary>
/// <remarks>
/// Corre contra o pipeline montado: o que interessa é o <c>Access-Control-Expose-Headers</c> que o
/// navegador recebe, e não o objeto de política. O <c>Location</c> dos <c>201</c> de atos e
/// produtos previstos e os <c>X-Certames-*</c> da vitrine pública só são legíveis pela SPA se
/// constarem dele.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class HeadersDoContratoExpostosNaRespostaTests
{
    private const string OrigemDoSpa = "http://localhost:4200";
    private const string RotaAnonima = "/api/configuracao/cursos?limit=1";

    private static readonly string[] HeadersDeclaradosPeloHost =
    [
        "Location",
        "X-Certames-Em-Breve",
        "X-Certames-Inscricoes-Abertas",
        "X-Certames-Ultimos-Dias",
        "X-Certames-Encerrados",
        "X-Certames-Revisao",
    ];

    private readonly MonolitoPostgresFixture _fixture;

    public HeadersDoContratoExpostosNaRespostaTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A resposta de outra origem expõe os headers declarados pelos controllers do host")]
    public async Task Responder_QuandoRequisicaoDeOutraOrigem_DeveExporOsHeadersDoContrato()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Get, RotaAnonima);
        requisicao.Headers.Add("Origin", OrigemDoSpa);

        using HttpResponseMessage resposta = await client.SendAsync(requisicao, CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        HeadersExpostos(resposta).Should().Contain(HeadersDeclaradosPeloHost);
    }

    private static IReadOnlyList<string> HeadersExpostos(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Access-Control-Expose-Headers", out IEnumerable<string>? valores)
            ? [.. valores.SelectMany(static v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))]
            : [];
}
