namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Manifestação e decisão concorrentes no mesmo item, com duas transações reais sobrepostas no PostgreSQL pelo host real
/// (ADR-0163, UNI-REQ-0374): a manifestação muda a linha do item, condicionada ao mesmo token de concorrência da decisão,
/// e quem grava depois de a outra ser confirmada perde com conflito, relê e refaz. Vale para o tipo de documento e o fato.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ManifestacaoEDecisaoConcorrentesTests : ManifestacaoDaProtecaoTestBase
{
    private const string Justificativa = "A classificação declarada basta: o documento só comprova a matrícula.";

    public ManifestacaoEDecisaoConcorrentesTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "A manifestação inadequada gravada antes vence a aprovação sem justificativa, que relida exige a justificativa")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task ManifestacaoAntesDaAprovacao(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        (HttpResponseMessage manifestacao, HttpResponseMessage aprovacao) = await CorridaAsync(cadastro, id,
            () => ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA"),
            () => DecidirComoAsync(client, Betina, cadastro, id, "APROVAR"));

        manifestacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await DeveSerConflitoComNovaTentativaAsync(aprovacao);
        (await DetalheAsync(client, cadastro, id)).GetProperty("aprovacao").GetProperty("manifestacaoDesfavoravel").GetBoolean().Should().BeTrue();
        await RefazerExigindoAJustificativaAsync(client, cadastro, id, "APROVAR", recomendada: null);
        (await UltimoAtoAsync(client, cadastro, id)).GetProperty("dianteDeManifestacaoDesfavoravel").GetBoolean().Should().BeTrue();
    }

    [Theory(DisplayName = "A manifestação de ajuste gravada antes vence o ajuste sem justificativa; relido, o ajuste exige a justificativa e o administrador aceita sem ela")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task ManifestacaoAntesDoAjuste(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        (HttpResponseMessage manifestacao, HttpResponseMessage ajuste) = await CorridaAsync(cadastro, id,
            () => ManifestarAsync(client, Kevin, cadastro, id, "RECOMENDA_AJUSTE"),
            () => DecidirComoAsync(client, Betina, cadastro, id, "RECOMENDAR_AJUSTE", recomendada: "SENSIVEL"));

        manifestacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await DeveSerConflitoComNovaTentativaAsync(ajuste);
        await RefazerExigindoAJustificativaAsync(client, cadastro, id, "RECOMENDAR_AJUSTE", recomendada: "SENSIVEL");
        (await AceitarAsync(client, cadastro, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory(DisplayName = "O ajuste gravado antes vence a manifestação, que relida encontra o item fora de solicitação; o administrador aceita sem justificativa")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task AjusteAntesDaManifestacao(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        (HttpResponseMessage ajuste, HttpResponseMessage manifestacao) = await CorridaAsync(cadastro, id,
            () => DecidirComoAsync(client, Betina, cadastro, id, "RECOMENDAR_AJUSTE", recomendada: "SENSIVEL"),
            () => ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA"));

        ajuste.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await DeveSerConflitoComNovaTentativaAsync(manifestacao);
        (await CodigoAsync(await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA"))).Should().EndWith(".item_fora_de_solicitacao");
        (await AceitarAsync(client, cadastro, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetalheAsync(client, cadastro, id)).GetProperty("aprovacao").GetProperty("ciclo").GetInt32().Should().Be(1);
    }

    [Theory(DisplayName = "Duas manifestações de pessoas diferentes se serializam: a refeita coexiste com a outra")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task DuasManifestacoes(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        (HttpResponseMessage doKevin, HttpResponseMessage daLucia) = await CorridaAsync(cadastro, id,
            () => ManifestarAsync(client, Kevin, cadastro, id, "ADEQUADA", texto: null),
            () => ManifestarAsync(client, Lucia, cadastro, id, "INADEQUADA"));

        doKevin.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await DeveSerConflitoComNovaTentativaAsync(daLucia);
        (await ManifestarAsync(client, Lucia, cadastro, id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement detalhe = await DetalheAsync(client, cadastro, id);
        (detalhe.GetProperty("manifestacoesVigentes").GetArrayLength(), detalhe.GetProperty("aprovacao").GetProperty("manifestacaoDesfavoravel").GetBoolean())
            .Should().Be((2, true));
    }

    [Theory(DisplayName = "Duas manifestações concorrentes no mesmo instante e com o mesmo indicador desfavorável se serializam: a segunda perde com 409")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task ManifestacoesNoMesmoInstanteEComOMesmoIndicador(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);
        using IDisposable congelado = ((ConfiguracaoEndpointFactory)Fixture.Factory).Relogio.Congelar();
        (await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Nem o instante nem o indicador mudam: só a contagem do ciclo faz a linha do item mudar e entrar na fila.
        (HttpResponseMessage doKevin, HttpResponseMessage daLucia) = await CorridaAsync(cadastro, id,
            () => ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA", "Reitero: o dado é sensível."),
            () => ManifestarAsync(client, Lucia, cadastro, id, "INADEQUADA"));

        doKevin.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await DeveSerConflitoComNovaTentativaAsync(daLucia);
    }

    [Theory(DisplayName = "Controle: a manifestação gravada sem tocar a linha do item deixaria passar a aprovação ou o ajuste sem justificativa")]
    [InlineData("TIPO_DOCUMENTO", "APROVAR", null)]
    [InlineData("FATO", "APROVAR", null)]
    [InlineData("TIPO_DOCUMENTO", "RECOMENDAR_AJUSTE", "SENSIVEL")]
    [InlineData("FATO", "RECOMENDAR_AJUSTE", "SENSIVEL")]
    public async Task SemEscritaNaLinhaDoItem_ACorridaPassaria(string cadastro, string decisao, string? recomendada)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);
        await using TravaDaLinhaDoItem trava = await TravaDaLinhaDoItem.TravarAsync(Fixture.Factory, cadastro, id);

        Task<HttpResponseMessage> decidida = DecidirComoAsync(client, Betina, cadastro, id, decisao, recomendada: recomendada);
        await trava.EsperarNaFilaAsync(decidida, 1);
        await InserirManifestacaoInadequadaAsync(trava, cadastro, id);
        await trava.LiberarAsync();

        (await decidida).StatusCode.Should().Be(HttpStatusCode.NoContent, "só a mudança na linha do item faz a decisão perder a corrida");
        (await HistoricoAsync(client, Betina, cadastro, id)).Should().ContainSingle().Which.GetProperty("parecer").GetString().Should().Be("INADEQUADA");
    }

    private async Task<Guid> PrepararAsync(HttpClient client, string cadastro)
    {
        await PrepararTodosAsync();
        return await SolicitadoAsync(client, cadastro);
    }

    // A primeira chamada entra na fila da linha, depois a segunda; liberada a trava, a primeira grava e a segunda perde.
    private async Task<(HttpResponseMessage Primeira, HttpResponseMessage Segunda)> CorridaAsync(
        string cadastro, Guid id, Func<Task<HttpResponseMessage>> primeira, Func<Task<HttpResponseMessage>> segunda)
    {
        await using TravaDaLinhaDoItem trava = await TravaDaLinhaDoItem.TravarAsync(Fixture.Factory, cadastro, id);
        Task<HttpResponseMessage> a = primeira();
        await trava.EsperarNaFilaAsync(a, 1);
        Task<HttpResponseMessage> b = segunda();
        await trava.EsperarNaFilaAsync(b, 2);
        await trava.LiberarAsync();
        return (await a, await b);
    }

    private static async Task DeveSerConflitoComNovaTentativaAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, await resposta.Content.ReadAsStringAsync());
        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("code").GetString().Should().Be("uniplus.configuracao.protecao_do_catalogo.conflito_de_concorrencia");
        problema.GetProperty("retryable").GetBoolean().Should().BeTrue();
    }

    // Relido o item, a decisão sem justificativa é recusada pela manifestação desfavorável, e com ela é gravada.
    private static async Task RefazerExigindoAJustificativaAsync(HttpClient client, string cadastro, Guid id, string decisao, string? recomendada)
    {
        HttpResponseMessage sem = await DecidirComoAsync(client, Betina, cadastro, id, decisao, recomendada: recomendada);
        HttpResponseMessage com = await DecidirComoAsync(client, Betina, cadastro, id, decisao, Justificativa, recomendada);

        (await CodigoAsync(sem)).Should().Be("uniplus.configuracao.protecao_do_catalogo.justificativa_exigida_pela_manifestacao");
        com.StatusCode.Should().Be(HttpStatusCode.NoContent, await com.Content.ReadAsStringAsync());
        (await UltimoAtoAsync(client, cadastro, id)).GetProperty("justificativa").GetString().Should().Be(Justificativa);
    }

    // A manifestação gravada só na tabela dela, na transação da trava, sem a mudança do circuito na linha do item.
    private static Task<int> InserirManifestacaoInadequadaAsync(TravaDaLinhaDoItem trava, string cadastro, Guid id) =>
        trava.Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.protecao_do_catalogo_manifestacoes
                (id, cadastro, item_id, ciclo, parecer, texto, substitui_id, autor_emissor, autor_sub, autor_nome, instante)
            VALUES ({Guid.CreateVersion7()}, {cadastro}, {id}, 1, 'INADEQUADA', 'O dado é sensível.', NULL,
                    {TestAuthHandler.DefaultIssuer}, {Kevin.Sub}, {Kevin.Nome}, now())
            """);
}
