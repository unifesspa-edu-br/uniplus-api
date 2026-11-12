namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A confirmação da instalação e a revogação de um gestor disputando as mesmas travas, em transações reais sobrepostas.
/// A transação retida segura a trava da pessoa do Diogo: a revogação da concessão dele entra primeiro na fila dessa trava, e
/// a confirmação da instalação, que designa o Diogo, entra depois. Na ordem única (perfil do gestor, pessoas, trava dos
/// gestores), a confirmação espera sem segurar a trava dos gestores, e as duas terminam sem ciclo.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class InstalacaoConcorrenteComARevogacaoTests : InstalacaoTestBase
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);

    public InstalacaoConcorrenteComARevogacaoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Instalação e revogação de gestor simultâneas terminam sem impasse, e sobram os dois gestores mínimos")]
    public async Task InstalacaoERevogacaoSimultaneas_TerminamSemImpasse()
    {
        string codigo = await ChegarAoCodigoComGestoresAtivosAsync();
        Guid concessaoDoDiogo = await ConcessaoDeGestorAsync(Diogo);
        using HttpClient betina = Cliente(Betina, papel: false);
        using HttpClient paulo = Cliente(Paulo);

        (HttpResponseMessage revogacao, HttpResponseMessage confirmacao) = await DispararSobATravaDoDiogoAsync(
            () => RevogarAsync(betina, concessaoDoDiogo, Diogo),
            () => EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo }));

        await ConferirDesfechoAsync(revogacao, confirmacao);
        (await ListarGestoresAtivosAsync()).Should().BeEquivalentTo([Betina.Sub.ToString(), Manoel.Sub.ToString()]);
    }

    private static Task<HttpResponseMessage> RevogarAsync(HttpClient administrador, Guid concessao, Persona beneficiario) =>
        EnviarAsync(
            administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{concessao}/revogacao",
            new { motivo = "Deixou a função.", confirmacaoEmail = beneficiario.Email });

    // A revogação, primeira na fila da trava da pessoa, conclui; a confirmação conta os gestores que sobraram e perde.
    private static async Task ConferirDesfechoAsync(HttpResponseMessage revogacao, HttpResponseMessage confirmacao)
    {
        using (revogacao)
        using (confirmacao)
        {
            revogacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await revogacao.Content.ReadAsStringAsync());
            await ConferirRecusaAsync(confirmacao, "ja_ha_gestor");
        }
    }

    // A instalação de Diogo e Isabella chega ao código enviado num ambiente sem gestor; depois disso os gestores Diogo,
    // Betina e Manoel passam a existir, e a confirmação tem de perder para eles.
    private async Task<string> ChegarAoCodigoComGestoresAtivosAsync()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoDocumentoVerificadoAsync(paulo);
        string codigo = await PedirCodigoAsync(paulo, id, Paulo);
        foreach (Persona gestor in new[] { Diogo, Betina, Manoel })
        {
            await SemearGestorComEmailAsync(gestor);
        }

        return codigo;
    }

    private async Task<(HttpResponseMessage Primeira, HttpResponseMessage Segunda)> DispararSobATravaDoDiogoAsync(
        Func<Task<HttpResponseMessage>> primeira, Func<Task<HttpResponseMessage>> segunda)
    {
        await using TransacaoRetidaDeTeste retida = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await retida.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('autorizacao.pessoa:' || {0} || chr(10) || {1}, 0))",
            TestAuthHandler.DefaultIssuer, Diogo.Sub.ToString());

        Task<HttpResponseMessage> tarefaDaPrimeira = primeira();
        await retida.AguardarBloqueioAsync(1);
        Task<HttpResponseMessage> tarefaDaSegunda = segunda();
        await retida.AguardarBloqueioAsync(2);
        await retida.ConfirmarAsync();

        return (await tarefaDaPrimeira.WaitAsync(Prazo), await tarefaDaSegunda.WaitAsync(Prazo));
    }

    private async Task SemearGestorComEmailAsync(Persona gestor)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(PerfisDeSistema.GestorDeAcessosId))!;
        await repositorio.AdicionarAsync(ConcessaoDeGestor(gestor, perfil, scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>()));
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    // Com o e-mail do beneficiário, que a revogação exige como confirmação.
    private static Concessao ConcessaoDeGestor(Persona gestor, Perfil perfil, ICatalogoDePermissoesConsultavel catalogo) =>
        Concessao.Conceder(
                new Identidade(TestAuthHandler.DefaultIssuer, gestor.Sub.ToString(), gestor.Username),
                perfil,
                EscopoDaConcessao.DaInstituicao(),
                new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
                new Identidade(TestAuthHandler.DefaultIssuer, Paulo.Sub.ToString()),
                DateTimeOffset.UtcNow,
                catalogo,
                beneficiarioEmail: gestor.Email)
            .Value!;

    private async Task<Guid> ConcessaoDeGestorAsync(Persona gestor)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        string sub = gestor.Sub.ToString();
        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId && c.BeneficiarioSub == sub && c.RevogadaEm == null)
            .Select(c => c.Id)
            .SingleAsync();
    }
}
