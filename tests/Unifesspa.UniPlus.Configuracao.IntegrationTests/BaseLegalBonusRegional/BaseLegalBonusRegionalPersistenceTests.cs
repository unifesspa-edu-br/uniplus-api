namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.BaseLegalBonusRegional;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta de <c>BaseLegalBonusRegional</c> contra Postgres real: prova, no
/// nível do banco, que o soft-delete do agregado preserva os municípios da coleção owned —
/// o que o teste HTTP não alcança, porque o filtro global esconde o registro pai por completo
/// depois do soft-delete (ver <see cref="BaseLegalBonusRegionalEndpointTests"/>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class BaseLegalBonusRegionalPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public BaseLegalBonusRegionalPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Soft-delete do agregado preserva as linhas de município na tabela filha (owned collection)")]
    public async Task Remover_PreservaMunicipiosNaTabelaFilha()
    {
        Domain.Entities.BaseLegalBonusRegional baseLegal = Domain.Entities.BaseLegalBonusRegional.Criar(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [("1504208", "Marabá", "PA"), ("1501402", "Belém", "PA")]).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.BaseLegaisBonus.Add(baseLegal);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            Domain.Entities.BaseLegalBonusRegional tracked = await ctx.BaseLegaisBonus
                .Include(b => b.Municipios)
                .SingleAsync(b => b.Id == baseLegal.Id);
            ctx.BaseLegaisBonus.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Domain.Entities.BaseLegalBonusRegional excluido = await readCtx.BaseLegaisBonus
            .IgnoreQueryFilters()
            .Include(b => b.Municipios)
            .SingleAsync(b => b.Id == baseLegal.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.Municipios.Should().HaveCount(2,
            "o soft-delete do agregado não pode apagar fisicamente os municípios da coleção owned");
        excluido.Municipios.Select(m => m.CodigoIbge).Should().BeEquivalentTo(["1504208", "1501402"]);
    }
}
