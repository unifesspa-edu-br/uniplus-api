namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>O que o modelo e o banco garantem sozinhos sobre o catálogo gravado e os fatos da reclassificação.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloDoCatalogoNoBancoTests : PerfilEndpointTestBase
{
    public ModeloDoCatalogoNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A ordem da classificação no banco é a do enum, valor a valor")]
    public async Task OrdemDaClassificacao_EhADoEnum()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        foreach (ClassificacaoDeProtecao valor in Enum.GetValues<ClassificacaoDeProtecao>())
        {
            int ordem = await db.Database
                .SqlQueryRaw<int>("""SELECT autorizacao.ordem_da_classificacao({0}) AS "Value" """, valor.ToString().ToLowerInvariant())
                .SingleAsync();

            ordem.Should().Be((int)valor, valor.ToString());
        }
    }

    [Fact(DisplayName = "A composição esperada da governança é a semente do modelo e a gravada no banco")]
    public async Task Composicao_EhASementeEOBanco()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        IEntityType permissoes = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Perfil))!
            .FindNavigation(nameof(Perfil.Permissoes))!.TargetEntityType;

        HashSet<PermissaoDeGovernanca> semente = [.. permissoes.GetSeedData()
            .Select(static d => new PermissaoDeGovernanca((Guid)d["PerfilId"]!, (string)d["Codigo"]!))];
        HashSet<PermissaoDeGovernanca> gravada = [.. await db.Perfis.AsNoTracking()
            .Where(p => p.Governanca != null)
            .SelectMany(p => p.Permissoes.Select(x => new PermissaoDeGovernanca(p.Id, x.Codigo)))
            .ToListAsync()];

        semente.Should().BeEquivalentTo(ComposicaoDaGovernanca.Esperada);
        gravada.Should().BeEquivalentTo(ComposicaoDaGovernanca.Esperada);
    }

    [Fact(DisplayName = "Os fatos da reclassificação são só de acréscimo: evidência forense, sem soft delete")]
    public void Fatos_SaoSoDeAcrescimo()
    {
        Type[] fatos =
        [
            typeof(ReclassificacaoDaPermissao), typeof(LiberacaoDaReclassificacao),
            typeof(BaseDaPermissaoNaConcessao), typeof(DivergenciaDaGovernanca),
        ];

        fatos.Should().OnlyContain(t => typeof(IForensicEntity).IsAssignableFrom(t));
    }

    [Fact(DisplayName = "O catálogo da aplicação grava uma linha por permissão, com classe, escopo e reserva")]
    public async Task CatalogoGravado_TemUmaLinhaPorPermissao()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        CatalogoDaAplicacao catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDaAplicacao>();

        ImplantacaoDasPermissoes atual = await db.ImplantacoesDasPermissoes.AsNoTracking().Include(c => c.Permissoes)
            .Where(c => c.Impressao == catalogo.Impressao).OrderByDescending(c => c.Sequencia).FirstAsync();

        atual.Permissoes!.Select(p => (p.Codigo, p.Classificacao, p.DeUnidade, p.Reservada))
            .Should().BeEquivalentTo(catalogo.Linhas.Select(l => (l.Codigo, l.Classificacao, l.DeUnidade, l.Reservada)));
    }
}
