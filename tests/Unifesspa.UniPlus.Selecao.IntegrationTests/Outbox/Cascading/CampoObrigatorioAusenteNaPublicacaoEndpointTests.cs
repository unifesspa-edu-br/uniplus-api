namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDaPublicacaoDoProcessoSeletivo;

/// <summary>
/// O <c>POST /processos-seletivos/{id}/publicacao</c> sem o documento do edital, sem o bloco do ato, sem o ano ou sem a
/// data: a recusa é 422 com o código de campo obrigatório e um item por campo, e o processo inexistente continua sendo
/// 404, que vem antes.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CampoObrigatorioAusenteNaPublicacaoEndpointTests
{
    private const string AtoSemAno =
        """{ "orgao": "CEPS", "serie": "EDITAL", "dataDoAto": "2026-01-01", "assinante": "Diretor do CEPS", "tipoAtoCodigo": "EDITAL_ABERTURA" }""";

    private const string AtoSemData =
        """{ "orgao": "CEPS", "serie": "EDITAL", "ano": 2026, "assinante": "Diretor do CEPS", "tipoAtoCodigo": "EDITAL_ABERTURA" }""";

    private const string AtoComAnoNulo =
        """{ "orgao": "CEPS", "serie": "EDITAL", "ano": null, "dataDoAto": "2026-01-01", "assinante": "Diretor do CEPS", "tipoAtoCodigo": "EDITAL_ABERTURA" }""";

    private const string AtoCompleto =
        """{ "orgao": "CEPS", "serie": "EDITAL", "ano": 2026, "dataDoAto": "2026-01-01", "assinante": "Diretor do CEPS", "tipoAtoCodigo": "EDITAL_ABERTURA" }""";

    private readonly CascadingFixture _fixture;

    public CampoObrigatorioAusenteNaPublicacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public static TheoryData<string, string[]> CorposComCampoAusente() => new()
    {
        { "sem o bloco do ato", ["ato"] },
        { "sem o documento do edital", ["documentoEditalId"] },
        { "com o ato sem o ano", ["ato.ano"] },
        { "com o ato sem a data", ["ato.dataDoAto"] },
        { "com o ano nulo", ["ato.ano"] },
        { "sem nenhum campo", ["documentoEditalId", "ato"] },
    };

    [Theory(DisplayName = "O campo obrigatório ausente ou nulo é recusado com 422, no campo do pedido, e nada é publicado")]
    [MemberData(nameof(CorposComCampoAusente))]
    public async Task CampoAusente_422_ENadaPublicado(string caso, string[] campos)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(CampoAusente_422_ENadaPublicado));

        using HttpResponseMessage recusa = await PostAsync(client, processoId, CorpoDo(caso, documentoId));

        await RecusaDeObrigatorioDoDominio.ConferirAsync(recusa, campos);
        using HttpResponseMessage publicada = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, "a recusa não deixou nada gravado");
    }

    [Fact(DisplayName = "O processo inexistente é 404, mesmo com o corpo sem nenhum campo")]
    public async Task ProcessoInexistente_404_AntesDaAusencia()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostAsync(client, Guid.CreateVersion7(), "{}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string CorpoDo(string caso, Guid documentoId) => caso switch
    {
        "sem o bloco do ato" => $$"""{ "documentoEditalId": "{{documentoId}}" }""",
        "sem o documento do edital" => $$"""{ "ato": {{AtoCompleto}} }""",
        "com o ato sem o ano" => $$"""{ "documentoEditalId": "{{documentoId}}", "ato": {{AtoSemAno}} }""",
        "com o ato sem a data" => $$"""{ "documentoEditalId": "{{documentoId}}", "ato": {{AtoSemData}} }""",
        "com o ano nulo" => $$"""{ "documentoEditalId": "{{documentoId}}", "ato": {{AtoComAnoNulo}} }""",
        _ => "{}",
    };

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid processoId, string corpoJson)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
