namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A revisão vencida e ainda não materializada diante de quem não tem acesso: a tentativa recusada não grava o fim da
/// revisão, que só a operação de quem pode (ou a rotina) materializa.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisaoVencidaNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private const string FuncaoQueFalhaDepoisDosAvisos =
        """
        CREATE OR REPLACE FUNCTION autorizacao.falha_depois_dos_avisos_da_revisao() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (SELECT 1 FROM autorizacao.avisos_emitidos a JOIN autorizacao.revisoes_das_concessoes j ON j.id = a.fato_id
                       WHERE a.tipo = 'VERSAO_ENCERRADA' AND j.perfil_id = NEW.perfil_id) THEN
                RAISE EXCEPTION 'falha forçada depois dos avisos da materialização';
            END IF;
            RETURN NEW;
        END $$
        """;

    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public RevisaoVencidaNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A aprovação de pedido sem permissão sobre perfil com revisão vencida é recusada e não materializa a revisão")]
    public async Task AprovacaoSemPermissao_NaoMaterializaARevisao()
    {
        (Guid perfilId, Guid pedido) = await PedidoEmPerfilComRevisaoAsync();
        Relogio.Avancar(DepoisDoPrazo);
        using HttpClient semPermissao = Cliente(Leandro);

        using HttpResponseMessage resposta = await EnviarAsync(semPermissao, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await EncerradaEmAsync(perfilId)).Should().BeNull();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpResponseMessage comPermissao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");
        comPermissao.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await EncerradaEmAsync(perfilId)).Should().NotBeNull();
    }

    [Fact(DisplayName = "O pedido criado depois do prazo, com a rotina parada, materializa o fim da revisão e não esbarra nela")]
    public async Task Pedido_DepoisDoPrazo_SemARotina_NaoEsbarraNaRevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient betina = Cliente(Betina);
        Relogio.Avancar(DepoisDoPrazo);

        Guid pedido = await CriarAsync(betina, cenario.PerfilId, await SemearUnidadeAsync(), null);

        pedido.Should().NotBeEmpty();
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
        using HttpClient gestao = await GestaoAsync();
        SituacaoDe(await RevisaoAsync(gestao, cenario.PerfilId), cenario.Concessoes[Leandro]).Should().Be("NAO_MIGRADA");
    }

    [Fact(DisplayName = "A última pendência revogada com a rotina parada deixa a revisão de bloquear o pedido, e a operação seguinte a encerra")]
    public async Task UltimaPendenciaRevogada_SemARotina_LiberaOPedido()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        using HttpResponseMessage revogacao = await RevogarAsync(administrador, cenario.Concessoes[Leandro], MotivoDaRevogacao, Leandro.Email);
        revogacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EncerradaEmAsync(cenario.PerfilId)).Should().BeNull();
        using HttpClient betina = Cliente(Betina);

        Guid pedido = await CriarAsync(betina, cenario.PerfilId, await SemearUnidadeAsync(), null);

        pedido.Should().NotBeEmpty();
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
    }

    [Fact(DisplayName = "O envio depois do encerramento antecipado grava o fim, é recusado e responde igual no replay")]
    public async Task Envio_DepoisDoEncerramentoAntecipado_ReplayIgual()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        (await RevogarAsync(administrador, cenario.Concessoes[Leandro], MotivoDaRevogacao, Leandro.Email)).Dispose();
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")], chave: chave);
        using HttpResponseMessage replay = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")], chave: chave);

        await ConferirRecusaDaMigracaoAsync(primeira, HttpStatusCode.UnprocessableEntity, "revisao_encerrada");
        await ConferirRecusaDaMigracaoAsync(replay, HttpStatusCode.UnprocessableEntity, "revisao_encerrada");
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
        (await RevisaoAsync(gestao, cenario.PerfilId)).GetProperty("forma").GetProperty("codigo").GetString().Should().Be("CONCLUIDA");
    }

    [Fact(DisplayName = "O replay do envio, da etapa e do encerramento devolve a mesma resposta sem repetir a transição")]
    public async Task Replay_DoEnvioDaEtapaEDoEncerramento_NaoRepeteATransicao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        object[] envio = [Decisao(cenario.Concessoes[Leandro], "MIGRAR"), Decisao(cenario.Concessoes[Betina], "MIGRAR")];
        object encerramento = new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true };

        await ConferirReplayIgualAsync(chave => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, envio, chave: chave));
        await ConferirReplayIgualAsync(chave => EtapaDaProtecaoAsync(protecao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")], chave));
        await ConferirReplayIgualAsync(chave => EnviarAsync(gestao, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, "/encerramento"), encerramento, chave));

        (await ContarAsync("SELECT count(*)::int AS \"Value\" FROM autorizacao.envios_de_decisoes")).Should().Be(2);
    }

    [Fact(DisplayName = "A etapa da proteção de dados com itens válidos e inválidos não grava nada")]
    public async Task Etapa_ComItensValidosEInvalidos_NaoGravaNada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        (await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR"), Decisao(cenario.Concessoes[Betina], "MIGRAR")])).Dispose();

        using HttpResponseMessage etapa = await EtapaDaProtecaoAsync(
            protecao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR"), Decisao(Guid.CreateVersion7(), "MIGRAR")]);

        etapa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await etapa.Content.ReadAsStringAsync());
        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);
        SituacaoDe(revisao, cenario.Concessoes[Leandro]).Should().Be("AGUARDANDO_PROTECAO_DE_DADOS");
        SituacaoDe(revisao, cenario.Concessoes[Betina]).Should().Be("AGUARDANDO_PROTECAO_DE_DADOS");
        (await ContarAsync("SELECT count(*)::int AS \"Value\" FROM autorizacao.envios_de_decisoes")).Should().Be(1);
    }

    [Fact(DisplayName = "A leitura da concessão não migrada mostra a revogação pela revisão, sem pessoa inventada")]
    public async Task Leitura_DaConcessaoNaoMigrada_MostraARevogacaoPelaRevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        Relogio.Avancar(DepoisDoPrazo);
        await ProcessarRevisoesAsync();

        JsonElement concessao = await LerAsync(administrador, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}");

        concessao.GetProperty("ativa").GetBoolean().Should().BeFalse();
        concessao.GetProperty("revogadaPor").ValueKind.Should().Be(JsonValueKind.Null);
        concessao.GetProperty("revogadaPelaRevisaoId").GetGuid().Should().NotBeEmpty();
        concessao.GetProperty("revogadaEm").GetDateTimeOffset().Should().Be(
            (await RevisaoAsync(await GestaoAsync(), cenario.PerfilId)).GetProperty("terminaEm").GetDateTimeOffset());
    }

    [Fact(DisplayName = "A falha da troca depois dos avisos da materialização desfaz o fim, as revogações, os registros e os envelopes juntos")]
    public async Task MaterializacaoDentroDaTroca_FalhaDepoisDosAvisos_DesfazTudo()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestor = await GestorAsync();

        (HttpStatusCode status, string[] envelopes) = await ComFalhaDepoisDosAvisosAsync(() => RemoverComEnvelopesAsync(gestor, cenario.PerfilId));

        envelopes.Should().BeEmpty("os envelopes preparados na troca voltaram com ela");
        status.Should().Be(HttpStatusCode.InternalServerError, "a falha só dispara depois de o aviso da versão encerrada estar registrado");
        (await EncerradaEmAsync(cenario.PerfilId)).Should().BeNull("o fim da revisão voltou com a troca");
        (await RevogadasPelaRevisaoAsync(cenario.PerfilId)).Should().Be(0);
        AvisosDaMaterializacao avisos = await AvisosDaMaterializacaoAsync(cenario.PerfilId);
        avisos.Revogadas.Should().BeEmpty();
        avisos.Encerradas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A revisão que vence enquanto a remoção espera a trava é materializada na própria remoção, com os registros e os envelopes dos avisos de revogação e de encerramento")]
    public async Task MaterializacaoDentroDaTroca_ConcluiNaMesmaOperacao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestor = await GestorAsync();

        (HttpStatusCode status, string[] envelopes) = await RemoverComEnvelopesAsync(gestor, cenario.PerfilId);

        status.Should().Be(HttpStatusCode.OK);
        await ConferirAMaterializacaoAsync(cenario.PerfilId, envelopes);
    }

    [Fact(DisplayName = "O command de materialização prévia, enviado antes da operação, grava o fim, a revogação, os registros e os envelopes dos avisos")]
    public async Task MaterializacaoPrevia_GravaOFimEOsAvisos()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient betina = Cliente(Betina);
        Guid unidade = await SemearUnidadeAsync();
        Relogio.Avancar(DepoisDoPrazo);

        string[] envelopes = await EnvelopesDeixadosPorAsync(async () => (await CriarAsync(betina, cenario.PerfilId, unidade, null)).Should().NotBeEmpty());

        await ConferirAMaterializacaoAsync(cenario.PerfilId, envelopes);
    }

    // O pedido nasce antes da revisão, que a revisão aberta o impediria; a troca de composição abre a revisão como a produção.
    private async Task<(Guid PerfilId, Guid Pedido)> PedidoEmPerfilComRevisaoAsync()
    {
        await ResetarAsync();
        Guid perfilId = await SemearPerfilAsync("Perfil com revisão", Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, perfilId, await SemearUnidadeAsync(), null);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.ContaId.ToString(), perfilId, EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()), email: Leandro.Email);
        await TrocarComposicaoAsync(perfilId, Consultar, Sensivel);

        return (perfilId, pedido);
    }

    // A revisão ainda vale quando a remoção sai; vence enquanto ela espera a trava do perfil, de modo que o fim é materializado dentro da troca.
    private async Task<HttpResponseMessage> RemoverVencendoARevisaoNaEsperaAsync(HttpClient gestor, Guid perfilId)
    {
        await using TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);
        Task<HttpResponseMessage> remocao = RemoverBrutoAsync(gestor, perfilId, [Sensivel]);
        await trava.AguardarBloqueioAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await trava.ConfirmarAsync();

        return await remocao;
    }

    // A remoção que vence a revisão na espera da trava, com os envelopes que ela deixou no outbox.
    private async Task<(HttpStatusCode Status, string[] Envelopes)> RemoverComEnvelopesAsync(HttpClient gestor, Guid perfilId)
    {
        HttpStatusCode status = default;
        string[] envelopes = await EnvelopesDeixadosPorAsync(async () =>
        {
            using HttpResponseMessage resposta = await RemoverVencendoARevisaoNaEsperaAsync(gestor, perfilId);
            status = resposta.StatusCode;
        });

        return (status, envelopes);
    }

    // Faz a gravação da vigência falhar depois de o aviso da versão encerrada estar registrado, só durante a operação.
    private async Task<T> ComFalhaDepoisDosAvisosAsync<T>(Func<Task<T>> operacao)
    {
        await ExecutarSqlAsync(FuncaoQueFalhaDepoisDosAvisos);
        await ExecutarSqlAsync("CREATE TRIGGER tg_teste_falha BEFORE INSERT OR UPDATE ON autorizacao.vigencias_do_perfil FOR EACH ROW EXECUTE FUNCTION autorizacao.falha_depois_dos_avisos_da_revisao()");
        try
        {
            return await operacao();
        }
        finally
        {
            await ExecutarSqlAsync("DROP TRIGGER IF EXISTS tg_teste_falha ON autorizacao.vigencias_do_perfil");
            await ExecutarSqlAsync("DROP FUNCTION IF EXISTS autorizacao.falha_depois_dos_avisos_da_revisao()");
        }
    }

    private static Task<HttpResponseMessage> EtapaDaProtecaoAsync(HttpClient protecao, Guid perfilId, object[] decisoes, string? chave = null) =>
        EnviarAsync(protecao, HttpMethod.Post, RotaDaRevisao(perfilId, "/protecao-de-dados"), new { decisoes }, chave);

    // O replay com a mesma chave devolve a mesma resposta, status e corpo.
    private static async Task ConferirReplayIgualAsync(Func<string, Task<HttpResponseMessage>> chamada)
    {
        string chave = Guid.NewGuid().ToString();
        string primeira = await TextoAsync(await chamada(chave));

        (await TextoAsync(await chamada(chave))).Should().Be(primeira);
    }

    private static async Task<string> TextoAsync(HttpResponseMessage resposta)
    {
        using (resposta)
        {
            return $"{(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}";
        }
    }

    private async Task ConferirAMaterializacaoAsync(Guid perfilId, string[] envelopes)
    {
        (await EncerradaEmAsync(perfilId)).Should().NotBeNull();
        (await RevogadasPelaRevisaoAsync(perfilId)).Should().Be(1);
        AvisosDaMaterializacao avisos = await AvisosDaMaterializacaoAsync(perfilId);
        avisos.Revogadas.Should().ContainSingle("uma concessão revogada pelo fim, a de Leandro");
        avisos.Encerradas.Should().NotBeEmpty("a gestão e a proteção de dados são avisadas do encerramento");
        foreach (Guid mensagem in avisos.Revogadas.Concat(avisos.Encerradas))
        {
            envelopes.Count(e => e.Contains(mensagem.ToString(), StringComparison.OrdinalIgnoreCase)).Should().Be(1, $"um envelope para o aviso {mensagem}");
        }
    }

    // Os registros dos avisos do fim da revisão: a revogação de cada concessão tem por fato a linha da migração, e o encerramento tem a revisão.
    private async Task<AvisosDaMaterializacao> AvisosDaMaterializacaoAsync(Guid perfilId) =>
        new(await MensagensDasRevogacoesAsync(perfilId), await MensagensDosEncerramentosAsync(perfilId));

    private Task<List<Guid>> MensagensDasRevogacoesAsync(Guid perfilId) =>
        MensagensAsync(
            $"""
            SELECT a.mensagem_id AS "Value" FROM autorizacao.avisos_emitidos a
            JOIN autorizacao.migracoes_da_concessao m ON m.id = a.fato_id
            JOIN autorizacao.revisoes_das_concessoes j ON j.id = m.revisao_id
            WHERE a.tipo = 'CONCESSAO_REVOGADA' AND j.perfil_id = {perfilId}
            """);

    private Task<List<Guid>> MensagensDosEncerramentosAsync(Guid perfilId) =>
        MensagensAsync(
            $"""
            SELECT a.mensagem_id AS "Value" FROM autorizacao.avisos_emitidos a
            JOIN autorizacao.revisoes_das_concessoes j ON j.id = a.fato_id
            WHERE a.tipo = 'VERSAO_ENCERRADA' AND j.perfil_id = {perfilId}
            """);

    private async Task<List<Guid>> MensagensAsync(FormattableString sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQuery<Guid>(sql).ToListAsync();
    }

    private sealed record AvisosDaMaterializacao(IReadOnlyList<Guid> Revogadas, IReadOnlyList<Guid> Encerradas);

    private Task<int> RevogadasPelaRevisaoAsync(Guid perfilId) =>
        ContarAsync($"SELECT count(*)::int AS \"Value\" FROM autorizacao.concessoes WHERE perfil_id = '{perfilId}' AND revogada_pela_revisao_id IS NOT NULL");

    private async Task<int> ContarAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQueryRaw<int>(sql).SingleAsync();
    }

    private async Task<DateTimeOffset?> EncerradaEmAsync(Guid perfilId)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<DateTimeOffset?>($"SELECT encerrada_em AS \"Value\" FROM autorizacao.revisoes_das_concessoes WHERE perfil_id = {perfilId}")
            .SingleAsync();
    }
}
