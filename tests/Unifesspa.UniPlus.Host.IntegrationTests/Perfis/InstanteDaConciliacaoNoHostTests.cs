namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A conciliação espera as travas dos perfis e o relógio do teste atravessa o prazo de uma janela durante a espera: o
/// instante que vale é o de depois das travas, e não o que se lia antes delas.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class InstanteDaConciliacaoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    public InstanteDaConciliacaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "O instante da conciliação é lido depois de obter as travas dos perfis")]
    public async Task Conciliacao_LeOInstanteDepoisDasTravas()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        CatalogoDaAplicacao real = Fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();
        await CatalogoAlternativoDeTeste.ImplantarAsync(
            Fixture.Factory.Services, CatalogoAlternativoDeTeste.Com(real, Sensivel, ClassificacaoDeProtecao.Interno));
        DateTimeOffset antesDaEspera = Relogio.GetUtcNow();

        await using (TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services))
        {
            await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", vigente.PerfilId);
            Task conciliacao = CatalogoAlternativoDeTeste.ImplantarAsync(Fixture.Factory.Services, real, Relogio);
            await trava.AguardarBloqueioAsync();
            Relogio.Avancar(TimeSpan.FromDays(3));
            await trava.ConfirmarAsync();
            await conciliacao;
        }

        (await ImplantadaEmDaImplantacaoAtualAsync()).Should().BeOnOrAfter(antesDaEspera + TimeSpan.FromDays(3));
    }

    private async Task<DateTimeOffset> ImplantadaEmDaImplantacaoAtualAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().ImplantacoesDasPermissoes.AsNoTracking()
            .OrderByDescending(c => c.Sequencia).Select(c => c.ImplantadaEm).FirstAsync();
    }
}
