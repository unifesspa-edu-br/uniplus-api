namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Os perfis de governança não têm versão, aprovação nem janela, e a composição deles só muda por migration. Por isso
/// só reúnem permissões do módulo de autorização: uma permissão de outro módulo chegaria a todos os gestores e à
/// proteção de dados sem passar pela aprovação que os perfis comuns exigem.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ComposicaoDaGovernancaNoHostTests
{
    private const string ModuloDeAutorizacao = "autorizacao";

    private readonly MonolitoDaInstalacaoFixture _fixture;

    public ComposicaoDaGovernancaNoHostTests(MonolitoDaInstalacaoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os perfis de governança da carga só reúnem permissões do módulo de autorização")]
    public async Task PerfisDeGovernanca_SoReunemPermissoesDaAutorizacao()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        CatalogoDaAplicacao catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDaAplicacao>();

        string[] codigos = await db.Perfis.AsNoTracking()
            .Where(perfil => perfil.Governanca != null)
            .SelectMany(perfil => perfil.Permissoes.Select(permissao => permissao.Codigo))
            .ToArrayAsync();

        codigos.Should().NotBeEmpty();
        codigos.Should().OnlyContain(codigo => ModuloDe(catalogo, codigo) == ModuloDeAutorizacao);
    }

    [Fact(DisplayName = "Toda permissão reservada à governança é do módulo de autorização")]
    public void PermissoesReservadas_SaoDaAutorizacao()
    {
        CatalogoDaAplicacao catalogo = _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

        catalogo.Definicoes
            .Where(static definicao => definicao.ReservadaAGovernanca)
            .Should().NotBeEmpty()
            .And.OnlyContain(static definicao => definicao.Modulo == ModuloDeAutorizacao);
    }

    private static string? ModuloDe(CatalogoDaAplicacao catalogo, string codigo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao) ? definicao!.Modulo : null;
}
