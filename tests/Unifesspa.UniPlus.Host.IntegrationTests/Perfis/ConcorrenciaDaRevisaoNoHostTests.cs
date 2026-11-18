namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A ordem das travas da revisão das concessões com operações concorrentes de verdade: a transação do teste segura a trava,
/// o banco registra cada operação parada nela e a liberação decide quem segue primeiro. Em nenhuma das ordens o banco
/// aponta deadlock, e o que a operação seguinte lê é o que a anterior confirmou. A revisão não toma a trava do papel de
/// Representante do controlador, porque nunca concede nem revoga o perfil dele (ADR-0162).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDaRevisaoNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public ConcorrenciaDaRevisaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "O envio que espera a revogação manual de uma concessão a encontra revogada e a ignora, decidindo só as demais")]
    public async Task Envio_ContraRevogacaoManual_EncontraARevogada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(cenario.Concessoes[Leandro]);

        (Task<HttpResponseMessage> revogacao, Task<HttpResponseMessage> envio) = await EmFilaAsync(
            trava, () => RevogarAsync(administrador, cenario.Concessoes[Leandro], MotivoDaRevogacao, Leandro.Email), () => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")]));

        using HttpResponseMessage respostaDaRevogacao = await revogacao;
        JsonElement revisao = await CorpoAsync(await envio, HttpStatusCode.OK);
        SituacaoDe(revisao, cenario.Concessoes[Betina]).Should().Be("aguardando_protecao_de_dados");
        revisao.GetProperty("itens").EnumerateArray().Should().NotContain(i => i.GetProperty("concessaoId").GetGuid() == cenario.Concessoes[Leandro]);
        (await ContarEnviosAsync(), await DeadlocksAsync()).Should().Be((1, deadlocks));
    }

    [Fact(DisplayName = "A etapa da proteção de dados que espera a trava e atravessa o prazo encontra a revisão encerrada e não decide")]
    public async Task Etapa_AtravessaOPrazoNaEspera_EhRecusada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        SituacaoDe(await CorpoAsync(await EnviarDecisoes(gestao, cenario), HttpStatusCode.OK), concessao).Should().Be("aguardando_protecao_de_dados");
        await using TransacaoRetidaDeTeste trava = await TravarConcessaoAsync(concessao);

        Task<HttpResponseMessage> etapa = EnviarAsync(protecao, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        await trava.AguardarBloqueioAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await trava.ConfirmarAsync();

        await ConferirRecusaDaMigracaoAsync(await etapa, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        SituacaoDe(await RevisaoAsync(gestao, cenario.PerfilId), concessao).Should().Be("nao_migrada");
    }

    [Theory(DisplayName = "A remoção na revisão e o envio do mesmo perfil se resolvem por quem trava primeiro, nas duas ordens, sem deadlock")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remocao_ContraEnvio_NasDuasOrdens(bool envioAntes)
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient gestor = await GestorAsync();
        long deadlocks = await DeadlocksAsync();
        await using TransacaoRetidaDeTeste trava = await TravarPerfilAsync(cenario.PerfilId);
        Func<Task<HttpResponseMessage>> remocao = () => RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]);
        Func<Task<HttpResponseMessage>> envio = () => EnviarDecisoes(gestao, cenario);

        (Task<HttpResponseMessage> primeira, Task<HttpResponseMessage> segunda) = await EmFilaAsync(trava, envioAntes ? envio : remocao, envioAntes ? remocao : envio);

        (await Task.WhenAll(primeira, segunda)).Should().OnlyContain(static r => r.StatusCode == HttpStatusCode.OK);
        (await ContarEnviosAsync(), await DeadlocksAsync()).Should().Be((1, deadlocks));
    }

    [Fact(DisplayName = "Na revisão aberta, a concessão de quem é Representante do controlador aguarda a etapa de proteção de dados: a migração não tem dispensa")]
    public async Task Envio_BeneficiarioRepresentante_AguardaAProtecaoDeDados()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.ContaId.ToString(), TipoDeGovernanca.RepresentanteDoControlador, Leandro.Email);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage resposta = await EnviarDecisoes(gestao, cenario);

        JsonElement item = ItemDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]);
        item.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        item.TryGetProperty("dispensa", out _).Should().BeFalse("a migração deixou de ter dispensa");
    }

    [Fact(DisplayName = "A revisão não toma a trava do papel de Representante: o envio não espera quem segura o perfil dele")]
    public async Task Envio_NaoEsperaATravaDoPapelDoRepresentante()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste papel = await TravarPerfilAsync(PerfisDeSistema.RepresentanteDoControladorId);

        using HttpResponseMessage resposta = await EnviarDecisoes(gestao, cenario).WaitAsync(TimeSpan.FromSeconds(30));

        SituacaoDe(await CorpoAsync(resposta, HttpStatusCode.OK), cenario.Concessoes[Leandro]).Should().Be("aguardando_protecao_de_dados");
        await papel.ConfirmarAsync();
    }

    [Fact(DisplayName = "A segunda instância da rotina pula enquanto a primeira executa, e a execução seguinte assume")]
    public async Task Rotina_DuasInstancias_APrimeiraExecutaSozinha()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Relogio.Avancar(DepoisDoPrazo);

        int pulada = await ProcessarComAExecucaoUnicaDaRotinaRetidaAsync();
        int seguinte = await ProcessarRevisoesAsync();

        pulada.Should().Be(0);
        seguinte.Should().BeGreaterThanOrEqualTo(1);
        using HttpClient gestao = await GestaoAsync();
        SituacaoDe(await RevisaoAsync(gestao, cenario.PerfilId), cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "Depois de uma falha na gravação da rotina a execução seguinte assume e conclui")]
    public async Task Rotina_DepoisDeFalha_ASeguinteAssume()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(25));

        int comFalha = await ComGatilhoAsync(
            "revisoes_das_concessoes", "RAISE EXCEPTION 'falha de teste';", async () =>
            {
                int processadas = await ProcessarRevisoesAsync();
                (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().BeNull();
                return processadas;
            });
        int seguinte = await ProcessarRevisoesAsync();

        (comFalha, seguinte >= 1).Should().Be((0, true));
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().NotBeNull();
    }

    [Fact(DisplayName = "A rotina atrasada, já dentro das últimas 24 horas, manda só o aviso de 24 horas e marca os dois")]
    public async Task Rotina_Atrasada_MandaSoOAvisoDe24Horas()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(50));

        await ProcessarRevisoesAsync();
        int segunda = await ProcessarRevisoesAsync();

        (await AvisoGravadoEmAsync(cenario.PerfilId, false)).Should().NotBeNull();
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().NotBeNull();
        segunda.Should().Be(0);
    }

    [Fact(DisplayName = "A gravação do envio que perde a corrida de concorrência responde 409 e deixa a chave de idempotência livre")]
    public async Task Envio_ConflitoNaGravacao_DeixaAChaveLivre()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string chave = Guid.NewGuid().ToString();
        Func<Task<HttpResponseMessage>> recusar = () => EnviarDecisoesDaGestaoAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "recusar")], confirmacaoDeExclusao: true, chave: chave);

        // O gatilho descarta a revogação da concessão que o envio recusa: a gravação amarrada ao xmin não encontra a linha na versão lida.
        HttpStatusCode comConflito = await ComGatilhoAsync("concessoes", "RETURN NULL;", async () => (await recusar()).StatusCode);
        using HttpResponseMessage reenvio = await recusar();

        comConflito.Should().Be(HttpStatusCode.Conflict);
        reenvio.StatusCode.Should().Be(HttpStatusCode.OK, await reenvio.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> EnviarDecisoes(HttpClient gestao, RevisaoAberta cenario) =>
        EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);

    private async Task<TransacaoRetidaDeTeste> TravarConcessaoAsync(Guid concessaoId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE", concessaoId);

        return trava;
    }

    private async Task<TransacaoRetidaDeTeste> TravarPerfilAsync(Guid perfilId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);

        return trava;
    }

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

    private async Task<int> ProcessarComAExecucaoUnicaDaRotinaRetidaAsync()
    {
        IAsyncDisposable? primeira = await IniciarAExecucaoUnicaDaRotinaAsync();
        primeira.Should().NotBeNull();
        try
        {
            return await ProcessarRevisoesAsync();
        }
        finally
        {
            await primeira!.DisposeAsync();
        }
    }

    private async Task<IAsyncDisposable?> IniciarAExecucaoUnicaDaRotinaAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider
            .GetRequiredService<Unifesspa.UniPlus.Autorizacao.Application.Abstractions.IExecucaoUnicaDaRotina>()
            .TentarIniciarAsync("autorizacao.revisoes_das_concessoes", CancellationToken.None);
    }

    private async Task<long> DeadlocksAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<long>($"SELECT deadlocks AS \"Value\" FROM pg_stat_database WHERE datname = current_database()")
            .SingleAsync();
    }

    private async Task<int> ContarEnviosAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.envios_de_decisoes")
            .SingleAsync();
    }
}
