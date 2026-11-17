namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;
using static AtosDaEntidadeApoio;

/// <summary>
/// A unicidade de linhagem por objeto (ADR-0107), exercitada por HTTP contra o monólito real: as recusas que
/// impedem um objeto de ser tratado por duas linhagens e os casos em que a vaga não se aplica.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class LinhagemUnicaNoObjetoEndpointTests
{
    private const string ObjetoJaTemAtoVivoDoTipo = "uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo";

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtosDaEntidadeApoio _apoio;

    public LinhagemUnicaNoObjetoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtosDaEntidadeApoio(_fixture);
    }

    [Fact(DisplayName = "Duas linhagens do mesmo tipo único no mesmo objeto: a segunda recebe 409")]
    public async Task Registrar_SegundaLinhagemNoMesmoObjeto_Retorna409()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage segunda = await PostAtoAsync(
            client, PayloadAto(tipo, "14", "2026-03-14", [Vinculo(processo)]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain(ObjetoJaTemAtoVivoDoTipo);
    }

    [Fact(DisplayName = "A retificação da linhagem que ocupa o objeto é aceita — a vaga é da linhagem, não do ato")]
    public async Task Registrar_RetificacaoDaLinhagemQueOcupa_Retorna201()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raiz = await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage retificacao = await PostAtoAsync(
            client, PayloadRetificacao(tipo, "13", "2026-03-13", raiz, "corrige o anexo", [Vinculo(processo)]));

        retificacao.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <remarks>
    /// Um índice único parcial filtrado por "o ato é raiz" deixaria isto passar: a retificação não é raiz e
    /// escaparia do filtro. A vaga é chaveada pela linhagem, e é isso que fecha o furo. A outra linhagem é
    /// publicada sem vínculo, e passa, porque sem objeto não há vaga; a retificação dela tenta vincular o
    /// objeto que a primeira linhagem já trata.
    /// </remarks>
    [Fact(DisplayName = "Uma retificação de outra linhagem não invade o objeto ocupado (o furo do índice sobre a raiz)")]
    public async Task Registrar_RetificacaoDeOutraLinhagemNoObjetoOcupado_Retorna409()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));
        Guid raizSemVinculo = await PostAtoIdAsync(client, PayloadAto(tipo, "99", "2026-03-20", []));

        HttpResponseMessage invasora = await PostAtoAsync(
            client,
            PayloadRetificacao(tipo, "99", "2026-03-21", raizSemVinculo, "vincula o processo", [Vinculo(processo)]));

        invasora.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await invasora.Content.ReadAsStringAsync()).Should().Contain(ObjetoJaTemAtoVivoDoTipo);
    }

    /// <remarks>
    /// O catálogo é editável: o primeiro ato não reservou vaga alguma, porque no seu instante o tipo não era
    /// único por objeto. É a consulta ao histórico de atos, e não a tabela de vagas, que enxerga isso.
    /// </remarks>
    [Fact(DisplayName = "Ato publicado antes de o tipo virar único por objeto ainda bloqueia a segunda linhagem")]
    public async Task Registrar_AtoAnteriorAoAtributo_AindaBloqueia()
    {
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        (string codigo, Guid tipoId) = await _apoio.CriarTipoComIdAsync(unicoPorObjeto: false);
        await PostAtoIdAsync(client, PayloadAto(codigo, "13", "2026-03-13", [Vinculo(processo)]));
        await AtualizarTipoParaUnicoPorObjetoAsync(client, tipoId, codigo);

        HttpResponseMessage segunda = await PostAtoAsync(
            client, PayloadAto(codigo, "14", "2026-03-14", [Vinculo(processo)]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain(ObjetoJaTemAtoVivoDoTipo);
    }

    /// <remarks>
    /// A ADR-0103 admite que um tipo emende outro (um aviso retifica um edital), desde que a classe de
    /// congelamento coincida. A vaga é por (objeto, tipo do ato): o aviso abre a sua própria vaga no mesmo
    /// objeto, sem disputar a do edital. Os dois atos tratam do mesmo processo, porque o aviso herda o
    /// vínculo do edital.
    /// </remarks>
    [Fact(DisplayName = "Um ato de tipo único retifica outro de tipo único distinto, e cada tipo tem a sua vaga no objeto")]
    public async Task Registrar_RetificacaoDeTipoDistinto_ReservaVagaDoSeuProprioTipo()
    {
        string edital = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        string aviso = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raiz = await PostAtoIdAsync(client, PayloadAto(edital, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage retificacao = await PostAtoAsync(
            client, PayloadRetificacao(aviso, "50", "2026-03-20", raiz, "prorroga o prazo", []));

        retificacao.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ListarDaEntidadeAsync(client, ObjetoA, processo)).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Tipo único por objeto sem vínculo algum é registrável — sem objeto, não há vaga")]
    public async Task Registrar_UnicoPorObjetoSemVinculo_Retorna201()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", []));
        HttpResponseMessage segundo = await PostAtoAsync(client, PayloadAto(tipo, "14", "2026-03-14", []));

        segundo.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Objetos distintos não disputam a mesma vaga")]
    public async Task Registrar_ObjetosDistintos_Retorna201()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: true);
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(Guid.CreateVersion7())]));
        HttpResponseMessage outro = await PostAtoAsync(
            client, PayloadAto(tipo, "14", "2026-03-14", [Vinculo(Guid.CreateVersion7())]));

        outro.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
