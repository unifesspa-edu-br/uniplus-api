namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Os perfis de governança não têm versão, aprovação nem janela, e a composição deles só muda por migration. Reúnem
/// permissões do módulo de autorização e, de outros módulos, só as reservadas à governança: uma permissão comum de outro
/// módulo chegaria a todos os gestores ou Representantes sem passar pela aprovação que os perfis comuns exigem. O perfil
/// comum, por sua vez, nunca reúne permissão reservada.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ComposicaoDaGovernancaNoHostTests
{
    private const string ModuloDeAutorizacao = "autorizacao";
    private const string AprovacaoDaProtecaoDoCatalogo = "configuracao:protecao-do-catalogo:aprovar";

    private readonly MonolitoDaInstalacaoFixture _fixture;

    public ComposicaoDaGovernancaNoHostTests(MonolitoDaInstalacaoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os perfis de governança da carga reúnem permissões da autorização e, de outros módulos, só reservadas")]
    public async Task PerfisDeGovernanca_SoReunemAutorizacaoOuReservadas()
    {
        CatalogoDaAplicacao catalogo = _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

        string[] codigos = [.. (await ComposicaoGravadaAsync()).Select(static p => p.Codigo)];

        codigos.Should().NotBeEmpty();
        codigos.Should().OnlyContain(codigo => DaAutorizacaoOuReservada(catalogo, codigo));
        codigos.Should().Contain(AprovacaoDaProtecaoDoCatalogo);
    }

    [Fact(DisplayName = "A composição gravada dos perfis de gestor de acessos e de Representante do controlador é a esperada")]
    public async Task PerfisDeGovernanca_ComposicaoGravadaEAEsperada()
    {
        PermissaoDeGovernanca[] gravada = await ComposicaoGravadaAsync();

        gravada.Where(static p => p.PerfilId == PerfisDeSistema.GestorDeAcessosId).Select(static p => p.Codigo)
            .Should().BeEquivalentTo(ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeAcessosId));
        gravada.Where(static p => p.PerfilId == PerfisDeSistema.RepresentanteDoControladorId).Select(static p => p.Codigo)
            .Should().BeEquivalentTo(ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.RepresentanteDoControladorId));
    }

    [Fact(DisplayName = "Toda permissão reservada de outro módulo é de decisão e está só no perfil de Representante do controlador")]
    public void PermissoesReservadasDeOutroModulo_SoNoRepresentante()
    {
        CatalogoDaAplicacao catalogo = _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

        string[] deOutroModulo =
            [.. catalogo.Definicoes.Where(static d => d.ReservadaAGovernanca && d.Modulo != ModuloDeAutorizacao).Select(static d => d.Codigo)];

        deOutroModulo.Should().Equal(AprovacaoDaProtecaoDoCatalogo);
        ComposicaoDaGovernanca.Esperada.Where(p => deOutroModulo.Contains(p.Codigo))
            .Should().OnlyContain(static p => p.PerfilId == PerfisDeSistema.RepresentanteDoControladorId);
    }

    [Fact(DisplayName = "O perfil comum recusa a aprovação da proteção do catálogo, reservada à governança")]
    public void PerfilComum_RecusaPermissaoReservadaDeOutroModulo()
    {
        ICatalogoDePermissoesConsultavel catalogo = _fixture.Factory.Services.GetRequiredService<ICatalogoDePermissoesConsultavel>();

        Unifesspa.UniPlus.Kernel.Results.Result<ConteudoDaVersao> conteudo = ConteudoDaVersao.Validar(
            "Aprovação do catálogo", "Aprova a proteção do catálogo.", [AprovacaoDaProtecaoDoCatalogo], "23479.000001/2026-01", 1024, catalogo);

        conteudo.Errors.Select(static e => e.Error.Code).Should().Contain(PerfilErrorCodes.PermissaoReservada);
    }

    private static bool DaAutorizacaoOuReservada(CatalogoDaAplicacao catalogo, string codigo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
        && (definicao!.Modulo == ModuloDeAutorizacao || definicao.ReservadaAGovernanca);

    private async Task<PermissaoDeGovernanca[]> ComposicaoGravadaAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.Perfis.AsNoTracking()
            .Where(perfil => perfil.Governanca != null)
            .SelectMany(perfil => perfil.Permissoes.Select(permissao => new PermissaoDeGovernanca(perfil.Id, permissao.Codigo)))
            .ToArrayAsync();
    }
}
