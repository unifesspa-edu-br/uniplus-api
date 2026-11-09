namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Montagens comuns às suítes da janela de migração pelo host real, com o relógio controlável: o perfil em vigor, as
/// concessões dele, a versão que acrescenta permissão e a leitura e as decisões da janela.
/// </summary>
public abstract class JanelaDeMigracaoNoHostTestBase : AprovacaoDaVersaoNoHostTestBase, IAsyncLifetime, IDisposable
{
    private protected const string RotaDeJanela = "janela";

    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    private protected JanelaDeMigracaoNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
        _host = Fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(Relogio);
        }));
    }

    private protected RelogioAvancavelDeTeste Relogio { get; } = new();

    public Task InitializeAsync() => ResetarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private protected override HttpClient NovoCliente() => _host.CreateClient();

    /// <summary>Abre a janela: perfil em vigor com as concessões dos beneficiários e a versão que acrescenta permissão sensível.</summary>
    private protected async Task<JanelaAberta> AbrirJanelaAsync(params Persona[] beneficiarios)
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Dictionary<Persona, Guid> concessoes = [];
        foreach (Persona pessoa in beneficiarios)
        {
            concessoes[pessoa] = await ConcessoesDeTeste.ConcederAsync(
                Fixture.Factory.Services,
                pessoa.Sub.ToString(),
                vigente.PerfilId,
                EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()),
                email: pessoa.Email);
        }

        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);
        await EnviarEVerificarAsync(gestor, nova);

        return new JanelaAberta(vigente.PerfilId, nova.VersaoId, concessoes);
    }

    private protected static string RotaDaJanela(Guid perfilId, string complemento = "") =>
        $"{RotaPerfis}/{perfilId}/{RotaDeJanela}{complemento}";

    private protected static object Decisao(Guid concessaoId, string decisao) => new { concessaoId, decisao };

    private protected static Task<HttpResponseMessage> EnviarLoteAsync(
        HttpClient cliente, Guid perfilId, object[] decisoes, bool confirmacaoDeExclusao = false, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, RotaDaJanela(perfilId, "/lote"), new { decisoes, confirmacaoDeExclusao }, chave);

    private protected static Task<JsonElement> JanelaAsync(HttpClient cliente, Guid perfilId) =>
        LerAsync(cliente, RotaDaJanela(perfilId));

    private protected static JsonElement ItemDe(JsonElement janela, Guid concessaoId) =>
        janela.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("concessaoId").GetGuid() == concessaoId);

    private protected static string SituacaoDe(JsonElement janela, Guid concessaoId) =>
        ItemDe(janela, concessaoId).GetProperty("situacao").GetString()!;

    private protected static async Task ConferirRecusaDaMigracaoAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.migracao.{codigo}");
    }

    private protected async Task<int> ProcessarJanelasAsync()
    {
        await using AsyncServiceScope escopo = _host.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ProcessamentoDasJanelas>().ExecutarAsync(CancellationToken.None);
    }

    private protected async Task<bool> TemConcessaoAtivaAsync(Persona pessoa, Guid perfilId)
    {
        await using AsyncServiceScope escopo = _host.Services.CreateAsyncScope();
        IReadOnlyList<ConcessaoAtiva> ativas = await escopo.ServiceProvider.GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(TestAuthHandler.DefaultIssuer, pessoa.Sub.ToString()).Value!, CancellationToken.None);

        return ativas.Any(c => c.PerfilId == perfilId && c.Permissoes.Count > 0);
    }

    /// <summary>Quando a rotina gravou o lembrete de 48 horas (ou o de 24 horas), ou nulo se ainda não o emitiu.</summary>
    private protected async Task<DateTimeOffset?> AvisoGravadoEmAsync(Guid perfilId, bool de48Horas)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return de48Horas
            ? await db.Database.SqlQuery<DateTimeOffset?>(
                $"SELECT aviso_48h_em AS \"Value\" FROM autorizacao.janelas_de_migracao WHERE perfil_id = {perfilId}").SingleAsync()
            : await db.Database.SqlQuery<DateTimeOffset?>(
                $"SELECT aviso_24h_em AS \"Value\" FROM autorizacao.janelas_de_migracao WHERE perfil_id = {perfilId}").SingleAsync();
    }

    private protected sealed record JanelaAberta(Guid PerfilId, Guid VersaoNovaId, IReadOnlyDictionary<Persona, Guid> Concessoes);
}
