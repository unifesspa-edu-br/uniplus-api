namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A ordem das travas da janela de migração com operações concorrentes de verdade: a transação do teste segura a trava,
/// o banco registra cada operação parada nela e a liberação decide quem segue primeiro. Em nenhuma das ordens o banco
/// aponta deadlock, e o que a operação seguinte lê é o que a anterior confirmou.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConcorrenciaDaJanelaNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public ConcorrenciaDaJanelaNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "O lote que espera a revogação manual de uma concessão a encontra revogada e a ignora, decidindo só as demais")]
    public async Task Lote_ContraRevogacaoManual_EncontraARevogada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(cenario.Concessoes[Leandro]);

        Task<HttpResponseMessage> revogacao = EnviarAsync(
            administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}/revogacao",
            new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email });
        await trava.AguardarBloqueioAsync();
        Task<HttpResponseMessage> lote = EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")]);
        await trava.AguardarBloqueioAsync(2);
        await trava.ConfirmarAsync();

        using HttpResponseMessage respostaDaRevogacao = await revogacao;
        using HttpResponseMessage respostaDoLote = await lote;
        JsonElement janela = await CorpoAsync(respostaDoLote, HttpStatusCode.OK);
        SituacaoDe(janela, cenario.Concessoes[Betina]).Should().Be("aguardando_protecao_de_dados");
        janela.GetProperty("itens").EnumerateArray().Should().NotContain(i => i.GetProperty("concessaoId").GetGuid() == cenario.Concessoes[Leandro]);
        (await ContarLotesAsync()).Should().Be(1);
        (await DeadlocksAsync()).Should().Be(deadlocks);
    }

    [Fact(DisplayName = "A etapa da proteção de dados que espera a trava e atravessa o prazo encontra a janela encerrada e não decide")]
    public async Task Etapa_AtravessaOPrazoNaEspera_EhRecusada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(concessao, "migrar")]);
        SituacaoDe(await CorpoAsync(lote, HttpStatusCode.OK), concessao).Should().Be("aguardando_protecao_de_dados");
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(concessao);

        Task<HttpResponseMessage> etapa = EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        await trava.AguardarBloqueioAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta = await etapa;
        await ConferirRecusaDaMigracaoAsync(resposta, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        SituacaoDe(await JanelaAsync(gestao, cenario.PerfilId), concessao).Should().Be("nao_migrada");
    }

    [Theory(DisplayName = "A remoção na janela e o lote do mesmo perfil se resolvem por quem trava primeiro, nas duas ordens, sem deadlock")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remocao_ContraLote_NasDuasOrdens(bool loteAntes)
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient gestor = await GestorAsync();
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarPerfilAsync(cenario.PerfilId);

        Task<HttpResponseMessage> primeira = loteAntes ? Lote(gestao, cenario) : RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]);
        await trava.AguardarBloqueioAsync();
        Task<HttpResponseMessage> segunda = loteAntes ? RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]) : Lote(gestao, cenario);
        await trava.AguardarBloqueioAsync(2);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta1 = await primeira;
        using HttpResponseMessage resposta2 = await segunda;
        resposta1.StatusCode.Should().Be(HttpStatusCode.OK, await resposta1.Content.ReadAsStringAsync());
        resposta2.StatusCode.Should().Be(HttpStatusCode.OK, await resposta2.Content.ReadAsStringAsync());
        (await ContarLotesAsync()).Should().Be(1);
        (await DeadlocksAsync()).Should().Be(deadlocks);
    }

    [Fact(DisplayName = "A concessão da proteção de dados criada antes do lote vale como dispensa da etapa")]
    public async Task GovernancaCriada_AntesDoLote_DispensaAProtecaoDeDados()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste criacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await ConcederNaTransacaoAsync(criacao, Leandro);

        Task<HttpResponseMessage> lote = Lote(gestao, cenario);
        await criacao.AguardarBloqueioAsync();
        await criacao.ConfirmarAsync();

        using HttpResponseMessage resposta = await lote;
        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("situacao").GetString().Should().Be("migrada");
        item.GetProperty("dispensa").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact(DisplayName = "A concessão da proteção de dados revogada antes do lote tira a dispensa e a etapa volta a ser exigida")]
    public async Task GovernancaRevogada_AntesDoLote_ExigeAProtecaoDeDados()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.ProtecaoDeDados, Leandro.Email);
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste revogacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await revogacao.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR SHARE", PerfisDeSistema.ProtecaoDeDadosId);
        await revogacao.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "UPDATE autorizacao.concessoes SET revogada_em = now(), revogada_por_emissor = 'e', revogada_por_sub = 's', motivo_revogacao = 'teste' "
            + "WHERE perfil_id = {0} AND beneficiario_sub = {1}",
            PerfisDeSistema.ProtecaoDeDadosId,
            Leandro.Sub.ToString());

        Task<HttpResponseMessage> lote = Lote(gestao, cenario);
        await revogacao.AguardarBloqueioAsync();
        await revogacao.ConfirmarAsync();

        using HttpResponseMessage resposta = await lote;
        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        item.GetProperty("dispensa").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "O lote que trava primeiro não vê a concessão da proteção de dados criada depois, que espera a trava do perfil")]
    public async Task Lote_TravaPrimeiro_NaoVeAGovernancaCriadaDepois()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(cenario.Concessoes[Leandro]);

        Task<HttpResponseMessage> lote = Lote(gestao, cenario);
        await trava.AguardarBloqueioAsync();
        Task<Guid> criacao = ConcessoesDeTeste.ConcederGovernancaAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.ProtecaoDeDados, Leandro.Email);
        await trava.AguardarBloqueioAsync(2);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta = await lote;
        await criacao;
        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        (await DeadlocksAsync()).Should().Be(deadlocks);
    }

    [Fact(DisplayName = "O lote que trava primeiro mantém a dispensa mesmo com a concessão da proteção de dados revogada depois")]
    public async Task Lote_TravaPrimeiro_MantemADispensaDaGovernanciaRevogadaDepois()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid governanca = await ConcessoesDeTeste.ConcederGovernancaAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.ProtecaoDeDados, Leandro.Email);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(cenario.Concessoes[Leandro]);

        Task<HttpResponseMessage> lote = Lote(gestao, cenario);
        await trava.AguardarBloqueioAsync();
        Task<HttpResponseMessage> revogacao = EnviarAsync(
            administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{governanca}/revogacao",
            new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email });
        await trava.AguardarBloqueioAsync(2);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta = await lote;
        using HttpResponseMessage respostaDaRevogacao = await revogacao;
        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("dispensa").ValueKind.Should().Be(JsonValueKind.String);
        respostaDaRevogacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await respostaDaRevogacao.Content.ReadAsStringAsync());
        (await DeadlocksAsync()).Should().Be(deadlocks);
    }

    [Fact(DisplayName = "A segunda instância da rotina pula enquanto a primeira mantém a trava, e a execução seguinte assume")]
    public async Task Rotina_DuasInstancias_APrimeiraMantemATrava()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Relogio.Avancar(DepoisDoPrazo);
        IAsyncDisposable? primeira = await ObterATravaDaRotinaAsync();
        int pulada;
        try
        {
            primeira.Should().NotBeNull();
            pulada = await ProcessarJanelasAsync();
        }
        finally
        {
            await primeira!.DisposeAsync();
        }

        int seguinte = await ProcessarJanelasAsync();

        pulada.Should().Be(0);
        seguinte.Should().BeGreaterThanOrEqualTo(1);
        using HttpClient gestao = await GestaoAsync();
        SituacaoDe(await JanelaAsync(gestao, cenario.PerfilId), cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "Depois de uma falha na gravação da rotina a execução seguinte assume e conclui")]
    public async Task Rotina_DepoisDeFalha_ASeguinteAssume()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(25));
        await ExecutarSqlAsync(
            "CREATE FUNCTION autorizacao.falha_de_teste() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'falha de teste'; END $$");
        await ExecutarSqlAsync(
            "CREATE TRIGGER falha_de_teste BEFORE UPDATE ON autorizacao.janelas_de_migracao FOR EACH ROW EXECUTE FUNCTION autorizacao.falha_de_teste()");
        int comFalha;
        try
        {
            comFalha = await ProcessarJanelasAsync();
            (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().BeNull();
        }
        finally
        {
            await ExecutarSqlAsync("DROP TRIGGER IF EXISTS falha_de_teste ON autorizacao.janelas_de_migracao");
            await ExecutarSqlAsync("DROP FUNCTION IF EXISTS autorizacao.falha_de_teste()");
        }

        int seguinte = await ProcessarJanelasAsync();

        comFalha.Should().Be(0);
        seguinte.Should().BeGreaterThanOrEqualTo(1);
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().NotBeNull();
    }

    [Fact(DisplayName = "A rotina atrasada, já dentro das últimas 24 horas, manda só o aviso de 24 horas e marca os dois")]
    public async Task Rotina_Atrasada_MandaSoOAvisoDe24Horas()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(50));

        await ProcessarJanelasAsync();
        int segunda = await ProcessarJanelasAsync();

        (await AvisoGravadoEmAsync(cenario.PerfilId, false)).Should().NotBeNull();
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().NotBeNull();
        segunda.Should().Be(0);
    }

    [Fact(DisplayName = "A gravação do lote que perde a corrida de concorrência responde 409 e deixa a chave de idempotência livre")]
    public async Task Lote_ConflitoNaGravacao_DeixaAChaveLivre()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string chave = Guid.NewGuid().ToString();
        // O gatilho descarta a revogação da concessão que o lote recusa: a gravação amarrada ao xmin não encontra a linha na versão lida.
        await ExecutarSqlAsync(
            "CREATE FUNCTION autorizacao.descarta_de_teste() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
        await ExecutarSqlAsync(
            "CREATE TRIGGER descarta_de_teste BEFORE UPDATE ON autorizacao.concessoes FOR EACH ROW EXECUTE FUNCTION autorizacao.descarta_de_teste()");
        HttpStatusCode comConflito;
        try
        {
            using HttpResponseMessage conflito = await EnviarLoteAsync(
                gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "recusar")], confirmacaoDeExclusao: true, chave: chave);
            comConflito = conflito.StatusCode;
        }
        finally
        {
            await ExecutarSqlAsync("DROP TRIGGER IF EXISTS descarta_de_teste ON autorizacao.concessoes");
            await ExecutarSqlAsync("DROP FUNCTION IF EXISTS autorizacao.descarta_de_teste()");
        }

        using HttpResponseMessage reenvio = await EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "recusar")], confirmacaoDeExclusao: true, chave: chave);

        comConflito.Should().Be(HttpStatusCode.Conflict);
        reenvio.StatusCode.Should().Be(HttpStatusCode.OK, await reenvio.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> Lote(HttpClient gestao, JanelaAberta cenario) =>
        EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);

    private async Task<TransacaoRetidaDeTeste> TravarConcessaoAsync(Guid concessaoId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE", concessaoId);

        return trava;
    }

    private async Task<TransacaoRetidaDeTeste> TravarPerfilAsync(Guid perfilId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);

        return trava;
    }

    // A criação da concessão da proteção de dados como a produção a faz: trava o perfil de governança em modo
    // compartilhado, e a gravação só vale quando a transação do teste confirma.
    private static async Task ConcederNaTransacaoAsync(TransacaoRetidaDeTeste transacao, Persona pessoa)
    {
        IConcessaoRepository concessoes = transacao.Servicos.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await concessoes.ObterParaConcederAsync(PerfisDeSistema.ProtecaoDeDadosId))!;
        Result<Concessao> concessao = Concessao.Conceder(
            new Identidade(TestAuthHandler.DefaultIssuer, pessoa.Sub.ToString()),
            perfil,
            EscopoDaConcessao.DaInstituicao(),
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            new Identidade(TestAuthHandler.DefaultIssuer, "concessao.de.teste"),
            transacao.Servicos.GetRequiredService<TimeProvider>().GetUtcNow(),
            transacao.Servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>(),
            beneficiarioEmail: pessoa.Email);
        await concessoes.AdicionarAsync(concessao.Value!);
        await transacao.Servicos.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    private async Task<IAsyncDisposable?> ObterATravaDaRotinaAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider
            .GetRequiredService<Unifesspa.UniPlus.Autorizacao.Application.Abstractions.ITravaDaRotina>()
            .TentarObterAsync("autorizacao.janelas_de_migracao", CancellationToken.None);
    }

    private async Task<long> DeadlocksAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<long>($"SELECT deadlocks AS \"Value\" FROM pg_stat_database WHERE datname = current_database()")
            .SingleAsync();
    }

    private async Task<int> ContarLotesAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.lotes_da_migracao")
            .SingleAsync();
    }
}
