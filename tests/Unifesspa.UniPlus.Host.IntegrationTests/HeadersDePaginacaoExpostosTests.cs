namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// <b>Os headers em que a navegação paginada vive chegam ao JavaScript de outra origem.</b>
/// </summary>
/// <remarks>
/// <para>
/// O corpo de uma coleção é um array puro (ADR-0025) e todo metadado de navegação vai em header
/// (ADR-0026). Numa resposta de origem cruzada, porém, o navegador só entrega ao JavaScript sete
/// headers considerados seguros — e nem <c>Link</c> nem <c>X-Page-Size</c> estão entre eles. Sem
/// constarem de <c>Access-Control-Expose-Headers</c>, o SPA recebe a página e não tem como pedir a
/// seguinte: o endereço de continuação só existe no header que ele não pode ler.
/// </para>
/// <para>
/// A falha é silenciosa dos dois lados. O servidor emite corretamente, o navegador recebe, e
/// <c>response.headers.get('Link')</c> devolve nulo — sem erro, sem status diferente, sem nada no
/// log. A suspeita cai no backend, que está certo.
/// </para>
/// <para>
/// Este teste corre contra o pipeline montado, e não contra o objeto de política, de propósito: uma
/// asserção sobre as opções passaria mesmo que a política nunca fosse aplicada ao pipeline. O que
/// interessa é o header que sai na resposta.
/// </para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class HeadersDePaginacaoExpostosTests
{
    /// <summary>Origem declarada na configuração de desenvolvimento, que o host de teste usa.</summary>
    private const string OrigemDoSpa = "http://localhost:4200";

    /// <summary>Listagem paginada anônima — serve o contrato de coleção sem exigir autenticação.</summary>
    private const string ListagemPaginada = "/api/configuracao/cursos?limit=1";

    private readonly MonolitoPostgresFixture _fixture;

    public HeadersDePaginacaoExpostosTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A resposta de outra origem expõe Link e X-Page-Size ao JavaScript")]
    public async Task RespostaDeOutraOrigem_ExpoeOsHeadersDaNavegacao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Get, ListagemPaginada);
        requisicao.Headers.Add("Origin", OrigemDoSpa);

        using HttpResponseMessage resposta = await client.SendAsync(requisicao, CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        IReadOnlyList<string> expostos = HeadersExpostos(resposta);

        expostos.Should().Contain("Link", "sem ele o cliente recebe a página e não tem como pedir a seguinte");
        expostos.Should().Contain("X-Page-Size", "o tamanho efetivo da página só existe neste header");
    }

    [Fact(DisplayName = "O selo de entidade continua exposto junto dos headers de paginação")]
    public async Task RespostaDeOutraOrigem_MantemOSeloExposto()
    {
        // O outro sentido da conferência: acrescentar nomes à lista não pode derrubar os que já
        // estavam. O selo é a precondição da mutação seguinte — sem lê-lo, toda edição sob
        // retificação sairia 428 no navegador, com o servidor correto.
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Get, ListagemPaginada);
        requisicao.Headers.Add("Origin", OrigemDoSpa);

        using HttpResponseMessage resposta = await client.SendAsync(requisicao, CancellationToken.None);

        HeadersExpostos(resposta).Should().Contain(["ETag", "Idempotency-Replayed"]);
    }

    /// <summary>
    /// Os nomes que a resposta autoriza o JavaScript a ler, como o navegador os leria.
    /// </summary>
    private static IReadOnlyList<string> HeadersExpostos(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Access-Control-Expose-Headers", out IEnumerable<string>? valores)
            ? [.. valores.SelectMany(static v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))]
            : [];
}
