namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa.ApoioDoEndpointDeTiposEtapa;

/// <summary>Contrato HTTP da leitura pública dos tipos de etapa (UNI-REQ-0015): vendor MIME e carga inicial do cadastro.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaLeituraPublicaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaLeituraPublicaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-etapa é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-etapa.v1+json");
    }

    /// <summary>
    /// A carga inicial declara o que cada tipo admite, e não um valor permissivo uniforme: sem
    /// isso o wizard voltaria a oferecer peso em toda etapa, e a decisão de quem opera o cadastro
    /// dependeria da idade do banco em que ela foi feita.
    /// </summary>
    [Theory(DisplayName = "Carga inicial declara o que cada tipo semeado admite e de onde vem a nota")]
    [InlineData("ANALISE_DOCUMENTAL", false, true, false, false)]
    [InlineData("BANCA_HETEROIDENTIFICACAO", false, true, false, false)]
    [InlineData("PROVA_OBJETIVA", true, true, false, false)]
    [InlineData("REDACAO", true, true, false, true)]
    [InlineData("ENTREVISTA", true, true, false, false)]
    [InlineData("ANALISE_HISTORICO", true, true, false, false)]
    [InlineData("NOTA_ENEM", true, true, true, false)]
    public async Task Listar_TiposSemeados_DeclaramOCaraterQueAdmitem(
        string codigo, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem, bool exigeCorrecaoIndependente)
    {
        using JsonDocument lista = await ListarTiposPublicosAsync();

        JsonElement avaliacao = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("codigo").GetString() == codigo).GetProperty("avaliacao");
        avaliacao.GetProperty("admitePontuacao").GetBoolean().Should().Be(admitePontuacao);
        avaliacao.GetProperty("admiteEliminacao").GetBoolean().Should().Be(admiteEliminacao);
        avaliacao.GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().Be(notaDeOrigemNoEnem);
        avaliacao.GetProperty("exigeCorrecaoIndependente").GetBoolean().Should().Be(exigeCorrecaoIndependente);
    }

    private async Task<JsonDocument> ListarTiposPublicosAsync()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static readonly HashSet<string> CodigosSemeados = new(StringComparer.Ordinal)
    {
        "PROVA_OBJETIVA", "REDACAO", "ENTREVISTA", "ANALISE_HISTORICO",
        "BANCA_HETEROIDENTIFICACAO", "ANALISE_DOCUMENTAL", "NOTA_ENEM",
    };

    [Fact(DisplayName = "Carga inicial usa UUIDv7 RFC 9562 nos sete tipos semeados")]
    public async Task Listar_TiposSemeados_UsamUuidV7()
    {
        using JsonDocument lista = await ListarTiposPublicosAsync();
        Guid[] idsSemeados = lista.RootElement.EnumerateArray()
            .Where(item => CodigosSemeados.Contains(item.GetProperty("codigo").GetString()!))
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        idsSemeados.Should().HaveCount(7);
        idsSemeados.Should().OnlyHaveUniqueItems();
        idsSemeados.Should().OnlyContain(id => EhUuidV7Rfc9562(id),
            "a migration semeia entidades de domínio e o projeto exige UUIDv7");
    }

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }
}
