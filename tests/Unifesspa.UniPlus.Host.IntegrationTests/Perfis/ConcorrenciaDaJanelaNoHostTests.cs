namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

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
/// aponta deadlock, e o que a operação seguinte lê é o que a anterior confirmou. A janela não toma a trava do papel de
/// Representante do controlador, porque nunca concede nem revoga o perfil dele (ADR-0162).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
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

        (Task<HttpResponseMessage> revogacao, Task<HttpResponseMessage> lote) = await EmFilaAsync(
            trava, () => RevogarAsync(administrador, cenario.Concessoes[Leandro], Leandro.Email), () => EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")]));

        using HttpResponseMessage respostaDaRevogacao = await revogacao;
        JsonElement janela = await CorpoAsync(await lote, HttpStatusCode.OK);
        SituacaoDe(janela, cenario.Concessoes[Betina]).Should().Be("aguardando_protecao_de_dados");
        janela.GetProperty("itens").EnumerateArray().Should().NotContain(i => i.GetProperty("concessaoId").GetGuid() == cenario.Concessoes[Leandro]);
        (await ContarLotesAsync(), await DeadlocksAsync()).Should().Be((1, deadlocks));
    }

    [Fact(DisplayName = "A etapa da proteção de dados que espera a trava e atravessa o prazo encontra a janela encerrada e não decide")]
    public async Task Etapa_AtravessaOPrazoNaEspera_EhRecusada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        SituacaoDe(await CorpoAsync(await Lote(gestao, cenario), HttpStatusCode.OK), concessao).Should().Be("aguardando_protecao_de_dados");
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(concessao);

        Task<HttpResponseMessage> etapa = EnviarAsync(protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        await trava.AguardarBloqueioAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await trava.ConfirmarAsync();

        await ConferirRecusaDaMigracaoAsync(await etapa, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
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
        Func<Task<HttpResponseMessage>> remocao = () => RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]);
        Func<Task<HttpResponseMessage>> lote = () => Lote(gestao, cenario);

        (Task<HttpResponseMessage> primeira, Task<HttpResponseMessage> segunda) = await EmFilaAsync(trava, loteAntes ? lote : remocao, loteAntes ? remocao : lote);

        (await Task.WhenAll(primeira, segunda)).Should().OnlyContain(static r => r.StatusCode == HttpStatusCode.OK);
        (await ContarLotesAsync(), await DeadlocksAsync()).Should().Be((1, deadlocks));
    }

    [Fact(DisplayName = "Na janela aberta, a concessão de quem é Representante do controlador aguarda a etapa de proteção de dados: a migração não tem dispensa")]
    public async Task Lote_BeneficiarioRepresentante_AguardaAProtecaoDeDados()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.RepresentanteDoControlador, Leandro.Email);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage resposta = await Lote(gestao, cenario);

        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        item.TryGetProperty("dispensa", out _).Should().BeFalse("a migração deixou de ter dispensa");
    }

    [Fact(DisplayName = "A janela não toma a trava do papel de Representante: o lote não espera quem segura o perfil dele")]
    public async Task Lote_NaoEsperaATravaDoPapelDoRepresentante()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste papel = await TravarPerfilAsync(PerfisDeSistema.RepresentanteDoControladorId);

        using HttpResponseMessage resposta = await Lote(gestao, cenario).WaitAsync(TimeSpan.FromSeconds(30));

        SituacaoDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]).Should().Be("aguardando_protecao_de_dados");
        await papel.ConfirmarAsync();
    }

    [Fact(DisplayName = "A segunda instância da rotina pula enquanto a primeira mantém a trava, e a execução seguinte assume")]
    public async Task Rotina_DuasInstancias_APrimeiraMantemATrava()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Relogio.Avancar(DepoisDoPrazo);

        int pulada = await ProcessarComATravaDaRotinaRetidaAsync();
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

        int comFalha = await ComGatilhoAsync(
            "janelas_de_migracao", "RAISE EXCEPTION 'falha de teste';", async () =>
            {
                int processadas = await ProcessarJanelasAsync();
                (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().BeNull();
                return processadas;
            });
        int seguinte = await ProcessarJanelasAsync();

        (comFalha, seguinte >= 1).Should().Be((0, true));
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
        Func<Task<HttpResponseMessage>> recusar = () => EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "recusar")], confirmacaoDeExclusao: true, chave: chave);

        // O gatilho descarta a revogação da concessão que o lote recusa: a gravação amarrada ao xmin não encontra a linha na versão lida.
        HttpStatusCode comConflito = await ComGatilhoAsync("concessoes", "RETURN NULL;", async () => (await recusar()).StatusCode);
        using HttpResponseMessage reenvio = await recusar();

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

    private static Task<HttpResponseMessage> RevogarAsync(HttpClient administrador, Guid concessaoId, string email) =>
        EnviarAsync(administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{concessaoId}/revogacao", new { motivo = "A pessoa deixou a função.", confirmacaoEmail = email });

    // As duas operações param na trava retida, na ordem pedida, e seguem quando a transação do teste confirma.
    private static async Task<(Task<HttpResponseMessage> Primeira, Task<HttpResponseMessage> Segunda)> EmFilaAsync(
        TransacaoRetidaDeTeste trava, Func<Task<HttpResponseMessage>> primeira, Func<Task<HttpResponseMessage>> segunda)
    {
        Task<HttpResponseMessage> antes = primeira();
        await trava.AguardarBloqueioAsync();
        Task<HttpResponseMessage> depois = segunda();
        await trava.AguardarBloqueioAsync(2);
        await trava.ConfirmarAsync();
        return (antes, depois);
    }

    // Roda a operação com um gatilho BEFORE UPDATE de teste na tabela, que executa o corpo pedido, e o remove depois.
    private async Task<T> ComGatilhoAsync<T>(string tabela, string corpo, Func<Task<T>> operacao)
    {
        await ExecutarSqlAsync($"CREATE FUNCTION autorizacao.gatilho_de_teste() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN {corpo} END $$");
        await ExecutarSqlAsync($"CREATE TRIGGER gatilho_de_teste BEFORE UPDATE ON autorizacao.{tabela} FOR EACH ROW EXECUTE FUNCTION autorizacao.gatilho_de_teste()");
        try
        {
            return await operacao();
        }
        finally
        {
            await ExecutarSqlAsync($"DROP TRIGGER IF EXISTS gatilho_de_teste ON autorizacao.{tabela}");
            await ExecutarSqlAsync("DROP FUNCTION IF EXISTS autorizacao.gatilho_de_teste()");
        }
    }

    private async Task<int> ProcessarComATravaDaRotinaRetidaAsync()
    {
        IAsyncDisposable? primeira = await ObterATravaDaRotinaAsync();
        primeira.Should().NotBeNull();
        try
        {
            return await ProcessarJanelasAsync();
        }
        finally
        {
            await primeira!.DisposeAsync();
        }
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
