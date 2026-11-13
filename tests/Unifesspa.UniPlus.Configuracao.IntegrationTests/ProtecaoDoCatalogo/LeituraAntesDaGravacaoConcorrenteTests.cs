namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A leitura de quem age feita enquanto a gravação do outro está em curso, com duas transações reais sobrepostas no
/// PostgreSQL pelo host real (UNI-REQ-0374): a manifestação confirmada depois da leitura do Representante do controlador
/// recusa a decisão; a decisão confirmada depois da leitura do Encarregado de dados recusa a manifestação. As duas
/// recusas vêm da linha do item, que a gravação muda: sem a conferência da contagem ou do ciclo e da situação, a
/// decisão e a manifestação passariam, e os controles mostram que a escrita fora da linha do item não as recusa.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class LeituraAntesDaGravacaoConcorrenteTests : ManifestacaoDaProtecaoTestBase
{
    private const string Justificativa = "A classificação declarada basta: o documento só comprova a matrícula.";

    public LeituraAntesDaGravacaoConcorrenteTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Lido o item com a manifestação ainda em curso, a decisão enviada depois dela é recusada por conflito, mesmo com justificativa")]
    [InlineData("TIPO_DOCUMENTO", "APROVAR", null)]
    [InlineData("FATO", "APROVAR", null)]
    [InlineData("TIPO_DOCUMENTO", "RECOMENDAR_AJUSTE", "SENSIVEL")]
    [InlineData("FATO", "RECOMENDAR_AJUSTE", "SENSIVEL")]
    public async Task ManifestacaoEmCursoNaLeitura_RecusaADecisao(string cadastro, string decisao, string? recomendada)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        LeituraDoCircuitoLida lida = await LerDuranteAsync(cadastro, id, Betina, () => ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA"));
        HttpResponseMessage resposta = await DecidirSobreALeituraAsync(
            client, new(cadastro, id, decisao, Classificacao(cadastro), recomendada, Justificativa), lida);

        await DeveSerConflitoSemNovaTentativaAsync(resposta, "manifestacao_nao_lida");
        (await DetalheAsync(client, cadastro, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
    }

    [Theory(DisplayName = "Lido o item com a decisão ainda em curso, a manifestação enviada depois dela é recusada por conflito")]
    [InlineData("TIPO_DOCUMENTO", "APROVAR", null)]
    [InlineData("FATO", "RECOMENDAR_AJUSTE", "SENSIVEL")]
    public async Task DecisaoEmCursoNaLeitura_RecusaAManifestacao(string cadastro, string decisao, string? recomendada)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        LeituraDoCircuitoLida lida = await LerDuranteAsync(
            cadastro, id, Kevin, () => DecidirComoAsync(client, Betina, cadastro, id, decisao, Justificativa, recomendada));
        HttpResponseMessage resposta = await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA", ciclo: lida.Ciclo);

        await DeveSerConflitoSemNovaTentativaAsync(resposta, "decisao_ja_tomada");
        (await HistoricoAsync(client, Kevin, cadastro, id)).Should().BeEmpty();
    }

    [Theory(DisplayName = "Controle: a manifestação gravada sem mudar a linha do item não recusaria a decisão lida antes dela")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task ManifestacaoForaDaLinhaDoItem_NaoRecusariaADecisao(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);
        LeituraDoCircuitoLida lida = await LerOCircuitoAsync(client, Betina, cadastro, id);

        await GravarForaDaLinhaDoItemAsync(cadastro, id, InserirManifestacao);
        HttpResponseMessage resposta = await DecidirSobreALeituraAsync(
            client, new(cadastro, id, "APROVAR", Classificacao(cadastro), Justificativa: Justificativa), lida);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "só a contagem gravada na linha do item faz a decisão perceber a manifestação");
    }

    [Theory(DisplayName = "Controle: o ato de decisão gravado sem mudar a linha do item não recusaria a manifestação lida antes dele")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task DecisaoForaDaLinhaDoItem_NaoRecusariaAManifestacao(string cadastro)
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await PrepararAsync(client, cadastro);

        await GravarForaDaLinhaDoItemAsync(cadastro, id, InserirAprovacao);
        HttpResponseMessage resposta = await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "só a situação gravada na linha do item faz a manifestação perceber a decisão");
    }

    private async Task<Guid> PrepararAsync(HttpClient client, string cadastro)
    {
        await PrepararTodosAsync();
        return await SolicitadoAsync(client, cadastro);
    }

    // A gravação entra na fila da linha do item; com ela em curso, quem lê vê o estado anterior; liberada a trava, ela é
    // confirmada. A leitura fica, assim, entre a abertura e a confirmação da gravação do outro.
    private async Task<LeituraDoCircuitoLida> LerDuranteAsync(string cadastro, Guid id, Persona quemLe, Func<Task<HttpResponseMessage>> gravacao)
    {
        using HttpClient leitor = Fixture.Factory.CreateClient();
        await using TravaDaLinhaDoItem trava = await TravaDaLinhaDoItem.TravarAsync(Fixture.Factory, cadastro, id);
        Task<HttpResponseMessage> emCurso = gravacao();
        await trava.EsperarNaFilaAsync(emCurso, 1);
        LeituraDoCircuitoLida lida = await LerOCircuitoAsync(leitor, quemLe, cadastro, id);
        await trava.LiberarAsync();
        using HttpResponseMessage confirmada = await emCurso;
        confirmada.StatusCode.Should().Be(HttpStatusCode.NoContent, await confirmada.Content.ReadAsStringAsync());
        return lida;
    }

    private async Task GravarForaDaLinhaDoItemAsync(string cadastro, Guid id, Func<TravaDaLinhaDoItem, string, Guid, Task<int>> gravar)
    {
        await using TravaDaLinhaDoItem trava = await TravaDaLinhaDoItem.TravarAsync(Fixture.Factory, cadastro, id);
        await gravar(trava, cadastro, id);
        await trava.LiberarAsync();
    }

    private static Task<int> InserirManifestacao(TravaDaLinhaDoItem trava, string cadastro, Guid id) =>
        trava.Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.protecao_do_catalogo_manifestacoes
                (id, cadastro, item_id, ciclo, parecer, texto, substitui_id, autor_emissor, autor_sub, autor_nome, instante)
            VALUES ({Guid.CreateVersion7()}, {cadastro}, {id}, 1, 'INADEQUADA', 'O dado é sensível.', NULL,
                    {TestAuthHandler.DefaultIssuer}, {Kevin.Sub}, {Kevin.Nome}, now())
            """);

    private static Task<int> InserirAprovacao(TravaDaLinhaDoItem trava, string cadastro, Guid id) =>
        trava.Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.protecao_do_catalogo_atos
                (id, cadastro, item_id, item_codigo, item_nome, ato, classificacao_protecao, hipotese_legal, finalidade_tratamento,
                 ciclo, classificacao_recomendada, justificativa, diante_de_manifestacao_desfavoravel, ator_emissor, ator_sub, ator_nome, instante)
            VALUES ({Guid.CreateVersion7()}, {cadastro}, {id}, 'CONTROLE', 'Controle', 'APROVACAO', {Classificacao(cadastro)},
                    'CUMPRIMENTO_OBRIGACAO_LEGAL', {Finalidade}, 1, NULL, NULL, false,
                    {TestAuthHandler.DefaultIssuer}, {Betina.Sub}, {Betina.Nome}, now())
            """);

    private static async Task DeveSerConflitoSemNovaTentativaAsync(HttpResponseMessage resposta, string codigo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, await resposta.Content.ReadAsStringAsync());
        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("code").GetString().Should().Be($"uniplus.configuracao.protecao_do_catalogo.{codigo}");
        problema.TryGetProperty("retryable", out _).Should().BeFalse();
    }
}
