namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;
using Wolverine.Transports;

/// <summary>
/// Montagens comuns às suítes da revisão das concessões pelo host real, com o relógio controlável: o perfil em vigor, as
/// concessões dele, a versão que acrescenta permissão e a leitura e as decisões da revisão.
/// </summary>
public abstract class RevisaoDasConcessoesNoHostTestBase : AprovacaoDaVersaoNoHostTestBase, IAsyncLifetime, IDisposable, IClassFixture<HostDaRevisaoDasConcessoes>
{
    private protected const string RotaDeRevisao = "revisao-das-concessoes";

    // A confirmação que o Wolverine devolve a quem o invocou por outro nó: o recebimento dela acontece no outro host, que o rastreio não vê.
    private static readonly Type ConfirmacaoDeInvocacaoRemota =
        typeof(IMessageBus).Assembly.GetType("Wolverine.Runtime.RemoteInvocation.Acknowledgement", throwOnError: true)!;

    private static readonly Uri EnderecoDaFilaDeEmail = new("postgresql://email_envio/");

    private readonly HostDaRevisaoDasConcessoes _hostDaClasse;

    private protected RevisaoDasConcessoesNoHostTestBase(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture)
    {
        _hostDaClasse = hostDaClasse;
    }

    private protected RelogioAvancavelDeTeste Relogio => _hostDaClasse.Relogio;

    private WebApplicationFactory<HostAssemblyMarker> Host => _hostDaClasse.Obter(Fixture);

    public Task InitializeAsync()
    {
        Relogio.Reiniciar();

        return ResetarAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => GC.SuppressFinalize(this);

    private protected override HttpClient NovoCliente() => Host.CreateClient();

    /// <summary>Abre a revisão: perfil em vigor com as concessões dos beneficiários e a versão que acrescenta permissão sensível.</summary>
    private protected async Task<RevisaoAberta> AbrirRevisaoAsync(params Persona[] beneficiarios)
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Dictionary<Persona, Guid> concessoes = [];
        foreach (Persona pessoa in beneficiarios)
        {
            concessoes[pessoa] = await ConcederAsync(pessoa, vigente.PerfilId);
        }

        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);
        await EnviarEVerificarAsync(gestor, nova);

        return new RevisaoAberta(vigente.PerfilId, nova.VersaoId, concessoes);
    }

    private Task<Guid> ConcederAsync(Persona pessoa, Guid perfilId) =>
        ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, pessoa.ContaId.ToString(), perfilId,
            EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()), email: pessoa.Email);

    /// <summary>
    /// Executa a ação e espera o Wolverine do host da classe concluir tudo o que ela disparou, o envio ao relay inclusive. O
    /// consumidor da fila de e-mail do host da fixture fica parado enquanto isso, para só o host rastreado consumir.
    /// </summary>
    private protected async Task EntregarAsync(Func<Task> acao)
    {
        await using PausaDosAgentes pausa = await PausaDosAgentes.PausarAsync([AgenteDaFila(Fixture.Factory.Services)]);
        await AcompanharEntregaAsync(acao);
    }

    private Task<ITrackedSession> AcompanharEntregaAsync(Func<Task> acao) =>
        Host.Services.GetRequiredService<IHost>()
            .TrackActivity()
            .IncludeExternalTransports()
            .IgnoreMessageType<PedidoDeVerificacaoDeArquivo>()
            .IgnoreMessageType<ResultadoDaVerificacaoDeArquivo>()
            .IgnoreMessagesMatchingType(tipo => tipo == ConfirmacaoDeInvocacaoRemota)
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(_ => acao());

    /// <summary>
    /// Executa a ação com os consumidores da fila de e-mail parados e devolve o corpo das mensagens que ela deixou na fila
    /// durável, que são os envelopes que o relay entregaria. A fila é esvaziada antes e depois, sem entregar nada.
    /// </summary>
    private protected async Task<string[]> EnvelopesDeixadosPorAsync(Func<Task> acao)
    {
        IListeningAgent[] agentes = [AgenteDaFila(Fixture.Factory.Services), AgenteDaFila(Host.Services)];
        (string conexao, string fila) = EnderecoDaFila();
        await using PausaDosAgentes pausa = await PausaDosAgentes.PausarAsync(agentes);

        try
        {
            await LimparEExecutarAsync(conexao, fila, acao);

            return await LerAFilaAsync(conexao, fila);
        }
        finally
        {
            await ExecutarNaFilaAsync(conexao, $"DELETE FROM {fila}");
        }
    }

    private (string Conexao, string Fila) EnderecoDaFila()
    {
        IServiceProvider servicos = Fixture.Factory.Services;
        string conexao = servicos.GetRequiredService<IConfiguration>().GetConnectionString("UniPlusDb")!;
        string fila = TabelasDoEnvioDeEmail.Do(servicos.GetRequiredService<IWolverineRuntime>()).Fila;
        return (conexao, fila);
    }

    private static async Task LimparEExecutarAsync(string conexao, string fila, Func<Task> acao)
    {
        await ExecutarNaFilaAsync(conexao, $"DELETE FROM {fila}");
        await acao();
    }

    private static IListeningAgent AgenteDaFila(IServiceProvider servicos) =>
        servicos.GetRequiredService<IWolverineRuntime>().Endpoints.FindListeningAgent(EnderecoDaFilaDeEmail)
            ?? throw new InvalidOperationException("O consumidor da fila de e-mail não foi encontrado.");

    private static async Task ExecutarNaFilaAsync(string conexao, string sql)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(sql, banco);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> LerAFilaAsync(string conexao, string fila)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new($"SELECT encode(body, 'escape') FROM {fila}", banco);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        List<string> corpos = [];
        while (await leitor.ReadAsync())
        {
            corpos.Add(leitor.GetString(0));
        }

        return [.. corpos];
    }

    private protected static string RotaDaRevisao(Guid perfilId, string complemento = "") =>
        $"{RotaPerfis}/{perfilId}/{RotaDeRevisao}{complemento}";

    private protected static object Decisao(Guid concessaoId, string decisao) => new { concessaoId, decisao };

    private protected static Task<HttpResponseMessage> EnviarDecisoesDaGestaoAsync(
        HttpClient cliente, Guid perfilId, object[] decisoes, bool confirmacaoDeExclusao = false, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, RotaDaRevisao(perfilId, "/decisoes"), new { decisoes, confirmacaoDeExclusao }, chave);

    private protected static Task<JsonElement> RevisaoAsync(HttpClient cliente, Guid perfilId) =>
        LerAsync(cliente, RotaDaRevisao(perfilId));

    private protected static JsonElement ItemDe(JsonElement revisao, Guid concessaoId) =>
        revisao.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("concessaoId").GetGuid() == concessaoId);

    private protected static string SituacaoDe(JsonElement revisao, Guid concessaoId) =>
        ItemDe(revisao, concessaoId).GetProperty("situacao").GetProperty("codigo").GetString()!;

    private protected static async Task ConferirRecusaDaMigracaoAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.revisao_das_concessoes.{codigo}");
    }

    private protected async Task<int> ProcessarRevisoesAsync()
    {
        await using AsyncServiceScope escopo = Host.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ProcessamentoDasRevisoesDasConcessoes>().ExecutarAsync(CancellationToken.None);
    }

    private protected async Task<bool> TemConcessaoAtivaAsync(Persona pessoa, Guid perfilId)
    {
        await using AsyncServiceScope escopo = Host.Services.CreateAsyncScope();
        IReadOnlyList<ConcessaoAtiva> ativas = await escopo.ServiceProvider.GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(TestAuthHandler.DefaultIssuer, pessoa.ContaId.ToString()).Value!, CancellationToken.None);

        return ativas.Any(c => c.PerfilId == perfilId && c.Permissoes.Count > 0);
    }

    /// <summary>Quando a rotina gravou o lembrete de 48 horas (ou o de 24 horas), ou nulo se ainda não o emitiu.</summary>
    private protected async Task<DateTimeOffset?> AvisoGravadoEmAsync(Guid perfilId, bool de48Horas)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return de48Horas
            ? await db.Database.SqlQuery<DateTimeOffset?>(
                $"SELECT aviso_48h_em AS \"Value\" FROM autorizacao.revisoes_das_concessoes WHERE perfil_id = {perfilId}").SingleAsync()
            : await db.Database.SqlQuery<DateTimeOffset?>(
                $"SELECT aviso_24h_em AS \"Value\" FROM autorizacao.revisoes_das_concessoes WHERE perfil_id = {perfilId}").SingleAsync();
    }

    private protected sealed record RevisaoAberta(Guid PerfilId, Guid VersaoNovaId, IReadOnlyDictionary<Persona, Guid> Concessoes);
}
